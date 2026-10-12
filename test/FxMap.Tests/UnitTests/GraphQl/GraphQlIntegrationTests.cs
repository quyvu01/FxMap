using System.Text.Json;
using FxMap.Tests.UnitTests.Mapping;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.GraphQl;

/// <summary>The FxMap integration with Hot Chocolate: fields of a type that has a profile are resolved through FxMap.</summary>
[Collection("GraphQl sequential")]
public class GraphQlIntegrationTests
{
    private static JsonElement Items(JsonElement result, string field) => result.GetProperty("data").GetProperty(field);

    [Fact]
    public async Task The_fields_of_a_profile_are_resolved_from_the_other_services()
    {
        using var h = GraphQlHarness.Create();

        var result = await h.QueryAsync("{ members { id userName userEmail } }");

        result.TryGetProperty("errors", out _).ShouldBeFalse(result.ToString());
        var members = Items(result, "members").EnumerateArray().ToArray();
        members.Select(m => m.GetProperty("userName").GetString()).ShouldBe(["user-name:u1", "user-name:u2", "user-name:u1"]);
        members.Select(m => m.GetProperty("userEmail").GetString())
            .ShouldBe(["user-email:u1", "user-email:u2", "user-email:u1"]);
    }

    [Fact]
    public async Task A_field_that_depends_on_another_is_resolved_after_it()
    {
        using var h = GraphQlHarness.Create();

        var result = await h.QueryAsync("{ members { userId provinceName } }");

        result.TryGetProperty("errors", out _).ShouldBeFalse(result.ToString());
        // u1 -> ProvinceId "province-id:u1" -> its name
        Items(result, "members")[0].GetProperty("provinceName").GetString()
            .ShouldBe("province-name:province-id:u1");
        h.Remote.CallsOf<UserKey>().ShouldNotBeEmpty();
        h.Remote.CallsOf<ProvinceKey>().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Only_the_requested_fields_are_fetched()
    {
        using var h = GraphQlHarness.Create();

        await h.QueryAsync("{ members { userName } }");

        h.Remote.NonEmptyCallsOf<UserKey>().SelectMany(c => c.Expressions).Distinct().ShouldBe(["Name"]);
    }

    [Fact]
    public async Task The_objects_of_a_list_share_one_request_with_distinct_ids()
    {
        using var h = GraphQlHarness.Create();

        await h.QueryAsync("{ members { userName userEmail } }");

        var call = h.Remote.NonEmptyCallsOf<UserKey>().ShouldHaveSingleItem();
        call.Ids.OrderBy(x => x).ShouldBe(["u1", "u2"]); // three members, two distinct users, one request
        call.Expressions.OrderBy(x => x).ShouldBe(["Email", "Name"]);
    }

    [Fact]
    public async Task An_object_without_a_key_makes_no_request_and_its_field_is_null()
    {
        using var h = GraphQlHarness.Create();

        var result = await h.QueryAsync("{ membersWithoutUser { id userName } }");

        result.TryGetProperty("errors", out _).ShouldBeFalse(result.ToString());
        Items(result, "membersWithoutUser")[0].GetProperty("userName").ValueKind.ShouldBe(JsonValueKind.Null);
        h.Remote.NonEmptyCallsOf<UserKey>().ShouldBeEmpty();
    }

    [Fact]
    public async Task An_id_that_the_owner_does_not_know_gives_a_null_field()
    {
        using var h = GraphQlHarness.Create(remote: r => r.Setup<UserKey>(
            MappingHarness.Standard("user"), exists: id => id != "u2"));

        var result = await h.QueryAsync("{ members { userId userName } }");

        var names = Items(result, "members").EnumerateArray().Select(m => m.GetProperty("userName"));
        names.Select(n => n.ValueKind == JsonValueKind.Null ? null : n.GetString())
            .ShouldBe(["user-name:u1", null, "user-name:u1"]);
    }

    [Fact]
    public async Task A_computed_key_is_resolved()
    {
        using var h = GraphQlHarness.Create();

        var result = await h.QueryAsync("{ labs { mrn code label } }");

        result.TryGetProperty("errors", out _).ShouldBeFalse(result.ToString());
        Items(result, "labs").EnumerateArray().Select(l => l.GetProperty("label").GetString())
            .ShouldBe(["user-name:M1A", "user-name:M2B"]); // the ids sent are Mrn + Code
        h.Remote.NonEmptyCallsOf<UserKey>().Single().Ids.OrderBy(x => x).ShouldBe(["M1A", "M2B"]);
    }

    [Theory]
    [InlineData(false, "Name", "user-name:u1")]
    [InlineData(true, "Email", "user-email:u1")]
    public async Task A_conditional_expression_sees_the_scope_of_the_request(bool showEmail, string expression,
        string expected)
    {
        using var h = GraphQlHarness.Create();

        var result = await h.QueryAsync("{ choices { display } }", flag => flag.ShowEmail = showEmail);

        result.TryGetProperty("errors", out _).ShouldBeFalse(result.ToString());
        Items(result, "choices")[0].GetProperty("display").GetString().ShouldBe(expected);
        h.Remote.NonEmptyCallsOf<UserKey>().Single().Expressions.ShouldBe([expression]);
    }

    [Fact]
    public async Task An_expression_the_owner_could_not_build_is_a_graphql_error_with_ThrowIfException()
    {
        using var h = GraphQlHarness.Create(throwIfException: true,
            remote: r => r.FailedExpressions = key => key == typeof(UserKey) ? ["Email"] : []);

        var result = await h.QueryAsync("{ members { userName userEmail } }");

        result.TryGetProperty("errors", out var errors).ShouldBeTrue(result.ToString());
        errors.ToString().ShouldContain("Email");
    }

    [Fact]
    public async Task Without_ThrowIfException_the_broken_field_is_null_and_the_others_are_resolved()
    {
        using var h = GraphQlHarness.Create(throwIfException: false,
            remote: r => r.FailedExpressions = key => key == typeof(UserKey) ? ["Email"] : []);

        var result = await h.QueryAsync("{ members { userName userEmail } }");

        result.TryGetProperty("errors", out _).ShouldBeFalse(result.ToString());
        var first = Items(result, "members")[0];
        first.GetProperty("userName").GetString().ShouldBe("user-name:u1");
        first.GetProperty("userEmail").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task A_conditional_rule_in_the_chain_is_resolved_for_the_request_too()
    {
        using var h = GraphQlHarness.Create();

        var result = await h.QueryAsync("{ chains { provinceName } }", flag => flag.ShowEmail = true);

        result.TryGetProperty("errors", out _).ShouldBeFalse(result.ToString());
        Items(result, "chains")[0].GetProperty("provinceName").GetString().ShouldBe("province-name:province-id:u1");
        // the first link was asked with its resolved expression, never with the default property (null)
        h.Remote.NonEmptyCallsOf<UserKey>().SelectMany(c => c.Expressions).Distinct().ShouldBe(["ProvinceId"]);
    }
}
