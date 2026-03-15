using System.Collections;
using System.Collections.Concurrent;
using FxMap.Abstractions;
using FxMap.Models;
using FxMap.Exceptions;
using FxMap.Extensions;
using FxMap.PublicContracts;
using FxMap.Responses;
using FxMap.Delegates;
using FxMap.Serializable;
using Microsoft.Extensions.DependencyInjection;

namespace FxMap.Implementations;

/// <summary>
/// Core implementation of <see cref="IDistributedMapper"/> that performs distributed data mapping.
/// </summary>
/// <remarks>
/// The DistributedMapper is the heart of the FxMap framework. It:
/// <list type="bullet">
///   <item><description>Scans objects for properties configured via <c>ProfileOf&lt;T&gt;</c></description></item>
///   <item><description>Groups properties by their dependency order for efficient batching</description></item>
///   <item><description>Sends requests through the configured transport to fetch remote data</description></item>
///   <item><description>Maps the returned data back to the original object properties</description></item>
///   <item><description>Recursively processes nested objects up to a configurable depth limit</description></item>
/// </list>
/// </remarks>
/// <param name="serviceProvider">The service provider for resolving transport handlers and pipelines.</param>
internal sealed class DistributedMapper(IServiceProvider serviceProvider) : IDistributedMapper
{
    private readonly ItemsResponse<DataResponse> _emptyResponse = new([]);

    private static readonly ConcurrentDictionary<Type, Type> SendOrchestratorTypes = new();

    public async Task MapDataAsync(object value, CancellationToken token = default)
    {
        var fxMapConfiguration = serviceProvider.GetRequiredService<IMapperConfiguration>();
        var currentNestingLevel = 0;
        while (true)
        {
            if (currentNestingLevel >= fxMapConfiguration.MaxNestingDepth)
            {
                if (fxMapConfiguration.ThrowIfExceptions) throw new DistributedMapException.MaxNestingDepthReached();
                return;
            }

            var allPropertyDatas = DiscoverResolvableProperties(value).ToArray();

            var attributes = fxMapConfiguration.DistributedKeyTypes;
            var typeData = GetDistributedKeyInfos(allPropertyDatas, attributes);

            // Pre-group once by order — avoids O(N×M) re-scan per order level
            var propertiesByOrder = allPropertyDatas
                .GroupBy(x => x.Property.Order)
                .ToDictionary(g => g.Key, IEnumerable<PropertyDescriptor> (g) => g);

            var typesDataGrouped = typeData
                .GroupBy(a => a.Order)
                .OrderBy(a => a.Key);

            foreach (var mappableTypes in typesDataGrouped)
            {
                var orderedProperties = propertiesByOrder.GetValueOrDefault(mappableTypes.Key, []);
                var tasks = mappableTypes.Select(async x =>
                {
                    var emptyResponse = (x.DistributedKeyType, Response: _emptyResponse);
                    var accessors = x.Accessors.ToList();
                    if (accessors is not { Count: > 0 }) return emptyResponse;
                    var selectorIds = accessors
                        .Select(c => c.PropertyInformation?.RequiredAccessor?.Get(c.Model)?
                            .ToString()).ToArray();

                    if (selectorIds is not { Length: > 0 }) return emptyResponse;

                    var requestCt = new RequestContext([], token);

                    // Resolve conditional expressions and store on PropertyDescriptor (request-scoped)
                    var effectiveExpressionTasks = accessors
                        .Select(async a => a.EffectiveExpression = await a.PropertyInformation
                            .ResolveExpression(serviceProvider, token));
                    await Task.WhenAll(effectiveExpressionTasks);

                    var expressions = accessors.Select(a => a.EffectiveExpression);

                    var result = await FetchDataAsync(x.DistributedKeyType,
                        new DistributedMapRequest(selectorIds, [..expressions]), requestCt);
                    return (x.DistributedKeyType, Response: result);
                });
                var fetchedResult = await Task.WhenAll(tasks);
                MapResponseData(orderedProperties, fetchedResult);
            }

            var nextMappableData = allPropertyDatas
                .Where(a => !a.PropertyInfo.PropertyType.IsPrimitiveType())
                .Aggregate(new List<object>(), (acc, next) =>
                {
                    var getProfileConfig = serviceProvider.GetRequiredService<GetProfileConfig>();
                    var profileConfig = getProfileConfig.Invoke(next.Model.GetType());
                    var propertyAccessor = profileConfig?.Accessors?.GetValueOrDefault(next.PropertyInfo);
                    var propertyValue = propertyAccessor?.Get(next.Model);
                    if (propertyValue is null) return acc;
                    acc.Add(propertyValue);
                    return acc;
                });
            if (nextMappableData is not { Count: > 0 }) break;
            currentNestingLevel += 1;
            value = nextMappableData;
        }
    }

    public Task<ItemsResponse<DataResponse>> FetchDataAsync<TDistributedKey>(DistributedMapRequest query,
        IContext context = null) where TDistributedKey : IDistributedKey =>
        FetchDataAsync(typeof(TDistributedKey), query, context);

    public async Task<ItemsResponse<DataResponse>> FetchDataAsync(Type runtimeType, DistributedMapRequest query,
        IContext context = null)
    {
        var sendPipelineType = SendOrchestratorTypes
            .GetOrAdd(runtimeType, static type => typeof(SendPipelinesOrchestrator<>).MakeGenericType(type));
        var sendPipelineWrapped = (SendPipelinesOrchestrator)serviceProvider.GetService(sendPipelineType)!;
        string[] selectorIds = [..new HashSet<string>(query.SelectorIds.Where(a => a is not null))];
        string[] expressions = [..new HashSet<string>(query.Expressions)];
        var result = await sendPipelineWrapped
            .ExecuteAsync(new DistributedMapRequest(selectorIds, expressions), context);
        return result;
    }


    private IEnumerable<PropertyDescriptor> DiscoverResolvableProperties(object rootObject)
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        return GetResolvablePropertiesRecursive(rootObject, visited);
    }

    private IEnumerable<PropertyDescriptor> GetResolvablePropertiesRecursive(object obj, HashSet<object> visited)
    {
        if (obj.IsNullOrPrimitive()) yield break;

        if (obj is IEnumerable enumerable)
        {
            foreach (var item in enumerable is IDictionary dictionary ? dictionary.Values : enumerable)
            foreach (var prop in GetResolvablePropertiesRecursive(item, visited))
                yield return prop;
            yield break;
        }

        if (!visited.Add(obj)) yield break;

        var objType = obj.GetType();
        var getProfileConfig = serviceProvider.GetRequiredService<GetProfileConfig>();
        var profileConfig = getProfileConfig.Invoke(objType);
        if (profileConfig is null) yield break;
        foreach (var (propertyInfo, accessor) in profileConfig.Accessors)
        {
            var propertyInformation = profileConfig.GetInformation(propertyInfo);
            if (propertyInformation.RequiredAccessor is not null)
            {
                yield return new PropertyDescriptor(propertyInfo, obj, propertyInformation);
                continue;
            }

            var propValue = accessor.Get(obj);
            foreach (var value in GetResolvablePropertiesRecursive(propValue, visited))
                yield return value;
        }
    }

    // To use merge-expression, we have to group by distributed key only, exclude expression as the older version!
    private static IEnumerable<DistributedKeyInfo> GetDistributedKeyInfos
        (IEnumerable<PropertyDescriptor> propertyDescriptors, IEnumerable<Type> distributedKeyTypes) =>
        propertyDescriptors
            .GroupBy(mdp => (DistributedKeyType: mdp.Property?.RuntimeDistributedKeyType,
                Order: mdp.Property?.Order ?? 0))
            .Join(distributedKeyTypes, gr => gr.Key.DistributedKeyType, at => at,
                (d, x) => new DistributedKeyInfo(x, d
                    .Select(a => new PropertyMappingData(a)), d.Key.Order));

    private void MapResponseData(IEnumerable<PropertyDescriptor> mappableProperties,
        IEnumerable<(Type DistributedKeyType, ItemsResponse<DataResponse> ItemsResponse)> dataFetched)
    {
        var dataWithExpression = dataFetched
            .Select(a => a.ItemsResponse.Items
                .Select(x => (x.Id, FxMapValues: x.Values))
                .Select(k => (a.DistributedKeyType, Data: k)))
            .SelectMany(x => x);
        mappableProperties.Join(dataWithExpression, ap => (ap.Property?.RuntimeDistributedKeyType, ap
                .Property?
                .RequiredAccessor?
                .Get(ap.Model)?.ToString()),
            dt => (dt.DistributedKeyType, dt.Data.Id), (ap, dt) =>
            {
                var value = dt.Data
                    .FxMapValues
                    .FirstOrDefault(a => a.Expression == ap.EffectiveExpression)?.Value;
                if (value is null || ap.PropertyInfo is not { } propertyInfo) return value;
                try
                {
                    var valueSet = JsonSerializer.DeserializeObject(value, propertyInfo.PropertyType);
                    var getProfileConfig = serviceProvider.GetRequiredService<GetProfileConfig>();
                    var profileConfig = getProfileConfig.Invoke(ap.Model.GetType());
                    var propertyAccessor = profileConfig?.Accessors?.GetValueOrDefault(ap.PropertyInfo);
                    propertyAccessor?.Set(ap.Model, valueSet);
                }
                catch (Exception)
                {
                    var fxMapConfiguration = serviceProvider.GetRequiredService<IMapperConfiguration>();
                    if (fxMapConfiguration.ThrowIfExceptions) throw;
                }

                return value;
            }).Evaluate();
    }
}