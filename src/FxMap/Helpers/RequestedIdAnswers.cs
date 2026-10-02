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
    /// Returns the rows re-keyed so that every requested id text that resolves to rows gets those rows under that
    /// exact text. A key that matches several rows (non-unique lookup) keeps all of them, in their original order.
    /// Rows nobody asked for are dropped; texts that do not resolve to a row get no answer.
    /// </summary>
    /// <param name="requestedIds">The id texts of the request (nulls are ignored).</param>
    /// <param name="rows">The rows returned by the provider, keyed by the canonical id text.</param>
    /// <param name="idConverter">The converter used to parse the requested ids for the query.</param>
    public static DataResponse[] Align(IEnumerable<string> requestedIds, DataResponse[] rows,
        IIdConverter idConverter)
    {
        if (rows.Length == 0) return rows;
        var requested = new HashSet<string>(requestedIds.Where(id => id is not null));

        var rowsByCanonicalId = new Dictionary<string, List<DataResponse>>();
        foreach (var row in rows)
        {
            if (!rowsByCanonicalId.TryGetValue(row.Id, out var group)) rowsByCanonicalId[row.Id] = group = [];
            group.Add(row);
        }

        // Common case: every requested text is itself the canonical id of some rows, nothing to translate.
        if (requested.Count == rowsByCanonicalId.Count && rowsByCanonicalId.Keys.All(requested.Contains)) return rows;

        var answers = new List<DataResponse>(rows.Length);
        foreach (var text in requested)
        {
            if (rowsByCanonicalId.TryGetValue(text, out var exact))
            {
                answers.AddRange(exact);
                continue;
            }

            var parsed = (idConverter.ConvertIds([text]) as IEnumerable)?.Cast<object>().FirstOrDefault();
            if (parsed?.ToString() is not { } canonical || !rowsByCanonicalId.TryGetValue(canonical, out var group))
                continue;
            answers.AddRange(group.Select(row => new DataResponse { Id = text, Values = row.Values }));
        }

        return [..answers];
    }
}
