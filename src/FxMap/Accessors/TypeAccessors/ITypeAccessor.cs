using System.Reflection;

namespace FxMap.Accessors.TypeAccessors;

public interface ITypeAccessor
{
    /// <summary>
    /// Gets property info by name, respecting ExposedName configuration.
    /// </summary>
    PropertyInfo GetPropertyInfo(string name);

    /// <summary>
    /// Gets property info directly by the actual property name, bypassing ExposedName configuration.
    /// Used for Id and defaultProperty access.
    /// </summary>
    PropertyInfo GetPropertyInfoDirect(string propertyName);
}