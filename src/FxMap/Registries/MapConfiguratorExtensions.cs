namespace FxMap.Registries;

internal static class MapConfiguratorExtensions
{
    /// <summary>
    /// The implementation behind <see cref="IMapConfigurator"/>, for the packages of FxMap that read the settings it holds
    /// (the supervisor options, the entity configurations...). The interface is the only thing the users of FxMap see.
    /// </summary>
    internal static MapConfigurator Internal(this IMapConfigurator configurator) =>
        configurator as MapConfigurator ??
        throw new InvalidOperationException(
            $"{configurator?.GetType().Name ?? "null"} is not the configurator that AddFxMap creates.");
}
