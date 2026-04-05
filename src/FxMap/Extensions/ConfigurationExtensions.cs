using System.Collections.Concurrent;
using FxMap.Abstractions;
using FxMap.Exceptions;
using FxMap.Models;

namespace FxMap.Extensions;

public static class ConfigurationExtensions
{
    private static readonly ConcurrentDictionary<string, DistributedTypeData> DistributedKeyAssemblyMapHandlers = [];

    public static DistributedTypeData GetDistributedTypeData(this IMapperConfiguration configuration,
        string typeAssembly)
    {
        return DistributedKeyAssemblyMapHandlers
            .GetOrAdd(typeAssembly, ta =>
            {
                var exited = configuration.DistributedKeyMapHandlers
                    .FirstOrDefault(a => a.Key.AssemblyQualifiedName == ta);
                return exited is { Key: { } key, Value: { } value }
                    ? new DistributedTypeData(key, value)
                    : throw new DistributedMapException.NoHandlerForDistributedKeyAssemblyType(ta);
            });
    }
}