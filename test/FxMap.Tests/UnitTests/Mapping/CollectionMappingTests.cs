using System.Text.Json;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Mapping;

/// <summary>
/// Collection rules in the mapper: a key that matches several rows (several DataResponse with the same id) fills one
/// element per row. A fake remote that returns several rows per id stands in for a non-unique lookup.
/// </summary>
public class CollectionMappingTests
{
    // Row of a visit-like table: Id, Name, Email and the ProvinceId it points to.
    private static Func<string, object> Row(int id, string name, string province = null) => e => e switch
    {
        "Id" => id,
        "Name" => name,
        "Email" => $"{name}@x.com",
        "ProvinceId" => province ?? FakeRemote.Absent,
        _ => FakeRemote.Absent
    };

    // A -> one row, B -> three rows, C -> none
    private static Action<FakeRemote> Rows(Action<FakeRemote> more = null) => remote =>
    {
        remote.SetupRows<UserKey>(id => id switch
        {
            "A" => [Row(1, "item-1", "p1")],
            "B" => [Row(2, "item-2", "p2"), Row(3, "item-3", "p3"), Row(4, "item-4", "p2")],
            _ => []
        });
        more?.Invoke(remote);
    };

    #region One element per row

    [Fact]
    public async Task Each_row_becomes_an_element_in_arrival_order()
    {
        using var h = MappingHarness.Create(remote: Rows());
        var a = new RowsDto { Code = "A" };
        var b = new RowsDto { Code = "B" };

        await h.Map(new[] { a, b });

        a.Items.Select(i => i.RowId).ShouldBe([1]);
        b.Items.Select(i => i.RowId).ShouldBe([2, 3, 4]);
        b.Items.Select(i => i.Name).ShouldBe(["item-2", "item-3", "item-4"]);
    }

    [Fact]
    public async Task The_properties_of_an_element_all_come_from_the_same_row()
    {
        using var h = MappingHarness.Create(remote: Rows());
        var b = new RowsDto { Code = "B" };

        await h.Map(b);

        b.Items.ShouldAllBe(i => i.Email == $"{i.Name}@x.com");
        b.Items.Single(i => i.RowId == 3).Name.ShouldBe("item-3");
    }

    [Fact]
    public async Task A_plain_rule_on_the_same_key_takes_the_first_row()
    {
        using var h = MappingHarness.Create(remote: Rows());
        var b = new RowsDto { Code = "B" };

        await h.Map(b);

        b.Name.ShouldBe("item-2");
    }

    [Fact]
    public async Task Objects_with_the_same_key_each_get_their_own_collection_and_elements()
    {
        using var h = MappingHarness.Create(remote: Rows());
        var first = new RowsDto { Code = "B" };
        var second = new RowsDto { Code = "B" };

        await h.Map(new[] { first, second });

        first.Items.Count.ShouldBe(3);
        second.Items.Count.ShouldBe(3);
        first.Items.ShouldNotBeSameAs(second.Items);
        first.Items[0].ShouldNotBeSameAs(second.Items[0]);
        first.Items[0].Name.ShouldBe(second.Items[0].Name);
    }

    [Fact]
    public async Task A_key_without_rows_leaves_the_property_untouched()
    {
        using var h = MappingHarness.Create(remote: Rows());
        var existing = new List<RowItem> { new() { Name = "keep" } };
        var c = new RowsDto { Code = "C", Items = existing };
        var unknown = new RowsDto { Code = "Z" };

        await h.Map(new[] { c, unknown });

        c.Items.ShouldBeSameAs(existing);
        unknown.Items.ShouldBeNull();
    }

    [Fact]
    public async Task A_null_selector_is_skipped_without_a_request_for_it()
    {
        using var h = MappingHarness.Create(remote: Rows());
        var none = new RowsDto { Code = null };
        var a = new RowsDto { Code = "A" };

        await h.Map(new[] { none, a });

        none.Items.ShouldBeNull();
        a.Items.ShouldHaveSingleItem();
        h.Remote.NonEmptyCallsOf<UserKey>().ShouldHaveSingleItem().Ids.ShouldBe(["A"]);
    }

    #endregion

    #region Request shape

    [Fact]
    public async Task One_request_carries_the_distinct_ids_and_the_expressions_of_the_item_rules_and_plain_rules()
    {
        using var h = MappingHarness.Create(remote: Rows());
        var dtos = new[] { "A", "B", "B", "A", "B" }.Select(c => new RowsDto { Code = c }).ToList();

        await h.Map(dtos);

        var call = h.Remote.CallsOf<UserKey>().ShouldHaveSingleItem();
        call.Ids.OrderBy(x => x).ShouldBe(["A", "B"]);
        call.Expressions.OrderBy(x => x).ShouldBe(["Email", "Id", "Name", "ProvinceId"]);
    }

    [Fact]
    public async Task A_conditional_item_rule_is_resolved_for_each_call()
    {
        using var h = MappingHarness.Create(remote: Rows());

        h.Mode.UseEmail = false;
        var byName = new RowsConditionalDto { Code = "B" };
        await h.Map(byName);
        h.Mode.UseEmail = true;
        var byEmail = new RowsConditionalDto { Code = "B" };
        await h.Map(byEmail);

        byName.Items.Select(i => i.Name).ShouldBe(["item-2", "item-3", "item-4"]);
        byEmail.Items.Select(i => i.Name).ShouldBe(["item-2@x.com", "item-3@x.com", "item-4@x.com"]);
        h.Remote.CallsOf<UserKey>().Select(c => c.Expressions.Single()).ShouldBe(["Name", "Email"]);
    }

    #endregion

    #region Limit and property types

    [Fact]
    public async Task The_limit_of_the_rule_caps_the_elements_and_an_array_property_gets_an_array()
    {
        using var h = MappingHarness.Create(remote: Rows());
        var b = new RowsArrayDto { Code = "B" };
        var a = new RowsArrayDto { Code = "A" };

        await h.Map(new[] { a, b });

        b.Items.Select(i => i.RowId).ShouldBe([2, 3]);
        b.Items.ShouldBeOfType<RowItem[]>();
        a.Items.Select(i => i.RowId).ShouldBe([1]);
    }

    [Fact]
    public async Task Interface_typed_properties_receive_their_collection()
    {
        using var h = MappingHarness.Create(remote: Rows());
        var b = new RowsInterfaceDto { Code = "B" };

        await h.Map(b);

        b.ReadOnly.Select(i => i.Name).ShouldBe(["item-2", "item-3", "item-4"]);
        b.Enumerable.Select(i => i.RowId).ShouldBe([2, 3, 4]);
        b.Collection.Select(i => i.Email).ShouldBe(["item-2@x.com", "item-3@x.com", "item-4@x.com"]);
        h.Remote.CallsOf<UserKey>().ShouldHaveSingleItem().Expressions.OrderBy(x => x).ShouldBe(["Email", "Id", "Name"]);
    }

    #endregion

    #region Further mapping of the elements and chains

    [Fact]
    public async Task Elements_with_their_own_profile_are_mapped_on_the_next_level()
    {
        using var h = MappingHarness.Create(remote: Rows());
        var views = new[] { new RowsDto { Code = "A" }, new RowsDto { Code = "B" } };

        await h.Map(views);

        views[0].Items.Single().ProvinceName.ShouldBe("province-name:p1");
        views[1].Items.Select(i => i.ProvinceName)
            .ShouldBe(["province-name:p2", "province-name:p3", "province-name:p2"]);
        h.Remote.CallsOf<ProvinceKey>().ShouldHaveSingleItem().Ids.OrderBy(x => x).ShouldBe(["p1", "p2", "p3"]);
    }

    [Fact]
    public async Task A_collection_whose_selector_is_filled_by_an_earlier_key_is_resolved_after_it()
    {
        using var h = MappingHarness.Create(remote: r =>
        {
            r.Setup<UserKey>((id, e) => e == "Code" ? "B" : FakeRemote.Absent);
            r.SetupRows<ProductKey>(id => id == "B" ? [Row(2, "item-2"), Row(3, "item-3")] : []);
        });
        var dto = new RowsChainDto { UserId = "u1" };

        await h.Map(dto);

        dto.Code.ShouldBe("B");
        dto.Items.Select(i => i.RowId).ShouldBe([2, 3]);
        h.Remote.CallsOf<ProductKey>().Single().StartTs
            .ShouldBeGreaterThanOrEqualTo(h.Remote.CallsOf<UserKey>().Single().EndTs);
    }

    #endregion

    #region Errors

    [Fact]
    public async Task A_malformed_value_is_swallowed_by_default_and_the_rest_of_the_element_is_filled()
    {
        using var h = MappingHarness.Create(remote: r => r.SetupRows<UserKey>(_ =>
            [e => e switch { "Id" => new RawJson("\"not a number\""), "Name" => "n", _ => FakeRemote.Absent }]));
        var dto = new RowsDto { Code = "A" };

        await h.Map(dto);

        var item = dto.Items.ShouldHaveSingleItem();
        item.RowId.ShouldBe(0);
        item.Name.ShouldBe("n");
    }

    [Fact]
    public async Task A_malformed_value_throws_when_configured_to_throw()
    {
        using var h = MappingHarness.Create(c => c.ThrowIfException(), r => r.SetupRows<UserKey>(_ =>
            [e => e == "Id" ? new RawJson("\"not a number\"") : FakeRemote.Absent]));

        await Should.ThrowAsync<JsonException>(() => h.Map(new RowsDto { Code = "A" }));
    }

    [Fact]
    public async Task A_row_without_a_value_for_an_expression_leaves_that_property_at_its_default()
    {
        using var h = MappingHarness.Create(remote: r =>
            r.SetupRows<UserKey>(_ => [e => e == "Name" ? "only-name" : FakeRemote.Absent]));
        var dto = new RowsDto { Code = "A" };

        await h.Map(dto);

        var item = dto.Items.ShouldHaveSingleItem();
        item.Name.ShouldBe("only-name");
        item.Email.ShouldBeNull();
        item.RowId.ShouldBe(0);
    }

    #endregion

    #region Oracle

    public static TheoryData<int> Seeds()
    {
        var data = new TheoryData<int>();
        for (var seed = 1; seed <= 25; seed++) data.Add(seed);
        return data;
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public async Task Random_row_counts_give_exactly_the_rows_of_each_key(int seed)
    {
        var rnd = new Random(seed);
        var keys = Enumerable.Range(0, 12).Select(i => $"k{i}").ToList();
        var rowsOfKey = keys.ToDictionary(k => k, _ => rnd.Next(0, 7));
        using var h = MappingHarness.Create(remote: r => r.SetupRows<UserKey>(id =>
            !rowsOfKey.TryGetValue(id, out var n)
                ? []
                : Enumerable.Range(0, n).Select(row => Row(row, $"{id}-row{row}", $"p{row % 3}")).ToList()));
        var dtos = Enumerable.Range(0, 60).Select(_ => new RowsDto { Code = keys[rnd.Next(keys.Count)] }).ToList();

        await h.Map(dtos);

        foreach (var dto in dtos)
        {
            var expected = rowsOfKey[dto.Code];
            if (expected == 0)
            {
                dto.Items.ShouldBeNull();
                continue;
            }

            dto.Items.Select(i => i.Name).ShouldBe(Enumerable.Range(0, expected).Select(r => $"{dto.Code}-row{r}"));
            dto.Items.Select(i => i.ProvinceName)
                .ShouldBe(Enumerable.Range(0, expected).Select(r => $"province-name:p{r % 3}"));
            dto.Name.ShouldBe($"{dto.Code}-row0");
        }
    }

    #endregion
}
