namespace FxMap.Models;

/// <summary>
/// Represents grouped mapping data for a specific FxMap distributed key type.
/// </summary>
/// <param name="DistributedKeyType">The type of distributed key associated with this mapping group.</param>
/// <param name="Properties">The properties sharing this distributed key type.</param>
/// <param name="Order">The dependency order for resolving this group (lower values are resolved first).</param>
internal sealed record DistributedKeyInfo(
    Type DistributedKeyType,
    IEnumerable<PropertyDescriptor> Properties,
    int Order);