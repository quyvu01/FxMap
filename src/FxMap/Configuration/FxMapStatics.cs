using System.Reflection;
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
///   <item><description>Registered FxMap attribute assemblies</description></item>
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
        AttributesRegister = [];
        MaxNestingDepth = DefaultNestingDepth;
        MaxConcurrentProcessing = ConcurrentProcessing;
        SupervisorOptions = null;
        ThrowIfExceptions = false;
        RetryPolicy = null;
        FluentConfigStore.Clear();
    }

    internal static List<Assembly> AttributesRegister { get; set; } = [];
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

    public static readonly Lazy<IReadOnlyCollection<MapModelData>> ModelConfigurations = new(() =>
    {
        var knownDistributedKeys = DistributedKeyTypes.Value;
        MapModelData[] models =
        [
            ..FluentConfigStore.EntityConfigs.Values.Select(cfg =>
            {
                var attributeType = cfg.DistributedKeyType
                                    ?? knownDistributedKeys.FirstOrDefault(t => t.Name == cfg.DistributedKey);
                return new MapModelData(cfg.ModelType, attributeType,
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

    internal static readonly Lazy<IReadOnlyCollection<Type>> DistributedKeyTypes = new(() =>
    [
        ..AttributesRegister.SelectMany(a => a.ExportedTypes)
            .Where(a => typeof(IDistributedKey).IsAssignableFrom(a) && a.IsConcrete())
    ]);

    internal static Dictionary<Type, Type> InternalAttributeMapHandlers { get; } = [];

    public static IReadOnlyDictionary<Type, Type> AttributeMapHandlers => InternalAttributeMapHandlers;
}