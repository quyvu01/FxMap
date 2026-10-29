using Microsoft.Extensions.DependencyInjection;

namespace FxMap.Registries;

/// <summary>
/// Provides the main configuration API for registering and configuring the FxMap framework.
/// </summary>
/// <remarks>
/// <para>
/// This class is the entry point for configuring FxMap in your application's startup.
/// Use the FluentAPI to register profiles, entity configurations, handlers, and other settings.
/// </para>
/// <example>
/// <code>
/// services.AddFxMap(cfg =>
/// {
///     cfg.AddProfilesFromAssemblyContaining&lt;OrderResponseProfile&gt;();
///     cfg.AddEntitiesFromAssemblyContaining&lt;UserEntityConfig&gt;();
///     cfg.SetRequestTimeOut(TimeSpan.FromSeconds(60));
/// });
/// </code>
/// </example>
/// </remarks>
/// <param name="services">The service collection to register services into.</param>
internal sealed class MapConfigurator(IServiceCollection services) : MapSettings, IMapConfigurator
{
    /// <summary>
    /// Gets the underlying service collection for advanced registration scenarios.
    /// </summary>
    public IServiceCollection Services { get; } = services;
}
