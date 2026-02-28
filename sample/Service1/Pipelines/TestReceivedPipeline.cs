using FxMap.Abstractions;
using FxMap.Responses;

namespace Service1.Pipelines;

public sealed class TestReceivedPipeline<TDistributedKey> : IReceivedPipelineBehavior<TDistributedKey> where TDistributedKey : IDistributedKey
{
    public async Task<ItemsResponse<DataResponse>> HandleAsync(RequestContext<TDistributedKey> requestContext,
        Func<Task<ItemsResponse<DataResponse>>> next)
    {
        var result = await next.Invoke();
        await Task.Delay(7000);
        return result;
    }
}