using FxMap.Abstractions;
using FxMap.Responses;
using Shouldly;

namespace FxMap.Tests.ProviderTests;

/// <summary>Runs one request through the real pipeline (mapper, handler of the provider, database).</summary>
public interface IProviderRunner
{
    Task<ItemsResponse<DataResponse>> RunAsync<TKey>(string[] ids, params string[] expressions)
        where TKey : IDistributedKey;

    Task MapAsync(object value);
}

/// <summary>
/// What every data provider has to answer the same way. A provider test class calls these scenarios with its own runner.
/// </summary>
public static class ProviderScenarios
{
    // `null` asks for the default property of the entity. (A bare null argument would be the whole params array.)
    private static readonly string[] DefaultProperty = [null];

    /// <summary>The value of an expression of a row, read as the consumer does (from its JSON text).</summary>
    public static T ValueOf<T>(DataResponse row, string expression)
    {
        var value = row.Values.Single(v => v.Expression == expression).Value;
        return value is null ? default : (T)Serializable.JsonSerializer.DeserializeObject(value, typeof(T));
    }

    public static async Task String_ids_are_answered_and_missing_ids_are_left_out(IProviderRunner run)
    {
        var response = await run.RunAsync<ProviderCustomerKey>(["c1", "c2", "nobody"], DefaultProperty[0], "Email");

        response.Items.Select(i => i.Id).OrderBy(x => x).ShouldBe(["c1", "c2"]);
        var alice = response.Items.Single(i => i.Id == "c1");
        ValueOf<string>(alice, null).ShouldBe("Alice", "null asks for the default property");
        ValueOf<string>(alice, "Email").ShouldBe("alice@example.com");
        ValueOf<string>(response.Items.Single(i => i.Id == "c2"), "Email").ShouldBe("bob@example.com");
    }

    public static async Task No_ids_gives_no_rows(IProviderRunner run)
    {
        var response = await run.RunAsync<ProviderCustomerKey>([], "Email");

        response.Items.ShouldBeEmpty();
    }

    public static async Task The_same_id_twice_gives_one_row(IProviderRunner run)
    {
        var response = await run.RunAsync<ProviderCustomerKey>(["c1", "c1"], "Email");

        response.Items.ShouldHaveSingleItem().Id.ShouldBe("c1");
    }

    public static async Task Navigation_and_aggregates_are_computed_by_the_database(IProviderRunner run)
    {
        var response = await run.RunAsync<ProviderCustomerKey>(["c1", "c2"],
            "Address.City", "Orders:count", "Orders:sum(Total)", "Orders(Status = 'Done'):count",
            "Orders(Status = 'Done'):sum(Total)");

        var alice = response.Items.Single(i => i.Id == "c1");
        ValueOf<string>(alice, "Address.City").ShouldBe("Hanoi");
        ValueOf<int>(alice, "Orders:count").ShouldBe(3);
        ValueOf<decimal>(alice, "Orders:sum(Total)").ShouldBe(4350.00m);
        ValueOf<int>(alice, "Orders(Status = 'Done'):count").ShouldBe(2);
        ValueOf<decimal>(alice, "Orders(Status = 'Done'):sum(Total)").ShouldBe(1850.00m);

        var bob = response.Items.Single(i => i.Id == "c2");
        ValueOf<string>(bob, "Address.City").ShouldBe("Saigon");
        ValueOf<int>(bob, "Orders:count").ShouldBe(1);
    }

    public static async Task A_collection_is_ordered_and_paged_by_the_database(IProviderRunner run)
    {
        var response = await run.RunAsync<ProviderCustomerKey>(["c1", "c3"],
            "Orders:orderBy(Total desc)", "Orders:orderBy(Status, Total desc)", "Orders:orderBy(Number desc):offset(1):limit(1)",
            "Orders:offset(5)", "Orders:limit(0)", "Orders:orderBy(Total desc):first", "Orders:orderBy(Total desc):last");

        var alice = response.Items.Single(i => i.Id == "c1");
        ValueOf<List<ProviderOrder>>(alice, "Orders:orderBy(Total desc)").Select(o => o.Number).ShouldBe([3, 1, 2]);
        // Done (1500, 350) before Pending, by total descending within a status
        ValueOf<List<ProviderOrder>>(alice, "Orders:orderBy(Status, Total desc)").Select(o => o.Number)
            .ShouldBe([1, 2, 3]);
        ValueOf<List<ProviderOrder>>(alice, "Orders:orderBy(Number desc):offset(1):limit(1)").Select(o => o.Number)
            .ShouldBe([2]);
        ValueOf<List<ProviderOrder>>(alice, "Orders:offset(5)").ShouldBeEmpty();
        ValueOf<List<ProviderOrder>>(alice, "Orders:limit(0)").ShouldBeEmpty();

        ValueOf<ProviderOrder>(alice, "Orders:orderBy(Total desc):first").Number.ShouldBe(3);
        ValueOf<ProviderOrder>(alice, "Orders:orderBy(Total desc):last").Number.ShouldBe(2);

        // No order at all: an empty list, not a failure
        var carol = response.Items.Single(i => i.Id == "c3");
        (ValueOf<List<ProviderOrder>>(carol, "Orders:orderBy(Total desc)") ?? []).ShouldBeEmpty();
    }

    public static async Task A_missing_nested_object_and_an_empty_collection_give_null_and_zero(IProviderRunner run)
    {
        var response = await run.RunAsync<ProviderCustomerKey>(["c3"], "Address.City", "Orders:count", "Orders:sum(Total)");

        var carol = response.Items.ShouldHaveSingleItem();
        ValueOf<string>(carol, "Address.City").ShouldBeNull();
        ValueOf<int>(carol, "Orders:count").ShouldBe(0);
        ValueOf<decimal>(carol, "Orders:sum(Total)").ShouldBe(0m);
    }

    public static async Task A_guid_id_is_answered_under_each_spelling_that_was_requested(IProviderRunner run)
    {
        var lower = ProviderData.DeviceId.ToString();
        var upper = lower.ToUpperInvariant();
        var plain = ProviderData.DeviceId.ToString("N");

        var response = await run.RunAsync<ProviderDeviceKey>([lower, upper, plain], DefaultProperty);

        response.Items.Select(i => i.Id).OrderBy(x => x).ShouldBe(new[] { lower, upper, plain }.OrderBy(x => x));
        response.Items.ShouldAllBe(i => ValueOf<string>(i, null) == "scanner");
    }

    public static async Task An_int_id_is_answered_under_each_spelling_that_was_requested(IProviderRunner run)
    {
        var response = await run.RunAsync<ProviderTicketKey>(["7", "007"], DefaultProperty);

        response.Items.Select(i => i.Id).OrderBy(x => x).ShouldBe(["007", "7"]);
        response.Items.ShouldAllBe(i => ValueOf<string>(i, null) == "seven");
    }

    public static async Task The_mapper_fills_a_view_from_the_database(IProviderRunner run)
    {
        var views = new[]
        {
            new ProviderMappingView
            {
                CustomerId = "c1", DeviceId = ProviderData.DeviceId.ToString().ToUpperInvariant(), TicketId = "007"
            },
            new ProviderMappingView { CustomerId = "c2", TicketId = "7" },
            new ProviderMappingView { CustomerId = "nobody" }
        };

        await run.MapAsync(views);

        views.Select(v => v.Name).ShouldBe(["Alice", "Bob", null]);
        views[0].Label.ShouldBe("scanner", "the Guid was written in upper case");
        views[0].Title.ShouldBe("seven", "the int was written with leading zeros");
        views[1].Title.ShouldBe("seven");
    }

    /// <summary>The id of the entity is Mrn + Code: only for the providers that can filter on a computed id.</summary>
    public static async Task The_mapper_fills_a_view_through_a_computed_key(IProviderRunner run)
    {
        var views = new[]
        {
            new ProviderLabView { Mrn = "MRN0001", Code = "GLU" }, new ProviderLabView { Mrn = "MRN0002", Code = "GLU" },
            new ProviderLabView { Mrn = "MRN0009", Code = "GLU" }
        };

        await run.MapAsync(views);

        views.Select(v => v.Value).ShouldBe([5.40m, 7.10m, null]);
    }

    /// <summary>The owner isolates expression errors: the valid expressions are answered, the others reported once.</summary>
    public static async Task Expressions_that_cannot_be_built_are_reported_and_the_others_answered(IProviderRunner run)
    {
        var response = await run.RunAsync<ProviderCustomerKey>(["c1", "c2"],
            DefaultProperty[0], "Orders:summ(Total)", "Email", "Name:bogus");

        response.Errors.Select(e => e.Expression).OrderBy(x => x, StringComparer.Ordinal)
            .ShouldBe(["Name:bogus", "Orders:summ(Total)"]); // once for each expression, whatever the number of rows
        response.Errors.ShouldAllBe(e => e.Message.Contains($"Expression '{e.Expression}' failed to build"));

        response.Items.Select(i => i.Id).OrderBy(x => x).ShouldBe(["c1", "c2"]);
        var alice = response.Items.Single(i => i.Id == "c1");
        ValueOf<string>(alice, null).ShouldBe("Alice");
        ValueOf<string>(alice, "Email").ShouldBe("alice@example.com");
        alice.Values.Single(v => v.Expression == "Orders:summ(Total)").Value.ShouldBeNull();
        alice.Values.Single(v => v.Expression == "Name:bogus").Value.ShouldBeNull();
    }

    public static async Task The_errors_are_reported_even_when_no_row_matches(IProviderRunner run)
    {
        var response = await run.RunAsync<ProviderCustomerKey>(["nobody"], "Email", "Name:bogus");

        response.Items.ShouldBeEmpty();
        response.Errors.ShouldHaveSingleItem().Expression.ShouldBe("Name:bogus");
    }

    /// <summary>The fixtures call ThrowIfException(): the caller gets the errors of the owner in an exception.</summary>
    public static async Task The_mapper_throws_with_the_error_of_a_broken_expression(IProviderRunner run)
    {
        var view = new ProviderBrokenExpressionView { CustomerId = "c1" };

        var error = await Should.ThrowAsync<FxMap.Exceptions.DistributedMapException.ExpressionsFailed>(() =>
            run.MapAsync(view));

        error.Errors.ShouldHaveSingleItem().Expression.ShouldBe("Name:bogus");
        error.Message.ShouldContain("Name:bogus");
    }
}
