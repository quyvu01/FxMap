using FxMap.Abstractions;
using FxMap.Models;
using FxMap.Exceptions;
using FxMap.Extensions;
using FxMap.MetadataCache;
using FxMap.Supervision;

namespace FxMap.Configuration;

/// <summary>
/// Provides static configuration and cached metadata for the FxMap framework.
/// </summary>
/// <remarks>
/// This class serves as the central repository for:
/// <list type="bullet">
///   <item><description>Registered FxMap distributed key types and profiles</description></item>
///   <item><description>Model configuration metadata</description></item>
///   <item><description>Cached property information for response types</description></item>
///   <item><description>Global settings like retry policies and exception handling</description></item>
/// </list>
/// </remarks>
public static class FxMapStatics
{
    private const int DefaultNestingDepth = 128;
    private const int ConcurrentProcessing = 128;
    public static TimeSpan DefaultRequestTimeout { get; internal set; } = TimeSpan.FromSeconds(30);

    internal static void Clear()
    {
        MaxNestingDepth = DefaultNestingDepth;
        MaxConcurrentProcessing = ConcurrentProcessing;
        SupervisorOptions = null;
        ThrowIfExceptions = false;
        RetryPolicy = null;
        FluentConfigStore.Clear();
        EntitiesConfigurations = CreateEntitiesConfigurationsLazy();
        DistributedKeyTypes = CreateDistributedKeyTypesLazy();
        DistributedKeyMapHandlers = CreateDistributedKeyMapHandlersLazy();
    }

    // internal static List<Assembly> DistributedKeysRegister { get; set; } = [];
    internal static int MaxNestingDepth { get; set; } = DefaultNestingDepth;
    public static int MaxConcurrentProcessing { get; internal set; } = ConcurrentProcessing;
    public static bool ThrowIfExceptions { get; internal set; }
    internal static RetryPolicy RetryPolicy { get; set; }

    /// <summary>
    /// Gets the global supervisor options configured for all transport servers.
    /// Individual transport packages can override these with their own settings.
    /// </summary>
    public static SupervisorOptions SupervisorOptions { get; internal set; }

    public static readonly Type QueryOfHandlerType = typeof(IQueryOfHandler<,>);

    public static readonly Type NoOpQueryOfHandlerType = typeof(NoOpQueryOfHandler<,>);

    /// <summary>
    /// Returns true if entity configurations have been registered via fluent configuration.
    /// </summary>
    public static bool HasModelConfigurations => FluentConfigStore.EntityConfigs.Count > 0;

    public static Lazy<IReadOnlyCollection<EntityMapData>> EntitiesConfigurations { get; private set; } =
        CreateEntitiesConfigurationsLazy();

    internal static Lazy<IReadOnlyCollection<Type>> DistributedKeyTypes { get; private set; } =
        CreateDistributedKeyTypesLazy();

    private static Lazy<IReadOnlyCollection<EntityMapData>> CreateEntitiesConfigurationsLazy() => new(() =>
    {
        EntityMapData[] models =
        [
            ..FluentConfigStore.EntityConfigs.Values.Select(cfg =>
            {
                var attributeType = cfg.GetDistributedKeyType();
                return new EntityMapData(cfg.ModelType, attributeType,
                    new FxMapEntityConfig(cfg.IdPropertyName, cfg.DefaultPropertyName));
            })
        ];
        // Validate if one attribute is assigned to multiple models.
        models.GroupBy(a => a.DistributedKeyType)
            .ForEach(a =>
            {
                if (a.Count() <= 1) return;
                throw new FxMapException.OneAttributedHasBeenAssignToMultipleEntities(a.Key,
                    [..a.Select(o => o.ModelType)]);
            });
        return models;
    });

    private static Lazy<IReadOnlyCollection<Type>> CreateDistributedKeyTypesLazy() => new(() =>
    [
        .. new HashSet<Type>(FluentConfigStore.ProfileConfigs.SelectMany(a => a.Value.RuleGroups)
            .Select(a => a.GetDistributedKeyType()))
    ]);

    public static Lazy<IReadOnlyDictionary<Type, Type>> DistributedKeyMapHandlers { get; private set; } =
        CreateDistributedKeyMapHandlersLazy();

    private static Lazy<IReadOnlyDictionary<Type, Type>> CreateDistributedKeyMapHandlersLazy() => new(() =>
    {
        var modelConfigurations = EntitiesConfigurations.Value;
        return modelConfigurations.Select(m =>
        {
            var serviceInterfaceType = QueryOfHandlerType.MakeGenericType(m.ModelType, m.DistributedKeyType);
            return (m.DistributedKeyType, ServiceInterfaceType: serviceInterfaceType);
        }).ToDictionary(k => k.DistributedKeyType, kv => kv.ServiceInterfaceType);
    });
}