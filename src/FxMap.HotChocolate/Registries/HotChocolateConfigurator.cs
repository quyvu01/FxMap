using FxMap.Delegates;
using FxMap.Extensions;
using FxMap.Helpers;
using FxMap.HotChocolate.Implementations;
using FxMap.HotChocolate.Resolvers;
using HotChocolate.Execution;
using HotChocolate.Execution.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FxMap.HotChocolate.Registries;

/// <summary>
/// Configuration class for registering HotChocolate GraphQL integration with the FxMap framework.
/// </summary>
/// <remarks>
/// This registrar automatically discovers types with FxMap profile configurations and creates
/// GraphQL resolvers and type extensions for them.
/// </remarks>
public sealed class HotChocolateConfigurator
{
    /// <summary>
    /// Configures the HotChocolate request executor builder with FxMap integration.
    /// </summary>
    /// <param name="builder">The HotChocolate request executor builder.</param>
    /// <remarks>
    /// This method:
    /// <list type="bullet">
    ///   <item><description>Registers the <see cref="DataMappingLoader"/> DataLoader for batched data fetching</description></item>
    ///   <item><description>Adds parameter middleware for expression parameter resolution</description></item>
    ///   <item><description>Automatically creates type extensions for types with FxMap profile configurations</description></item>
    /// </list>
    /// </remarks>
    public void AddRequestExecutorBuilder(IRequestExecutorBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddDataLoader<DataMappingLoader>();

        // Note: BuildSchemaAsync().Result is used here because HotChocolate's builder API
        // requires synchronous registration during startup. This is a known limitation.
        // The call happens during app startup, not during request processing, so deadlock
        // risk is minimal in typical ASP.NET Core hosting scenarios.
        var schema = builder.BuildSchemaAsync().GetAwaiter().GetResult();
        var types = schema.Types;
        var getProfileConfig = schema.Services.GetRequiredService<GetProfileConfig>();
        types.ForEach(a =>
        {
            var dataType = a.GetType();
            if (!dataType.IsGenericType) return;
            var genericType = dataType.GetGenericTypeDefinition();
            if (genericType != typeof(ObjectType<>)) return;
            var objectType = dataType.GetGenericArguments().FirstOrDefault();
            if (objectType is null) return;
            if (!objectType.IsClass || objectType.IsAbstract || GeneralHelpers.IsPrimitiveType(objectType)) return;
            var profileConfig = getProfileConfig.Invoke(objectType);
            if (profileConfig?.DependencyGraphs is not { Count: > 0 }) return;
            builder
                .AddType(typeof(FxMapObjectTypeExtension<>).MakeGenericType(objectType))
                .AddResolver(typeof(DataResolvers<>).MakeGenericType(objectType));
        });
    }
}