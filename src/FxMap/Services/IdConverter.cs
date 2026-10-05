#nullable enable
using Microsoft.Extensions.DependencyInjection;
using FxMap.Abstractions;
using FxMap.Exceptions;

namespace FxMap.Services;

/// <summary>
/// Abstract base class providing common ID conversion functionality for the FxMap framework.
/// </summary>
/// <remarks>
/// This class contains a dictionary of built-in converters for primitive types (string, Guid, int, long, etc.)
/// and provides methods for parsing strongly-typed IDs. The converters transform string arrays
/// into typed arrays suitable for database queries.
/// </remarks>
internal abstract class AbstractIdConverter
{
    // Each parser returns a boxed List<T> of its own type, which IdConverter<TId> casts back to List<TId>.
    protected static readonly Dictionary<Type, Func<string[], object>> IdConverters = new()
    {
        { typeof(string), ids => new List<string>(ids) },
        { typeof(Guid), ParseIdsToList<Guid> },
        { typeof(Guid?), ParseNullableIdsToList<Guid> },
        { typeof(int), ParseIdsToList<int> },
        { typeof(int?), ParseNullableIdsToList<int> },
        { typeof(long), ParseIdsToList<long> },
        { typeof(long?), ParseNullableIdsToList<long> },
        { typeof(ulong), ParseIdsToList<ulong> },
        { typeof(ulong?), ParseNullableIdsToList<ulong> },
        { typeof(short), ParseIdsToList<short> },
        { typeof(short?), ParseNullableIdsToList<short> },
        { typeof(ushort), ParseIdsToList<ushort> },
        { typeof(ushort?), ParseNullableIdsToList<ushort> },
        { typeof(float), ParseIdsToList<float> },
        { typeof(float?), ParseNullableIdsToList<float> },
        { typeof(double), ParseIdsToList<double> },
        { typeof(double?), ParseNullableIdsToList<double> },
        { typeof(decimal), ParseIdsToList<decimal> },
        { typeof(decimal?), ParseNullableIdsToList<decimal> },
        { typeof(sbyte), ParseIdsToList<sbyte> },
        { typeof(sbyte?), ParseNullableIdsToList<sbyte> },
        { typeof(uint), ParseIdsToList<uint> },
        { typeof(uint?), ParseNullableIdsToList<uint> },
        { typeof(byte), ParseIdsToList<byte> },
        { typeof(byte?), ParseNullableIdsToList<byte> }
    };

    private static object ParseIdsToList<T>(string[] selectorIds) where T : IParsable<T>
    {
        var ids = new List<T>(selectorIds?.Length ?? 0);
        if (selectorIds is null) return ids;
        foreach (var id in selectorIds)
            if (T.TryParse(id, null, out var parsed))
                ids.Add(parsed);
        return ids;
    }

    private static object ParseNullableIdsToList<T>(string[] selectorIds) where T : struct, IParsable<T>
    {
        var ids = new List<T?>(selectorIds?.Length ?? 0);
        if (selectorIds is null) return ids;
        foreach (var id in selectorIds)
            if (T.TryParse(id, null, out var parsed))
                ids.Add(parsed);
        return ids;
    }

    protected static List<TId> ParseStronglyTypeIds<TId>(IServiceProvider serviceProvider, string[] selectorIds)
    {
        var stronglyTypeService = serviceProvider.GetService<IStronglyTypeConverter<TId>>();
        if (stronglyTypeService is null) throw new DistributedMapException.CurrentIdTypeWasNotSupported();
        return
        [
            .. selectorIds
                .Where(stronglyTypeService.CanConvert)
                .Select(stronglyTypeService.Convert)
        ];
    }
}

/// <summary>
/// Converts string-based selector IDs to the strongly-typed ID format required by the data source.
/// </summary>
/// <typeparam name="TId">The target ID type (e.g., Guid, int, long, or a custom strongly-typed ID).</typeparam>
/// <param name="serviceProvider">The service provider for resolving custom ID converters.</param>
/// <remarks>
/// This converter handles both built-in primitive types and custom strongly-typed IDs.
/// For custom ID types, it delegates to <see cref="IStronglyTypeConverter{TId}"/> implementations.
/// </remarks>
internal class IdConverter<TId>(IServiceProvider serviceProvider) : AbstractIdConverter, IIdConverter<TId>
{
    /// <inheritdoc />
    public List<TId> ConvertIds(string[] selectorIds) => IdConverters.TryGetValue(typeof(TId), out var converter)
        ? (List<TId>)converter(selectorIds)
        : ParseStronglyTypeIds<TId>(serviceProvider, selectorIds);
}