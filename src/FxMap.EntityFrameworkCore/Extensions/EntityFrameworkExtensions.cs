using System.Collections.Concurrent;
using FxMap.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using FxMap.EntityFrameworkCore.Abstractions;
using FxMap.EntityFrameworkCore.Implementations;
using FxMap.Exceptions;
using FxMap.Extensions;
using FxMap.EntityFrameworkCore.Registries;
using FxMap.Wrappers;

namespace FxMap.EntityFrameworkCore.Extensions;

/// <summary>
/// Provides extension methods for integrating Entity Framework Core with the FxMap framework.
/// </summary>
public static class EntityFrameworkExtensions
{
    /// <summary>
    /// Adds Entity Framework Core support for FxMap data fetching.
    /// </summary>
    /// <param name="serviceInjector">The FxMap registration wrapper.</param>
    /// <param name="registrarAction">Configuration action for registering DbContexts.</param>
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
    /// .AddEntityFrameworkCore(cfg =>
    /// {
    ///     cfg.AddDbContexts(typeof(ApplicationDbContext));
    /// });
    /// </code>
    /// </example>
    public static ConfiguratorWrapped AddEntityFrameworkCore(this ConfiguratorWrapped serviceInjector,
        Action<EfCoreConfigurator> registrarAction)
    {
        var entityConfig = serviceInjector.MapConfigurator.EntityConfigs;
        if (entityConfig is not { Count: > 0 })
            throw new DistributedMapException.AddProfilesFromAssemblyContaining(); // Todo: update Exception again!

        var serviceCollection = serviceInjector.MapConfigurator.Services;
        var newFxMapEfCoreRegistrar = new EfCoreConfigurator(serviceCollection);
        registrarAction.Invoke(newFxMapEfCoreRegistrar);

        var modelCacheLookup = new ConcurrentDictionary<Type, bool>();

        serviceCollection.AddScoped(typeof(IDbContextResolver<>), typeof(DbContextResolverInternal<>));

        // var efQueryHandler = typeof(EntityFrameworkQueryHandler<,>);
        var efQueryHandler = typeof(EntityFrameworkQueryHandler<,>);
        serviceCollection.AddScoped(efQueryHandler);

        entityConfig
            .ForEach(m =>
            {
                var config = m.Value;
                var modelType = config.EntityType;
                var distributedKeyType = config.GetDistributedKeyType();
                var serviceType = typeof(IQueryOfHandler<,>).MakeGenericType(modelType, distributedKeyType);
                var implementedType = efQueryHandler.MakeGenericType(modelType, distributedKeyType);
                var defaultHandlerType = typeof(NoOpQueryOfHandler<,>).MakeGenericType(modelType, distributedKeyType);
                serviceCollection.AddScoped(serviceType, sp =>
                {
                    var modelCached = modelCacheLookup.GetOrAdd(modelType, mt =>
                    {
                        var fxMapDbContexts = sp.GetServices<IDbContext>();
                        return fxMapDbContexts.Any(x => x.HasCollection(mt));
                    });
                    return sp.GetService(modelCached ? implementedType : defaultHandlerType);
                });
            });


        return serviceInjector;
    }
}