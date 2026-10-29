using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using FxMap.Abstractions;
using FxMap.Accessors.TypeAccessors;
using FxMap.Builders;
using FxMap.Delegates;
using FxMap.Handlers;
using FxMap.Implementations;
using FxMap.BuiltInPipelines;
using FxMap.Registries;
using FxMap.Services;
using FxMap.Exceptions;
using FxMap.Fluent;
using FxMap.Models;
using FxMap.Wrappers;

namespace FxMap.Extensions;

/// <summary>
/// Provides the main extension method for adding FxMap services to the dependency injection container.
/// </summary>
public static class DependencyExtensions
{
    private static readonly ConcurrentDictionary<Type, Type> SendOrchestratorTypes = new();

    private static readonly Func<IServiceProvider, Type, SendPipelinesOrchestrator> ContainerSender = static (sp, keyType) =>
        (SendPipelinesOrchestrator)sp.GetRequiredService(SendOrchestratorTypes.GetOrAdd(keyType,
            static t => typeof(SendPipelinesOrchestrator<>).MakeGenericType(t)));

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
    public static ConfiguratorWrapped AddFxMap(this IServiceCollection services, Action<IMapConfigurator> options)
    {
        var mapConfigurator = new MapConfigurator(services);
        options.Invoke(mapConfigurator);

        var noOpClientRequestHandlerType = typeof(NoOpClientRequestHandler<>);

        var entitiesInfos = GetEntitiesInfos(mapConfigurator.EntityConfigs);

        var distributedKeyMapHandlers = GetDistributedKeyMapHandlers(mapConfigurator.EntityConfigs);

        services.AddSingleton<GetProfileConfig>(mapConfigurator.GetProfileConfig);

        services.AddSingleton<GetEntityConfig>(entityType =>
            mapConfigurator.EntityConfigs.GetValueOrDefault(entityType));

        services.AddSingleton<GetTypeAccessor>(sp =>
            type => new TypeAccessor(type, sp.GetRequiredService<GetEntityConfig>()));

        var distributedKeyTypes = new HashSet<Type>(mapConfigurator.ProfileConfigs
            .SelectMany(a => a.Value.RuleGroups)
            .Select(a => a.GetDistributedKeyType()));

        services.AddSingleton<IMapperConfiguration>(new MapperConfiguration(distributedKeyTypes, entitiesInfos,
            distributedKeyMapHandlers, mapConfigurator.MaxNestingDepth, mapConfigurator.MaxConcurrentProcessing,
            mapConfigurator.ThrowIfExceptions, mapConfigurator.DefaultRequestTimeout, mapConfigurator.RetryPolicy,
            mapConfigurator.SupervisorOptions));

        var clientHandlerGenericType = typeof(ClientRequestHandler<>);
        distributedKeyTypes
            .Select(a => (DistributedKeyType: a, HandlerType: clientHandlerGenericType.MakeGenericType(a),
                ServiceType: typeof(IClientRequestHandler<>).MakeGenericType(a)))
            .ForEach(x =>
            {
                var existedService = services.FirstOrDefault(a => a.ServiceType == x.ServiceType);
                if (existedService is not null)
                {
                    var implType = noOpClientRequestHandlerType.MakeGenericType(x.DistributedKeyType);
                    if (existedService.ImplementationType != implType) return;
                    services.Replace(new ServiceDescriptor(x.ServiceType, x.HandlerType, ServiceLifetime.Transient));
                    return;
                }

                services.AddTransient(x.ServiceType, x.HandlerType);
            });


        // Transient, so the send behaviors (Scoped by default) come from the scope of whoever asks for the mapper.
        services.AddTransient<IDistributedMapper>(sp => new DistributedMapper(new MapperRuntime(
            sp.GetRequiredService<IMapperConfiguration>(), sp.GetRequiredService<GetProfileConfig>(),
            ContainerSender, sp, sp.GetService<ILogger<DistributedMapper>>())));

        services.AddSingleton(typeof(IIdConverter<>), typeof(IdConverter<>));

        services.AddTransient(typeof(ReceivedPipelinesOrchestrator<,>));

        services.AddTransient(typeof(SendPipelinesOrchestrator<>));

        services.AddTransient(typeof(NoOpQueryOfHandler<,>));

        services.TryAddSingleton<MapperDelegates>((mt, at) =>
        {
            var config = entitiesInfos
                .First(a => a.EntityType == mt && a.DistributedKeyType == at)
                .MapEntityConfig;
            return new EntityConfiguration(config.IdPropertySelector, config.DefaultPropertySelector);
        });

        // The first one registered is the outermost. The routing sends a request that a local handler can answer to that handler and
        // calls the next ones only for a request that goes through a transport. For such a request the suppression of the errors
        // (ThrowIfExceptions off) must wrap the retries: an exception it swallowed inside them would never be retried, and a retry
        // policy would do nothing by default. The errors of a local handler are neither suppressed nor retried.
        mapConfigurator.AddSendPipelines(c => c
            .OfType(typeof(SendPipelineRoutingBehavior<>))
            .OfType(typeof(ExceptionPipelineBehavior<>))
            .OfType(typeof(RetryPipelineBehavior<>))
        );

        services.AddSingleton(typeof(QueryHandlerBuilder<,,>));
        return new ConfiguratorWrapped(mapConfigurator);
    }

    private static EntityInfo[] GetEntitiesInfos(IReadOnlyDictionary<Type, IFluentEntityConfig> entityConfigs)
    {
        var models = entityConfigs.Select(cfg =>
        {
            var config = cfg.Value;
            var distributedKeyType = config.GetDistributedKeyType();
            return new EntityInfo(config.EntityType, distributedKeyType,
                new EntityConfiguration(config.IdPropertySelector, config.DefaultPropertyNameSelector));
        }).ToArray();
        // Validate if one attribute is assigned to multiple models.
        models.GroupBy(a => a.DistributedKeyType)
            .ForEach(a =>
            {
                if (a.Count() <= 1) return;
                throw new DistributedMapException.DistributedKeyAssignedToMultipleEntities(a.Key,
                    [.. a.Select(o => o.EntityType)]);
            });
        return models;
    }

    private static Dictionary<Type, Type> GetDistributedKeyMapHandlers(
        IReadOnlyDictionary<Type, IFluentEntityConfig> entityConfigs)
    {
        var queryOfHandlerType = typeof(IQueryOfHandler<,>);
        return entityConfigs.Select(m =>
        {
            var config = m.Value;
            var distributedKeyType = config.GetDistributedKeyType();
            var serviceInterfaceType = queryOfHandlerType.MakeGenericType(config.EntityType, distributedKeyType);
            return (DistributedKeyType: distributedKeyType, ServiceInterfaceType: serviceInterfaceType);
        }).ToDictionary(k => k.DistributedKeyType, kv => kv.ServiceInterfaceType);
    }
}