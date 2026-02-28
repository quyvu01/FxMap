using System.Reflection;

namespace FxMap.HotChocolate.Registries;

/// <summary>
/// Represents the context for a field that needs to be resolved through FxMap data fetching.
/// </summary>
/// <param name="ParentObject">The parent object instance containing this field.</param>
/// <param name="Expression">The FxMap expression for this field.</param>
/// <param name="Order">The dependency order for resolving this field.</param>
/// <param name="DistributedKeyType">The FxMap attribute type associated with this field.</param>
/// <param name="TargetPropertyInfo">The property info of the field to be populated.</param>
/// <param name="RequiredPropertyInfo">The property info of the selector ID property.</param>
/// <remarks>
/// This record is used as the key in the DataLoader for batching and caching field resolutions.
/// </remarks>
internal sealed record FieldBearing(
    object ParentObject,
    string Expression,
    int Order,
    Type DistributedKeyType,
    PropertyInfo TargetPropertyInfo,
    PropertyInfo RequiredPropertyInfo)
{
    public string SelectorId { get; set; }

    public bool Equals(FieldBearing other)
    {
        if (other is null) return false;
        return ParentObject.Equals(other.ParentObject) && Expression == other.Expression && Order == other.Order &&
               DistributedKeyType == other.DistributedKeyType && TargetPropertyInfo == other.TargetPropertyInfo;
    }

    public (PropertyInfo, object) PreviousComparable => (TargetPropertyInfo, ParentObject);
    public (PropertyInfo, object) NextComparable => (RequiredPropertyInfo, ParentObject);

    public override int GetHashCode() =>
        HashCode.Combine(Expression, Order, DistributedKeyType, TargetPropertyInfo, RequiredPropertyInfo);

    public FieldBearing Copy() => new(ParentObject, Expression, Order, DistributedKeyType, TargetPropertyInfo,
        RequiredPropertyInfo) { SelectorId = SelectorId };
}