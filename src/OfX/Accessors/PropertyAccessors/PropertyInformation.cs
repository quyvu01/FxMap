using OfX.Abstractions;
using OfX.Fluent.Rules;

namespace OfX.Accessors.PropertyAccessors;

/// <summary>
/// Represents the mapping metadata for a property decorated with an OfX attribute.
/// </summary>
/// <param name="Order">
/// The dependency order of this property. Properties with higher order values
/// depend on properties with lower order values and must be resolved later.
/// </param>
/// <param name="Expression">
/// The expression string used to map or project the property value.
/// Can be <c>null</c> if no expression is specified (uses default property).
/// </param>
/// <param name="RuntimeAttributeType">
/// The runtime <see cref="Type"/> of the <see cref="IDistributedKey"/> decorating this property.
/// </param>
/// <param name="RequiredAccessor">
/// The compiled property accessor for the dependency property that this property requires.
/// Used to retrieve the selector value needed for data fetching.
/// </param>
public sealed record PropertyInformation(
    int Order,
    string Expression,
    Type RuntimeAttributeType,
    IPropertyAccessor RequiredAccessor)
{
    /// <summary>
    /// Gets the conditional expression for runtime expression resolution, if any.
    /// </summary>
    internal ConditionalExpression ConditionalExpression { get; init; }

    /// <summary>
    /// Returns true if this property uses a conditional expression that must be resolved at runtime.
    /// </summary>
    internal bool IsConditional => ConditionalExpression is not null;

    /// <summary>
    /// Stores the resolved expression value after evaluating a conditional expression at runtime.
    /// </summary>
    internal string ResolvedExpression { get; set; }

    /// <summary>
    /// Returns the effective expression: resolved conditional if available, otherwise the static expression.
    /// </summary>
    internal string EffectiveExpression => ResolvedExpression ?? Expression;
}