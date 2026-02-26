using System.Reflection;
using OfX.Abstractions;
using OfX.Configuration;
using OfX.Extensions;
using OfX.Fluent;
using OfX.Fluent.Rules;
using OfX.PropertyMappingContexts;

namespace OfX.Accessors.PropertyAccessors;

/// <summary>
/// Represents the metadata model for a CLR type, including its property accessors and dependency graphs
/// for OfX attribute-based mapping.
/// </summary>
/// <remarks>
/// <para>
/// This class analyzes a given CLR type at construction time to:
/// </para>
/// <list type="bullet">
/// <item>Build compiled property accessors for efficient runtime access.</item>
/// <item>Construct a dependency graph based on <see cref="IDistributedKey"/> annotations.</item>
/// <item>Identify properties that require mapping based on their attribute decorations.</item>
/// </list>
/// <para>
/// The dependency graph is used by the mapping engine to determine the correct order
/// of property resolution when properties depend on values from other properties.
/// </para>
/// </remarks>
public class TypeMetadataAccessor
{
    /// <summary>
    /// Gets the CLR type that this model represents.
    /// </summary>
    public Type ClrType { get; }

    /// <summary>
    /// Gets the dictionary of compiled property accessors, keyed by their <see cref="PropertyInfo"/>.
    /// </summary>
    public IReadOnlyDictionary<PropertyInfo, IPropertyAccessor> Accessors { get; }

    /// <summary>
    /// Gets the dependency graph for properties decorated with <see cref="IDistributedKey"/>.
    /// Each key is a property that depends on other properties, and the value is an array
    /// of <see cref="PropertyContext"/> representing its dependencies in resolution order.
    /// </summary>
    public IReadOnlyDictionary<PropertyInfo, PropertyContext[]> DependencyGraphs { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeMetadataAccessor"/> class for the specified CLR type.
    /// </summary>
    /// <param name="clrType">The CLR type to analyze and build accessors for.</param>
    public TypeMetadataAccessor(Type clrType)
    {
        ClrType = clrType;
        if (!FluentConfigStore.ProfileConfigs.TryGetValue(clrType, out var ruleGroups)) return;
        var properties = clrType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var dependencyGraph = BuildDependencyGraphFromFluentRules(properties, ruleGroups);

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

    private static IPropertyAccessor CreateAccessor(Type type, PropertyInfo p)
    {
        var accessorType = typeof(PropertyAccessor<,>).MakeGenericType(type, p.PropertyType);
        return (IPropertyAccessor)Activator.CreateInstance(accessorType, p)!;
    }

    private static Dictionary<PropertyInfo, PropertyContext[]> BuildDependencyGraphFromFluentRules(
        PropertyInfo[] properties, List<AttributeRuleGroup> ruleGroups)
    {
        var knownAttributes = OfXStatics.OfXAttributeTypes.Value;

        // Build a lookup of target property → its direct PropertyContext
        var directDeps = new Dictionary<PropertyInfo, PropertyContext>();

        foreach (var group in ruleGroups)
        {
            var attributeType = FluentConfigStore.ResolveAttributeType(group, knownAttributes);
            var selectorProperty = properties.FirstOrDefault(p => p.Name == group.SelectorPropertyName);
            if (selectorProperty is null || attributeType is null) continue;

            foreach (var rule in group.Rules)
            {
                var targetProperty = properties.FirstOrDefault(p => p.Name == rule.TargetPropertyName);
                if (targetProperty is null) continue;

                directDeps[targetProperty] = new PropertyContext
                {
                    TargetPropertyInfo = targetProperty,
                    Expression = rule.Expression,
                    SelectorPropertyName = selectorProperty.Name,
                    RuntimeAttributeType = attributeType,
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
        if (!visited.Add(property)) return [];
        if (!directDeps.TryGetValue(property, out var context)) return [];

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
    /// expression, attribute type, and required accessor.
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
        return new PropertyInformation(dependencies.Length - 1, dependency.Expression, dependency.RuntimeAttributeType,
            requiredAccessor) { ConditionalExpression = dependency.ConditionalExpression };
    }
}