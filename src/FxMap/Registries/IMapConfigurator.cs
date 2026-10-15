using System.Reflection;
using FxMap.Fluent;
using FxMap.Supervision;
using Microsoft.Extensions.DependencyInjection;

namespace FxMap.Registries;

/// <summary>
/// The configuration API of FxMap, passed to the callback of <c>AddFxMap</c>. Register the profiles and the entity configurations, set the
/// limits, the timeout, the retry policy and the supervisor, and add a transport or a data provider with its own extension method.
/// </summary>
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
public interface IMapConfigurator
{
    /// <summary>
    /// Gets the underlying service collection for advanced registration scenarios.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Scans the specified assembly for <see cref="ProfileOf{TModel}"/> and <see cref="ProfileOf{TModel}"/>
    /// implementations, builds them, and registers their configurations.
    /// </summary>
    /// <typeparam name="TAssemblyMarker">A type in the assembly to scan for fluent configurations.</typeparam>
    void AddProfilesFromAssemblyContaining<TAssemblyMarker>();

    /// <summary>
    /// Scans the specified assemblies for fluent configurations.
    /// </summary>
    void AddProfilesFromAssemblies(params Assembly[] assemblies);

    /// <summary>Registers one profile (a <see cref="ProfileOf{TModel}"/>).</summary>
    void AddProfileConfig<TProfileConfig>() where TProfileConfig : IFluentProfileConfig;

    /// <summary>Registers profiles by type; the types that are not closed, concrete profiles are ignored.</summary>
    void AddProfileConfigs(params Type[] profiles);

    /// <summary>Scans the assembly of the type for entity configurations (an <see cref="EntityConfigureOf{TEntity}"/>) and registers them.</summary>
    void AddEntitiesFromAssemblyContaining<TAssemblyMarker>();

    /// <summary>Scans the assemblies for entity configurations and registers them.</summary>
    void AddEntitiesFromAssemblies(params Assembly[] assemblies);

    /// <summary>Registers one entity configuration (an <see cref="EntityConfigureOf{TEntity}"/>).</summary>
    void AddEntityConfig<TEntityConfig>() where TEntityConfig : IFluentEntityConfig;

    /// <summary>Registers entity configurations by type; the types that are not closed, concrete configurations are ignored.</summary>
    void AddEntityConfigs(params Type[] profiles);

    /// <summary>
    /// Enables throwing exceptions during mapping operations instead of silently failing.
    /// </summary>
    void ThrowIfException();

    /// <summary>
    /// Sets the maximum depth for nested object mapping to prevent infinite recursion.
    /// </summary>
    /// <param name="maxNestingDepth">The maximum nesting depth. Must be non-negative.</param>
    void SetMaxNestingDepth(int maxNestingDepth);

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
    void SetMaxConcurrentProcessing(int maxConcurrentProcessing);

    /// <summary>
    /// Sets the default timeout for FxMap requests.
    /// </summary>
    /// <param name="timeout">The timeout duration. Must be non-negative.</param>
    void SetRequestTimeOut(TimeSpan timeout);

    /// <summary>
    /// Configures the retry policy for failed requests.
    /// </summary>
    /// <param name="retryCount">The maximum number of retries after the first attempt (3 means at most 4 attempts).</param>
    /// <param name="sleepDurationProvider">
    /// A function that returns the delay before a retry; its parameter is the number of the retry (1 for the first retry).
    /// Without it the retries follow each other at once. An exponential backoff with jitter is usual:
    /// <c>n => TimeSpan.FromMilliseconds(Random.Shared.Next(0, 200 * (1 &lt;&lt; (n - 1))))</c>.
    /// </param>
    /// <param name="onRetry">
    /// A callback invoked before each retry, with the exception of the attempt that failed and the delay that is about to be waited.
    /// It is not invoked for the last failure.
    /// </param>
    /// <param name="shouldRetry">
    /// Decides whether an exception is worth a retry; when it returns false the exception is thrown at once. Without it every exception
    /// is retried (but not when the caller cancelled the request). Retry only the failures that can go away, such as a timeout or a
    /// connection that was lost, not an error that the same request will cause again.
    /// </param>
    /// <remarks>
    /// The retries wrap the requests that go through a transport (a request that a local handler answers is not retried), and the
    /// suppression of the errors (when <see cref="ThrowIfException"/> is not called) wraps the retries: an exception is suppressed only
    /// after the last attempt failed. The last exception is the one that is thrown (or suppressed).
    /// </remarks>
    void SetRetryPolicy(int retryCount = 3, Func<int, TimeSpan> sleepDurationProvider = null, Action<Exception, TimeSpan> onRetry = null, Func<Exception, bool> shouldRetry = null);

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
    void ConfigureSupervisor(Action<SupervisorOptions> configure);
}
