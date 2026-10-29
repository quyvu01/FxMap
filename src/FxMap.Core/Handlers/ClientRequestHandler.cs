using FxMap.Abstractions;
using FxMap.Abstractions.Transporting;
using FxMap.Exceptions;
using FxMap.Responses;

namespace FxMap.Handlers;

/// <summary>
/// Internal handler that routes client requests through the configured <see cref="IRequestClient"/> transport.
/// </summary>
/// <typeparam name="TDistributedKey">The FxMap distributed key type for this handler.</typeparam>
/// <param name="client">The transport; without it the key has no handler.</param>
internal sealed class ClientRequestHandler<TDistributedKey>(IRequestClient client = null)
    : IClientRequestHandler<TDistributedKey> where TDistributedKey : IDistributedKey
{
    /// <inheritdoc />
    public async Task<ItemsResponse<DataResponse>> RequestAsync(RequestContext<TDistributedKey> requestContext)
    {
        if (client is null) throw new DistributedMapException.NoHandlerForDistributedKey(typeof(TDistributedKey));
        var result = await client.RequestAsync(requestContext);
        return result;
    }
}