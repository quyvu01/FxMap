using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace FxMap.Helpers;

/// <summary>What the object walker has to do with a property, decided from its declared type only.</summary>
internal enum TypeKind
{
    /// <summary>Primitive, string, enum or any value type: the value can never hold an object to map.</summary>
    Leaf,

    /// <summary>Array / list / set / dictionary whose items (or values) are all leaves, e.g. <c>byte[]</c>, <c>List&lt;string&gt;</c>.</summary>
    LeafCollection,

    /// <summary>A framework type that never holds a mappable model (<c>Uri</c>, <c>Type</c>, <c>Stream</c>...).</summary>
    Opaque,

    /// <summary>May hold objects that have to be walked: models, interfaces, <c>object</c>, collections of models...</summary>
    Walkable
}

/// <summary>
/// Single place that decides whether the walker should look inside a type. Everything that is not provably
/// useless to walk is <see cref="TypeKind.Walkable"/>, so a wrong answer can only cost time, never skip a model.
/// </summary>
internal static class PropertyClassifier
{
    private static readonly ConcurrentDictionary<Type, TypeKind> Cache = new();

    // Closed list on purpose: a declared type assignable to one of these can never contain a user model.
    private static readonly Type[] OpaqueBaseTypes =
    [
        typeof(Uri), typeof(Version), typeof(MemberInfo), typeof(Assembly), typeof(Module), typeof(Delegate),
        typeof(Stream), typeof(CultureInfo), typeof(Encoding)
    ];

    internal static TypeKind Classify(Type type) => Cache.GetOrAdd(type, static t => Compute(t, 0));

    /// <summary>True when values of the declared type may contain objects the walker has to visit.</summary>
    internal static bool ShouldWalk(Type declaredType) => Classify(declaredType) == TypeKind.Walkable;

    private static TypeKind Compute(Type type, int depth)
    {
        if (GeneralHelpers.IsPrimitiveType(type)) return TypeKind.Leaf;
        if (OpaqueBaseTypes.Any(b => b.IsAssignableFrom(type))) return TypeKind.Opaque;
        // Depth guard only protects against pathological self-referencing generic shapes.
        if (depth > 8) return TypeKind.Walkable;

        var itemType = ItemTypeOf(type);
        if (itemType is null) return TypeKind.Walkable;
        var itemKind = Compute(itemType, depth + 1);
        return itemKind is TypeKind.Leaf or TypeKind.LeafCollection or TypeKind.Opaque
            ? TypeKind.LeafCollection
            : TypeKind.Walkable;
    }

    /// <summary>
    /// The type of what the walker would visit inside a collection: array element, dictionary value, or the single
    /// <c>IEnumerable&lt;T&gt;</c> item. Null when the type is not such a collection (or the item type is ambiguous).
    /// </summary>
    private static Type ItemTypeOf(Type type)
    {
        if (type.IsArray) return type.GetElementType();
        if (type == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(type)) return null;

        var interfaces = type.IsInterface ? [type, ..type.GetInterfaces()] : type.GetInterfaces();

        var dictionaryValues = interfaces
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() is var d &&
                        (d == typeof(IDictionary<,>) || d == typeof(IReadOnlyDictionary<,>)))
            .Select(i => i.GetGenericArguments()[1])
            .Distinct()
            .ToArray();
        if (dictionaryValues.Length == 1) return dictionaryValues[0];
        if (dictionaryValues.Length > 1) return null;

        var itemTypes = interfaces
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            .Select(i => i.GetGenericArguments()[0])
            .Distinct()
            .ToArray();
        return itemTypes.Length == 1 ? itemTypes[0] : null;
    }
}
