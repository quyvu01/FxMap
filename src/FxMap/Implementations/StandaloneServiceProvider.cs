using System.Collections.Concurrent;
using FxMap.Abstractions;
using FxMap.Abstractions.Transporting;
using FxMap.BuiltInPipelines;
using FxMap.Delegates;
using FxMap.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FxMap.Implementations;

/// <summary>
/// The services that <see cref="DistributedMapper"/> and its send pipelines look up, for a mapper built by
/// <see cref="FxMapBuilder"/>. It is not a container: it answers a fixed set of FxMap types with the objects the builder
/// was given, builds the generic ones of a distributed key once, on first use, and hands every other type to the
/// services the caller gave (or answers nothing).
/// </summary>
internal sealed class StandaloneServiceProvider : IServiceProvider
{
    private readonly IMapperConfiguration _configuration;
    private readonly GetProfileConfig _getProfileConfig;
    private readonly IRequestClient _requestClient;
    private readonly IReadOnlyList<Type> _sendBehaviorTypes;
    private readonly IReadOnlyDictionary<Type, object> _localOrchestrators;
    private readonly IServiceProvider _services;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<Type, object> _built = new();

    internal StandaloneServiceProvider(IMapperConfiguration configuration, GetProfileConfig getProfileConfig,
        IRequestClient requestClient, IReadOnlyList<Type> sendBehaviorTypes,
        IReadOnlyDictionary<Type, object> localOrchestrators, IServiceProvider services, ILoggerFactory loggerFactory)
    {
        _configuration = configuration;
        _getProfileConfig = getProfileConfig;
        _requestClient = requestClient;
        _sendBehaviorTypes = sendBehaviorTypes;
        _localOrchestrators = localOrchestrators;
        _services = services;
        _loggerFactory = loggerFactory;
    }

    public object GetService(Type serviceType)
    {
        if (serviceType == typeof(IServiceProvider)) return this;
        if (serviceType == typeof(IMapperConfiguration)) return _configuration;
        if (serviceType == typeof(GetProfileConfig)) return _getProfileConfig;
        if (serviceType == typeof(IRequestClient)) return _requestClient;
        if (serviceType == typeof(ILoggerFactory)) return _loggerFactory;
        if (!serviceType.IsGenericType) return _services?.GetService(serviceType);

        var definition = serviceType.GetGenericTypeDefinition();
        var arguments = serviceType.GetGenericArguments();
        if (definition == typeof(SendPipelinesOrchestrator<>))
            return _built.GetOrAdd(serviceType, static (type, sp) => Activator.CreateInstance(type, sp), this);
        if (definition == typeof(IClientRequestHandler<>))
            return _built.GetOrAdd(serviceType, static (type, sp) => Activator.CreateInstance(
                typeof(ClientRequestHandler<>).MakeGenericType(type.GetGenericArguments()), sp), this);
        if (definition == typeof(ReceivedPipelinesOrchestrator<,>))
            return _localOrchestrators.GetValueOrDefault(arguments[1]) is { } local && local.GetType() == serviceType
                ? local
                : null;
        if (definition == typeof(ILogger<>))
            return Activator.CreateInstance(typeof(Logger<>).MakeGenericType(arguments), _loggerFactory);
        if (definition == typeof(IEnumerable<>) && arguments[0] is { IsGenericType: true } element &&
            element.GetGenericTypeDefinition() == typeof(ISendPipelineBehavior<>))
            return _built.GetOrAdd(serviceType, static (_, state) =>
                state.Self.CreateSendBehaviors(state.Element), (Self: this, Element: element));

        return _services?.GetService(serviceType);
    }

    // The order of AddFxMap: the behaviors of the user (registered in its options, before the built-in ones), then the
    // routing to a local handler, the suppression of the errors, and the retries. The first one is the outermost.
    private Array CreateSendBehaviors(Type behaviorType)
    {
        var keyType = behaviorType.GetGenericArguments()[0];
        var behaviors = new List<object>();
        foreach (var type in _sendBehaviorTypes)
        {
            var closed = type.IsGenericTypeDefinition ? type.MakeGenericType(keyType) : type;
            if (closed.IsAssignableTo(behaviorType)) behaviors.Add(ActivatorUtilities.CreateInstance(this, closed));
        }

        behaviors.Add(Activator.CreateInstance(typeof(SendPipelineRoutingBehavior<>).MakeGenericType(keyType), this));
        behaviors.Add(Activator.CreateInstance(typeof(ExceptionPipelineBehavior<>).MakeGenericType(keyType), this));
        behaviors.Add(Activator.CreateInstance(typeof(RetryPipelineBehavior<>).MakeGenericType(keyType), this));

        var array = Array.CreateInstance(behaviorType, behaviors.Count);
        for (var i = 0; i < behaviors.Count; i++) array.SetValue(behaviors[i], i);
        return array;
    }
}
