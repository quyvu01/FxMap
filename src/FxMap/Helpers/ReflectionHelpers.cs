using System.Collections;
using FxMap.Models;
using FxMap.Extensions;
using FxMap.Responses;
using FxMap.Serializable;
using FxMap.Configuration;
using FxMap.MetadataCache;

namespace FxMap.Helpers;

/// <summary>
/// Provides reflection-based helper methods for discovering and mapping FxMap-configured properties.
/// </summary>
/// <remarks>
/// This class handles the core reflection logic for:
/// <list type="bullet">
///   <item><description>Discovering properties mapped via <c>ProfileOf&lt;T&gt;</c> on objects</description></item>
///   <item><description>Grouping properties by distributed key type and execution order</description></item>
///   <item><description>Mapping response data back to object properties</description></item>
/// </list>
/// </remarks>
internal static class ReflectionHelpers
{
    internal static IEnumerable<PropertyDescriptor> DiscoverResolvableProperties(object rootObject)
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        return GetResolvablePropertiesRecursive(rootObject, visited);
    }

    private static IEnumerable<PropertyDescriptor> GetResolvablePropertiesRecursive(object obj, HashSet<object> visited)
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
        var profileConfig = FluentConfigStore.ProfileConfigs.GetValueOrDefault(objType);
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

    // To use merge-expression, we have to group by attribute only, exclude expression as the older version!
    internal static IEnumerable<DistributedKeyInfo> GetFxMapTypesData
        (IEnumerable<PropertyDescriptor> mappableDataProperties, IEnumerable<Type> attributeTypes) =>
        mappableDataProperties
            .GroupBy(mdp => (AttributeType: mdp.PropertyInformation?.RuntimeAttributeType,
                Order: mdp.PropertyInformation?.Order ?? 0))
            .Join(attributeTypes, gr => gr.Key.AttributeType, at => at,
                (d, x) => new DistributedKeyInfo(x, d
                    .Select(a => new PropertyMappingData(a)), d.Key.Order));

    internal static void MapResponseData(IEnumerable<PropertyDescriptor> mappableProperties,
        IEnumerable<(Type DistributedKeyType, ItemsResponse<DataResponse> ItemsResponse)> dataFetched)
    {
        var dataWithExpression = dataFetched
            .Select(a => a.ItemsResponse.Items
                .Select(x => (x.Id, FxMapValues: x.Values))
                .Select(k => (a.DistributedKeyType, Data: k)))
            .SelectMany(x => x);
        mappableProperties.Join(dataWithExpression, ap => (ap.PropertyInformation?.RuntimeAttributeType, ap
                .PropertyInformation?
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
                    var profileConfig = FluentConfigStore.ProfileConfigs.GetValueOrDefault(ap.Model.GetType());
                    var propertyAccessor = profileConfig?.Accessors?.GetValueOrDefault(ap.PropertyInfo);
                    propertyAccessor?.Set(ap.Model, valueSet);
                }
                catch (Exception)
                {
                    if (FxMapStatics.ThrowIfExceptions) throw;
                }

                return value;
            }).Evaluate();
    }
}