namespace FxMap.Models;

/// <summary>
/// Represents grouped mapping data for a specific FxMap attribute type.
/// </summary>
/// <param name="DistributedKeyType">The type of FxMap attribute associated with this mapping group.</param>
/// <param name="Accessors">The collection of property accessor data for properties sharing this attribute type.</param>
/// <param name="Order">The dependency order for resolving this group (lower values are resolved first).</param>
internal sealed record DistributedKeyInfo(
    Type DistributedKeyType,
    IEnumerable<PropertyMappingData> Accessors,
    int Order);