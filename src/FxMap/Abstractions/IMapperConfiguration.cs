using FxMap.Models;
using FxMap.Supervision;

namespace FxMap.Abstractions;

public interface IMapperConfiguration
{
    IReadOnlyCollection<Type> DistributedKeyTypes { get; }
    IReadOnlyCollection<EntityInfo> EntityInfos { get; }
    IReadOnlyDictionary<Type, Type> DistributedKeyMapHandlers { get; }
    public int MaxNestingDepth { get; }
    public int MaxConcurrentProcessing { get; }
    public bool ThrowIfExceptions { get; }
    public TimeSpan DefaultRequestTimeout { get; }
    internal RetryPolicy RetryPolicy { get; }
    public SupervisorOptions SupervisorOptions { get; }
}