using FxMap.Delegates;
using FxMap.Extensions;
using FxMap.Helpers;
using FxMap.HotChocolate.Implementations;
using FxMap.HotChocolate.Resolvers;
using HotChocolate.Execution.Configuration;
using HotChocolate.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace FxMap.HotChocolate.Registries;

public interface IHotChocolateConfigurator
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
    void AddRequestExecutorBuilder(IRequestExecutorBuilder builder);
}
