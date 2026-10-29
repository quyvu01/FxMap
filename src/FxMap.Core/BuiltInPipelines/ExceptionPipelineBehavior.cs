using Microsoft.Extensions.Logging;
using FxMap.Abstractions;
using FxMap.Delegates;
using FxMap.Responses;

namespace FxMap.BuiltInPipelines;

/// <summary>
/// Internal send pipeline behavior that handles exception suppression based on configuration.
/// </summary>
/// <typeparam name="TDistributedKey">The FxMap distributed key type.</typeparam>
/// <remarks>
/// When <see cref="IMapperConfiguration.ThrowIfExceptions"/> is false, this behavior catches exceptions
/// and returns an empty response instead of propagating the error. This enables graceful
/// degradation in production environments where missing data shouldn't crash the application.
/// </remarks>
internal sealed class ExceptionPipelineBehavior<TDistributedKey>(
    IMapperConfiguration configuration,
    ILogger<ExceptionPipelineBehavior<TDistributedKey>> logger = null)
    : ISendPipelineBehavior<TDistributedKey>
    where TDistributedKey : IDistributedKey
{
    public async Task<ItemsResponse<DataResponse>> HandleAsync(RequestContext<TDistributedKey> requestContext,
        SendHandlerDelegate next)
    {
        try
        {
            return await next.Invoke();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error in pipeline for {@DistributedKey}", typeof(TDistributedKey).Name);

            // Only suppress non-critical exceptions
            if (ex is OutOfMemoryException or StackOverflowException or ThreadAbortException) throw;
            if (configuration.ThrowIfExceptions) throw;
            return new ItemsResponse<DataResponse>([]);
        }
    }
}