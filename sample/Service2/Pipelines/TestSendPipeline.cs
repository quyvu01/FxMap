using FxMap.Abstractions;
using FxMap.Responses;

namespace Service2.Pipelines;

public sealed class TestSendPipeline<TDistributedKey> : ISendPipelineBehavior<TDistributedKey>
    where TDistributedKey : IDistributedKey
{
    public async Task<ItemsResponse<DataResponse>> HandleAsync(RequestContext<TDistributedKey> requestContext,
        Func<Task<ItemsResponse<DataResponse>>> next)
    {
        var result = await next.Invoke();
        return result;
    }
}