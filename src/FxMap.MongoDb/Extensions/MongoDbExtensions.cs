using Microsoft.Extensions.DependencyInjection;
using FxMap.Abstractions;
using FxMap.Exceptions;
using FxMap.Extensions;
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
    /// <exception cref="DistributedMapException.AddProfilesFromAssemblyContaining">
    /// Thrown when model configurations have not been set up before calling this method.
    /// </exception>
    /// <example>
    /// <code>
    /// services.AddFxMap(cfg =>
    /// {
    ///     cfg.AddProfilesFromAssemblyContaining&lt;OrderResponseProfile&gt;();
    ///     cfg.AddEntitiesFromAssemblyContaining&lt;UserEntityConfig&gt;();
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
        var entityConfig = serviceInjector.MapConfigurator.EntityConfigs;
        if (entityConfig is not { Count: > 0 })
            throw new DistributedMapException.AddProfilesFromAssemblyContaining(); // Todo: update Exception again!
        var registrar = new MongoDbConfigurator(serviceInjector.MapConfigurator.Services);
        registrarAction.Invoke(registrar);
        var mongoModelTypes = registrar.MongoModelTypes;
        var serviceCollection = serviceInjector.MapConfigurator.Services;
        entityConfig
            .Select(a => a.Value)
            .Where(m => mongoModelTypes.Contains(m.EntityType))
            .ForEach(m =>
            {
                var modelType = m.EntityType;
                var distributedKeyType = m.GetDistributedKeyType();
                var serviceType = typeof(IQueryOfHandler<,>).MakeGenericType(modelType, distributedKeyType);
                var implementedType = MongoDbQueryOfHandlerType.MakeGenericType(modelType, distributedKeyType);
                serviceCollection.AddTransient(serviceType, implementedType);
            });
        return serviceInjector;
    }
}