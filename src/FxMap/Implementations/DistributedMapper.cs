using System.Collections;
using System.Collections.Concurrent;
using FxMap.Abstractions;
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

    public async Task MapDataAsync(object value, CancellationToken token = default) =>
        await MapDataAsync(value, null, token);

    public async Task MapDataAsync(object value, IContext context, CancellationToken token = default)
    {
        // Cancelling either the caller's token or the token of the context cancels the requests; the headers of the
        // context travel with them. Without a context this is the caller's token and no headers.
        using var linked = context is { CancellationToken.CanBeCanceled: true } && token.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(token, context.CancellationToken)
            : null;
        token = linked?.Token ?? (context is not null && !token.CanBeCanceled ? context.CancellationToken : token);
        var requestContext = new RequestContext(context?.Headers ?? [], token);

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
                    var properties = x.Properties.ToList();
                    if (properties is not { Count: > 0 }) return emptyResponse;

                    // Collect each distinct id once instead of one entry per object: many objects share few ids.
                    var selectorIds = new HashSet<string>();
                    foreach (var property in properties)
                        if (property.Property.RequiredAccessor?.Get(property.Model)?.ToString() is { } id)
                            selectorIds.Add(id);

                    // Resolve conditional expressions and store on PropertyDescriptor (request-scoped).
                    // Plain expressions need no resolution, so no task is created for them.
                    var conditionalTasks = new List<Task>();
                    foreach (var property in properties)
                    {
                        if (property.Property.ConditionalExpression is null)
                        {
                            property.EffectiveExpression = property.Property.Expression;
                            continue;
                        }

                        conditionalTasks.Add(ResolveConditionalAsync(property, serviceProvider, token));
                    }

                    if (conditionalTasks.Count > 0) await Task.WhenAll(conditionalTasks);

                    var expressions = new HashSet<string>(properties.Select(p => p.EffectiveExpression));

                    var result = await FetchDataAsync(x.DistributedKeyType,
                        new DistributedMapRequest([.. selectorIds], [.. expressions]), requestContext);
                    return (x.DistributedKeyType, Response: result);
                });
                var fetchedResult = await Task.WhenAll(tasks);
                MapResponseData(orderedProperties, fetchedResult);
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
            .ExecuteAsync(new DistributedMapRequest(selectorIds, expressions), context);
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

    // To use merge-expression, we have to group by distributed key only, exclude expression as the older version!
    private static IEnumerable<DistributedKeyInfo> GetDistributedKeyInfos
        (IEnumerable<PropertyDescriptor> propertyDescriptors, IEnumerable<Type> distributedKeyTypes) =>
        propertyDescriptors
            .GroupBy(mdp => (DistributedKeyType: mdp.Property?.RuntimeDistributedKeyType,
                Order: mdp.Property?.Order ?? 0))
            .Join(distributedKeyTypes, gr => gr.Key.DistributedKeyType, at => at,
                (d, x) => new DistributedKeyInfo(x, d, d.Key.Order));

    private void MapResponseData(IEnumerable<PropertyDescriptor> mappableProperties,
        IEnumerable<(Type DistributedKeyType, ItemsResponse<DataResponse> ItemsResponse)> dataFetched)
    {
        var dataWithExpression = dataFetched
            .Select(a => a.ItemsResponse.Items
                .Select(x => (x.Id, x.Values))
                .Select(k => (a.DistributedKeyType, Data: k)))
            .SelectMany(x => x);
        // The keys are read before any value is written: a rule may fill a property that the key of another rule reads
        // (Of(x => x.Id + x.Email) and For(x => x.Email, ...)), and the join is lazy.
        var keyed = mappableProperties
            .Select(ap => (Property: ap, Key: (ap.Property?.RuntimeDistributedKeyType, ap.Property?.RequiredAccessor?
                .Get(ap.Model)?.ToString())))
            .ToList();
        keyed.Join(dataWithExpression, k => k.Key,
            dt => (dt.DistributedKeyType, dt.Data.Id), (k, dt) =>
            {
                var ap = k.Property;
                var value = dt.Data
                    .Values
                    .FirstOrDefault(a => a.Expression == ap.EffectiveExpression)?.Value;
                if (value is null || ap.PropertyInfo is not { } propertyInfo) return value;
                try
                {
                    var valueSet = JsonSerializer.DeserializeObject(value, propertyInfo.PropertyType);
                    ap.Accessor.Set(ap.Model, valueSet);
                }
                catch (Exception)
                {
                    var mapperConfiguration = serviceProvider.GetRequiredService<IMapperConfiguration>();
                    if (mapperConfiguration.ThrowIfExceptions) throw;
                }

                return value;
            }).Evaluate();
    }
}