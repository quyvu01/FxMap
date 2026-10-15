using FxMap.Registries;

namespace FxMap.Wrappers;

/// <summary>
/// What <c>AddFxMap</c> returns: it lets the extension methods of the data providers and of the integrations
/// (<c>AddEntityFrameworkCore</c>, <c>AddMongoDb</c>, <c>AddHotChocolate</c>) chain their registration onto the FxMap configuration.
/// </summary>
/// <example>
/// <code>
/// services.AddFxMap(cfg => { /* configuration */ })
///     .AddEntityFrameworkCore(cfg => cfg.AddDbContexts(typeof(AppDbContext)));
/// </code>
/// </example>
public sealed class ConfiguratorWrapped
{
    internal ConfiguratorWrapped(MapConfigurator mapConfigurator) => MapConfigurator = mapConfigurator;

    /// <summary>The configuration that was built by <c>AddFxMap</c>.</summary>
    internal MapConfigurator MapConfigurator { get; }
}
