using FxMap.Abstractions;
using FxMap.Fluent.Rules;

namespace FxMap.Accessors.PropertyAccessors;

/// <summary>
/// Represents the mapping metadata for a property configured via FxMap FluentAPI.
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
/// The runtime <see cref="Type"/> of the <see cref="IDistributedKey"/> associated with this property.
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

    public async ValueTask<string> ResolveExpression(IServiceProvider serviceProvider, CancellationToken token)
    {
        if (ConditionalExpression != null)
            return await ConditionalExpression.ResolveAsync(serviceProvider, token);
        return Expression;
    }
}