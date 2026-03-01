using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using FxMap.Abstractions;
using FxMap.Delegates;
using FxMap.Exceptions;
using FxMap.Handlers;
using FxMap.Implementations;
using FxMap.BuiltInPipelines;
using FxMap.Registries;
using FxMap.Services;
using FxMap.Configuration;
using FxMap.Wrappers;

namespace FxMap.Extensions;

/// <summary>
/// Provides the main extension method for adding FxMap services to the dependency injection container.
/// </summary>
public static class FxMapExtensions
{
    /// <summary>
    /// Adds the FxMap distributed mapping framework to the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="options">Configuration action for setting up FxMap.</param>
    /// <returns>A wrapped registration object for chaining transport extensions.</returns>
    /// <example>
    /// <code>
    /// services.AddFxMap(cfg =>
    /// {
    ///     cfg.AddProfilesFromAssemblyContaining&lt;OrderResponseProfile&gt;();
    ///     cfg.AddEntitiesFromAssemblyContaining&lt;UserEntityConfig&gt;();
    /// })
    /// .AddEntityFrameworkCore(cfg => cfg.AddDbContexts(typeof(ApplicationDbContext)));
    /// </code>
    /// </example>
    public static ConfiguratorWrapped AddFxMap(this IServiceCollection services, Action<MapConfigurator> options)
    {
        FxMapStatics.Clear();
        var newOfRegister = new MapConfigurator(services);
        options.Invoke(newOfRegister);

        var noOpClientRequestHandlerType = typeof(NoOpClientRequestHandler<>);

        var modelConfigurations = FxMapStatics.EntitiesConfigurations.Value;
        var attributeTypes = FxMapStatics.DistributedKeyTypes.Value;

        var clientHandlerGenericType = typeof(ClientRequestHandler<>);
        attributeTypes
            .Select(a => (AttributeType: a, HandlerType: clientHandlerGenericType.MakeGenericType(a),
                ServiceType: typeof(IClientRequestHandler<>).MakeGenericType(a)))
            .ForEach(x =>
            {
                var existedService = services.FirstOrDefault(a => a.ServiceType == x.ServiceType);
                if (existedService is not null)
                {
                    var implType = noOpClientRequestHandlerType.MakeGenericType(x.AttributeType);
                    if (existedService.ImplementationType != implType) return;
                    services.Replace(new ServiceDescriptor(x.ServiceType, x.HandlerType, ServiceLifetime.Transient));
                    return;
                }

                services.AddTransient(x.ServiceType, x.HandlerType);
            });


        services.AddTransient<IDistributedMapper, DistributedMapper>();

        services.AddSingleton(typeof(IIdConverter<>), typeof(IdConverter<>));

        services.AddTransient(typeof(ReceivedPipelinesOrchestrator<,>));

        services.AddTransient(typeof(SendPipelinesOrchestrator<>));

        services.AddTransient(typeof(NoOpQueryOfHandler<,>));

        services.TryAddSingleton<GetFxMapConfiguration>(_ => (mt, at) =>
        {
            var config = modelConfigurations
                .First(a => a.ModelType == mt && a.DistributedKeyType == at)
                .MapEntityConfig;
            return new FxMapEntityConfig(config.IdProperty, config.DefaultProperty);
        });

        newOfRegister.AddSendPipelines(c => c
            .OfType(typeof(RetryPipelineBehavior<>))
            .OfType(typeof(SendPipelineRoutingBehavior<>))
            .OfType(typeof(ExceptionPipelineBehavior<>))
        );

        return new ConfiguratorWrapped(newOfRegister);
    }
}