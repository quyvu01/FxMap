using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using FxMap.Abstractions;
using FxMap.Delegates;
using FxMap.Expressions.Building;
using FxMap.Helpers;
using FxMap.Models;
using FxMap.Responses;

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
///   <item>Step 2: Transform object[] to FxMapDataResponse in memory</item>
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
public abstract class QueryHandlerBuilder<TModel, TDistributedKey>(IServiceProvider serviceProvider)
    where TModel : class
    where TDistributedKey : IDistributedKey
{
    private const string ParameterName = "x";

    protected readonly MapEntityConfig FxMapEntityConfig = serviceProvider
        .GetRequiredService<MapperDelegates>()
        .Invoke(typeof(TModel), typeof(TDistributedKey));

    // Static cache per generic type combination - this is correct behavior in C#
    // Each QueryHandlerBuilder<User, UserOfAttribute> gets its own static fields
    private static readonly Lazy<FilterExpressionCache> FilterCache = new(() => new FilterExpressionCache());
    private static readonly ConcurrentDictionary<string, Expression<Func<TModel, object[]>>> ProjectionCache = new();

    /// <summary>
    /// Builds a filter expression for the given query using native Expression building.
    /// </summary>
    /// <remarks>
    /// Generates: x => ids.Contains(x.Id)
    /// Uses cached MethodInfo and ParameterExpression for better performance.
    /// </remarks>
    protected Expression<Func<TModel, bool>> BuildFilter(MapRequest<TDistributedKey> query)
    {
        var cache = FilterCache.Value;
        // Initialize cache on first use (lazy, thread-safe)
        cache.EnsureInitialized(FxMapEntityConfig.IdProperty, serviceProvider);
        // Convert selector ids using cached converter
        var idsConverted = cache.IdConverter.ConvertIds(query.SelectorIds);
        // Build expression using cached metadata
        return cache.BuildFilterExpression(idsConverted);
    }

    /// <summary>
    /// Builds a projection expression that returns object[].
    /// </summary>
    /// <param name="query">The request containing expression strings.</param>
    /// <param name="rows">The response for each Id</param>
    /// <returns>A projection expression and the list of expressions for transformation.</returns>
    /// <summary>Makes the response answer to the ids the way the caller wrote them (see <see cref="RequestedIdAnswers"/>).</summary>
    protected DataResponse[] AnswerRequestedIds(MapRequest<TDistributedKey> query, DataResponse[] rows) =>
        RequestedIdAnswers.Align(query.SelectorIds, rows, FilterCache.Value.IdConverter);

    /// <summary>
    /// Sorts the query by the order of a collection request (property names of the entity, or exposed names, with
    /// dots to go through navigations). Returns the query as it is when the request sets no order.
    /// </summary>
    /// <exception cref="InvalidOperationException">A property of the order does not exist on the entity.</exception>
    protected IQueryable<TModel> ApplyOrder(IQueryable<TModel> query, CollectionOptions options)
    {
        if (options?.OrderBy is not { Length: > 0 } order) return query;
        var getTypeAccessor = serviceProvider.GetRequiredService<GetTypeAccessor>();
        var ordered = query;
        for (var i = 0; i < order.Length; i++)
        {
            var parameter = Expression.Parameter(typeof(TModel), ParameterName);
            Expression body = parameter;
            foreach (var segment in order[i].PropertyName.Split('.'))
            {
                var property = getTypeAccessor.Invoke(body.Type).GetPropertyInfo(segment)
                               ?? throw new InvalidOperationException(
                                   $"Cannot order by '{order[i].PropertyName}': property '{segment}' not found on type '{body.Type.Name}'");
                body = Expression.Property(body, property);
            }

            var methodName = (i == 0, order[i].Descending) switch
            {
                (true, false) => nameof(Queryable.OrderBy),
                (true, true) => nameof(Queryable.OrderByDescending),
                (false, false) => nameof(Queryable.ThenBy),
                (false, true) => nameof(Queryable.ThenByDescending)
            };
            var method = typeof(Queryable).GetMethods()
                .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(TModel), body.Type);
            ordered = (IQueryable<TModel>)method.Invoke(null, [ordered, Expression.Lambda(body, parameter)])!;
        }

        return ordered;
    }

    protected (Expression<Func<TModel, object[]>> Projection, IReadOnlyList<string> Expressions) BuildProjection(
        MapRequest<TDistributedKey> request)
    {
        var expressionList = request.Expressions.ToList();

        // Try get from cache
        var cacheKey = ComputeCacheKey(expressionList);
        if (ProjectionCache.TryGetValue(cacheKey, out var cached))
            return (cached, expressionList);

        // Build new projection
        var builder = new ProjectionBuilder<TModel>(
            FxMapEntityConfig.IdProperty,
            FxMapEntityConfig.DefaultProperty,
            serviceProvider.GetRequiredService<GetTypeAccessor>());

        var projection = builder.Build(expressionList);

        // Cache it
        ProjectionCache.TryAdd(cacheKey, projection);
        return (projection, expressionList);
    }

    // The key is the expression list itself (order matters: values are returned by position). A hash code is not
    // enough: two different lists with the same hash would silently share one projection.
    private static string ComputeCacheKey(IReadOnlyList<string> expressions) => string.Join('\u001f', expressions);

    /// <summary>
    /// Caches all metadata needed for building filter expressions.
    /// Thread-safe, initialized once per generic type combination.
    /// </summary>
    private sealed class FilterExpressionCache
    {
        private volatile bool _isInitialized;
        private readonly object _initLock = new();

        // Cached metadata
        private ParameterExpression ModelParameter { get; set; }
        private PropertyInfo IdPropertyInfo { get; set; }
        private Type IdPropertyType { get; set; }
        private MethodInfo ContainsMethod { get; set; }
        public IIdConverter IdConverter { get; private set; }
        private MemberExpression IdPropertyAccess { get; set; }

        // Cached FilterContext type and factory delegate (faster than Activator.CreateInstance)
        private Type FilterContextType { get; set; }
        private Func<FilterContext> FilterContextFactory { get; set; }

        public void EnsureInitialized(string idPropertyName, IServiceProvider serviceProvider)
        {
            lock (_initLock)
                if (_isInitialized)
                    return;

            lock (_initLock)
            {
                if (_isInitialized) return;

                // Create parameter expression
                ModelParameter = Expression.Parameter(typeof(TModel), ParameterName);

                // Get Id property info - use GetPropertyInfoDirect to bypass ExposedName
                var getTypeAccessor = serviceProvider.GetRequiredService<GetTypeAccessor>();
                var typeAccessor = getTypeAccessor.Invoke(typeof(TModel));
                IdPropertyInfo = typeAccessor.GetPropertyInfoDirect(idPropertyName)
                                 ?? throw new InvalidOperationException(
                                     $"Id property '{idPropertyName}' not found on type '{typeof(TModel).Name}'");

                IdPropertyType = IdPropertyInfo.PropertyType;

                // Cache Id property access expression
                IdPropertyAccess = Expression.Property(ModelParameter, IdPropertyInfo);

                // Get IdConverter
                var idConverterType = typeof(IIdConverter<>).MakeGenericType(IdPropertyType);
                IdConverter = (IIdConverter)serviceProvider.GetService(idConverterType)!;

                // Cache Contains method - we'll use Enumerable.Contains<T> which works with any IEnumerable<T>
                ContainsMethod = typeof(Enumerable)
                    .GetMethods()
                    .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
                    .MakeGenericMethod(IdPropertyType);

                // Cache FilterContext type and compile factory delegate
                FilterContextType = typeof(FilterContext<>).MakeGenericType(IdPropertyType);
                FilterContextFactory = CompileFilterContextFactory(FilterContextType);

                _isInitialized = true;
            }
        }

        /// <summary>
        /// Compiles a factory delegate for creating FilterContext instances.
        /// Much faster than Activator.CreateInstance for repeated calls.
        /// </summary>
        private static Func<FilterContext> CompileFilterContextFactory(Type filterContextType)
        {
            // () => new FilterContext<TId>()
            var newExpr = Expression.New(filterContextType);
            var lambda = Expression.Lambda<Func<FilterContext>>(newExpr);
            return lambda.Compile();
        }

        /// <summary>
        /// Builds filter expression using cached metadata.
        /// Uses MemberAccess on a closure object to enable EF Core parameterization.
        /// </summary>
        /// <remarks>
        /// <para>
        /// EF Core parameterizes queries when it encounters MemberAccess expressions on closure objects,
        /// but treats direct ConstantExpression values as literal values embedded in SQL.
        /// </para>
        /// <para>
        /// By wrapping ids in FilterContext and using MemberAccess, EF Core 8+ generates parameterized queries:
        /// </para>
        /// <code>WHERE EXISTS (SELECT 1 FROM OPENJSON(@__Ids_0) WHERE [value] = [u].[Id])</code>
        /// <para>
        /// Instead of inline values:
        /// </para>
        /// <code>WHERE [u].[Id] IN ('1', '2', '3')</code>
        /// </remarks>
        public Expression<Func<TModel, bool>> BuildFilterExpression(object idsConverted)
        {
            // Create NEW FilterContext instance per request to avoid race conditions
            var filterContext = FilterContextFactory();
            filterContext.SetIds(idsConverted);

            // Expression.Constant with the new instance - EF Core will see MemberAccess on it
            var filterContextExpr = Expression.Constant(filterContext, FilterContextType);

            // Use cached PropertyInfo from FilterContext
            var idsAccess = Expression.Property(filterContextExpr, filterContext.IdsPropertyInfo);

            // Enumerable.Contains(filterContext.Ids, x.Id)
            var containsCall = Expression.Call(ContainsMethod, idsAccess, IdPropertyAccess);

            return Expression.Lambda<Func<TModel, bool>>(containsCall, ModelParameter);
        }
    }
}