using Microsoft.Extensions.DependencyInjection;
using FxMap.Abstractions;
using FxMap.Abstractions.Transporting;
using FxMap.Responses;

namespace FxMap.Azure.ServiceBus.Implementations;

internal class AzureServiceBusClient(IServiceProvider serviceProvider) : IRequestClient
{
    public async Task<ItemsResponse<DataResponse>> RequestAsync<TDistributedKey>(
        RequestContext<TDistributedKey> requestContext) where TDistributedKey : IDistributedKey
    {
        var client = serviceProvider.GetRequiredService<OpenAzureServiceBusClient<TDistributedKey>>();
        var result = await client.RequestAsync(requestContext);
        return result;
    }
}