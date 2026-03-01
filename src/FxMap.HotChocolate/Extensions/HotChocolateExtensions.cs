using FxMap.HotChocolate.Registries;
using FxMap.Wrappers;

namespace FxMap.HotChocolate.Extensions;

/// <summary>
/// Provides extension methods for integrating HotChocolate GraphQL with the FxMap framework.
/// </summary>
public static class HotChocolateExtensions
{
    /// <summary>
    /// Adds HotChocolate GraphQL integration for automatic FxMap field resolution.
    /// </summary>
    /// <param name="serviceInjector">The FxMap registration wrapper.</param>
    /// <param name="action">Configuration action for HotChocolate settings.</param>
    /// <returns>The FxMap registration wrapper for method chaining.</returns>
    /// <remarks>
    /// This integration automatically:
    /// <list type="bullet">
    ///   <item><description>Creates GraphQL resolvers for FxMap-mapped properties</description></item>
    ///   <item><description>Batches data fetching using HotChocolate DataLoaders</description></item>
    ///   <item><description>Handles field dependencies and ordering</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddFxMap(cfg => { /* config */ })
    ///     .AddHotChocolate(hc =>
    ///     {
    ///         hc.AddRequestExecutorBuilder(builder);
    ///     });
    /// </code>
    /// </example>
    public static ConfiguratorWrapped AddHotChocolate(this ConfiguratorWrapped serviceInjector,
        Action<HotChocolateConfigurator> action)
    {
        var hotChocolateRegister = new HotChocolateConfigurator();
        action.Invoke(hotChocolateRegister);
        return serviceInjector;
    }
}