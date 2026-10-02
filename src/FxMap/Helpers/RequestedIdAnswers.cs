using System.Collections;
using FxMap.Abstractions;
using FxMap.Responses;

namespace FxMap.Helpers;

/// <summary>
/// Lets a data provider answer to the ids the way the caller wrote them.
/// </summary>
/// <remarks>
/// A provider returns rows keyed by the canonical text of the entity id (<c>Guid.ToString()</c> is lower case with
/// hyphens, an <c>int</c> has no leading zeros, an <c>ObjectId</c> is lower case hex...), while the caller matches a
/// response to its object by comparing the text it sent. An id that the <see cref="IIdConverter"/> accepts in another
/// spelling (upper case or braced Guid, <c>"007"</c> for 7, surrounding spaces) is found by the query, but its row
/// would never be matched back. <see cref="Align"/> returns one answer per requested spelling.
/// </remarks>
public static class RequestedIdAnswers
{
    /// <summary>
    /// Returns the rows re-keyed so that every requested id text that resolves to a row gets an answer under that
    /// exact text. Rows nobody asked for are dropped; texts that do not resolve to a row get no answer.
    /// </summary>
    /// <param name="requestedIds">The id texts of the request (nulls are ignored).</param>
    /// <param name="rows">The rows returned by the provider, keyed by the canonical id text.</param>
    /// <param name="idConverter">The converter used to parse the requested ids for the query.</param>
    public static DataResponse[] Align(IEnumerable<string> requestedIds, DataResponse[] rows,
        IIdConverter idConverter)
    {
        if (rows.Length == 0) return rows;
        var requested = new HashSet<string>(requestedIds.Where(id => id is not null));
        // Common case: every requested text is itself the canonical id of a row, nothing to translate.
        if (requested.Count == rows.Length && rows.All(r => requested.Contains(r.Id))) return rows;

        var byCanonicalId = new Dictionary<string, DataResponse>(rows.Length);
        foreach (var row in rows) byCanonicalId.TryAdd(row.Id, row);

        var answers = new List<DataResponse>(requested.Count);
        foreach (var text in requested)
        {
            if (byCanonicalId.TryGetValue(text, out var exact))
            {
                answers.Add(exact);
                continue;
            }

            var parsed = (idConverter.ConvertIds([text]) as IEnumerable)?.Cast<object>().FirstOrDefault();
            if (parsed?.ToString() is { } canonical && byCanonicalId.TryGetValue(canonical, out var row))
                answers.Add(new DataResponse { Id = text, Values = row.Values });
        }

        return [..answers];
    }
}
