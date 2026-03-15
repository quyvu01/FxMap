using System.Collections.Concurrent;

namespace FxMap.Helpers;

/// <summary>
/// Provides request coalescing (deduplication) for concurrent identical in-flight async operations.
/// Inspired by Go's <c>singleflight.Group</c>.
/// </summary>
/// <typeparam name="TValue">The result type of the async operation.</typeparam>
/// <remarks>
/// <para>
/// When multiple callers concurrently invoke <see cref="ExecuteAsync"/> with the same key,
/// only one actual operation is executed. All other callers share the same <see cref="Task{TValue}"/>
/// and receive the same result once it completes.
/// </para>
/// <para>
/// The in-flight entry is removed from the dictionary as soon as the shared task completes
/// (success or failure), so subsequent calls after completion start a new operation.
/// </para>
/// <para>
/// <b>CancellationToken note:</b> The shared task uses the <see cref="CancellationToken"/> from
/// the first caller. Later callers that share the task are not able to cancel it independently.
/// If the first caller cancels and the task faults, all coalesced callers will receive the
/// <see cref="OperationCanceledException"/>.
/// </para>
/// </remarks>
internal sealed class SingleFlightGroup<TValue>
{
    private readonly ConcurrentDictionary<int, Lazy<Task<TValue>>> _inFlight = new();

    /// <summary>
    /// Executes <paramref name="factory"/> if no in-flight operation with the same <paramref name="key"/> exists;
    /// otherwise, returns the shared task from the existing in-flight operation.
    /// </summary>
    /// <param name="key">The deduplication key. Must uniquely identify the logical operation.</param>
    /// <param name="factory">The async factory to invoke if no in-flight operation exists.</param>
    /// <returns>A task that resolves to the result of the operation.</returns>
    public Task<TValue> ExecuteAsync(int key, Func<Task<TValue>> factory)
    {
        // GetOrAdd with Lazy<Task> ensures only one Task is ever created per key,
        // even under high concurrency. LazyThreadSafetyMode.ExecutionAndPublication
        // guarantees a single invocation of the value factory.
        var lazy = _inFlight.GetOrAdd(key, _ => new Lazy<Task<TValue>>(
            () => ExecuteAndCleanup(key, factory),
            LazyThreadSafetyMode.ExecutionAndPublication));
        return lazy.Value;
    }

    private async Task<TValue> ExecuteAndCleanup(int key, Func<Task<TValue>> factory)
    {
        try
        {
            return await factory().ConfigureAwait(false);
        }
        finally
        {
            _inFlight.TryRemove(key, out _);
        }
    }
}