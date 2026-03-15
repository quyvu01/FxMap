using System.Reflection;
using FxMap.Abstractions;
using FxMap.Accessors.PropertyAccessors;
using FxMap.Extensions;
using FxMap.Fluent.Builders;
using FxMap.Fluent.Rules;
using FxMap.PropertyMappingContexts;

namespace FxMap.Fluent;

public abstract class ProfileOf<TModel> : IFluentProfileConfig
{
    private readonly List<KeyRuleGroup> _ruleGroups = [];

    /// <summary>
    /// Gets the CLR type that this model represents.
    /// </summary>
    public Type ClrType { get; private set; }

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

    void IFluentProfileConfig.Build()
    {
        Configure();
        // Build DependencyGraph!
        var clrType = typeof(TModel);
        ClrType = clrType;
        var properties = clrType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var dependencyGraph = BuildDependencyGraphFromFluentRules(properties, _ruleGroups);

        var propertiesWithinAttribute = dependencyGraph
            .Keys
            .Concat(dependencyGraph.Values
                .Select(a => a.Select(p => p.RequiredPropertyInfo))
                .SelectMany(a => a))
            .Distinct()
            .ToArray();

        var nonPrimitiveProperties = properties
            .Where(a => !a.PropertyType.IsPrimitiveType())
            .Except(propertiesWithinAttribute)
            .ToArray();

        Accessors = propertiesWithinAttribute
            .Concat(nonPrimitiveProperties)
            .ToDictionary(p => p, p => CreateAccessor(clrType, p));

        DependencyGraphs = dependencyGraph;
    }


    protected DistributedKeyRuleBuilder<TModel> UseDistributedKey(string key)
    {
        var group = new KeyRuleGroup { DistributedKey = key };
        _ruleGroups.Add(group);
        return new DistributedKeyRuleBuilder<TModel>(group);
    }

    protected DistributedKeyRuleBuilder<TModel> UseDistributedKey<TDistributedKey>()
        where TDistributedKey : IDistributedKey
    {
        var group = new KeyRuleGroup { DistributedKeyType = typeof(TDistributedKey) };
        _ruleGroups.Add(group);
        return new DistributedKeyRuleBuilder<TModel>(group);
    }

    protected abstract void Configure();

    private static IPropertyAccessor CreateAccessor(Type type, PropertyInfo p)
    {
        var accessorType = typeof(PropertyAccessor<,>).MakeGenericType(type, p.PropertyType);
        return (IPropertyAccessor)Activator.CreateInstance(accessorType, p)!;
    }

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
    public PropertyInformation GetInformation(PropertyInfo propertyInfo)
    {
        if (!DependencyGraphs.TryGetValue(propertyInfo, out var dependencies))
            return new PropertyInformation(0, null, null, null);
        var dependency = dependencies.First();
        var requiredAccessor = GetAccessor(dependency.RequiredPropertyInfo);
        return new PropertyInformation(dependencies.Length - 1, dependency.Expression,
            dependency.RuntimeDistributedKeyType,
            requiredAccessor) { ConditionalExpression = dependency.ConditionalExpression };
    }
}