using FxMap.Responses;

namespace FxMap.Helpers;

/// <summary>Cuts the rows of each selector value, for requests that fill a collection with a limit.</summary>
public static class RowLimiter
{
    /// <summary>
    /// Keeps at most <paramref name="limit"/> rows for each id, the first ones in the order they are in
    /// <paramref name="rows"/> (so sort the rows first). Rows of other ids are not affected, and the order of the
    /// rows is preserved. With no limit the same array is returned.
    /// </summary>
    public static DataResponse[] LimitPerId(DataResponse[] rows, int? limit)
    {
        if (limit is not { } max || rows.Length == 0) return rows;
        var kept = new Dictionary<string, int>();
        var result = new List<DataResponse>(rows.Length);
        foreach (var row in rows)
        {
            kept.TryGetValue(row.Id, out var count);
            if (count >= max) continue;
            kept[row.Id] = count + 1;
            result.Add(row);
        }

        return result.Count == rows.Length ? rows : [..result];
    }
}
