using System.Text.Json;
using FxMap.Abstractions;
using FxMap.Fluent.Rules;
using FxMap.Helpers;
using FxMap.Models;
using FxMap.Responses;
using FxMap.Tests.UnitTests.Mapping;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Helpers;

public class RowLimiterAndOptionsTests
{
    private static DataResponse Row(string id, string tag) => new()
        { Id = id, Values = [new ValueResponse { Expression = "t", Value = tag }] };

    private static string[] Tags(DataResponse[] rows) => [..rows.Select(r => $"{r.Id}:{r.Values[0].Value}")];

    #region RowLimiter

    [Fact]
    public void Keeps_the_first_rows_of_each_id_and_preserves_the_order()
    {
        var rows = new[] { Row("a", "1"), Row("b", "1"), Row("a", "2"), Row("a", "3"), Row("b", "2"), Row("c", "1") };

        var limited = RowLimiter.LimitPerId(rows, 2);

        Tags(limited).ShouldBe(["a:1", "b:1", "a:2", "b:2", "c:1"]);
    }

    [Fact]
    public void Without_a_limit_or_when_nothing_is_cut_the_same_array_is_returned()
    {
        var rows = new[] { Row("a", "1"), Row("a", "2") };

        RowLimiter.LimitPerId(rows, null).ShouldBeSameAs(rows);
        RowLimiter.LimitPerId(rows, 5).ShouldBeSameAs(rows);
        RowLimiter.LimitPerId([], 1).ShouldBeEmpty();
    }

    [Fact]
    public void A_limit_of_one_keeps_one_row_per_id()
    {
        var rows = new[] { Row("a", "1"), Row("a", "2"), Row("b", "1"), Row("b", "2") };

        Tags(RowLimiter.LimitPerId(rows, 1)).ShouldBe(["a:1", "b:1"]);
    }

    #endregion

    #region CollectionOptions

    [Fact]
    public void Equal_options_have_equal_signatures_and_different_ones_do_not()
    {
        CollectionOptions Make(string name, bool desc, int? limit) => new([new CollectionOrder(name, desc)], limit);

        Make("Date", true, 5).Signature.ShouldBe(Make("Date", true, 5).Signature);
        Make("Date", true, 5).Signature.ShouldNotBe(Make("Date", false, 5).Signature);
        Make("Date", true, 5).Signature.ShouldNotBe(Make("Id", true, 5).Signature);
        Make("Date", true, 5).Signature.ShouldNotBe(Make("Date", true, 6).Signature);
        Make("Date", true, null).Signature.ShouldNotBe(Make("Date", true, 0).Signature);
    }

    [Fact]
    public void Empty_options_say_so()
    {
        new CollectionOptions([], null).IsEmpty.ShouldBeTrue();
        new CollectionOptions(null!, null).IsEmpty.ShouldBeTrue();
        new CollectionOptions([], 3).IsEmpty.ShouldBeFalse();
        new CollectionOptions([new CollectionOrder("X", false)], null).IsEmpty.ShouldBeFalse();
    }

    #endregion

    #region The request on the wire

    private static readonly CollectionOptions Options =
        new([new CollectionOrder("VisitDate", true), new CollectionOrder("Id", false)], 5);

    [Fact]
    public void The_options_survive_a_json_round_trip_of_the_request()
    {
        var request = new DistributedMapRequest(["a", "b"], ["Name", "Id"]) { Collection = Options };

        var back = JsonSerializer.Deserialize<DistributedMapRequest>(JsonSerializer.Serialize(request))!;

        back.SelectorIds.ShouldBe(["a", "b"]);
        back.Expressions.ShouldBe(["Name", "Id"]);
        back.Collection.ShouldNotBeNull();
        back.Collection!.Limit.ShouldBe(5);
        back.Collection.OrderBy.Select(o => (o.PropertyName, o.Descending)).ShouldBe([("VisitDate", true), ("Id", false)]);
    }

    [Fact]
    public void A_request_from_an_older_client_has_no_options()
    {
        var back = JsonSerializer.Deserialize<DistributedMapRequest>("{\"SelectorIds\":[\"a\"],\"Expressions\":[\"Name\"]}")!;

        back.Collection.ShouldBeNull();
    }

    [Fact]
    public void An_older_server_ignores_the_options_it_does_not_know()
    {
        var json = JsonSerializer.Serialize(new { SelectorIds = new[] { "a" }, Expressions = new[] { "Name" }, Collection = Options });

        var back = JsonSerializer.Deserialize<LegacyRequest>(json)!;

        back.SelectorIds.ShouldBe(["a"]);
        back.Expressions.ShouldBe(["Name"]);
    }

    private sealed record LegacyRequest(string[] SelectorIds, string[] Expressions);

    [Fact]
    public void The_typed_and_the_untyped_request_convert_both_ways_keeping_the_options()
    {
        var typed = new MapRequest<UserKey>(["a"], ["Name"]) { Collection = Options };

        var untyped = typed.ToDistributedMapRequest();
        var again = untyped.ToMapRequest<UserKey>();

        untyped.Collection.ShouldBeSameAs(Options);
        again.SelectorIds.ShouldBe(["a"]);
        again.Collection.ShouldBeSameAs(Options);
    }

    [Fact]
    public void A_typed_request_serialized_by_a_client_is_read_back_as_the_untyped_one_by_the_server()
    {
        var typed = new MapRequest<UserKey>(["a"], ["Name"]) { Collection = Options };

        var untyped = JsonSerializer.Deserialize<DistributedMapRequest>(JsonSerializer.Serialize(typed))!;

        untyped.Collection!.Signature.ShouldBe(Options.Signature);
    }

    [Fact]
    public void Record_with_keeps_the_options_as_the_received_orchestrator_does()
    {
        var typed = new MapRequest<UserKey>(["a"], ["Name", "Custom"]) { Collection = Options };

        var narrowed = typed with { Expressions = ["Name"] };

        narrowed.Collection.ShouldBeSameAs(Options);
    }

    [Fact]
    public void The_grpc_message_carries_the_options_as_json()
    {
        var message = new FxMap.Grpc.GetFxMapGrpcQuery { CollectionOptions = JsonSerializer.Serialize(Options) };
        message.SelectorIds.Add("a");

        var parsed = FxMap.Grpc.GetFxMapGrpcQuery.Parser.ParseFrom(Google.Protobuf.MessageExtensions.ToByteArray(message));

        JsonSerializer.Deserialize<CollectionOptions>(parsed.CollectionOptions)!.Signature.ShouldBe(Options.Signature);
        new FxMap.Grpc.GetFxMapGrpcQuery().CollectionOptions.ShouldBeNull("unset for single-row requests");
    }

    #endregion
}
