using OfX.Abstractions;

namespace OfX.Implementations;

/// <summary>
/// Concrete implementation of <see cref="RequestContext{TDistributedKey}"/> that carries request data through pipelines.
/// </summary>
/// <typeparam name="TDistributedKey">The OfX attribute type for this request.</typeparam>
/// <param name="query">The query containing selector IDs and expressions.</param>
/// <param name="headers">Optional headers for passing context information (e.g., authentication, tracing).</param>
/// <param name="token">Cancellation token for request cancellation.</param>
/// <remarks>
/// This implementation is used internally by the OfX framework to pass request context
/// through both send and received pipeline behaviors.
/// </remarks>
public class RequestContextImpl<TDistributedKey>(
    OfXQueryRequest<TDistributedKey> query,
    Dictionary<string, string> headers,
    CancellationToken token)
    : RequestContext<TDistributedKey> where TDistributedKey : IDistributedKey
{
    /// <inheritdoc />
    public Dictionary<string, string> Headers { get; } = headers ?? [];

    /// <inheritdoc />
    public OfXQueryRequest<TDistributedKey> Query { get; } = query;

    /// <inheritdoc />
    public CancellationToken CancellationToken { get; } = token;
}