using System.Reflection;
using FxMap.Fluent.Rules;

namespace FxMap.PropertyMappingContexts;

/// <summary>
/// Represents the context for a property that participates in FxMap mapping,
/// including its dependencies and expression configuration.
/// </summary>
public sealed class PropertyContext
{
    /// <summary>
    /// Gets or sets the target property that will receive the mapped value.
    /// </summary>
    public PropertyInfo TargetPropertyInfo { get; set; }

    /// <summary>
    /// Gets or sets the expression used to project or navigate the source data.
    /// </summary>
    public string Expression { get; set; }

    /// <summary>
    /// Gets or sets the name of the property that provides the selector ID value.
    /// </summary>
    public string SelectorPropertyName { get; set; }

    /// <summary>
    /// Gets or sets the property info of the required dependency property.
    /// </summary>
    public PropertyInfo RequiredPropertyInfo { get; set; }

    /// <summary>
    /// Gets or sets the runtime type of the FxMap attribute decorating the target property.
    /// </summary>
    public Type RuntimeDistributedKeyType { get; set; }

    /// <summary>
    /// Gets or sets the conditional expression for runtime expression resolution.
    /// When set, the Expression is resolved dynamically at mapping time.
    /// </summary>
    internal ConditionalExpression ConditionalExpression { get; set; }
}