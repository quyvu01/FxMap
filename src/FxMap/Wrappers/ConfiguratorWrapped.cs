using FxMap.Registries;

namespace FxMap.Wrappers;

/// <summary>
/// A wrapper record that encapsulates an <see cref="MapConfigurator"/> instance.
/// </summary>
/// <remarks>
/// This wrapper is returned by the <c>AddFxMap</c> extension method and allows
/// transport extensions (e.g., gRPC, RabbitMQ, NATS) to chain their registration
/// onto the FxMap configuration.
/// </remarks>
/// <param name="MapConfigurator">The underlying FxMap registration instance.</param>
/// <example>
/// <code>
/// services.AddFxMap(cfg => { /* configuration */ })
///     .AddFxMapEFCore();
/// </code>
/// </example>
public sealed record ConfiguratorWrapped(MapConfigurator MapConfigurator);