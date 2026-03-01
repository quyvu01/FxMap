using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FxMap.Abstractions;
using FxMap.Responses;
using FxMap.Configuration;

namespace FxMap.BuiltInPipelines;

/// <summary>
/// Internal send pipeline behavior that handles exception suppression based on configuration.
/// </summary>
/// <typeparam name="TDistributedKey">The FxMap distributed key type.</typeparam>
/// <remarks>
/// When <see cref="FxMapStatics.ThrowIfExceptions"/> is false, this behavior catches exceptions
/// and returns an empty response instead of propagating the error. This enables graceful
/// degradation in production environments where missing data shouldn't crash the application.
/// </remarks>
internal sealed class ExceptionPipelineBehavior<TDistributedKey>(IServiceProvider serviceProvider)
    : ISendPipelineBehavior<TDistributedKey>
    where TDistributedKey : IDistributedKey
{
    private readonly ILogger<ExceptionPipelineBehavior<TDistributedKey>> _logger =
        serviceProvider.GetService<ILogger<ExceptionPipelineBehavior<TDistributedKey>>>();

    public async Task<ItemsResponse<DataResponse>> HandleAsync(RequestContext<TDistributedKey> requestContext,
        Func<Task<ItemsResponse<DataResponse>>> next)
    {
        try
        {
            return await next.Invoke();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in pipeline for {@Attribute}", typeof(TDistributedKey).Name);

            // Only suppress non-critical exceptions
            if (ex is OutOfMemoryException or StackOverflowException or ThreadAbortException) throw;

            if (FxMapStatics.ThrowIfExceptions) throw;
            return new ItemsResponse<DataResponse>([]);
        }
    }
}