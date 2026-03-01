using Microsoft.Extensions.DependencyInjection;
using FxMap.Exceptions;
using FxMap.Extensions;
using FxMap.Configuration;
using FxMap.MongoDb.Registries;
using FxMap.Wrappers;

namespace FxMap.MongoDb.Extensions;

/// <summary>
/// Provides extension methods for integrating MongoDB with the FxMap framework.
/// </summary>
public static class MongoDbExtensions
{
    private static readonly Type MongoDbQueryOfHandlerType = typeof(MongoDbQueryHandler<,>);

    /// <summary>
    /// Adds MongoDB support for FxMap data fetching.
    /// </summary>
    /// <param name="serviceInjector">The FxMap registration wrapper.</param>
    /// <param name="registrarAction">Configuration action for registering MongoDB collections.</param>
    /// <returns>The FxMap registration wrapper for method chaining.</returns>
    /// <exception cref="FxMapException.AddProfilesFromAssemblyContaining">
    /// Thrown when model configurations have not been set up before calling this method.
    /// </exception>
    /// <example>
    /// <code>
    /// services.AddFxMap(cfg =>
    /// {
    ///     cfg.AddAttributesContainNamespaces(typeof(UserOfAttribute).Assembly);
    ///     cfg.AddProfilesFromAssemblyContaining&lt;User&gt;();
    /// })
    /// .AddMongoDb(cfg =>
    /// {
    ///     cfg.AddCollection(mongoDatabase.GetCollection&lt;User&gt;("users"));
    /// });
    /// </code>
    /// </example>
    public static ConfiguratorWrapped AddMongoDb(this ConfiguratorWrapped serviceInjector,
        Action<MongoDbConfigurator> registrarAction)
    {
        if (!FxMapStatics.HasModelConfigurations) throw new FxMapException.AddProfilesFromAssemblyContaining();
        var registrar = new MongoDbConfigurator(serviceInjector.MapConfigurator.ServiceCollection);
        registrarAction.Invoke(registrar);
        var mongoModelTypes = registrar.MongoModelTypes;
        var serviceCollection = serviceInjector.MapConfigurator.ServiceCollection;
        FxMapStatics.EntitiesConfigurations.Value
            .Where(m => mongoModelTypes.Contains(m.ModelType))
            .ForEach(m =>
            {
                var modelType = m.ModelType;
                var attributeType = m.DistributedKeyType;
                var serviceType = FxMapStatics.QueryOfHandlerType.MakeGenericType(modelType, attributeType);
                var implementedType = MongoDbQueryOfHandlerType.MakeGenericType(modelType, attributeType);
                serviceCollection.AddTransient(serviceType, implementedType);
            });
        return serviceInjector;
    }
}