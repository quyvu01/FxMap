using OfX.Responses;

namespace OfX.Abstractions;

/// <summary>
/// Defines a client-side pipeline behavior for sending requests, analogous to 
/// <see cref="IReceivedPipelineBehavior{TDistributedKey}"/> on the server side.
/// </summary>
/// <typeparam name="TDistributedKey">
/// The <see cref="IDistributedKey"/> type associated with the outgoing request.
/// </typeparam>
/// <remarks>
/// <para>
/// The <see cref="ISendPipelineBehavior{TDistributedKey}"/> interface allows you to insert custom logic
/// into the client-side request pipeline before and/or after the actual request is sent.
/// </para>
/// <para>
/// Typical use cases include:
/// </para>
/// <list type="bullet">
/// <item>Adding or modifying request headers.</item>
/// <item>Performing client-side validation before the request is sent.</item>
/// <item>Implementing retries, logging, or metrics collection.</item>
/// <item>Transforming the outgoing <see cref="RequestContext{TDistributedKey}"/>.</item>
/// </list>
/// Multiple send pipeline behaviors can be registered and will be executed in order.
/// </remarks>
public interface ISendPipelineBehavior<TDistributedKey> : IOfXBase<TDistributedKey>
    where TDistributedKey : IDistributedKey
{
    /// <summary>
    /// Handles the request context as part of the send pipeline and invokes the next behavior or the final request handler.
    /// </summary>
    /// <param name="requestContext">
    /// The outgoing request context containing selector IDs, expressions, headers, 
    /// and an optional <see cref="CancellationToken"/>.
    /// </param>
    /// <param name="next">
    /// The delegate representing the next behavior or the final client request operation.
    /// Call <c>await next()</c> to continue the pipeline execution.
    /// </param>
    /// <returns>
    /// A task resolving to an <see cref="ItemsResponse{OfXDataResponse}"/> containing 
    /// the result of the pipeline execution and final response from the server.
    /// </returns>
    Task<ItemsResponse<DataResponse>> HandleAsync(
        RequestContext<TDistributedKey> requestContext,
        Func<Task<ItemsResponse<DataResponse>>> next
    );
}