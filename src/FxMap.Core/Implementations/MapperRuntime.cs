using FxMap.Abstractions;
using FxMap.Delegates;
using Microsoft.Extensions.Logging;

namespace FxMap.Implementations;

/// <summary>
/// What <see cref="DistributedMapper"/> needs while it maps, given to it explicitly: AddFxMap fills it from the
/// container, <see cref="FxMapBuilder"/> from what it was given.
/// </summary>
/// <param name="configuration">The settings of the mapper.</param>
/// <param name="getProfileConfig">The profile of a model type.</param>
/// <param name="senderFor">The send pipeline of a distributed key.</param>
/// <param name="services">What conditional expressions receive; may be null.</param>
/// <param name="logger">Where the expression errors are logged; may be null.</param>
internal sealed class MapperRuntime(
    IMapperConfiguration configuration,
    GetProfileConfig getProfileConfig,
    Func<Type, SendPipelinesOrchestrator> senderFor,
    IServiceProvider services,
    ILogger logger)
{
    public IMapperConfiguration Configuration { get; } = configuration;
    public GetProfileConfig GetProfileConfig { get; } = getProfileConfig;
    public IServiceProvider Services { get; } = services;
    public ILogger Logger { get; } = logger;
    public SendPipelinesOrchestrator SenderFor(Type distributedKeyType) => senderFor(distributedKeyType);
}
