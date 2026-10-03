using System.Collections;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using FxMap.Abstractions;
using FxMap.Accessors.PropertyAccessors;
using FxMap.Fluent.Rules;
using FxMap.Helpers;
using FxMap.Models;
using FxMap.Fluent;
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
            var allPropertyDatas = DiscoverResolvableProperties(value);

            // Nothing left to map (e.g. the next level only holds empty collections): done, whatever the depth.
            if (allPropertyDatas.Count == 0) break;

            if (currentNestingLevel >= fxMapConfiguration.MaxNestingDepth)
            {
                if (fxMapConfiguration.ThrowIfExceptions) throw new DistributedMapException.MaxNestingDepthReached();
                return;
            }

            var distributedKeyTypes = fxMapConfiguration.DistributedKeyTypes;
            var typeData = GetDistributedKeyInfos(allPropertyDatas, distributedKeyTypes);

            var typesDataGrouped = typeData
                .GroupBy(a => a.Order)
                .OrderBy(a => a.Key);

            foreach (var mappableTypes in typesDataGrouped)
            {
                var tasks = mappableTypes.Select(async x =>
                {
                    var emptyResponse = (Info: x, Response: _emptyResponse);
                    var properties = x.Properties.ToList();
                    if (properties is not { Count: > 0 }) return emptyResponse;

                    // Collect each distinct id once instead of one entry per object: many objects share few ids.
                    var selectorIds = new HashSet<string>();
                    foreach (var property in properties)
                        if (property.Property.RequiredAccessor?.Get(property.Model)?.ToString() is { } id)
                            selectorIds.Add(id);

                    var requestCt = new RequestContext([], token);

                    // Resolve conditional expressions and store on PropertyDescriptor (request-scoped).
                    // Plain expressions need no resolution, so no task is created for them.
                    var conditionalTasks = new List<Task>();
                    foreach (var property in properties)
                    {
                        // A collection property asks for the expressions of its item rules instead.
                        if (property.Property.CollectionPlan is { } collectionPlan)
                        {
                            property.ItemExpressions = new string[collectionPlan.Rules.Length];
                            for (var i = 0; i < collectionPlan.Rules.Length; i++)
                            {
                                var itemRule = collectionPlan.Rules[i].Rule;
                                if (itemRule.ConditionalExpression is null)
                                    property.ItemExpressions[i] = itemRule.Expression;
                                else
                                    conditionalTasks.Add(ResolveItemExpressionAsync(property, i,
                                        itemRule.ConditionalExpression, serviceProvider, token));
                            }

                            continue;
                        }

                        if (property.Property.ConditionalExpression is null)
                        {
                            property.EffectiveExpression = property.Property.Expression;
                            continue;
                        }

                        conditionalTasks.Add(ResolveConditionalAsync(property, serviceProvider, token));
                    }

                    if (conditionalTasks.Count > 0) await Task.WhenAll(conditionalTasks);

                    var expressions = new HashSet<string>();
                    foreach (var property in properties)
                        if (property.ItemExpressions is { } itemExpressions)
                            foreach (var itemExpression in itemExpressions)
                                expressions.Add(itemExpression);
                        else
                            expressions.Add(property.EffectiveExpression);

                    var result = await FetchDataAsync(x.DistributedKeyType,
                        new DistributedMapRequest([.. selectorIds], [.. expressions]) { Collection = x.Collection }, requestCt);
                    return (Info: x, Response: result);
                });
                var fetchedResult = await Task.WhenAll(tasks);
                // Each fetch is mapped on its own: two requests for the same key (different order/limit of the rows)
                // must not see each other's rows.
                foreach (var (info, response) in fetchedResult)
                    MapResponseData(info.Properties, [(info.DistributedKeyType, response)]);
            }

            var nextMappableData = allPropertyDatas
                .Where(a => !a.PropertyInfo.PropertyType.IsPrimitiveType())
                .Aggregate(new List<object>(), (acc, next) =>
                {
                    var propertyValue = next.Accessor.Get(next.Model);
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
        string[] selectorIds = [.. new HashSet<string>(query.SelectorIds.Where(a => a is not null))];
        string[] expressions = [.. new HashSet<string>(query.Expressions)];
        var result = await sendPipelineWrapped
            .ExecuteAsync(new DistributedMapRequest(selectorIds, expressions) { Collection = query.Collection }, context);
        return result;
    }


    /// <summary>
    /// Finds every property that has a mapping rule in the object graph below <paramref name="rootObject"/>.
    /// Iterative (explicit stack) so the depth of the graph is not limited by the call stack and no iterator state
    /// machine is allocated per object. Each object is processed once, whatever the number of paths leading to it.
    /// </summary>
    private List<PropertyDescriptor> DiscoverResolvableProperties(object rootObject)
    {
        var descriptors = new List<PropertyDescriptor>();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var getProfileConfig = serviceProvider.GetRequiredService<GetProfileConfig>();
        var pending = new Stack<object>();
        PushIfWalkable(pending, rootObject);

        while (pending.TryPop(out var current))
        {
            if (current is IEnumerable enumerable)
            {
                // A runtime byte[] / List<string> / ... behind an `object` or interface property: nothing inside.
                if (!PropertyClassifier.ShouldWalk(current.GetType())) continue;
                foreach (var item in enumerable is IDictionary dictionary ? dictionary.Values : enumerable)
                    PushIfWalkable(pending, item);
                continue;
            }

            if (!visited.Add(current)) continue;

            var profileConfig = getProfileConfig.Invoke(current.GetType());
            if (profileConfig is null) continue;
            var plan = PlanOf(profileConfig);

            foreach (var entry in plan.RuleEntries)
                descriptors.Add(new PropertyDescriptor(entry.Property, current, entry.Information, entry.Accessor));

            foreach (var entry in plan.WalkEntries)
                PushIfWalkable(pending, entry.Accessor.Get(current));
        }

        return descriptors;
    }

    private static void PushIfWalkable(Stack<object> pending, object value)
    {
        if (!value.IsNullOrPrimitive()) pending.Push(value);
    }

    private static ProfilePlan PlanOf(IFluentProfileConfig profileConfig) =>
        (profileConfig as IProfilePlanSource)?.Plan ?? BuildPlanFor(profileConfig);

    // Only for profiles that do not come from ProfileOf<T> (they cannot cache a plan themselves).
    private static ProfilePlan BuildPlanFor(IFluentProfileConfig profileConfig)
    {
        var entries = profileConfig.Accessors
            .Select(kv =>
            {
                var information = profileConfig.GetInformation(kv.Key);
                return new ProfileEntry(kv.Key, kv.Value, information, information.RequiredAccessor is not null);
            })
            .ToArray();
        return new ProfilePlan([.. entries.Where(e => e.IsRule)],
            [.. entries.Where(e => !e.IsRule && PropertyClassifier.ShouldWalk(e.Property.PropertyType))]);
    }

    private static async Task ResolveConditionalAsync(PropertyDescriptor property, IServiceProvider serviceProvider,
        CancellationToken token) => property.EffectiveExpression =
        await property.Property.ResolveExpression(serviceProvider, token);

    private static async Task ResolveItemExpressionAsync(PropertyDescriptor property, int index,
        ConditionalExpression conditional, IServiceProvider serviceProvider, CancellationToken token) =>
        property.ItemExpressions[index] = await conditional.ResolveAsync(serviceProvider, token);

    // To use merge-expression, we have to group by distributed key only, exclude expression as the older version!
    private static IEnumerable<DistributedKeyInfo> GetDistributedKeyInfos
        (IEnumerable<PropertyDescriptor> propertyDescriptors, IEnumerable<Type> distributedKeyTypes) =>
        propertyDescriptors
            // Rules whose rows must be cut differently (order, limit) cannot share a query with the others.
            .GroupBy(mdp => (DistributedKeyType: mdp.Property?.RuntimeDistributedKeyType,
                Order: mdp.Property?.Order ?? 0,
                Options: mdp.Property?.CollectionPlan?.Options?.Signature ?? ""))
            .Join(distributedKeyTypes, gr => gr.Key.DistributedKeyType, at => at,
                (d, x) => new DistributedKeyInfo(x, d, d.Key.Order, d.First().Property.CollectionPlan?.Options));

    private void MapResponseData(IEnumerable<PropertyDescriptor> mappableProperties,
        IEnumerable<(Type DistributedKeyType, ItemsResponse<DataResponse> ItemsResponse)> dataFetched)
    {
        // The rows of one selector value, in the order they arrived. A unique key has one row; a key that matches
        // several rows has one DataResponse per row, all with the same id.
        var rowsByKeyAndId = new Dictionary<(Type, string), RowSet>();
        foreach (var (distributedKeyType, itemsResponse) in dataFetched)
            foreach (var item in itemsResponse.Items)
                CollectionsMarshal.GetValueRefOrAddDefault(rowsByKeyAndId, (distributedKeyType, item.Id), out _)
                    .Add(item.Values);

        foreach (var property in mappableProperties)
        {
            if (property.Property?.RuntimeDistributedKeyType is not { } keyType) continue;
            if (property.Property.RequiredAccessor?.Get(property.Model)?.ToString() is not { } selectorId) continue;
            if (!rowsByKeyAndId.TryGetValue((keyType, selectorId), out var rows)) continue;

            if (property.Property.CollectionPlan is { } plan) MapCollection(property, plan, rows);
            // A plain rule on a key that matches several rows takes the first row.
            else MapValue(property, rows.First);
        }
    }

    private void MapValue(PropertyDescriptor property, ValueResponse[] row)
    {
        var value = row.FirstOrDefault(a => a.Expression == property.EffectiveExpression)?.Value;
        if (value is null) return;
        SetValue(property.Accessor, property.Model, value, property.PropertyInfo.PropertyType);
    }

    // One element per row (up to the limit of the rule), each filled by the item rules from its own row.
    private void MapCollection(PropertyDescriptor property, CollectionPlan plan, RowSet rows)
    {
        var list = plan.CreateList();
        var count = plan.Limit is { } limit ? Math.Min(limit, rows.Count) : rows.Count;
        for (var r = 0; r < count; r++)
        {
            var item = plan.CreateItem();
            for (var i = 0; i < plan.Rules.Length; i++)
            {
                var expression = property.ItemExpressions[i];
                var value = rows[r].FirstOrDefault(a => a.Expression == expression)?.Value;
                if (value is null) continue;
                var rule = plan.Rules[i];
                SetValue(rule.Accessor, item, value, rule.PropertyType);
            }

            list.Add(item);
        }

        property.Accessor.Set(property.Model, plan.ToContainer(list));
    }

    // The rows of one selector value. Almost every key has a single row, so the first one is kept inline and
    // nothing is allocated unless a key matches several rows.
    private struct RowSet
    {
        public ValueResponse[] First { get; private set; }
        private List<ValueResponse[]> _more;

        public int Count { get; private set; }

        public ValueResponse[] this[int index] => index == 0 ? First : _more[index - 1];

        public void Add(ValueResponse[] row)
        {
            if (Count == 0) First = row;
            else (_more ??= []).Add(row);
            Count++;
        }
    }

    private void SetValue(IPropertyAccessor accessor, object instance, string json, Type propertyType)
    {
        try
        {
            accessor.Set(instance, JsonSerializer.DeserializeObject(json, propertyType));
        }
        catch (Exception)
        {
            var fxMapConfiguration = serviceProvider.GetRequiredService<IMapperConfiguration>();
            if (fxMapConfiguration.ThrowIfExceptions) throw;
        }
    }
}
