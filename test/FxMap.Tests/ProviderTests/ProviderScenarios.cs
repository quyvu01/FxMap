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
            "Address.City", "Orders:count", "Orders:sum(Total)", "Orders:where(Status = 'Done'):count",
            "Orders:where(Status = 'Done'):sum(Total)");

        var alice = response.Items.Single(i => i.Id == "c1");
        ValueOf<string>(alice, "Address.City").ShouldBe("Hanoi");
        ValueOf<int>(alice, "Orders:count").ShouldBe(3);
        ValueOf<decimal>(alice, "Orders:sum(Total)").ShouldBe(4350.00m);
        ValueOf<int>(alice, "Orders:where(Status = 'Done'):count").ShouldBe(2);
        ValueOf<decimal>(alice, "Orders:where(Status = 'Done'):sum(Total)").ShouldBe(1850.00m);

        var bob = response.Items.Single(i => i.Id == "c2");
        ValueOf<string>(bob, "Address.City").ShouldBe("Saigon");
        ValueOf<int>(bob, "Orders:count").ShouldBe(1);
    }

    public static async Task A_collection_is_ordered_and_paged_by_the_database(IProviderRunner run)
    {
        var response = await run.RunAsync<ProviderCustomerKey>(["c1", "c3"],
            "Orders:orderBy(Total desc)", "Orders:orderBy(Status, Total desc)", "Orders:orderBy(Number desc):offset(1):limit(1)",
            "Orders:offset(5)", "Orders:limit(0)", "Orders:orderBy(Total desc):first", "Orders:orderBy(Total desc):last",
            "Orders:orderBy(Total desc):where(Status = 'Done')", "Orders:where(Status = 'Done'):orderBy(Total):first");

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

        // where after orderBy keeps the order; where before orderBy then first
        ValueOf<List<ProviderOrder>>(alice, "Orders:orderBy(Total desc):where(Status = 'Done')").Select(o => o.Number)
            .ShouldBe([1, 2]);
        ValueOf<ProviderOrder>(alice, "Orders:where(Status = 'Done'):orderBy(Total):first").Number.ShouldBe(2);

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

    // ------------------------------------------------------------------------------------------------------------------
    // Every function of the expression language, answered by the database. The expected values are the ones that
    // docs/expression.md gives (computed by hand from the rows of ProviderData), and are the same for every provider.
    // ------------------------------------------------------------------------------------------------------------------

    private static string Canonical(System.Text.Json.Nodes.JsonNode node) => node switch
    {
        null => "null",
        System.Text.Json.Nodes.JsonArray a => "[" + string.Join(",", a.Select(Canonical).Order(StringComparer.Ordinal)) + "]",
        System.Text.Json.Nodes.JsonObject o => "{" + string.Join(",", o.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => $"\"{p.Key}\":{Canonical(p.Value)}")) + "}",
        System.Text.Json.Nodes.JsonValue v when v.GetValueKind() == System.Text.Json.JsonValueKind.Number && v.TryGetValue<decimal>(out var d) =>
            d.ToString("0.#############################", System.Globalization.CultureInfo.InvariantCulture),
        _ => node.ToJsonString()
    };

    /// <summary>(customer id, expression, expected value as JSON; arrays are compared without their order)</summary>
    public static readonly (string Id, string Expression, string Expected)[] FunctionCases =
    [
        // string functions
        ("c1", "Name:upper", "\"ALICE\""), ("c1", "Name:lower", "\"alice\""),
        ("c1", "Name:substring(0, 3)", "\"Ali\""), ("c1", "Name:substring(2)", "\"ice\""), ("c1", "Name:substring(1, 2)", "\"li\""),
        ("c1", "Name:replace('i', 'I')", "\"AlIce\""), ("c1", "Name:concat('-', Email)", "\"Alice-alice@example.com\""),
        ("c1", "Tags:split(',')", "[\"a\",\"b\",\"c\"]"), ("c1", "Name:upper:lower", "\"alice\""),
        ("c1", "Name:concat(' ', Email):upper", "\"ALICE ALICE@EXAMPLE.COM\""),
        // date functions (2024-12-25 14:30:45 UTC, a Wednesday)
        ("c1", "JoinedAt:year", "2024"), ("c1", "JoinedAt:month", "12"), ("c1", "JoinedAt:day", "25"), ("c1", "JoinedAt:hour", "14"),
        ("c1", "JoinedAt:minute", "30"), ("c1", "JoinedAt:second", "45"), ("c1", "JoinedAt:dayOfWeek", "3"),
        ("c1", "JoinedAt:format('yyyy-MM-dd')", "\"2024-12-25\""), ("c1", "JoinedAt:format('dd/MM/yyyy HH:mm:ss')", "\"25/12/2024 14:30:45\""),
        ("c1", "JoinedAt:year:add(1)", "2025"),
        // aggregations (the orders of c1: 1500, 350, 2500)
        ("c1", "Orders:count", "3"), ("c1", "Orders:sum(Total)", "4350"), ("c1", "Orders:avg(Total)", "1450"),
        ("c1", "Orders:min(Total)", "350"), ("c1", "Orders:max(Total)", "2500"),
        // a customer with no order (c3): the count and the sum are 0, the others have no value
        ("c3", "Orders:count", "0"), ("c3", "Orders:sum(Total)", "0"),
        ("c3", "Orders:avg(Total)", "null"), ("c3", "Orders:min(Total)", "null"), ("c3", "Orders:max(Total)", "null"),
        // math functions
        ("c1", "Orders:sum(Total):add(50)", "4400"), ("c1", "Orders:sum(Total):subtract(50)", "4300"),
        ("c1", "Orders:sum(Total):multiply(2)", "8700"), ("c1", "Orders:sum(Total):divide(2)", "2175"),
        ("c1", "Orders:sum(Total):mod(7)", "3"), ("c1", "Orders:sum(Total):round(0)", "4350"),
        ("c1", "Orders:avg(Total):floor", "1450"), ("c1", "Orders:avg(Total):ceil", "1450"), ("c1", "Orders:avg(Total):abs", "1450"),
        ("c1", "Score:pow(2)", "7225"), ("c1", "Score:mod(10)", "5"), ("c1", "Score:add(5):multiply(2)", "180"),
        // filters
        ("c1", "Orders:where(Status = 'Done'):count", "2"), ("c1", "Orders:where(Status = 'Done'):sum(Total)", "1850"),
        ("c1", "Orders:where(Total > 1000):count", "2"), ("c1", "Orders:where(Status = 'Done' && Total > 1000):count", "1"),
        ("c1", "Orders:where(Status = 'Done' || Total > 2000):count", "3"), ("c1", "Orders:where(Status != 'Done'):count", "1"),
        ("c1", "Orders:where(Status contains 'on'):count", "2"), ("c1", "Orders:where(Status startswith 'Pe'):count", "1"),
        ("c1", "Orders:where(Status endswith 'ne'):count", "2"), ("c1", "Orders:where(Status = 'Done', Total > 1000):count", "1"),
        ("c1", "Orders:where(Total >= 350 && Total <= 1500):count", "2"), ("c1", "Orders:where(Status:lower = 'done'):count", "2"),
        ("c1", "Orders:where(Number > 1):sum(Total)", "2850"),
        // boolean functions
        ("c1", "Orders:any", "true"), ("c3", "Orders:any", "false"), ("c1", "Orders:any(Status = 'Pending')", "true"),
        ("c1", "Orders:any(Total > 5000)", "false"), ("c1", "Orders:all(Total > 100)", "true"), ("c1", "Orders:all(Total > 400)", "false"),
        // coalesce and ternary
        ("c1", "Nick ?? Name", "\"Alice\""), ("c2", "Nick ?? Name", "\"Bobby\""), ("c1", "Nick ?? 'Anonymous'", "\"Anonymous\""),
        ("c3", "Address.City ?? 'Unknown'", "\"Unknown\""), ("c1", "Address.City ?? 'Unknown'", "\"Hanoi\""),
        ("c1", "Name = 'Alice' ? 'Yes' : 'No'", "\"Yes\""), ("c1", "Score >= 90 ? 'A' : Score >= 80 ? 'B' : 'C'", "\"B\""),
        ("c2", "Score >= 90 ? 'A' : Score >= 80 ? 'B' : 'C'", "\"A\""),
        ("c1", "Orders:any(Status = 'Pending') ? 'Has' : 'None'", "\"Has\""), ("c1", "Orders:count > 2 ? 'Many' : 'Few'", "\"Many\""),
        ("c1", "Name = 'Alice' && Score > 80 ? 'Hit' : 'Miss'", "\"Hit\""), ("c1", "Name = 'Zed' || Score > 90 ? 'Hit' : 'Miss'", "\"Miss\""),
        ("c1", "Name = 'Zed' || Score > 80 ? 'Hit' : 'Miss'", "\"Hit\""),
        // distinct and group by
        ("c1", "Orders:distinct(Status)", "[\"Done\",\"Pending\"]"), ("c1", "Orders:distinct(Status):count", "2"),
        ("c1", "Orders:groupBy(Status).{Status, :count as N, :sum(Total) as Sum}",
            "[{\"Status\":\"Done\",\"N\":2,\"Sum\":1850},{\"Status\":\"Pending\",\"N\":1,\"Sum\":2500}]"),
        ("c1", "Orders:groupBy(Status).{Status, :avg(Total) as Avg, :min(Total) as Min, :max(Total) as Max}",
            "[{\"Status\":\"Done\",\"Avg\":925,\"Min\":350,\"Max\":1500},{\"Status\":\"Pending\",\"Avg\":2500,\"Min\":2500,\"Max\":2500}]"),
        ("c1", "Orders:groupBy(Status, Number).{Status, Number, :count as N}",
            "[{\"Status\":\"Done\",\"Number\":1,\"N\":1},{\"Status\":\"Done\",\"Number\":2,\"N\":1},{\"Status\":\"Pending\",\"Number\":3,\"N\":1}]"),
        ("c1", "Orders:groupBy(Status).{Status, {Number, Total} as Items}",
            "[{\"Status\":\"Done\",\"Items\":[{\"Number\":1,\"Total\":1500},{\"Number\":2,\"Total\":350}]},{\"Status\":\"Pending\",\"Items\":[{\"Number\":3,\"Total\":2500}]}]"),
        // projections
        ("c1", "{Name, Email}", "{\"Name\":\"Alice\",\"Email\":\"alice@example.com\"}"),
        ("c1", "{Name:upper as Loud}", "{\"Loud\":\"ALICE\"}"), ("c1", "{(Nick ?? Name) as D}", "{\"D\":\"Alice\"}"),
        ("c1", "Orders.{Number, Status}", "[{\"Number\":1,\"Status\":\"Done\"},{\"Number\":2,\"Status\":\"Done\"},{\"Number\":3,\"Status\":\"Pending\"}]"),
        // navigation
        ("c1", "Address.City", "\"Hanoi\""), ("c1", "Address.Country", "\"VN\""),
    ];

    public static async Task Every_function_answers_the_documented_value(IProviderRunner run)
    {
        var problems = new List<string>();
        foreach (var (id, expression, expected) in FunctionCases)
        {
            try
            {
                var response = await run.RunAsync<ProviderCustomerKey>([id], expression);
                if (response.Errors is { Length: > 0 })
                {
                    problems.Add($"{id} {expression}  =>  error reported: {response.Errors[0].Message}");
                    continue;
                }

                var row = response.Items.SingleOrDefault();
                var value = row?.Values.SingleOrDefault(v => v.Expression == expression)?.Value;
                var actual = value is null ? "null" : Canonical(System.Text.Json.Nodes.JsonNode.Parse(value));
                var wanted = Canonical(System.Text.Json.Nodes.JsonNode.Parse(expected));
                if (actual != wanted) problems.Add($"{id} {expression}  =>  expected {wanted}, got {actual}");
            }
            catch (Exception e)
            {
                problems.Add($"{id} {expression}  =>  threw {e.GetType().Name}: {e.Message.Split('\n')[0]}");
            }
        }

        problems.ShouldBeEmpty($"{problems.Count} of {FunctionCases.Length} expressions:\n" + string.Join("\n", problems));
    }
}
