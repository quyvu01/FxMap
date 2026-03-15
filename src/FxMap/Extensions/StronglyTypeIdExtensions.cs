using FxMap.Models;
using FxMap.Exceptions;
using FxMap.Registries;

namespace FxMap.Extensions;

/// <summary>
/// Provides extension methods for registering strongly-typed ID converters in the FxMap framework.
/// </summary>
public static class StronglyTypeIdExtensions
{
    /// <summary>
    /// Registers custom strongly-typed ID converters for the FxMap framework.
    /// </summary>
    /// <param name="mapRegister">The FxMap registration instance.</param>
    /// <param name="options">Configuration action for registering ID converters.</param>
    /// <exception cref="DistributedMapException.StronglyTypeConfigurationMustNotBeNull">
    /// Thrown when the options parameter is null.
    /// </exception>
    /// <example>
    /// <code>
    /// services.AddFxMap(cfg =>
    /// {
    ///     cfg.AddStronglyTypeId(c => c.OfType&lt;UserIdConverter&gt;().OfType&lt;OrderIdConverter&gt;());
    /// });
    /// </code>
    /// </example>
    public static void AddStronglyTypeIdConverter(this MapConfigurator mapRegister, Action<StronglyTypeIdRegister> options)
    {
        if (options is null) throw new DistributedMapException.StronglyTypeConfigurationMustNotBeNull();
        var stronglyTypeIdRegister = new StronglyTypeIdRegister(mapRegister.Services);
        options.Invoke(stronglyTypeIdRegister);
    }
}