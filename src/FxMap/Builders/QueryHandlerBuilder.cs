using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using FxMap.Abstractions;
using FxMap.Delegates;
using FxMap.Expressions.Building;
using FxMap.Helpers;
using FxMap.Responses;
using Microsoft.Extensions.DependencyInjection;

namespace FxMap.Builders;

/// <summary>
/// Version 2 of QueryHandlerBuilder using the new Expression DSL system.
/// </summary>
/// <typeparam name="TModel">The entity model type being queried.</typeparam>
/// <typeparam name="TDistributedKey">The FxMap distributed key type associated with this handler.</typeparam>
/// <remarks>
/// <para>
/// This builder uses a two-step approach for better database compatibility:
/// </para>
/// <list type="bullet">
///   <item>Step 1: Build projection to object[] and execute database query</item>
///   <item>Step 2: Transform object[] to DataResponse in memory</item>
/// </list>
/// <para>
/// Key improvements over V1:
/// </para>
/// <list type="bullet">
///   <item>Support for complex expressions: filters, indexers, aggregations</item>
///   <item>Support for ExposedName configuration for property masking</item>
///   <item>Support for null-safe navigation (?. operator)</item>
///   <item>Better error handling per expression</item>
/// </list>
/// </remarks>
public abstract class QueryHandlerBuilder<TModel, TDistributedKey>
    where TModel : class
    where TDistributedKey : IDistributedKey
{
    public abstract Expression<Func<TModel, bool>> BuildFilter(MapRequest<TDistributedKey> query);

    public abstract (Expression<Func<TModel, object[]>> Projection, IReadOnlyList<string> Expressions) BuildProjection(
        MapRequest<TDistributedKey> request);

    /// <summary>
    /// Makes the response answer to the ids the way the caller wrote them (see <see cref="RequestedIdAnswers"/>):
    /// rows come back keyed by the canonical text of the entity id, while the caller matches a response to its object
    /// with the text it sent (upper case or braced Guid, <c>"007"</c> for 7...).
    /// </summary>
    /// <param name="query">The request, holding the id texts the caller sent.</param>
    /// <param name="rows">The rows found, keyed by the canonical id text.</param>
    public abstract DataResponse[] AnswerRequestedIds(MapRequest<TDistributedKey> query, DataResponse[] rows);
}

/// <summary>
/// The query building of a handler for an entity identified by <typeparamref name="TId"/>: the filter (ids of the
/// request), the projection (expressions of the request) and the answer to the requested ids.
/// Registered as an open generic singleton by <c>AddFxMap</c>.
/// </summary>
public class QueryHandlerBuilder<TModel, TId, TDistributedKey>(IServiceProvider serviceProvider)
    : QueryHandlerBuilder<TModel, TDistributedKey> where TModel : class
    where TDistributedKey : IDistributedKey
{
    private readonly IMapEntityConfig _entityConfig = serviceProvider
        .GetRequiredService<MapperDelegates>()
        .Invoke(typeof(TModel), typeof(TDistributedKey));

    private static readonly MethodInfo ListContainsMethod =
        typeof(List<TId>).GetMethod(nameof(List<>.Contains), [typeof(TId)])!;

    // Static cache per generic type combination - this is correct behavior in C#
    // Each QueryHandlerBuilder<User, UserOfAttribute> gets its own static fields
    private static readonly ConcurrentDictionary<string, Expression<Func<TModel, object[]>>> ProjectionCache = new();

    public override Expression<Func<TModel, bool>> BuildFilter(MapRequest<TDistributedKey> query)
    {
        var idConverter = serviceProvider.GetRequiredService<IIdConverter<TId>>();
        var idsHolder = new IdsHolder(idConverter.ConvertIds(query.SelectorIds));
        var idSelector = _entityConfig.IdPropertySelector;

        var parameter = idSelector.Parameters[0];
        var idBody = idSelector.Body;

        var idsAccess = Expression.Property(Expression.Constant(idsHolder), nameof(IdsHolder.Ids));

        var body = Expression.Call(idsAccess, ListContainsMethod, idBody);

        return Expression.Lambda<Func<TModel, bool>>(body, parameter);
    }

    public override (Expression<Func<TModel, object[]>> Projection, IReadOnlyList<string> Expressions) BuildProjection(
        MapRequest<TDistributedKey> request)
    {
        var expressionList = request.Expressions.ToList();

        // Try get from cache
        var cacheKey = ComputeCacheKey(expressionList);
        if (ProjectionCache.TryGetValue(cacheKey, out var cached))
            return (cached, expressionList);

        // Build new projection
        var builder = new ProjectionBuilder<TModel>(
            _entityConfig.IdPropertySelector,
            _entityConfig.DefaultPropertySelector,
            serviceProvider.GetRequiredService<GetTypeAccessor>());

        var projection = builder.Build(expressionList);

        // Cache it
        ProjectionCache.TryAdd(cacheKey, projection);
        return (projection, expressionList);
    }

    public override DataResponse[] AnswerRequestedIds(MapRequest<TDistributedKey> query, DataResponse[] rows) =>
        RequestedIdAnswers.Align(query.SelectorIds, rows, serviceProvider.GetRequiredService<IIdConverter<TId>>());

    // The key is the expression list itself (order matters: values are returned by position). A hash code is not
    // enough: two different lists with the same hash would silently share one projection.
    private static string ComputeCacheKey(IReadOnlyList<string> expressions) => string.Join('\u001f', expressions);

    private sealed record IdsHolder(List<TId> Ids);
}