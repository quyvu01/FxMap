using FxMap.Abstractions;
using FxMap.Responses;
using FxMap.Models;
using Microsoft.Extensions.DependencyInjection;

namespace FxMap.BuiltInPipelines;

/// <summary>
/// Internal send pipeline behavior that implements retry logic with configurable backoff.
/// </summary>
/// <typeparam name="TDistributedKey">The FxMap distributed key type.</typeparam>
/// <remarks>
/// This behavior uses the <see cref="RetryPolicy"/> configuration to retry failed requests.
/// Features include:
/// <list type="bullet">
///   <item><description>Configurable retry count</description></item>
///   <item><description>Custom sleep duration provider for backoff strategies</description></item>
///   <item><description>Optional callback for retry notifications</description></item>
/// </list>
/// </remarks>
internal sealed class RetryPipelineBehavior<TDistributedKey>(IServiceProvider serviceProvider)
    : ISendPipelineBehavior<TDistributedKey>
    where TDistributedKey : IDistributedKey
{
    public async Task<ItemsResponse<DataResponse>> HandleAsync(RequestContext<TDistributedKey> requestContext,
        Func<Task<ItemsResponse<DataResponse>>> next)
    {
        var fxMapConfiguration = serviceProvider.GetRequiredService<IMapperConfiguration>();
        var retryPolicy = fxMapConfiguration.RetryPolicy;
        if (retryPolicy is null) return await next.Invoke();
        var ct = requestContext.CancellationToken;
        try
        {
            return await next.Invoke();
        }
        catch (Exception)
        {
            foreach (var retryCount in Enumerable.Range(1, retryPolicy.RetryCount))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    return await next.Invoke();
                }
                catch (Exception ex)
                {
                    var retryAfter = TimeSpan.Zero;
                    if (retryPolicy.SleepDurationProvider is { } sleepDurationProvider)
                    {
                        retryAfter = sleepDurationProvider.Invoke(retryCount);
                        await Task.Delay(retryAfter, ct);
                    }

                    retryPolicy.OnRetry?.Invoke(ex, retryAfter);
                }
            }

            throw;
        }
    }
}