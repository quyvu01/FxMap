namespace FxMap.Abstractions;

/// <summary>
/// Represents the raw request payload used to initiate a query in FxMap.
/// </summary>
/// <typeparam name="TDistributedKey">
/// The <see cref="IDistributedKey"/> type that defines the mapping or behavior for the request.
/// </typeparam>
/// <param name="SelectorIds">
/// The list of string-based selector IDs identifying the target entities or records to be queried.  
/// These will later be converted into model IDs using <see cref="IIdConverter{TId}"/>.
/// </param>
/// <param name="Expressions">
/// The filter or selection expression (in string form) used to shape or restrict the query results.  
/// This expression will be parsed and executed by the server-side <see cref="IQueryOfHandler{TModel, TDistributedKey}"/>.
/// </param>
/// <remarks>
/// <para>
/// The <see cref="MapRequest{TDistributedKey}"/> is a lightweight, immutable record that holds the 
/// **essential request data** (selector IDs and expression).  
/// </para>
/// <para>
/// It is later wrapped in a <see cref="RequestContext{TDistributedKey}"/>, which adds additional context 
/// such as headers and <see cref="CancellationToken"/> for end-to-end request processing.
/// </para>
/// </remarks>
public sealed record MapRequest<TDistributedKey>(string[] SelectorIds, string[] Expressions)
    where TDistributedKey : IDistributedKey;