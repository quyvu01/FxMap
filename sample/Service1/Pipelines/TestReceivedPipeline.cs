using FxMap.Abstractions;
using FxMap.Delegates;
using FxMap.Responses;

namespace Service1.Pipelines;

public sealed class TestReceivedPipeline<TDistributedKey> : IReceivedPipelineBehavior<TDistributedKey>
    where TDistributedKey : IDistributedKey
{
    public async Task<ItemsResponse<DataResponse>> HandleAsync(RequestContext<TDistributedKey> requestContext,
        ReceivedHandlerDelegate next)
    {
        var result = await next.Invoke();
        await Task.Delay(7000);
        return result;
    }
}