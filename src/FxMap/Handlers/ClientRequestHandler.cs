using Microsoft.Extensions.DependencyInjection;
using FxMap.Abstractions;
using FxMap.Abstractions.Transporting;
using FxMap.Exceptions;
using FxMap.Responses;

namespace FxMap.Handlers;

/// <summary>
/// Internal handler that routes client requests through the configured <see cref="IRequestClient"/> transport.
/// </summary>
/// <typeparam name="TDistributedKey">The FxMap distributed key type for this handler.</typeparam>
/// <param name="serviceProvider">The service provider for resolving the transport client.</param>
internal sealed class ClientRequestHandler<TDistributedKey>(IServiceProvider serviceProvider)
    : IClientRequestHandler<TDistributedKey> where TDistributedKey : IDistributedKey
{
    /// <inheritdoc />
    public async Task<ItemsResponse<DataResponse>> RequestAsync(RequestContext<TDistributedKey> requestContext)
    {
        var client = serviceProvider.GetService<IRequestClient>();
        if (client is null) throw new FxMapException.NoHandlerForDistributedKey(typeof(TDistributedKey));
        var result = await client.RequestAsync(requestContext);
        return result;
    }
}