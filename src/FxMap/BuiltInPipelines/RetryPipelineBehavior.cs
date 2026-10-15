using FxMap.Abstractions;
using FxMap.Delegates;
using FxMap.Responses;
using FxMap.Models;
using Microsoft.Extensions.DependencyInjection;

namespace FxMap.BuiltInPipelines;

/// <summary>
/// Internal send pipeline behavior that implements retry logic with configurable backoff.
/// </summary>
/// <typeparam name="TDistributedKey">The FxMap distributed key type.</typeparam>
/// <remarks>
/// This behavior uses the <see cref="RetryPolicy"/> configuration to retry failed requests:
/// <list type="bullet">
///   <item><description>At most <c>RetryCount</c> retries after the first attempt.</description></item>
///   <item><description>The delay (custom backoff) is waited <b>before</b> each retry, not after the last failure.</description></item>
///   <item><description><c>OnRetry</c> is invoked before each retry, with the exception of the attempt that failed and the delay.</description></item>
///   <item><description><c>ShouldRetry</c> can refuse a retry; the exception is thrown at once.</description></item>
///   <item><description>When the caller cancels the request nothing is retried and no more delay is waited.</description></item>
///   <item><description>When the attempts are used up, the last exception is thrown.</description></item>
/// </list>
/// </remarks>
internal sealed class RetryPipelineBehavior<TDistributedKey>(IServiceProvider serviceProvider)
    : ISendPipelineBehavior<TDistributedKey>
    where TDistributedKey : IDistributedKey
{
    public async Task<ItemsResponse<DataResponse>> HandleAsync(RequestContext<TDistributedKey> requestContext,
        SendHandlerDelegate next)
    {
        var mapperConfiguration = serviceProvider.GetRequiredService<IMapperConfiguration>();
        var retryPolicy = mapperConfiguration.RetryPolicy;
        if (retryPolicy is null) return await next.Invoke();
        var ct = requestContext.CancellationToken;
        var retry = 0;

        while (true)
        {
            try
            {
                return await next.Invoke();
            }
            catch (Exception ex) when (retry < retryPolicy.RetryCount && !ct.IsCancellationRequested &&
                                       ex is not OutOfMemoryException)
            {
                // The exception of a retry that is refused (or the last one) leaves from here as it is
                if (retryPolicy.ShouldRetry is { } shouldRetry && !shouldRetry(ex)) throw;

                retry++;
                var delay = retryPolicy.SleepDurationProvider?.Invoke(retry) ?? TimeSpan.Zero;
                retryPolicy.OnRetry?.Invoke(ex, delay);
                if (delay > TimeSpan.Zero) await Task.Delay(delay, ct);
            }
        }
    }
}
