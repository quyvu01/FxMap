using System.Reflection;
using FxMap.Accessors.PropertyAccessors;

namespace FxMap.Models;

/// <summary>
/// Represents the metadata for a property that can be mapped by the FxMap framework.
/// </summary>
/// <param name="PropertyInfo">The reflection metadata for the property.</param>
/// <param name="Model">The object instance containing the property.</param>
/// <param name="Property">The FxMap mapping information for the property.</param>
/// <param name="Accessor">The compiled accessor of the property, used to read and write its value.</param>
internal sealed record PropertyDescriptor(
    PropertyInfo PropertyInfo,
    object Model,
    PropertyInformation Property,
    IPropertyAccessor Accessor)
{
    internal string EffectiveExpression { get; set; }
}
