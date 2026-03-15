using System.Collections.Concurrent;
using System.Text.Json;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using FxMap.Abstractions;
using FxMap.Abstractions.Transporting;
using FxMap.Models;
using FxMap.Extensions;
using FxMap.Grpc.Delegates;
using FxMap.Grpc.Implementations;
using FxMap.Registries;
using FxMap.Responses;
using FxMap.Grpc.Registries;

namespace FxMap.Grpc.Extensions;

/// <summary>
/// Provides extension methods for integrating gRPC transport with the FxMap framework.
/// </summary>
public static class GrpcExtensions
{
    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(3);

    // Channel pool for reusing gRPC channels (channels are expensive to create)
    private static readonly ConcurrentDictionary<string, GrpcChannel> ChannelPool = new();

    /// <summary>
    /// Adds gRPC client configuration for FxMap distributed data fetching.
    /// </summary>
    /// <param name="mapRegister">The FxMap registration instance.</param>
    /// <param name="options">Configuration action for specifying gRPC server hosts.</param>
    /// <remarks>
    /// This method configures the client side of gRPC transport. The client will:
    /// <list type="bullet">
    ///   <item><description>Probe configured hosts to discover which distributed keys each server handles</description></item>
    ///   <item><description>Route requests to the appropriate server based on distributed key type</description></item>
    ///   <item><description>Handle failover and retry logic through the pipeline behaviors</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddFxMap(cfg =>
    /// {
    ///     cfg.AddGrpcClients(grpc =>
    ///     {
    ///         grpc.AddGrpcHosts("https://users-service:5001", "https://products-service:5002");
    ///     });
    /// });
    /// </code>
    /// </example>
    public static void AddGrpcClients(this MapConfigurator mapRegister, Action<GrpcClientsConfigurator> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var clientsRegister = new GrpcClientsConfigurator();
        options.Invoke(clientsRegister);
        if (clientsRegister.ServiceHosts is not { Count: > 0 } serviceHosts) return;
        ConcurrentDictionary<HostProbe, Type[]> hostMapDistributedKeys = [];
        serviceHosts.ForEach(h => hostMapDistributedKeys.TryAdd(new HostProbe(h, false), []));
        var semaphore = new SemaphoreSlim(1, 1);
        var throwIfExceptions = mapRegister.ThrowIfExceptions;
        var services = mapRegister.Services;
        services.TryAddTransient<GetFxMapResponseFunc>(_ => distributedKeyType => async (query, context) =>
        {
            if (!hostMapDistributedKeys.Any(a => a.Value.Contains(distributedKeyType)))
            {
                await semaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (hostMapDistributedKeys.Any(a => a.Value.Contains(distributedKeyType))) goto resolveData;
                    var probeHosts = hostMapDistributedKeys
                        .Where(a => !a.Key.IsProbed)
                        .Select(a => a.Key.ServiceHost);
                    var missingTypes = await GetHostMapAttributesAsync(probeHosts, context, throwIfExceptions);
                    missingTypes.Where(a => a.Key.IsProbed)
                        .ForEach(x =>
                        {
                            if (hostMapDistributedKeys.Any(a => a.Key.ServiceHost == x.Key.ServiceHost))
                            {
                                var hostProbeKey = hostMapDistributedKeys
                                    .First(a => a.Key.ServiceHost == x.Key.ServiceHost);
                                hostMapDistributedKeys.TryRemove(hostProbeKey);
                            }

                            hostMapDistributedKeys.TryAdd(x.Key, x.Value);
                        });
                    if (hostMapDistributedKeys.Any(a => a.Value.Contains(distributedKeyType))) goto resolveData;
                    return new ItemsResponse<DataResponse>([]);
                }
                finally
                {
                    semaphore.Release();
                }
            }

            resolveData:
            var host = hostMapDistributedKeys
                .FirstOrDefault(a => a.Value.Contains(distributedKeyType))
                .Key;
            var result = await GetFxMapItemsAsync(host.ServiceHost, context, query, distributedKeyType);
            var dataResponse = result.Items.Select(x =>
            {
                var values = x.FxmapValues
                    .Select(a => new ValueResponse { Expression = a.Expression, Value = a.Value });
                return new DataResponse { Id = x.Id, Values = [..values] };
            });
            return new ItemsResponse<DataResponse>([..dataResponse]);
        });

        services.TryAddTransient<IRequestClient, GrpcRequestClient>();
    }

    private static async Task<Dictionary<HostProbe, Type[]>> GetHostMapAttributesAsync(
        IEnumerable<string> serverHosts, IContext context, bool throwIfExceptions)
    {
        var tasks = serverHosts
            .Select(a => (Host: a, DistributedKeysTask: GeTDistributedKeysByHost(a, context, throwIfExceptions))).ToList();
        await Task.WhenAll(tasks.Select(t => t.DistributedKeysTask));
        var result = new Dictionary<HostProbe, Type[]>();
        tasks.ForEach(a =>
        {
            var isProbed = a.DistributedKeysTask.Result.IsProbed;
            var distributedKeyTypes = a.DistributedKeysTask.Result.DistributedKeyTypes;
            result.TryAdd(new HostProbe(a.Host, isProbed), distributedKeyTypes);
        });
        return result;
    }

    private static async Task<FxMapItemsGrpcResponse> GetFxMapItemsAsync(string serverHost, IContext context,
        DistributedMapRequest query, Type distributedKeyType)
    {
        var channel = GetOrCreateChannel(serverHost);
        var client = new FxMapTransportService.FxMapTransportServiceClient(channel);
        var metadata = new Metadata();
        context?.Headers?.ForEach(h => metadata.Add(h.Key, h.Value));
        var grpcQuery = new GetFxMapGrpcQuery();
        using var cancellationTokenSource = CancellationTokenSource
            .CreateLinkedTokenSource(context?.CancellationToken ?? CancellationToken.None);
        cancellationTokenSource.CancelAfter(DefaultRequestTimeout);
        grpcQuery.SelectorIds.AddRange(query.SelectorIds ?? []);
        grpcQuery.Expression = JsonSerializer.Serialize(query.Expressions);
        grpcQuery.DistributedKeyAssemblyType = distributedKeyType.GetAssemblyName();
        return await client.GetItemsAsync(grpcQuery, metadata, cancellationToken: cancellationTokenSource.Token);
    }

    private static async Task<DistributedKeysProbe> GeTDistributedKeysByHost(string serverHost, IContext context,
        bool throwIfExceptions)
    {
        try
        {
            var channel = GetOrCreateChannel(serverHost);
            var client = new FxMapTransportService.FxMapTransportServiceClient(channel);
            var query = new GetDistributedKeysQuery();
            using var cancellationTokenSource = CancellationTokenSource
                .CreateLinkedTokenSource(context?.CancellationToken ?? CancellationToken.None);
            cancellationTokenSource.CancelAfter(DefaultRequestTimeout);
            var response = await client.GetDistributedKeysAsync(query, cancellationToken: cancellationTokenSource.Token);
            return new DistributedKeysProbe(true, [..response.DistributedKeyTypes.Select(Type.GetType)]);
        }
        catch (Exception)
        {
            if (throwIfExceptions) throw;
            return new DistributedKeysProbe(false, []);
        }
    }

    private static GrpcChannel GetOrCreateChannel(string serverHost) =>
        ChannelPool.GetOrAdd(serverHost, static host => GrpcChannel.ForAddress(host));

    /// <summary>
    /// Maps the FxMap gRPC service endpoint for handling incoming FxMap requests.
    /// </summary>
    /// <param name="builder">The endpoint route builder.</param>
    /// <remarks>
    /// This method should be called in the server application's startup to enable
    /// handling of incoming FxMap gRPC requests.
    /// </remarks>
    /// <example>
    /// <code>
    /// app.MapFxMapGrpcService();
    /// </code>
    /// </example>
    public static void MapFxMapGrpcService(this IEndpointRouteBuilder builder) => builder.MapGrpcService<GrpcServer>();
}