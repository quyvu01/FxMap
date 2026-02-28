using OfX.Responses;

namespace OfX.Abstractions;

/// <summary>
/// Defines the server-side abstraction for retrieving data for a given <typeparamref name="TModel"/>
/// based on a specific <typeparamref name="TDistributedKey"/>.
/// </summary>
/// <typeparam name="TModel">
/// The model type representing the entity being queried (e.g., <c>User</c>, <c>Order</c>).
/// </typeparam>
/// <typeparam name="TDistributedKey">
/// The <see cref="IDistributedKey"/> type that describes the query mapping for <typeparamref name="TModel"/>.
/// </typeparam>
/// <remarks>
/// This interface is implemented on the **server side** of the OfX framework.  
/// Its primary purpose is to fetch data from the underlying data provider 
/// (e.g., Entity Framework, MongoDB...) in response to
/// a client request sent via <see cref="IClientRequestHandler{TDistributedKey}"/>.
/// </remarks>
public interface IQueryOfHandler<TModel, TDistributedKey> where TModel : class where TDistributedKey : IDistributedKey
{
    /// <summary>
    /// Retrieves data for the given <typeparamref name="TModel"/> based on the incoming request context.
    /// </summary>
    /// <param name="context">
    /// The request context containing selector IDs, expressions, headers, and cancellation token.
    /// </param>
    /// <returns>
    /// A task that resolves to an <see cref="ItemsResponse{OfXDataResponse}"/> containing
    /// the resulting data from the provider.
    /// </returns>
    Task<ItemsResponse<DataResponse>> GetDataAsync(RequestContext<TDistributedKey> context);
}

/// <summary>
/// Serves as the non-generic base class for default server-side query handlers.
/// </summary>
/// <remarks>
/// This type is primarily used for type resolution and should not be used directly.
/// </remarks>
internal class NoOpQueryOfHandler;

/// <summary>
/// Provides a default no-op implementation of <see cref="IQueryOfHandler{TModel, TDistributedKey}"/>.
/// </summary>
/// <typeparam name="TModel">
/// The model type representing the entity being queried.
/// </typeparam>
/// <typeparam name="TDistributedKey">
/// The <see cref="IDistributedKey"/> type that describes the query mapping for <typeparamref name="TModel"/>.
/// </typeparam>
/// <remarks>
/// This default implementation always returns an empty <see cref="ItemsResponse{OfXDataResponse}"/>.
/// It is typically used as a fallback when no specific query handler is registered.
/// </remarks>
internal sealed class NoOpQueryOfHandler<TModel, TDistributedKey>
    : NoOpQueryOfHandler, IQueryOfHandler<TModel, TDistributedKey>
    where TModel : class
    where TDistributedKey : IDistributedKey
{
    /// <inheritdoc />
    public Task<ItemsResponse<DataResponse>> GetDataAsync(RequestContext<TDistributedKey> context) =>
        Task.FromResult(new ItemsResponse<DataResponse>([]));
}