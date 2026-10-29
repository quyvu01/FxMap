using System.Collections.Concurrent;
using System.Reflection;
using FxMap.Abstractions;
using FxMap.Abstractions.Transporting;
using FxMap.BuiltInPipelines;
using FxMap.Exceptions;
using FxMap.Handlers;
using FxMap.Helpers;
using FxMap.Implementations;
using FxMap.Registries;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FxMap;

/// <summary>
/// Builds an <see cref="IDistributedMapper"/> without a dependency injection container: for a console app, a function,
/// a test, or any host that does not use Microsoft.Extensions.DependencyInjection: FxMap.Core does not reference it.
/// Every object of the mapper is created here with <c>new</c>. <c>services.AddFxMap(...)</c> (FxMap package) stays the
/// way to use FxMap with a container.
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
    private static readonly MethodInfo CreateSenderMethod = typeof(FxMapBuilder)
        .GetMethod(nameof(CreateSender), BindingFlags.NonPublic | BindingFlags.Static)!;

    // The profiles and the options, as AddFxMap reads them.
    private readonly MapSettings _configurator = new();
    private readonly List<Type> _sendBehaviorTypes = [];
    private readonly Dictionary<Type, (Type ModelType, object Orchestrator)> _localHandlers = [];
    private IRequestClient _requestClient;
    private IServiceProvider _services = EmptyServiceProvider.Instance;
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
    /// A profile uses a distributed key that has neither a local handler nor a request client to send it, or a send
    /// behavior cannot be created from the services given to <see cref="UseServices"/>.
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

        var configuration = new MapperConfiguration(distributedKeyTypes, [], new Dictionary<Type, Type>(),
            _configurator.MaxNestingDepth, _configurator.MaxConcurrentProcessing, _configurator.ThrowIfExceptions,
            _configurator.DefaultRequestTimeout, _configurator.RetryPolicy, _configurator.SupervisorOptions);
        var setup = new SenderSetup(configuration, _requestClient, [.. _sendBehaviorTypes],
            _localHandlers.ToDictionary(kv => kv.Key, kv => kv.Value.Orchestrator), _services, _loggerFactory);
        var senders = new ConcurrentDictionary<Type, SendPipelinesOrchestrator>();
        var runtime = new MapperRuntime(configuration, _configurator.GetProfileConfig,
            keyType => senders.GetOrAdd(keyType, static (type, state) =>
                (SendPipelinesOrchestrator)CreateSenderMethod.MakeGenericMethod(type)
                    .Invoke(null, BindingFlags.DoNotWrapExceptions, null, [state], null)!, setup),
            _services, _loggerFactory.CreateLogger<DistributedMapper>());
        // The keys of the profiles get their pipeline now, so a send behavior that cannot be created fails here.
        foreach (var keyType in distributedKeyTypes) runtime.SenderFor(keyType);
        return new DistributedMapper(runtime);
    }

    private sealed record SenderSetup(
        IMapperConfiguration Configuration,
        IRequestClient RequestClient,
        Type[] SendBehaviorTypes,
        Dictionary<Type, object> LocalHandlers,
        IServiceProvider Services,
        ILoggerFactory LoggerFactory);

    // The send pipeline of a key, built once. Same order as AddFxMap: the behaviors of the user (registered in its
    // options, before the built-in ones), the routing to a local handler, the suppression of the errors, the retries.
    private static SendPipelinesOrchestrator<TKey> CreateSender<TKey>(SenderSetup setup) where TKey : IDistributedKey
    {
        var behaviors = new List<ISendPipelineBehavior<TKey>>();
        foreach (var type in setup.SendBehaviorTypes)
        {
            var closed = type.IsGenericTypeDefinition ? type.MakeGenericType(typeof(TKey)) : type;
            if (closed.IsAssignableTo(typeof(ISendPipelineBehavior<TKey>)))
                behaviors.Add((ISendPipelineBehavior<TKey>)ServiceActivator.Create(closed, setup.Services));
        }

        if (setup.LocalHandlers.GetValueOrDefault(typeof(TKey)) is IReceivedPipelinesOrchestrator<TKey> local)
            behaviors.Add(new LocalHandlerRoutingBehavior<TKey>(local));
        behaviors.Add(new ExceptionPipelineBehavior<TKey>(setup.Configuration,
            setup.LoggerFactory.CreateLogger<ExceptionPipelineBehavior<TKey>>()));
        behaviors.Add(new RetryPipelineBehavior<TKey>(setup.Configuration));
        return new SendPipelinesOrchestrator<TKey>(new ClientRequestHandler<TKey>(setup.RequestClient),
            setup.Configuration, behaviors);
    }
}
