using FxMap.Abstractions;
using FxMap.Helpers;
using FxMap.Responses;

namespace FxMap.BuiltInPipelines;

/// <summary>
/// A client-side send pipeline behavior that coalesces concurrent identical requests
/// into a single in-flight operation, inspired by Go's <c>singleflight.Group</c>.
/// </summary>
/// <typeparam name="TDistributedKey">The FxMap distributed key type.</typeparam>
/// <remarks>
/// <para>
/// When multiple concurrent requests share the same set of selector IDs and expressions,
/// only one actual network call is made. All other concurrent callers receive the same
/// <see cref="Task{T}"/> and share its result.
/// </para>
/// <para>
/// This is most effective under high concurrency (e.g., multiple parallel resolvers or
/// requests arriving within the same tick). For sequential requests it has no effect.
/// </para>
/// <para>
/// <b>Registration:</b> Register this as a singleton so the in-flight dictionary is shared
/// across all requests within the same process:
/// </para>
/// <code>
/// services.AddFxMap(cfg =>
///     cfg.SendPipeline(pipe =>
///         pipe.OfType&lt;SingleFlightBehavior&lt;IUserKey&gt;&gt;(ServiceLifetime.Singleton)));
/// </code>
/// <para>
/// Alternatively, register via the open-generic form to apply to all distributed keys:
/// </para>
/// <code>
/// pipe.OfType(typeof(SingleFlightBehavior&lt;&gt;), ServiceLifetime.Singleton)
/// </code>
/// <para>
/// <b>CancellationToken note:</b> The shared task uses the token from the first caller.
/// Subsequent coalesced callers cannot cancel independently. If the token is cancelled,
/// all coalesced callers receive an <see cref="OperationCanceledException"/>.
/// </para>
/// <para>
/// <b>Key computation:</b> The deduplication key is a hash of the sorted selector IDs
/// and sorted expressions, so order-independent requests are correctly coalesced.
/// </para>
/// </remarks>
public sealed class SingleFlightBehavior<TDistributedKey> : ISendPipelineBehavior<TDistributedKey>
    where TDistributedKey : IDistributedKey
{
    // Static per closed generic type — each TDistributedKey gets its own flight group.
    // This is intentional: User requests must not coalesce with Order requests.
    private static readonly SingleFlightGroup<ItemsResponse<DataResponse>> FlightGroup = new();

    /// <inheritdoc />
    public Task<ItemsResponse<DataResponse>> HandleAsync(
        RequestContext<TDistributedKey> requestContext,
        Func<Task<ItemsResponse<DataResponse>>> next)
    {
        var key = ComputeKey(requestContext.Query);
        return FlightGroup.ExecuteAsync(key, next);
    }

    private static int ComputeKey(MapRequest<TDistributedKey> query)
    {
        var hash = new HashCode();
        // Sort to ensure order-independence: [1,2] and [2,1] coalesce into the same flight.
        foreach (var id in query.SelectorIds.Order()) hash.Add(id);
        foreach (var expr in query.Expressions.Order()) hash.Add(expr);
        return hash.ToHashCode();
    }
}