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

    internal static Type ResolveDistributedKeyType(KeyRuleGroup group) => group.GetDistributedKeyType();
}