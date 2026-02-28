using System.Collections.Concurrent;
using FxMap.Accessors.TypeAccessors;

namespace FxMap.MetadataCache;

public static class TypeCaching
{
    private static readonly ConcurrentDictionary<Type, ITypeAccessor> TypesLookup = new();

    public static ITypeAccessor GetTypeAccessor(Type type) =>
        TypesLookup.GetOrAdd(type, static t => new TypeAccessor(t));
}