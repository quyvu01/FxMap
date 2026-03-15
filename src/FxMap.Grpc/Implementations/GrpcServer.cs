using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using FxMap.Abstractions;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FxMap.Models;
using FxMap.Exceptions;
using FxMap.Extensions;
using FxMap.Grpc.Exceptions;
using FxMap.Implementations;
using FxMap.Telemetry;

namespace FxMap.Grpc.Implementations;

/// <summary>
/// gRPC server implementation that handles incoming FxMap data requests.
/// </summary>
/// <param name="serviceProvider">The service provider for resolving handlers and pipelines.</param>
/// <remarks>
/// This server exposes two gRPC endpoints:
/// <list type="bullet">
///   <item><description><c>GetItems</c> - Fetches data for a specific distributed key type and selector IDs</description></item>
///   <item><description><c>GeTDistributedKeys</c> - Returns the list of distributed key types this server can handle (for discovery)</description></item>
/// </list>
/// </remarks>
public sealed class GrpcServer : FxMapTransportService.FxMapTransportServiceBase
{
    private static readonly Lazy<ConcurrentDictionary<string, Type>> ReceivedPipelineTypes = new(() => []);
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GrpcServer> _logger;
    private readonly IMapperConfiguration _mapperConfiguration;
    private const string TransportName = "grpc";

    public GrpcServer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetService<ILogger<GrpcServer>>();
        _mapperConfiguration = serviceProvider.GetRequiredService<IMapperConfiguration>();
    }

    public override async Task<FxMapItemsGrpcResponse> GetItems(GetFxMapGrpcQuery request, ServerCallContext context)
    {
        // Extract distributedKey name for telemetry
        var distributedKeyName = request.DistributedKeyAssemblyType?.Split(',')[0].Split('.').Last() ?? "Unknown";

        // Extract parent trace context from gRPC metadata
        ActivityContext parentContext = default;
        var traceparentHeader = context.RequestHeaders.FirstOrDefault(h => h.Key == "traceparent");
        if (traceparentHeader != null) ActivityContext.TryParse(traceparentHeader.Value, null, out parentContext);

        using var activity = FxMapActivitySource.StartServerActivity(distributedKeyName, parentContext);
        var stopwatch = Stopwatch.StartNew();

        // Create timeout CTS
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        cts.CancelAfter(_mapperConfiguration.DefaultRequestTimeout);
        var cancellationToken = cts.Token;

        try
        {
            activity?.SetMessagingTags(system: TransportName, destination: "grpc-endpoint",
                messageId: Activity.Current?.Id ?? Guid.NewGuid().ToString(),
                operation: "process");

            FxMapDiagnostics.MessageReceive(TransportName, "grpc-endpoint", Activity.Current?.Id);

            var receivedPipelinesType = ReceivedPipelineTypes.Value
                .GetOrAdd(request.DistributedKeyAssemblyType, typeAssembly =>
                {
                    var distributedKeyType = Type.GetType(typeAssembly);
                    if (distributedKeyType is null)
                        throw new GrpcExceptions.CannotDeserializeDistributedKeyType(typeAssembly);

                    if (!_mapperConfiguration.DistributedKeyMapHandlers.TryGetValue(distributedKeyType,
                            out var handlerType))
                        throw new DistributedMapException.CannotFindHandlerForDistributedKey(distributedKeyType);

                    var modelArg = handlerType.GetGenericArguments()[0];
                    return typeof(ReceivedPipelinesOrchestrator<,>).MakeGenericType(modelArg, distributedKeyType);
                });

            // Use scoped service to prevent concurrent issues (e.g., with DbContext)
            using var scope = _serviceProvider.CreateScope();
            var receivedPipelinesOrchestrator = (ReceivedPipelinesOrchestrator)scope.ServiceProvider
                .GetRequiredService(receivedPipelinesType)!;

            var headers = context.RequestHeaders.ToDictionary(k => k.Key, v => v.Value);

            string[] selectorIds = [..request.SelectorIds];
            var expressions = JsonSerializer.Deserialize<string[]>(request.Expression);

            var message = new DistributedMapRequest(selectorIds, expressions);
            var response = await receivedPipelinesOrchestrator
                .ExecuteAsync(message, headers, cancellationToken);

            var res = new FxMapItemsGrpcResponse();
            response.Items.ForEach(a =>
            {
                var itemGrpc = new ItemGrpc { Id = a.Id };
                a.Values.ForEach(x =>
                    itemGrpc.FxmapValues.Add(new FxMapValueItemGrpc { Expression = x.Expression, Value = x.Value }));
                res.Items.Add(itemGrpc);
            });

            // Record success metrics
            stopwatch.Stop();
            var itemCount = response.Items?.Length ?? 0;

            FxMapMetrics.RecordRequest(
                distributedKeyName,
                TransportName,
                stopwatch.Elapsed.TotalMilliseconds,
                itemCount);

            activity?.SetFxMapTags(expressions, selectorIds, itemCount);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return res;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested &&
                                                 !context.CancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            _logger?.LogWarning("Request timeout for gRPC GetItems: {DistributedKeyType}", distributedKeyName);

            FxMapMetrics.RecordError(
                distributedKeyName,
                TransportName,
                stopwatch.Elapsed.TotalMilliseconds,
                "TimeoutException");

            activity?.SetStatus(ActivityStatusCode.Error, "Request timeout");

            throw new RpcException(new Status(StatusCode.DeadlineExceeded,
                $"Request timeout for {distributedKeyName}"));
        }
        catch (Exception e)
        {
            stopwatch.Stop();

            _logger?.LogError(e, "Error while execute get items: {DistributedKeyType}", distributedKeyName);

            FxMapMetrics.RecordError(distributedKeyName, TransportName,
                stopwatch.Elapsed.TotalMilliseconds, e.GetType().Name);

            FxMapDiagnostics.RequestError(distributedKeyName, TransportName, e, stopwatch.Elapsed);

            activity?.RecordException(e);
            activity?.SetStatus(ActivityStatusCode.Error, e.Message);

            throw;
        }
    }

    public override Task<DistributedKeyTypeResponse> GetDistributedKeys(GetDistributedKeysQuery request,
        ServerCallContext context)
    {
        var entityInfos = _mapperConfiguration.EntityInfos;
        var response = new DistributedKeyTypeResponse();
        var distributedKeyTypes = entityInfos
            .Select(a => a.DistributedKeyType.GetAssemblyName());
        response.DistributedKeyTypes.AddRange(distributedKeyTypes);
        return Task.FromResult(response);
    }
}