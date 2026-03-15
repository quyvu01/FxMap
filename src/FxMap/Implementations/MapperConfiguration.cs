using FxMap.Abstractions;
using FxMap.Models;
using FxMap.Supervision;

namespace FxMap.Implementations;

internal sealed class MapperConfiguration(
    HashSet<Type> distributedKeyTypes,
    IReadOnlyCollection<EntityInfo> entityInformation,
    IReadOnlyDictionary<Type, Type> distributedKeyMapHandlers,
    int maxNestingDepth,
    int maxConcurrentProcessing,
    bool throwIfExceptions,
    TimeSpan defaultRequestTimeout,
    RetryPolicy retryPolicy,
    SupervisorOptions supervisorOptions)
    : IMapperConfiguration
{
    public IReadOnlyCollection<Type> DistributedKeyTypes { get; } = [..distributedKeyTypes];
    public IReadOnlyCollection<EntityInfo> EntityInfos { get; } = entityInformation;
    public IReadOnlyDictionary<Type, Type> DistributedKeyMapHandlers { get; } = distributedKeyMapHandlers;
    public int MaxNestingDepth { get; } = maxNestingDepth;
    public int MaxConcurrentProcessing { get; } = maxConcurrentProcessing;
    public bool ThrowIfExceptions { get; } = throwIfExceptions;
    public TimeSpan DefaultRequestTimeout { get; } = defaultRequestTimeout;
    public RetryPolicy RetryPolicy { get; } = retryPolicy;
    public SupervisorOptions SupervisorOptions { get; } = supervisorOptions;
}