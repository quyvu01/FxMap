using System.Diagnostics;
using FxMap.Abstractions;
using FxMap.Abstractions.Transporting;
using FxMap.Models;
using FxMap.Grpc.Delegates;
using FxMap.Grpc.Internals;
using FxMap.Responses;
using FxMap.Telemetry;

namespace FxMap.Grpc.Implementations;

/// <summary>
/// gRPC implementation of <see cref="IRequestClient"/> for sending FxMap requests over gRPC.
/// </summary>
/// <param name="mapperResponseFunc">The function delegate for making gRPC calls based on distributed key type.</param>
/// <remarks>
/// This client is automatically registered when <c>AddGrpcClients</c> is called and handles
/// the serialization and transport of FxMap requests to remote gRPC servers.
/// </remarks>
public sealed class GrpcRequestClient(GetMapperResponseFunc mapperResponseFunc) : IRequestClient
{
    private const string TransportName = "grpc";

    public async Task<ItemsResponse<DataResponse>> RequestAsync<TDistributedKey>(
        RequestContext<TDistributedKey> requestContext) where TDistributedKey : IDistributedKey
    {
        // Start client-side activity for distributed tracing
        using var activity = FxMapActivitySource.StartClientActivity<TDistributedKey>(TransportName);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Emit diagnostic event
            FxMapDiagnostics.RequestStart(typeof(TDistributedKey).Name, TransportName, requestContext.Query.SelectorIds,
                requestContext.Query.Expressions);

            // Track active requests
            FxMapMetrics.UpdateActiveRequests(1);

            // Note: W3C trace context propagation will be handled by gRPC infrastructure
            if (activity != null)
            {
                activity.SetMessagingTags(system: TransportName, destination: "grpc-server",
                    messageId: Activity.Current?.Id ?? Guid.NewGuid().ToString(), operation: "call");

                activity.SetFxMapTags(requestContext.Query.Expressions, requestContext.Query.SelectorIds);
            }

            var func = mapperResponseFunc.Invoke(typeof(TDistributedKey).AssemblyQualifiedName);
            var result = await func.Invoke(
                new DistributedMapRequest(requestContext.Query.SelectorIds, requestContext.Query.Expressions),
                new GrpcClientContext(requestContext.Headers, requestContext.CancellationToken));

            // Record success metrics
            stopwatch.Stop();
            var itemCount = result?.Items?.Length ?? 0;

            FxMapMetrics.RecordRequest(typeof(TDistributedKey).Name, TransportName, stopwatch.Elapsed.TotalMilliseconds,
                itemCount);

            FxMapDiagnostics.RequestStop(typeof(TDistributedKey).Name, TransportName, itemCount, stopwatch.Elapsed);

            activity?.SetFxMapTags(itemCount: itemCount);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Record error metrics
            FxMapMetrics.RecordError(typeof(TDistributedKey).Name, TransportName, stopwatch.Elapsed.TotalMilliseconds,
                ex.GetType().Name);

            FxMapDiagnostics.RequestError(typeof(TDistributedKey).Name, TransportName, ex, stopwatch.Elapsed);

            activity?.RecordException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            throw;
        }
        finally
        {
            FxMapMetrics.UpdateActiveRequests(-1);
        }
    }
}