using System.Collections.Concurrent;
using FxMap.Abstractions;
using FxMap.Models;
using FxMap.MetadataCache;
using FxMap.Exceptions;
using FxMap.Extensions;
using FxMap.PublicContracts;
using FxMap.Helpers;
using FxMap.Queries;
using FxMap.Responses;
using FxMap.Configuration;

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
        var currentNestingLevel = 0;
        while (true)
        {
            if (currentNestingLevel >= FxMapStatics.MaxNestingDepth)
            {
                if (FxMapStatics.ThrowIfExceptions) throw new FxMapException.MaxNestingDepthReached();
                return;
            }

            var allPropertyDatas = ReflectionHelpers.DiscoverResolvableProperties(value).ToArray();

            var attributes = FxMapStatics.DistributedKeyTypes.Value;
            var typeData = ReflectionHelpers.GetFxMapTypesData(allPropertyDatas, attributes);

            // Pre-group once by order — avoids O(N×M) re-scan per order level
            var propertiesByOrder = allPropertyDatas
                .GroupBy(x => x.PropertyInformation.Order)
                .ToDictionary(g => g.Key, IEnumerable<PropertyDescriptor> (g) => g);

            var typesDataGrouped = typeData
                .GroupBy(a => a.Order)
                .OrderBy(a => a.Key);

            foreach (var mappableTypes in typesDataGrouped)
            {
                var orderedProperties = propertiesByOrder.GetValueOrDefault(mappableTypes.Key, []);
                var tasks = mappableTypes.Select(async x =>
                {
                    var emptyResponse = (FxMapAttributeType: x.DistributedKeyType, Response: _emptyResponse);
                    var accessors = x.Accessors.ToList();
                    if (accessors is not { Count: > 0 }) return emptyResponse;
                    var selectorIds = new HashSet<string>(accessors
                        .Select(c => c.PropertyInformation?.RequiredAccessor?.Get(c.Model)?.ToString())
                        .Where(c => c is not null));

                    if (selectorIds is not { Count: > 0 }) return emptyResponse;

                    var requestCt = new RequestContext([], token);

                    // Resolve conditional expressions and store on PropertyDescriptor (request-scoped)
                    foreach (var a in accessors)
                        a.EffectiveExpression = await a.PropertyInformation
                            .ResolveExpression(serviceProvider, token);

                    var expressions = accessors
                        .Select(a => a.EffectiveExpression);

                    var result = await FetchDataAsync(x.DistributedKeyType,
                        new DataFetchQuery([..selectorIds], [..expressions]), requestCt);
                    return (FxMapAttributeType: x.DistributedKeyType, Response: result);
                });
                var fetchedResult = await Task.WhenAll(tasks);
                ReflectionHelpers.MapResponseData(orderedProperties, fetchedResult);
            }

            var nextMappableData = allPropertyDatas
                .Where(a => !a.PropertyInfo.PropertyType.IsPrimitiveType())
                .Aggregate(new List<object>(), (acc, next) =>
                {
                    var profileConfig = FluentConfigStore.ProfileConfigs.GetValueOrDefault(next.Model.GetType());
                    var propertyAccessor = profileConfig.Accessors.GetValueOrDefault(next.PropertyInfo);
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

    public Task<ItemsResponse<DataResponse>> FetchDataAsync<TDistributedKey>(DataFetchQuery query,
        IContext context = null) where TDistributedKey : IDistributedKey =>
        FetchDataAsync(typeof(TDistributedKey), query, context);

    public async Task<ItemsResponse<DataResponse>> FetchDataAsync(Type runtimeType, DataFetchQuery query,
        IContext context = null)
    {
        var sendPipelineType = SendOrchestratorTypes
            .GetOrAdd(runtimeType, static type => typeof(SendPipelinesOrchestrator<>).MakeGenericType(type));
        var sendPipelineWrapped = (SendPipelinesOrchestrator)serviceProvider.GetService(sendPipelineType)!;
        var result = await sendPipelineWrapped
            .ExecuteAsync(new FxMapRequest(query.SelectorIds, [..new HashSet<string>(query.Expressions)]), context);
        return result;
    }
}