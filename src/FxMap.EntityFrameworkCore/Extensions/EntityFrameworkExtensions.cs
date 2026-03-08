using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using FxMap.EntityFrameworkCore.Abstractions;
using FxMap.EntityFrameworkCore.Implementations;
using FxMap.Exceptions;
using FxMap.Extensions;
using FxMap.Configuration;
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
    /// <exception cref="FxMapException.AddProfilesFromAssemblyContaining">
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
        if (!FxMapStatics.HasModelConfigurations) throw new FxMapException.AddProfilesFromAssemblyContaining();

        var serviceCollection = serviceInjector.MapConfigurator.ServiceCollection;
        var newFxMapEfCoreRegistrar = new EfCoreConfigurator(serviceCollection);
        registrarAction.Invoke(newFxMapEfCoreRegistrar);

        var modelCacheLookup = new ConcurrentDictionary<Type, bool>();

        serviceCollection.AddScoped(typeof(IDbContextResolver<>), typeof(DbContextResolverInternal<>));

        // var efQueryHandler = typeof(EntityFrameworkQueryHandler<,>);
        var efQueryHandler = typeof(EntityFrameworkQueryHandler<,>);
        serviceCollection.AddScoped(efQueryHandler);

        FxMapStatics.EntitiesConfigurations.Value
            .ForEach(m =>
            {
                var modelType = m.ModelType;
                var distributedKeyType = m.DistributedKeyType;
                var serviceType = FxMapStatics.QueryOfHandlerType.MakeGenericType(modelType, distributedKeyType);
                var implementedType = efQueryHandler.MakeGenericType(modelType, distributedKeyType);
                var defaultHandlerType = FxMapStatics.NoOpQueryOfHandlerType.MakeGenericType(modelType, distributedKeyType);
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