using System.Reflection;
using FxMap.Accessors.PropertyAccessors;

namespace FxMap.Models;

/// <summary>
/// Represents the metadata for a property that can be mapped by the FxMap framework.
/// </summary>
/// <param name="PropertyInfo">The reflection metadata for the property.</param>
/// <param name="Model">The object instance containing the property.</param>
/// <param name="PropertyInformation">The FxMap mapping information for the property.</param>
internal sealed record PropertyDescriptor(PropertyInfo PropertyInfo, object Model, PropertyInformation PropertyInformation)
{
    internal string EffectiveExpression { get; set; }
}
