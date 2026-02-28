using System.Collections.Concurrent;
using FxMap.Fluent;
using FxMap.Fluent.Rules;

namespace FxMap.MetadataCache;

public static class FluentConfigStore
{
    internal static ConcurrentDictionary<Type, EntityConfigMetadata> EntityConfigs { get; } = [];
    public static ConcurrentDictionary<Type, IFluentProfileConfig> ProfileConfigs { get; } = [];

    internal static void Clear()
    {
        EntityConfigs.Clear();
        ProfileConfigs.Clear();
    }

    internal static Type ResolveAttributeType(KeyRuleGroup group, IReadOnlyCollection<Type> knownDistributedKeys) =>
        group.DistributedKeyType ?? knownDistributedKeys.FirstOrDefault(t => t.Name == group.DistributedKey);
}

internal sealed record EntityConfigMetadata(
    Type ModelType,
    Type DistributedKeyType,
    string DistributedKey,
    string IdPropertyName,
    string DefaultPropertyName,
    IReadOnlyCollection<ExposedNameStore> ExposedNameStores);