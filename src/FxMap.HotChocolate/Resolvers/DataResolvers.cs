using System.Text.Json;
using HotChocolate.Resolvers;
using FxMap.Extensions;
using FxMap.HotChocolate.Constants;
using FxMap.HotChocolate.GraphQlContext;
using FxMap.HotChocolate.Implementations;
using FxMap.HotChocolate.Registries;
using FxMap.MetadataCache;

namespace FxMap.HotChocolate.Resolvers;

/// <summary>
/// GraphQL resolver class that handles data fetching for FxMap-decorated properties.
/// </summary>
/// <typeparam name="TResponse">The GraphQL object type being resolved.</typeparam>
/// <remarks>
/// This resolver uses the <see cref="DataMappingLoader"/> to batch and cache data fetching,
/// ensuring efficient N+1 query resolution in GraphQL.
/// </remarks>
public sealed class DataResolvers<TResponse> where TResponse : class
{
    /// <summary>
    /// Resolves FxMap-decorated field data asynchronously.
    /// </summary>
    /// <param name="response">The parent object being resolved.</param>
    /// <param name="resolverContext">The HotChocolate resolver context.</param>
    /// <returns>The resolved field value.</returns>
    public async Task<object> GetDataAsync(
        [Parent] TResponse response, IResolverContext resolverContext)
    {
        var dataMappingLoader = resolverContext.Resolver<DataMappingLoader>();

        var methodPath = resolverContext.Path.ToList().FirstOrDefault()?.ToString();
        var fieldContextHeader = GraphQlConstants.GetContextFieldContextHeader(methodPath);

        if (!resolverContext.ContextData
                .TryGetValue(fieldContextHeader, out var ctx) ||
            ctx is not FieldContext currentContext || currentContext.TargetPropertyInfo is null)
            throw new InvalidOperationException($"{nameof(FieldContext)} must be added with key: {fieldContextHeader}");

        var obj = FluentConfigStore.ProfileConfigs.GetValueOrDefault(typeof(TResponse));

        List<Task<string>> allTasks =
            [FieldResultAsync(currentContext), ..GetDependencyTasks(currentContext, FieldResultAsync)];

        await Task.WhenAll(allTasks);
        var data = allTasks.First().Result;
        try
        {
            if (data is null) return null;
            var result = JsonSerializer.Deserialize(data, currentContext.TargetPropertyInfo.PropertyType);
            return result;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Could not deserialize value to {currentContext.TargetPropertyInfo.PropertyType.FullName}.", ex);
        }

        async Task<string> FieldResultAsync(FieldContext fieldContext)
        {
            var selectorId = obj?
                .Accessors.GetValueOrDefault(fieldContext.RequiredPropertyInfo)
                .Get(response)?.ToString();
            // Fetch the dependency fields
            var fieldBearing = new FieldBearing(response, fieldContext.Expression, fieldContext.Order,
                fieldContext.RuntimeDistributedKeyType, fieldContext.TargetPropertyInfo,
                fieldContext.RequiredPropertyInfo)
            {
                SelectorId = selectorId,
                Expression = fieldContext.Expression
            };

            var fieldResult = await dataMappingLoader
                .LoadAsync(fieldBearing, resolverContext.RequestAborted);
            return fieldResult;
        }
    }

    private static Task<string>[] GetDependencyTasks(FieldContext currentContext,
        Func<FieldContext, Task<string>> fieldResultTask)
    {
        var obj = FluentConfigStore.ProfileConfigs.GetValueOrDefault(typeof(TResponse));
        if (obj is null) return [];
        var dependenciesGraph = obj.DependencyGraphs;
        if (!dependenciesGraph.TryGetValue(currentContext.TargetPropertyInfo, out var infos)) return [];
        return
        [
            ..infos.Select(p =>
            {
                var fieldContext = new FieldContext
                {
                    TargetPropertyInfo = p.TargetPropertyInfo, Expression = p.Expression,
                    SelectorPropertyName = p.SelectorPropertyName, RequiredPropertyInfo = p.RequiredPropertyInfo,
                    RuntimeDistributedKeyType = p.RuntimeDistributedKeyType,
                    Order = dependenciesGraph.GetPropertyOrder(p.TargetPropertyInfo)
                };
                return fieldResultTask.Invoke(fieldContext);
            })
        ];
    }
}