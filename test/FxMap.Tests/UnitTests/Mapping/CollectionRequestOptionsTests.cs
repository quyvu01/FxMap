using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Mapping;

/// <summary>The order and limit of a collection rule travel with the request, and requests that need different ones do not share a query.</summary>
public class CollectionRequestOptionsTests
{
    // A server that honours the options: rows carry a Date, sorted and cut like a real provider would.
    private static readonly (int Id, string Name, int Date)[] Visits =
        [(1, "v1", 10), (2, "v2", 50), (3, "v3", 30), (4, "v4", 50), (5, "v5", 20)];

    private static IReadOnlyList<Func<string, object>> Honour(string id, FxMap.Models.CollectionOptions options)
    {
        if (id != "M") return [];
        IEnumerable<(int Id, string Name, int Date)> rows = Visits;
        IOrderedEnumerable<(int Id, string Name, int Date)> ordered = null;
        foreach (var o in options?.OrderBy ?? [])
        {
            Func<(int Id, string Name, int Date), object> key = o.PropertyName == "Date" ? v => v.Date : v => v.Id;
            ordered = ordered is null
                ? o.Descending ? rows.OrderByDescending(key) : rows.OrderBy(key)
                : o.Descending ? ordered.ThenByDescending(key) : ordered.ThenBy(key);
        }

        rows = ordered ?? rows;
        if (options?.Limit is { } limit) rows = rows.Take(limit);
        return [..rows.Select(v => (Func<string, object>)(e => e switch
        {
            "Id" => v.Id, "Name" => v.Name, _ => FakeRemote.Absent
        }))];
    }

    [Fact]
    public async Task Rules_with_different_options_get_their_own_request_and_their_own_rows()
    {
        using var h = MappingHarness.Create(remote: r => r.SetupRows<UserKey>(Honour));
        var dto = new RowsOptionsDto { Code = "M" };

        await h.Map(dto);

        dto.Recent.Select(i => i.RowId).ShouldBe([2, 4]);
        dto.Oldest.Select(i => i.RowId).ShouldBe([1]);
        dto.All.Select(i => i.RowId).ShouldBe([1, 2, 3, 4, 5]);
        var options = h.Remote.CallsOf<UserKey>().Select(c => c.Collection?.Signature).OrderBy(x => x).ToList();
        options.ShouldBe([null, "Date:a|1", "Date:d,Id:a|2"]);
    }

    [Fact]
    public async Task Rules_without_options_share_one_request_with_the_plain_rules()
    {
        using var h = MappingHarness.Create(remote: r => r.SetupRows<UserKey>(Honour));
        var dto = new RowsOptionsDto { Code = "M" };

        await h.Map(dto);

        var shared = h.Remote.CallsOf<UserKey>().Single(c => c.Collection is null);
        shared.Expressions.OrderBy(x => x).ShouldBe(["Id", "Name"]);
        dto.Name.ShouldBe("v1");
    }

    [Fact]
    public async Task The_options_reach_the_server_as_declared()
    {
        using var h = MappingHarness.Create(remote: r => r.SetupRows<UserKey>(Honour));

        await h.Map(new RowsOptionsDto { Code = "M" });

        var recent = h.Remote.CallsOf<UserKey>().Single(c => c.Collection?.Limit == 2).Collection!;
        recent.OrderBy.Select(o => (o.PropertyName, o.Descending)).ShouldBe([("Date", true), ("Id", false)]);
        recent.Limit.ShouldBe(2);
    }

    [Fact]
    public async Task A_server_that_ignores_the_options_still_gets_the_limit_applied_by_the_client()
    {
        using var h = MappingHarness.Create(remote: r => r.SetupRows<UserKey>(id =>
            id == "M" ? [..Visits.Select(v => (Func<string, object>)(e => e == "Id" ? v.Id : FakeRemote.Absent))] : []));
        var dto = new RowsOptionsDto { Code = "M" };

        await h.Map(dto);

        dto.Recent.Count.ShouldBe(2);
        dto.Oldest.Count.ShouldBe(1);
        dto.All.Count.ShouldBe(5);
    }

    [Fact]
    public async Task Two_objects_with_the_same_key_share_the_requests()
    {
        using var h = MappingHarness.Create(remote: r => r.SetupRows<UserKey>(Honour));

        await h.Map(new[] { new RowsOptionsDto { Code = "M" }, new RowsOptionsDto { Code = "M" } });

        h.Remote.CallsOf<UserKey>().Count.ShouldBe(3);
        h.Remote.CallsOf<UserKey>().ShouldAllBe(c => c.Ids.SequenceEqual(new[] { "M" }));
    }
}
