using System.Collections.Concurrent;
using System.Reflection;
using FxMap.Exceptions;
using FxMap.MetadataCache;

namespace FxMap.Accessors.TypeAccessors;

public sealed class TypeAccessor(Type objectType) : ITypeAccessor
{
    private readonly ConcurrentDictionary<string, PropertyInfo> _properties = [];
    private readonly ConcurrentDictionary<string, PropertyInfo> _directProperties = [];

    public PropertyInfo GetPropertyInfo(string name)
    {
        var objectTypeCached = FluentConfigStore.EntityConfigs[objectType];
        var result = _properties.GetOrAdd(name, n =>
        {
            var properties = objectType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            var matches = properties.Where(p =>
            {
                var exposedNameStore = objectTypeCached.ExposedNameStores
                    .FirstOrDefault(a => a.PropertyInfo == p);
                if (exposedNameStore is not null) return exposedNameStore.ExposedPropertyName == n;
                return p.Name == n;
            }).ToArray();
            return matches.Length switch
            {
                0 => null,
                1 => matches[0],
                _ => throw new FxMapException.DuplicatedNameByExposedName(objectType, n)
            };
        });
        return result;
    }

    public PropertyInfo GetPropertyInfoDirect(string propertyName)
    {
        return _directProperties.GetOrAdd(propertyName,
            n => objectType.GetProperty(n, BindingFlags.Public | BindingFlags.Instance));
    }
}