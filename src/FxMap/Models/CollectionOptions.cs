using FxMap.Fluent.Rules;

namespace FxMap.Models;

/// <summary>
/// How the server should order and cut the rows of each selector value when a request fills a collection
/// (see <c>Collection(...)</c> in a profile). Travels with the request; a server that does not know it ignores it.
/// </summary>
/// <param name="OrderBy">Sort keys, by priority, named after properties of the entity that owns the data.</param>
/// <param name="Limit">Maximum number of rows kept for each selector value, or null for no limit.</param>
public sealed record CollectionOptions(CollectionOrder[] OrderBy, int? Limit)
{
    /// <summary>True when there is nothing to apply.</summary>
    public bool IsEmpty => OrderBy is not { Length: > 0 } && Limit is null;

    /// <summary>A text that is equal for equal options, used to group requests that can share one query.</summary>
    public string Signature => string.Join(',', (OrderBy ?? []).Select(o => $"{o.PropertyName}:{(o.Descending ? "d" : "a")}"))
                               + "|" + Limit;
}
