using System.Collections.Concurrent;
using System.Reflection;
using FxMap.Delegates;
using FxMap.Exceptions;

namespace FxMap.Accessors.TypeAccessors;

public sealed class TypeAccessor(Type objectType, GetEntityConfig getEntityConfig) : ITypeAccessor
{
    private readonly ConcurrentDictionary<string, PropertyInfo> _properties = [];
    private readonly ConcurrentDictionary<string, PropertyInfo> _directProperties = [];

    public PropertyInfo GetPropertyInfo(string name)
    {
        var entityConfig = getEntityConfig.Invoke(objectType);
        var hasConfig = entityConfig is not null;
        var result = _properties.GetOrAdd(name, n =>
        {
            var properties = objectType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            if (!hasConfig || entityConfig!.ExposedNameStores.Count == 0)
                return properties.FirstOrDefault(p => p.Name == n);

            var matches = properties.Where(p =>
            {
                var exposedNameStore = entityConfig.ExposedNameStores
                    .FirstOrDefault(a => a.PropertyInfo == p);
                if (exposedNameStore is not null) return exposedNameStore.ExposedPropertyName == n;
                return p.Name == n;
            }).ToArray();
            return matches.Length switch
            {
                0 => null,
                1 => matches[0],
                _ => throw new DistributedMapException.DuplicatedNameByExposedName(objectType, n)
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