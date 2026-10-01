using System.Reflection;
using FxMap.Abstractions;
using FxMap.Accessors.PropertyAccessors;
using FxMap.Exceptions;
using FxMap.Extensions;
using FxMap.Fluent.Builders;
using FxMap.Fluent.Rules;
using FxMap.Helpers;
using FxMap.Models;
using FxMap.PropertyMappingContexts;

namespace FxMap.Fluent;

/// <summary>
/// Base class for defining how properties of <typeparamref name="TModel"/> are populated
/// via distributed key lookups using the FxMap mapping engine.
/// </summary>
/// <typeparam name="TModel">The DTO or response type being enriched.</typeparam>
public abstract class ProfileOf<TModel> : IFluentProfileConfig, IProfilePlanSource
{
    private static readonly PropertyInformation NoInformation = new(0, null, null, null);

    private readonly IReadOnlyDictionary<PropertyInfo, PropertyInformation> _information;

    ProfilePlan IProfilePlanSource.Plan => _plan;
    private readonly ProfilePlan _plan;

    private readonly List<KeyRuleGroup> _ruleGroups = [];

    /// <summary>
    /// Gets the CLR type that this profile maps.
    /// </summary>
    public Type ClrType { get; private set; }

    /// <summary>
    /// Gets the collection of key rule groups that define all distributed key mappings
    /// configured in <see cref="Configure"/>.
    /// </summary>
    public IReadOnlyCollection<KeyRuleGroup> RuleGroups => _ruleGroups;

    /// <summary>
    /// Gets the dictionary of compiled property accessors, keyed by their <see cref="PropertyInfo"/>.
    /// </summary>
    public IReadOnlyDictionary<PropertyInfo, IPropertyAccessor> Accessors { get; private set; }

    /// <summary>
    /// Gets the dependency graph for properties decorated with <see cref="IDistributedKey"/>.
    /// Each key is a property that depends on other properties, and the value is an array
    /// of <see cref="PropertyContext"/> representing its dependencies in resolution order.
    /// </summary>
    public IReadOnlyDictionary<PropertyInfo, PropertyContext[]> DependencyGraphs { get; private set; }

    Type IFluentProfileConfig.ModelType => typeof(TModel);

    // Keep this public ctor, this will be dynamically invoked by Activator.
    // Derived classes must NOT declare any constructor with parameters — doing so will cause
    // Activator.CreateInstance to fail at runtime when FxMap scans and instantiates profiles.
    public ProfileOf()
    {
        Configure();
        // Build DependencyGraph!
        var clrType = typeof(TModel);
        ClrType = clrType;
        var properties = clrType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var dependencyGraph = BuildDependencyGraphFromFluentRules(properties, _ruleGroups);

        var propertiesWithinKeys = dependencyGraph
            .Keys
            .Concat(dependencyGraph.Values
                .Select(a => a.Select(p => p.RequiredPropertyInfo))
                .SelectMany(a => a))
            .Distinct()
            .ToArray();

        // Properties that can hold objects to walk. Leaves, collections of leaves (byte[], List<string>...) and
        // framework types such as Uri are left out: walking them can never produce a descriptor.
        var nonPrimitiveProperties = properties
            .Where(a => PropertyClassifier.ShouldWalk(a.PropertyType))
            .Except(propertiesWithinKeys)
            .ToArray();

        Accessors = propertiesWithinKeys
            .Concat(nonPrimitiveProperties)
            .ToDictionary(p => p, p => CreateAccessor(clrType, p));

        DependencyGraphs = dependencyGraph;

        // The information of a property never changes after construction, so build it once instead of on every walk.
        _information = dependencyGraph.Keys.ToDictionary(p => p, BuildInformation);
        _plan = BuildPlan();
    }

    private ProfilePlan BuildPlan()
    {
        var entries = Accessors
            .Select(kv => new ProfileEntry(kv.Key, kv.Value, GetInformation(kv.Key),
                _information.ContainsKey(kv.Key)))
            .ToArray();
        // A property that is neither a rule target nor able to hold an object (declared as a primitive) can
        // never produce anything while walking, so the walker never needs to look at it.
        return new ProfilePlan(
            [..entries.Where(e => e.IsRule)],
            [..entries.Where(e => !e.IsRule && PropertyClassifier.ShouldWalk(e.Property.PropertyType))]);
    }


    /// <summary>
    /// Begins a mapping rule group using a string-based distributed key, enabling full service
    /// decoupling without a shared key type reference.
    /// </summary>
    /// <param name="key">
    /// The distributed key name. Must start with a letter or underscore and contain only
    /// letters, digits, or underscores (e.g., <c>"UserKey"</c>).
    /// </param>
    /// <returns>A <see cref="DistributedKeyRuleBuilder{TModel}"/> for chaining <c>.Of()</c> and <c>.For()</c> rules.</returns>
    /// <exception cref="DistributedMapException.DistributedKeyNullOrEmpty">
    /// Thrown when <paramref name="key"/> is <c>null</c> or whitespace.
    /// </exception>
    /// <exception cref="DistributedMapException.InvalidDistributedKeyName">
    /// Thrown when <paramref name="key"/> does not match the valid identifier pattern.
    /// </exception>
    protected DistributedKeyRuleBuilder<TModel> UseDistributedKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new DistributedMapException.DistributedKeyNullOrEmpty();
        DistributedKeyTypeFactory.ValidateKeyName(key);
        var group = new KeyRuleGroup { DistributedKey = key };
        _ruleGroups.Add(group);
        return new DistributedKeyRuleBuilder<TModel>(group);
    }

    /// <summary>
    /// Begins a mapping rule group using a strongly-typed distributed key, providing
    /// compile-time safety and shared-type coupling between services.
    /// </summary>
    /// <typeparam name="TDistributedKey">
    /// The <see cref="IDistributedKey"/> implementation that identifies the target entity.
    /// </typeparam>
    /// <returns>A <see cref="DistributedKeyRuleBuilder{TModel}"/> for chaining <c>.Of()</c> and <c>.For()</c> rules.</returns>
    protected DistributedKeyRuleBuilder<TModel> UseDistributedKey<TDistributedKey>()
        where TDistributedKey : IDistributedKey
    {
        var group = new KeyRuleGroup { DistributedKeyType = typeof(TDistributedKey) };
        _ruleGroups.Add(group);
        return new DistributedKeyRuleBuilder<TModel>(group);
    }

    /// <summary>
    /// Override this method to declare all distributed key mapping rules for <typeparamref name="TModel"/>
    /// using <see cref="UseDistributedKey(string)"/> or <see cref="UseDistributedKey{TDistributedKey}"/>.
    /// </summary>
    protected abstract void Configure();

    /// <summary>
    /// Creates a compiled property accessor for the given property on <paramref name="type"/>.
    /// </summary>
    /// <param name="type">The declaring type.</param>
    /// <param name="p">The property to create an accessor for.</param>
    /// <returns>A compiled <see cref="IPropertyAccessor"/> for fast get/set operations.</returns>
    private static IPropertyAccessor CreateAccessor(Type type, PropertyInfo p)
    {
        var accessorType = typeof(PropertyAccessor<,>).MakeGenericType(type, p.PropertyType);
        return (IPropertyAccessor)Activator.CreateInstance(accessorType, p)!;
    }

    /// <summary>
    /// Builds a dependency graph from the fluent rule groups, mapping each target property
    /// to the ordered chain of <see cref="PropertyContext"/> entries it depends on.
    /// </summary>
    /// <param name="properties">All public instance properties of <typeparamref name="TModel"/>.</param>
    /// <param name="ruleGroups">The rule groups registered via <see cref="UseDistributedKey(string)"/> calls.</param>
    /// <returns>
    /// A dictionary keyed by target property, where the value is the dependency chain
    /// in resolution order (deepest dependency first).
    /// </returns>
    private Dictionary<PropertyInfo, PropertyContext[]> BuildDependencyGraphFromFluentRules(
        PropertyInfo[] properties, List<KeyRuleGroup> ruleGroups)
    {
        // Build a lookup of target property → its direct PropertyContext
        var directDeps = new Dictionary<PropertyInfo, PropertyContext>();

        foreach (var group in ruleGroups)
        {
            var distributedKeyType = group.GetDistributedKeyType();
            var selectorProperty = properties.FirstOrDefault(p => p.Name == group.SelectorPropertyName);
            if (selectorProperty is null || distributedKeyType is null) continue;

            foreach (var rule in group.Rules)
            {
                var targetProperty = properties.FirstOrDefault(p => p.Name == rule.TargetPropertyName);
                if (targetProperty is null) continue;

                directDeps[targetProperty] = new PropertyContext
                {
                    TargetPropertyInfo = targetProperty,
                    Expression = rule.Expression,
                    SelectorPropertyName = selectorProperty.Name,
                    RuntimeDistributedKeyType = distributedKeyType,
                    RequiredPropertyInfo = selectorProperty,
                    ConditionalExpression = rule.ConditionalExpression
                };
            }
        }

        // Build recursive dependency graph
        var graph = new Dictionary<PropertyInfo, PropertyContext[]>();
        foreach (var (targetProp, _) in directDeps)
        {
            var deps = CollectDependencies(targetProp, directDeps, []);
            if (deps is { Length: > 0 }) graph[targetProp] = deps;
        }

        return graph;
    }

    /// <summary>
    /// Recursively collects the full dependency chain for a given <paramref name="property"/>,
    /// following selector properties that are themselves mapped targets.
    /// </summary>
    /// <param name="property">The property to resolve dependencies for.</param>
    /// <param name="directDeps">The flat map of direct property-to-context relationships.</param>
    /// <param name="visited">Tracks already-visited properties to prevent infinite cycles.</param>
    /// <returns>
    /// An ordered array of <see cref="PropertyContext"/> entries representing the full
    /// resolution chain, starting from the current property down to its root selector.
    /// </returns>
    private static PropertyContext[] CollectDependencies(
        PropertyInfo property, Dictionary<PropertyInfo, PropertyContext> directDeps,
        HashSet<PropertyInfo> visited)
    {
        if (!visited.Add(property) || !directDeps.TryGetValue(property, out var context)) return [];
        var result = new List<PropertyContext> { context };
        // Recursively resolve if the selector property is itself a mapped target
        result.AddRange(CollectDependencies(context.RequiredPropertyInfo, directDeps, visited));
        return result.ToArray();
    }

    /// <summary>
    /// Gets the compiled property accessor for the specified property.
    /// </summary>
    /// <param name="propertyInfo">The property for which to retrieve the accessor.</param>
    /// <returns>
    /// The <see cref="IPropertyAccessor"/> for the property, or <c>null</c> if no accessor exists.
    /// </returns>
    public IPropertyAccessor GetAccessor(PropertyInfo propertyInfo) => Accessors.GetValueOrDefault(propertyInfo);

    /// <summary>
    /// Gets the mapping information for the specified property, including its dependency order,
    /// expression, distributed key type, and required accessor.
    /// </summary>
    /// <param name="propertyInfo">The property for which to retrieve mapping information.</param>
    /// <returns>
    /// A <see cref="PropertyInformation"/> record containing the property's mapping metadata.
    /// If the property has no dependencies, returns default information with order 0.
    /// </returns>
    public PropertyInformation GetInformation(PropertyInfo propertyInfo) =>
        _information.GetValueOrDefault(propertyInfo) ?? NoInformation;

    private PropertyInformation BuildInformation(PropertyInfo propertyInfo)
    {
        var dependencies = DependencyGraphs[propertyInfo];
        var dependency = dependencies.First();
        var requiredAccessor = GetAccessor(dependency.RequiredPropertyInfo);
        return new PropertyInformation(dependencies.Length - 1, dependency.Expression,
            dependency.RuntimeDistributedKeyType,
            requiredAccessor) { ConditionalExpression = dependency.ConditionalExpression };
    }
}
