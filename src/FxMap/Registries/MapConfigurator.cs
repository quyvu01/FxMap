using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using FxMap.Exceptions;
using FxMap.Models;
using FxMap.Extensions;
using FxMap.Fluent;
using FxMap.Supervision;

namespace FxMap.Registries;

/// <summary>
/// Provides the main configuration API for registering and configuring the FxMap framework.
/// </summary>
/// <remarks>
/// <para>
/// This class is the entry point for configuring FxMap in your application's startup.
/// Use the FluentAPI to register profiles, entity configurations, handlers, and other settings.
/// </para>
/// <example>
/// <code>
/// services.AddFxMap(cfg =>
/// {
///     cfg.AddProfilesFromAssemblyContaining&lt;OrderResponseProfile&gt;();
///     cfg.AddEntitiesFromAssemblyContaining&lt;UserEntityConfig&gt;();
///     cfg.SetRequestTimeOut(TimeSpan.FromSeconds(60));
/// });
/// </code>
/// </example>
/// </remarks>
/// <param name="services">The service collection to register services into.</param>
public class MapConfigurator(IServiceCollection services)
{
    private readonly HashSet<Type> _knownEntityTypes = [];
    private readonly HashSet<Type> _knownProfileTypes = [];
    private readonly ConcurrentDictionary<Type, IFluentProfileConfig> _profileConfigs = [];
    private readonly ConcurrentDictionary<Type, IFluentEntityConfig> _entityConfigs = [];
    public IReadOnlyDictionary<Type, IFluentProfileConfig> ProfileConfigs => _profileConfigs.AsReadOnly();
    public IReadOnlyDictionary<Type, IFluentEntityConfig> EntityConfigs => _entityConfigs;
    public int MaxNestingDepth { get; private set; } = 128;
    public int MaxConcurrentProcessing { get; private set; } = 128;
    public bool ThrowIfExceptions { get; private set; }
    public TimeSpan DefaultRequestTimeout { get; private set; } = TimeSpan.FromSeconds(30);
    internal RetryPolicy RetryPolicy { get; private set; }
    public SupervisorOptions SupervisorOptions { get; private set; }

    /// <summary>
    /// Gets the underlying service collection for advanced registration scenarios.
    /// </summary>
    public IServiceCollection Services { get; } = services;

    #region Profile Configurations

    /// <summary>
    /// Scans the specified assembly for <see cref="ProfileOf{TModel}"/> and <see cref="ProfileOf{TModel}"/>
    /// implementations, builds them, and registers their configurations.
    /// </summary>
    /// <typeparam name="TAssemblyMarker">A type in the assembly to scan for fluent configurations.</typeparam>
    public void AddProfilesFromAssemblyContaining<TAssemblyMarker>()
    {
        var assembly = typeof(TAssemblyMarker).Assembly;
        ScanProfileConfigs(assembly);
    }

    /// <summary>
    /// Scans the specified assemblies for fluent configurations.
    /// </summary>
    public void AddProfilesFromAssemblies(params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies)
            ScanProfileConfigs(assembly);
    }

    public void AddProfileConfig<TProfileConfig>() where TProfileConfig : IFluentProfileConfig
    {
        var profileType = typeof(TProfileConfig);
        if (profileType.IsClosedConcreteType()) AddProfileConfig(profileType);
    }

    public void AddProfileConfigs(params Type[] profiles)
    {
        var candidates = profiles
            .Where(t => t.IsClosedConcreteType() && t.IsAssignableTo(typeof(IFluentProfileConfig)) &&
                        _knownProfileTypes.Add(t))
            .ToList();
        candidates
            .Where(t => !candidates.Any(other => other != t && other.IsSubclassOf(t)))
            .ForEach(AddProfileConfig);
    }

    private void ScanProfileConfigs(Assembly assembly) => AddProfileConfigs([..assembly.ExportedTypes]);

    private void AddProfileConfig(Type profileType)
    {
        var profile = (IFluentProfileConfig)Activator.CreateInstance(profileType)!;
        profile.Build();
        if (!_profileConfigs.TryAdd(profile.ModelType, profile))
            throw new DistributedMapException.AmbiguousProfileConfiguration(
                _profileConfigs[profile.ModelType].GetType(), profileType, profile.ModelType);
    }

    internal IFluentProfileConfig GetProfileConfig(Type modelType) => _profileConfigs
        .GetOrAdd(modelType, static mt =>
        {
            var profile = (IFluentProfileConfig)Activator
                .CreateInstance(typeof(VirtualProfileOf<>).MakeGenericType(mt))!;
            profile.Build();
            return profile;
        });

    #endregion

    #region Entity Configurations

    public void AddEntitiesFromAssemblyContaining<TAssemblyMarker>()
    {
        var assembly = typeof(TAssemblyMarker).Assembly;
        ScanEntityConfigs(assembly);
    }

    public void AddEntitiesFromAssemblies(params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies)
            ScanEntityConfigs(assembly);
    }

    public void AddEntityConfig<TEntityConfig>() where TEntityConfig : IFluentEntityConfig
    {
        var type = typeof(TEntityConfig);
        if (type.IsClosedConcreteType()) AddEntityConfig(type);
    }

    public void AddEntityConfigs(params Type[] profiles)
    {
        var candidates = profiles
            .Where(t => t.IsClosedConcreteType() && t.IsAssignableTo(typeof(IFluentEntityConfig)) &&
                        _knownEntityTypes.Add(t))
            .ToList();
        candidates
            .Where(t => !candidates.Any(other => other != t && other.IsSubclassOf(t)))
            .ForEach(AddEntityConfig);
    }

    private void ScanEntityConfigs(Assembly assembly) => AddEntityConfigs([..assembly.ExportedTypes]);

    private void AddEntityConfig(Type entityType)
    {
        var config = (IFluentEntityConfig)Activator.CreateInstance(entityType)!;
        if (!_entityConfigs.TryAdd(config.EntityType, config))
            throw new DistributedMapException.AmbiguousEntityConfiguration(
                _entityConfigs[config.EntityType].GetType(), entityType, config.EntityType);
    }

    #endregion

    /// <summary>
    /// Enables throwing exceptions during mapping operations instead of silently failing.
    /// </summary>
    public void ThrowIfException() => ThrowIfExceptions = true;

    /// <summary>
    /// Sets the maximum depth for nested object mapping to prevent infinite recursion.
    /// </summary>
    /// <param name="maxNestingDepth">The maximum nesting depth. Must be non-negative.</param>
    public void SetMaxNestingDepth(int maxNestingDepth)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxNestingDepth);
        MaxNestingDepth = maxNestingDepth;
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
        MaxConcurrentProcessing = maxConcurrentProcessing;
    }

    /// <summary>
    /// Sets the default timeout for FxMap requests.
    /// </summary>
    /// <param name="timeout">The timeout duration. Must be non-negative.</param>
    public void SetRequestTimeOut(TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);
        DefaultRequestTimeout = timeout;
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
        RetryPolicy = new RetryPolicy(retryCount, sleepDurationProvider, onRetry);
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
        SupervisorOptions ??= new SupervisorOptions();
        configure(SupervisorOptions);
    }
}