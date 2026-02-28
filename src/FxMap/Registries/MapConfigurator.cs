using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using FxMap.Abstractions;
using FxMap.Models;
using FxMap.Exceptions;
using FxMap.Extensions;
using FxMap.Fluent;
using FxMap.Helpers;
using FxMap.Configuration;
using FxMap.MetadataCache;
using FxMap.Supervision;

namespace FxMap.Registries;

/// <summary>
/// Provides the main configuration API for registering and configuring the FxMap framework.
/// </summary>
/// <remarks>
/// <para>
/// This class is the entry point for configuring FxMap in your application's startup.
/// Use the fluent API to register attributes, handlers, model configurations, and other settings.
/// </para>
/// <example>
/// <code>
/// services.AddFxMap(cfg =>
/// {
///     cfg.AddAttributesContainNamespaces(typeof(UserOfAttribute).Assembly);
///     cfg.AddProfilesFromAssemblyContaining&lt;User&gt;();
///     cfg.SetRequestTimeOut(TimeSpan.FromSeconds(60));
/// });
/// </code>
/// </example>
/// </remarks>
/// <param name="serviceCollection">The service collection to register services into.</param>
public class MapConfigurator(IServiceCollection serviceCollection)
{
    /// <summary>
    /// Gets the underlying service collection for advanced registration scenarios.
    /// </summary>
    public IServiceCollection ServiceCollection { get; } = serviceCollection;

    /// <summary>
    /// Registers custom client request handlers from the specified assembly.
    /// </summary>
    /// <typeparam name="TAssemblyMarker">A type in the assembly to scan for handlers.</typeparam>
    public void AddHandlersFromNamespaceContaining<TAssemblyMarker>()
    {
        var mappableRequestHandlerType = typeof(IClientRequestHandler<>);
        var deepestClassesWithInterface = LeafImplementationFinder
            .GetDeepestClassesWithInterface(typeof(TAssemblyMarker).Assembly, mappableRequestHandlerType);

        deepestClassesWithInterface.GroupBy(a => a.ImplementedClosedInterface)
            .ForEach(it =>
            {
                var interfaceType = it.Key;
                if (it.Count() > 1) throw new FxMapException.AmbiguousHandlers(it.Key);
                var attributeArgument = interfaceType.GetGenericArguments()[0];
                var serviceType = mappableRequestHandlerType.MakeGenericType(attributeArgument);
                var existedService = ServiceCollection.FirstOrDefault(a => a.ServiceType == serviceType);
                var handlerType = it.First().ClassType;
                if (existedService is null)
                {
                    ServiceCollection.AddTransient(serviceType, handlerType);
                    return;
                }

                ServiceCollection.Replace(new ServiceDescriptor(serviceType, handlerType,
                    ServiceLifetime.Transient));
            });
    }

    /// <summary>
    /// Registers the assemblies containing FxMap attribute definitions.
    /// </summary>
    /// <param name="attributeAssembly">The primary assembly containing FxMap attributes.</param>
    /// <param name="otherAssemblies">Additional assemblies to scan for attributes.</param>
    public void AddAttributesContainNamespaces(Assembly attributeAssembly, params Assembly[] otherAssemblies)
    {
        ArgumentNullException.ThrowIfNull(attributeAssembly);
        FxMapStatics.AttributesRegister = [attributeAssembly, ..otherAssemblies ?? []];
    }

    /// <summary>
    /// Scans the specified assembly for <see cref="AbstractFxMapConfig{TModel}"/> and <see cref="ProfileOf{TModel}"/>
    /// implementations, builds them, and registers their configurations.
    /// </summary>
    /// <typeparam name="TAssemblyMarker">A type in the assembly to scan for fluent configurations.</typeparam>
    public void AddProfilesFromAssemblyContaining<TAssemblyMarker>()
    {
        var assembly = typeof(TAssemblyMarker).Assembly;
        ScanFluentConfigs(assembly);
    }

    /// <summary>
    /// Scans the specified assemblies for fluent configurations.
    /// </summary>
    public void AddProfilesFromAssemblies(params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies)
            ScanFluentConfigs(assembly);
    }

    private static void ScanFluentConfigs(Assembly assembly)
    {
        var concreteTypes = assembly.ExportedTypes
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .ToArray();

        foreach (var type in concreteTypes)
        {
            if (type.IsAssignableTo(typeof(IFluentEntityConfig)))
            {
                var config = (IFluentEntityConfig)Activator.CreateInstance(type)!;
                FluentConfigStore.EntityConfigs[config.ModelType] = new EntityConfigMetadata(
                    config.ModelType,
                    config.DistributedKeyType,
                    config.DistributedKey,
                    config.IdPropertyName,
                    config.DefaultPropertyName,
                    config.ExposedNameStores);
            }
            else if (type.IsAssignableTo(typeof(IFluentProfileConfig)))
            {
                var profile = (IFluentProfileConfig)Activator.CreateInstance(type)!;
                profile.Build();
                FluentConfigStore.ProfileConfigs[profile.ModelType] = profile;
            }
        }
    }

    /// <summary>
    /// Enables throwing exceptions during mapping operations instead of silently failing.
    /// </summary>
    public void ThrowIfException() => FxMapStatics.ThrowIfExceptions = true;

    /// <summary>
    /// Sets the maximum depth for nested object mapping to prevent infinite recursion.
    /// </summary>
    /// <param name="maxNestingDepth">The maximum nesting depth. Must be non-negative.</param>
    public void SetMaxNestingDepth(int maxNestingDepth)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxNestingDepth);
        FxMapStatics.MaxNestingDepth = maxNestingDepth;
    }

    /// <summary>
    /// Sets the maximum number of concurrent message processing operations for transport servers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This setting controls backpressure in message-based transports (NATS, RabbitMQ, Kafka).
    /// When the limit is reached, new incoming messages will wait until a processing slot becomes available.
    /// </para>
    /// <para>
    /// Higher values allow more throughput but consume more memory and CPU resources.
    /// Lower values provide better resource control but may reduce throughput under high load.
    /// </para>
    /// </remarks>
    /// <param name="maxConcurrentProcessing">The maximum number of concurrent operations. Must be at least 1. Default is 128.</param>
    /// <example>
    /// <code>
    /// services.AddFxMap(cfg =>
    /// {
    ///     cfg.SetMaxConcurrentProcessing(256); // Allow up to 256 concurrent message processing
    /// });
    /// </code>
    /// </example>
    public void SetMaxConcurrentProcessing(int maxConcurrentProcessing)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrentProcessing, 1);
        FxMapStatics.MaxConcurrentProcessing = maxConcurrentProcessing;
    }

    /// <summary>
    /// Sets the default timeout for FxMap requests.
    /// </summary>
    /// <param name="timeout">The timeout duration. Must be non-negative.</param>
    public void SetRequestTimeOut(TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);
        FxMapStatics.DefaultRequestTimeout = timeout;
    }

    /// <summary>
    /// Configures the retry policy for failed requests.
    /// </summary>
    /// <param name="retryCount">The maximum number of retry attempts.</param>
    /// <param name="sleepDurationProvider">A function to calculate delay between retries.</param>
    /// <param name="onRetry">A callback invoked on each retry attempt.</param>
    public void SetRetryPolicy(int retryCount = 3, Func<int, TimeSpan> sleepDurationProvider = null,
        Action<Exception, TimeSpan> onRetry = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(retryCount, 0);
        FxMapStatics.RetryPolicy = new RetryPolicy(retryCount, sleepDurationProvider, onRetry);
    }

    /// <summary>
    /// Configures the global supervisor options for message-based transport servers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This sets the supervisor configuration for message-based transports: NATS, RabbitMQ, Kafka, and Azure Service Bus.
    /// </para>
    /// <para>
    /// <b>Note:</b> This configuration does <b>not</b> apply to gRPC transport. gRPC uses HTTP/2 which has built-in
    /// connection recovery managed by ASP.NET Core's Kestrel server, making the supervisor pattern unnecessary.
    /// </para>
    /// <para>
    /// The supervisor pattern provides automatic failure recovery with features like:
    /// </para>
    /// <list type="bullet">
    /// <item>Exponential backoff for restart delays</item>
    /// <item>Configurable restart limits within a time window</item>
    /// <item>Circuit breaker to prevent restart storms</item>
    /// <item>Exception type to directive mapping</item>
    /// </list>
    /// </remarks>
    /// <param name="configure">An action to configure the supervisor options.</param>
    /// <example>
    /// <code>
    /// services.AddFxMap(cfg =>
    /// {
    ///     cfg.ConfigureSupervisor(opts =>
    ///     {
    ///         opts.Strategy = SupervisionStrategy.OneForOne;
    ///         opts.MaxRestarts = 5;
    ///         opts.EnableCircuitBreaker = true;
    ///         opts.CircuitBreakerThreshold = 3;
    ///     });
    ///     cfg.AddRabbitMq(c => c.Host("localhost", "/"));  // Supervisor applies
    ///     cfg.AddNats(c => c.Url("nats://localhost:4222")); // Supervisor applies
    ///     // cfg.AddGrpcClients(...); // Supervisor does NOT apply to gRPC
    /// });
    /// </code>
    /// </example>
    public void ConfigureSupervisor(Action<SupervisorOptions> configure)
    {
        FxMapStatics.SupervisorOptions ??= new SupervisorOptions();
        configure(FxMapStatics.SupervisorOptions);
    }
}