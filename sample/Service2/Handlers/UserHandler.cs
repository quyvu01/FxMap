using FxMap.Abstractions;
using FxMap.Responses;
using Shared.DistributedKeys;

namespace Service2.Handlers;

public sealed class UserHandler : IClientRequestHandler<UserDistributedKey>
{
    public Task<ItemsResponse<DataResponse>> RequestAsync(RequestContext<UserDistributedKey> requestContext)
    {
        throw new NotImplementedException();
    }
}