using Microsoft.Extensions.DependencyInjection;
using OfX.Abstractions;
using OfX.Abstractions.Transporting;
using OfX.Responses;

namespace OfX.Azure.ServiceBus.Implementations;

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