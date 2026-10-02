using System.Reflection;

namespace FxMap.Fluent.Rules;

/// <summary>One sort key of a <see cref="CollectionRule"/>, named after a property of the entity that owns the data.</summary>
/// <param name="PropertyName">Entity property (or exposed name) to sort by.</param>
/// <param name="Descending">True for a descending sort.</param>
public sealed record CollectionOrder(string PropertyName, bool Descending);

/// <summary>
/// Maps every row found for one selector value to one element of a collection property: the rules in
/// <see cref="Rules"/> describe how each element is filled from a row, so a key that matches several rows
/// produces a collection instead of a single value.
/// </summary>
public sealed class CollectionRule
{
    /// <summary>The collection property of the model that receives the elements (for example <c>Objects</c>).</summary>
    public PropertyInfo TargetPropertyInfo { get; init; }

    /// <summary>Name of <see cref="TargetPropertyInfo"/>.</summary>
    public string TargetPropertyName => TargetPropertyInfo.Name;

    /// <summary>The element type created for each row.</summary>
    public Type ItemType { get; init; }

    /// <summary>Rules that fill the properties of one element. Their targets are properties of <see cref="ItemType"/>.</summary>
    public List<PropertyMappingRule> Rules { get; } = [];

    /// <summary>Sort applied to the rows of one selector value, in order of priority. Empty means the provider's order.</summary>
    public List<CollectionOrder> OrderBy { get; } = [];

    /// <summary>Maximum number of rows (elements) kept for one selector value. Null means the provider's default limit.</summary>
    public int? Limit { get; set; }
}
