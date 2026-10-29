using FxMap.Abstractions;
using FxMap.Delegates;
using FxMap.Models;
using FxMap.Responses;

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
    internal abstract Task<ItemsResponse<DataResponse>> ExecuteAsync(DistributedMapRequest message, IContext context);
}

/// <summary>
/// Client-side pipeline orchestrator that executes send pipeline behaviors before transport.
/// </summary>
/// <typeparam name="TDistributedKey">The FxMap distributed key type for which pipelines are being executed.</typeparam>
/// <param name="handler">Sends the request through the transport.</param>
/// <param name="configuration">The settings of the mapper (the timeout of a request).</param>
/// <param name="behaviors">The send behaviors; the first one is the outermost.</param>
/// <remarks>
/// This orchestrator:
/// <list type="bullet">
///   <item><description>Resolves expression placeholders with runtime parameters</description></item>
///   <item><description>Executes send pipeline behaviors in reverse order (middleware pattern)</description></item>
///   <item><description>Handles timeout using the configured default request timeout</description></item>
///   <item><description>Maps resolved expressions back to original expressions in the response</description></item>
/// </list>
/// </remarks>
internal sealed class SendPipelinesOrchestrator<TDistributedKey>(
    IClientRequestHandler<TDistributedKey> handler,
    IMapperConfiguration configuration,
    IEnumerable<ISendPipelineBehavior<TDistributedKey>> behaviors) :
    SendPipelinesOrchestrator where TDistributedKey : IDistributedKey
{
    // The container already gives an array; it is not copied, since each mapping resolves a new orchestrator.
    private readonly ISendPipelineBehavior<TDistributedKey>[] _behaviors =
        behaviors as ISendPipelineBehavior<TDistributedKey>[] ?? [.. behaviors];

    internal override async Task<ItemsResponse<DataResponse>> ExecuteAsync(DistributedMapRequest message,
        IContext context)
    {
        var cancellationToken = context?.CancellationToken ?? CancellationToken.None;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(configuration.DefaultRequestTimeout);

        var request = new MapRequest<TDistributedKey>(message.SelectorIds, message.Expressions);
        var requestContext = new RequestContextImpl<TDistributedKey>(request, context?.Headers ?? [], cts.Token);
        SendHandlerDelegate next = () => handler.RequestAsync(requestContext);
        for (var i = _behaviors.Length - 1; i >= 0; i--)
        {
            var behavior = _behaviors[i];
            var inner = next;
            next = () => behavior.HandleAsync(requestContext, inner);
        }

        return await next();
    }
}