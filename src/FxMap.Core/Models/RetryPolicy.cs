namespace FxMap.Models;

/// <summary>
/// Represents the configuration for retry behavior when requests fail.
/// </summary>
/// <param name="RetryCount">
/// The maximum number of retries after the first attempt: <c>RetryCount = 3</c> makes at most 4 attempts.
/// </param>
/// <param name="SleepDurationProvider">
/// A function that calculates the delay before each retry. The parameter is the number of the retry (1-based:
/// 1 is the delay before the first retry). When it is null the retries follow each other without a delay.
/// </param>
/// <param name="OnRetry">
/// A callback invoked before each retry, after the delay was calculated and before it is waited, with the exception of the
/// attempt that failed and the delay. It is not invoked for the last failure, because no retry follows it.
/// Useful for logging or metrics collection.
/// </param>
/// <param name="ShouldRetry">
/// A predicate that decides whether an exception is worth a retry. When it returns false the exception is thrown at once.
/// When it is null every exception is retried, except those of the caller cancelling the request.
/// </param>
internal record RetryPolicy(
    int RetryCount,
    Func<int, TimeSpan> SleepDurationProvider,
    Action<Exception, TimeSpan> OnRetry,
    Func<Exception, bool> ShouldRetry = null);
