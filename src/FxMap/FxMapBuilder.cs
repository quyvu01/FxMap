using FxMap.Abstractions;
using FxMap.Abstractions.Transporting;
using FxMap.Exceptions;
using FxMap.Implementations;
using FxMap.Registries;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FxMap;

/// <summary>
/// Builds an <see cref="IDistributedMapper"/> without a dependency injection container: for a console app, a function,
/// a test, or any host that does not use Microsoft.Extensions.DependencyInjection. <c>services.AddFxMap(...)</c> stays
/// the way to use FxMap with a container.
/// </summary>
/// <example>
/// <code>
/// await using var nats = new NatsClient("nats://localhost:4222");
/// var mapper = FxMapBuilder.Create()
///     .AddProfilesFromAssemblyContaining&lt;OrderResponseProfile&gt;()
///     .UseRequestClient(FxMapNats.CreateRequestClient(nats))
///     .SetRetryPolicy(3)
///     .Build();
/// await mapper.MapDataAsync(orders);
/// </code>
/// </example>
public sealed class FxMapBuilder
{
    // The configurator of AddFxMap without a service collection: only its profiles and its options are used.
    private readonly MapConfigurator _configurator = new(null);
    private readonly List<Type> _sendBehaviorTypes = [];
    private readonly Dictionary<Type, (Type ModelType, object Orchestrator)> _localHandlers = [];
    private IRequestClient _requestClient;
    private IServiceProvider _services;
    private ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

    private FxMapBuilder()
    {
    }

    public static FxMapBuilder Create() => new();

    /// <inheritdoc cref="IMapConfigurator.AddProfilesFromAssemblyContaining{TAssemblyMarker}"/>
    public FxMapBuilder AddProfilesFromAssemblyContaining<TAssemblyMarker>()
    {
        _configurator.AddProfilesFromAssemblyContaining<TAssemblyMarker>();
        return this;
    }

    /// <inheritdoc cref="IMapConfigurator.AddProfileConfigs"/>
    public FxMapBuilder AddProfiles(params Type[] profileTypes)
    {
        _configurator.AddProfileConfigs(profileTypes);
        return this;
    }

    /// <inheritdoc cref="IMapConfigurator.ThrowIfException"/>
    public FxMapBuilder ThrowIfException()
    {
        _configurator.ThrowIfException();
        return this;
    }

    /// <inheritdoc cref="IMapConfigurator.SetMaxNestingDepth"/>
    public FxMapBuilder SetMaxNestingDepth(int maxNestingDepth)
    {
        _configurator.SetMaxNestingDepth(maxNestingDepth);
        return this;
    }

    /// <inheritdoc cref="IMapConfigurator.SetRequestTimeOut"/>
    public FxMapBuilder SetRequestTimeOut(TimeSpan timeout)
    {
        _configurator.SetRequestTimeOut(timeout);
        return this;
    }

    /// <inheritdoc cref="IMapConfigurator.SetRetryPolicy"/>
    public FxMapBuilder SetRetryPolicy(int retryCount = 3, Func<int, TimeSpan> sleepDurationProvider = null,
        Action<Exception, TimeSpan> onRetry = null, Func<Exception, bool> shouldRetry = null)
    {
        _configurator.SetRetryPolicy(retryCount, sleepDurationProvider, onRetry, shouldRetry);
        return this;
    }

    /// <summary>The transport that sends the requests (NATS, RabbitMQ, gRPC...).</summary>
    public FxMapBuilder UseRequestClient(IRequestClient requestClient)
    {
        _requestClient = requestClient ?? throw new ArgumentNullException(nameof(requestClient));
        return this;
    }

    /// <summary>
    /// A send behavior, as <c>AddSendPipelines(c => c.OfType(...))</c>: an open generic type
    /// (<c>typeof(LoggingBehavior&lt;&gt;)</c>) runs for every distributed key, a closed one for its key only. The
    /// parameters of its constructor come from <see cref="UseServices"/>. The first one added is the outermost.
    /// </summary>
    public FxMapBuilder AddSendBehavior(Type behaviorType)
    {
        ArgumentNullException.ThrowIfNull(behaviorType);
        var isBehavior = behaviorType.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISendPipelineBehavior<>));
        if (!isBehavior) throw new DistributedMapException.TypeIsNotSendPipelineBehavior(behaviorType);
        _sendBehaviorTypes.Add(behaviorType);
        return this;
    }

    /// <summary>
    /// Answers <typeparamref name="TDistributedKey"/> in process with <paramref name="handler"/>, as a service that owns
    /// the entity does with AddFxMap: such a key needs no request client, and the errors of the handler are neither
    /// suppressed nor retried.
    /// </summary>
    public FxMapBuilder AddQueryHandler<TModel, TDistributedKey>(IQueryOfHandler<TModel, TDistributedKey> handler,
        params IReceivedPipelineBehavior<TDistributedKey>[] behaviors)
        where TModel : class where TDistributedKey : IDistributedKey
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (_localHandlers.TryGetValue(typeof(TDistributedKey), out var existing))
            throw new DistributedMapException.DistributedKeyAssignedToMultipleEntities(typeof(TDistributedKey),
                [existing.ModelType, typeof(TModel)]);
        var orchestrator = new ReceivedPipelinesOrchestrator<TModel, TDistributedKey>(behaviors ?? [], [handler], []);
        _localHandlers[typeof(TDistributedKey)] = (typeof(TModel), orchestrator);
        return this;
    }

    /// <summary>
    /// What conditional expressions (<c>If(sp => ...)</c>) and the constructors of send behaviors receive. Any
    /// <see cref="IServiceProvider"/> works; without it they find no service.
    /// </summary>
    public FxMapBuilder UseServices(IServiceProvider services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        return this;
    }

    /// <summary>Where the mapper and its pipelines log; nothing is logged without it.</summary>
    public FxMapBuilder UseLoggerFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        return this;
    }

    /// <exception cref="InvalidOperationException">
    /// A profile uses a distributed key that has neither a local handler nor a request client to send it.
    /// </exception>
    public IDistributedMapper Build()
    {
        var distributedKeyTypes = new HashSet<Type>(_configurator.ProfileConfigs
            .SelectMany(a => a.Value.RuleGroups)
            .Select(a => a.GetDistributedKeyType()));
        var remoteKeys = distributedKeyTypes.Where(k => !_localHandlers.ContainsKey(k)).ToArray();
        if (_requestClient is null && remoteKeys.Length > 0)
            throw new InvalidOperationException(
                $"Nothing answers {string.Join(", ", remoteKeys.Select(k => k.Name))}: call UseRequestClient or AddQueryHandler.");

        // The routing to a local handler reads the handler type of a key here, as with AddFxMap.
        var localHandlerTypes = _localHandlers.ToDictionary(kv => kv.Key,
            kv => typeof(IQueryOfHandler<,>).MakeGenericType(kv.Value.ModelType, kv.Key));
        var configuration = new MapperConfiguration(distributedKeyTypes, [], localHandlerTypes,
            _configurator.MaxNestingDepth, _configurator.MaxConcurrentProcessing, _configurator.ThrowIfExceptions,
            _configurator.DefaultRequestTimeout, _configurator.RetryPolicy, _configurator.SupervisorOptions);
        var serviceProvider = new StandaloneServiceProvider(configuration, _configurator.GetProfileConfig,
            _requestClient, [.. _sendBehaviorTypes],
            _localHandlers.ToDictionary(kv => kv.Key, kv => kv.Value.Orchestrator), _services, _loggerFactory);
        return new DistributedMapper(serviceProvider);
    }
}
