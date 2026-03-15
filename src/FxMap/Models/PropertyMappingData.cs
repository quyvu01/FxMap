namespace FxMap.Models;

/// <summary>
/// Represents the association between an object model and its property mapping information.
/// Wraps a <see cref="PropertyDescriptor"/> so that <see cref="EffectiveExpression"/> lives
/// on the per-request descriptor, not on the shared (potentially cached) PropertyInformation.
/// </summary>
internal sealed class PropertyMappingData(PropertyDescriptor descriptor)
{
    public object Model => descriptor.Model;
    public Accessors.PropertyAccessors.PropertyInformation PropertyInformation => descriptor.Property;

    internal string EffectiveExpression
    {
        get => descriptor.EffectiveExpression;
        set => descriptor.EffectiveExpression = value;
    }
}
