using Microsoft.Extensions.DependencyInjection;
using FxMap.Abstractions;
using FxMap.Models;
using FxMap.Responses;
using FxMap.Configuration;

namespace FxMap.Implementations;

/// <summary>
/// Abstract base class for client-side pipeline orchestration in the FxMap framework.
/// </summary>
/// <remarks>
/// This class provides the abstract contract for executing send pipelines on the client side
/// before requests are transmitted to remote services.
/// </remarks>
internal abstract class SendPipelinesOrchestrator
{
    /// <summary>
    /// Executes the send pipeline and returns the data response.
    /// </summary>
    /// <param name="message">The FxMap request containing selector IDs and expressions.</param>
    /// <param name="context">Optional context containing headers and parameters.</param>
    /// <returns>The items response containing the fetched data.</returns>
    internal abstract Task<ItemsResponse<DataResponse>> ExecuteAsync(FxMapRequest message, IContext context);
}

/// <summary>
/// Client-side pipeline orchestrator that executes send pipeline behaviors before transport.
/// </summary>
/// <typeparam name="TDistributedKey">The FxMap distributed key type for which pipelines are being executed.</typeparam>
/// <param name="serviceProvider">The service provider for resolving handlers and pipeline behaviors.</param>
/// <remarks>
/// This orchestrator:
/// <list type="bullet">
///   <item><description>Resolves expression placeholders with runtime parameters</description></item>
///   <item><description>Executes send pipeline behaviors in reverse order (middleware pattern)</description></item>
///   <item><description>Handles timeout using the configured default request timeout</description></item>
///   <item><description>Maps resolved expressions back to original expressions in the response</description></item>
/// </list>
/// </remarks>
internal sealed class SendPipelinesOrchestrator<TDistributedKey>(IServiceProvider serviceProvider) :
    SendPipelinesOrchestrator where TDistributedKey : IDistributedKey
{
    internal override async Task<ItemsResponse<DataResponse>> ExecuteAsync(FxMapRequest message, IContext context)
    {
        var handler = serviceProvider.GetRequiredService<IClientRequestHandler<TDistributedKey>>();
        var cancellationToken = context?.CancellationToken ?? CancellationToken.None;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(FxMapStatics.DefaultRequestTimeout);

        var request = new MapRequest<TDistributedKey>(message.SelectorIds, message.Expressions);
        var requestContext = new RequestContextImpl<TDistributedKey>(request, context?.Headers ?? [], cts.Token);
        var result = await serviceProvider
            .GetServices<ISendPipelineBehavior<TDistributedKey>>()
            .Reverse()
            .Aggregate(() => handler.RequestAsync(requestContext),
                (acc, pipeline) => () => pipeline.HandleAsync(requestContext, acc)).Invoke();
        return result;
    }
}