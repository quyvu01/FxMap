using System.Diagnostics;
using NATS.Client.Core;
using FxMap.Abstractions;
using FxMap.Abstractions.Transporting;
using FxMap.Exceptions;
using FxMap.Extensions;
using FxMap.Nats.Abstractions;
using FxMap.Nats.Wrappers;
using FxMap.Responses;
using FxMap.Telemetry;

namespace FxMap.Nats.Implementations;

internal sealed class NatsRequestClient(
    NatsClientWrapper natsClientWrapper,
    IMapperConfiguration mapperConfiguration,
    INatsConfiguration natsConfiguration)
    : IRequestClient
{
    private const string TransportName = "nats";

    public async Task<ItemsResponse<DataResponse>> RequestAsync<TDistributedKey>(
        RequestContext<TDistributedKey> requestContext) where TDistributedKey : IDistributedKey
    {
        // Start client-side activity for distributed tracing
        using var activity = FxMapActivitySource.StartClientActivity<TDistributedKey>(TransportName);

        try
        {
            // Add trace context to headers for propagation
            var natsHeaders = new NatsHeaders();
            requestContext.Headers?.ForEach(h => natsHeaders.Add(h.Key, h.Value));
            var query = requestContext.Query;
            var natsSubject = natsConfiguration.GetSubject(typeof(TDistributedKey));
            // Propagate W3C trace context
            if (activity != null)
            {
                if (!string.IsNullOrEmpty(activity.Id)) 
                    natsHeaders.Add("traceparent", activity.Id);
                if (!string.IsNullOrEmpty(activity.TraceStateString))
                    natsHeaders.Add("tracestate", activity.TraceStateString);

                // Add FxMap-specific tags
                activity.SetMessagingTags(system: TransportName, destination: natsSubject, operation: "publish");
                activity.SetFxMapTags(query.Expressions, selectorIds: query.SelectorIds);
            }
            
            var reply = await natsClientWrapper.NatsClient
                .RequestAsync<MapRequest<TDistributedKey>, Result>(natsSubject, query, natsHeaders,
                    replyOpts: new NatsSubOpts { Timeout = mapperConfiguration.DefaultRequestTimeout },
                    cancellationToken: requestContext.CancellationToken);

            var response = reply.Data;
            if (response is null)
                throw new DistributedMapException.ReceivedException("Received null response from server");

            if (!response.IsSuccess)
            {
                throw response.Fault?.ToException()
                      ?? new DistributedMapException.ReceivedException("Unknown error from server");
            }
            
            var itemCount = response.Data?.Items?.Length ?? 0;
            
            // Add item count to activity
            activity?.SetFxMapTags(itemCount: itemCount);

            return response.Data;
        }
        catch (Exception ex)
        {
            // Record exception on activity
            activity?.RecordException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            throw;
        }
    }
}