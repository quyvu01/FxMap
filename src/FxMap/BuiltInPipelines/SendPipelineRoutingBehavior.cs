using FxMap.Abstractions;
using FxMap.Implementations;
using FxMap.Responses;
using Microsoft.Extensions.DependencyInjection;

namespace FxMap.BuiltInPipelines;

/// <summary>
/// Internal send pipeline behavior that routes requests to local handlers when available.
/// </summary>
/// <typeparam name="TDistributedKey">The FxMap distributed key type.</typeparam>
/// <param name="serviceProvider">The service provider for resolving handlers.</param>
/// <remarks>
/// This behavior implements the "short-circuit" optimization pattern. When the application
/// has a local handler registered for the requested distributed key type, the request is processed
/// locally through <see cref="ReceivedPipelinesOrchestrator{TModel,TDistributedKey}"/> instead of
/// being sent over the network transport. This provides significant performance improvements
/// for monolithic deployments or services that handle their own data.
/// </remarks>
internal sealed class SendPipelineRoutingBehavior<TDistributedKey>(
    IServiceProvider serviceProvider) :
    ISendPipelineBehavior<TDistributedKey> where TDistributedKey : IDistributedKey
{
    private static Type _receivedPipelinesOrchestratorType;

    public async Task<ItemsResponse<DataResponse>> HandleAsync(RequestContext<TDistributedKey> requestContext,
        Func<Task<ItemsResponse<DataResponse>>> next)
    {
        // Check if we have the inner handler for `TDistributedKey` or not. If have, we will call the ReceivedPipelinesOrchestrator<,> instead of sending via the message!
        var fxConfig = serviceProvider.GetRequiredService<IMapperConfiguration>();
        var handlers = fxConfig.DistributedKeyMapHandlers;
        if (!handlers.TryGetValue(typeof(TDistributedKey), out var handlerType) || !handlerType.IsGenericType)
            return await next.Invoke();
        _receivedPipelinesOrchestratorType ??= typeof(ReceivedPipelinesOrchestrator<,>)
            .MakeGenericType(handlerType.GetGenericArguments());
        var receivedPipelineBehavior = serviceProvider.GetService(_receivedPipelinesOrchestratorType);
        if (receivedPipelineBehavior is not IReceivedPipelinesOrchestrator<TDistributedKey> receivedPipelinesBase)
            return await next.Invoke();
        return await receivedPipelinesBase.ExecuteAsync(requestContext);
    }
}