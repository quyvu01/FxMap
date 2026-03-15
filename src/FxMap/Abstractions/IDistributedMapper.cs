using FxMap.Models;
using FxMap.Responses;

namespace FxMap.Abstractions;

/// <summary>
/// Defines the abstraction for mapping and fetching data in FxMap.
/// </summary>
/// <remarks>
/// This service acts as the entry point for mapping objects and retrieving data using
/// FxMap distributed key-based models configured via <c>ProfileOf&lt;T&gt;</c>.
/// Use <see cref="MapDataAsync(object, CancellationToken)"/> to map arbitrary objects,
/// or <see cref="FetchDataAsync{TDistributedKey}(DistributedMapRequest, IContext)"/> /
/// <see cref="FetchDataAsync(Type, DistributedMapRequest, IContext)"/> to retrieve strongly-typed data.
/// </remarks>
public interface IDistributedMapper
{
    /// <summary>
    /// Maps the specified object to its corresponding model using the FxMap mapping engine.
    /// </summary>
    /// <param name="value">
    /// The source object to be mapped. This can be any type that is supported by the FxMap mapping system.
    /// </param>
    /// <param name="token">
    /// A token that can be used to cancel the mapping operation before it completes.
    /// Useful for handling timeouts or user-initiated cancellations.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous mapping operation.
    /// </returns>
    Task MapDataAsync(object value, CancellationToken token = default);

    /// <summary>
    /// Fetches data for a given <typeparamref name="TDistributedKey"/> type.
    /// </summary>
    /// <typeparam name="TDistributedKey">
    /// The type of <see cref="IDistributedKey"/> representing the model or entity being queried.
    /// </typeparam>
    /// <param name="query">
    /// The input data, such as selector IDs and expressions used to filter or project the result.
    /// </param>
    /// <param name="context">
    /// (Optional) The request context, including headers and a <see cref="CancellationToken"/>.
    /// </param>
    /// <returns>
    /// A task that resolves to an <see cref="ItemsResponse{FxMapDataResponse}"/> containing the fetched data.
    /// </returns>
    Task<ItemsResponse<DataResponse>> FetchDataAsync<TDistributedKey>(DistributedMapRequest query,
        IContext context = null)
        where TDistributedKey : IDistributedKey;

    /// <summary>
    /// Fetches data for a model determined at runtime, using the specified <see cref="Type"/>.
    /// </summary>
    /// <param name="runtimeType">
    /// The runtime type of the <see cref="IDistributedKey"/> (e.g., <c>typeof(IUserKey)</c>).
    /// </param>
    /// <param name="query">
    /// The input data, such as selector IDs and expressions used to filter or project the result.
    /// </param>
    /// <param name="context">
    /// (Optional) The request context, including headers and a <see cref="CancellationToken"/>.
    /// </param>
    /// <returns>
    /// A task that resolves to an <see cref="ItemsResponse{FxMapDataResponse}"/> containing the fetched data.
    /// </returns>
    Task<ItemsResponse<DataResponse>>
        FetchDataAsync(Type runtimeType, DistributedMapRequest query, IContext context = null);
}