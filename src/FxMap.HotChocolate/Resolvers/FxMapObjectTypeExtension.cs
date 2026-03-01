using FxMap.Extensions;
using FxMap.HotChocolate.Constants;
using FxMap.HotChocolate.GraphQlContext;
using FxMap.MetadataCache;

namespace FxMap.HotChocolate.Resolvers;

/// <summary>
/// HotChocolate type extension that configures FxMap-mapped fields for automatic resolution.
/// </summary>
/// <typeparam name="T">The GraphQL object type being extended.</typeparam>
/// <remarks>
/// This extension automatically:
/// <list type="bullet">
///   <item><description>Discovers properties configured via FxMap profiles</description></item>
///   <item><description>Adds middleware to extract expression parameters</description></item>
///   <item><description>Configures resolvers to use the <see cref="DataResolvers{TResponse}"/></description></item>
/// </list>
/// </remarks>
internal class FxMapObjectTypeExtension<T> : ObjectTypeExtension<T> where T : class
{
    protected override void Configure(IObjectTypeDescriptor<T> descriptor)
    {
        var profileConfig = FluentConfigStore.ProfileConfigs.GetValueOrDefault(typeof(T));
        profileConfig?.DependencyGraphs
            .SelectMany(a => a.Value)
            .Select(x => new
            {
                x.TargetPropertyInfo, x.RequiredPropertyInfo,
                RuntimeDistributedData = new { x.Expression, PropertyName = x.SelectorPropertyName },
                x.RuntimeDistributedKeyType
            })
            .ForEach(data => descriptor.Field(data!.TargetPropertyInfo)
                .Use(next => async context =>
                {
                    var methodPath = context.Path.ToList().FirstOrDefault()?.ToString();

                    var distributedData = data.RuntimeDistributedData;

                    var dependencyGraphs = profileConfig.DependencyGraphs;

                    // Check and resolve for dependencies too, just not for selectors only!
                    var props = profileConfig.GetInformation(data.TargetPropertyInfo);
                    var expression = await props.ResolveExpression(context.Services, context.RequestAborted);

                    var ctx = new FieldContext
                    {
                        TargetPropertyInfo = data.TargetPropertyInfo,
                        Expression = expression,
                        RuntimeDistributedKeyType = data.RuntimeDistributedKeyType,
                        SelectorPropertyName = distributedData.PropertyName,
                        RequiredPropertyInfo = data.RequiredPropertyInfo,
                        Order = dependencyGraphs.GetPropertyOrder(data.TargetPropertyInfo)
                    };

                    context.ContextData[GraphQlConstants.GetContextFieldContextHeader(methodPath)] = ctx;

                    await next(context);
                })
                .ResolveWith<DataResolvers<T>>(x => x.GetDataAsync(null!, null!))
                .Type(data.TargetPropertyInfo.PropertyType));
    }
}