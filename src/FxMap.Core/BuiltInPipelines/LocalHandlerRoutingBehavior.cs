using FxMap.Abstractions;
using FxMap.Delegates;
using FxMap.Responses;

namespace FxMap.BuiltInPipelines;

/// <summary>
/// The routing of <see cref="FxMapBuilder"/> to a local handler: a key that has one is answered in process and never
/// reaches the transport (with AddFxMap, <c>SendPipelineRoutingBehavior</c> finds the handler in the container).
/// </summary>
internal sealed class LocalHandlerRoutingBehavior<TDistributedKey>(
    IReceivedPipelinesOrchestrator<TDistributedKey> localHandler)
    : ISendPipelineBehavior<TDistributedKey> where TDistributedKey : IDistributedKey
{
    public Task<ItemsResponse<DataResponse>> HandleAsync(RequestContext<TDistributedKey> requestContext,
        SendHandlerDelegate next) => localHandler.ExecuteAsync(requestContext);
}
