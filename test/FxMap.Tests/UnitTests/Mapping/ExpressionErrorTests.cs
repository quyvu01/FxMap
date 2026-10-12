using System.Text.Json;
using FxMap.Exceptions;
using FxMap.Responses;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Mapping;

/// <summary>The expressions that an owner could not build are reported once in the response; the caller decides.</summary>
public class ExpressionErrorTests
{
    private static readonly string[] Broken = ["Broken.One", "Broken:two"];

    private static MappingHarness Create(bool throwIfException) => MappingHarness.Create(
        c =>
        {
            if (throwIfException) c.ThrowIfException();
        },
        r => r.FailedExpressions = key => key == typeof(UserKey) ? Broken : []);

    [Fact]
    public async Task Without_ThrowIfException_the_valid_expressions_are_mapped_and_the_broken_ones_are_left_alone()
    {
        using var h = Create(throwIfException: false);
        var dto = new ExpressionErrorDto { UserId = "u1" };

        await h.Map(dto);

        dto.UserName.ShouldBe("user-name:u1");
        dto.BrokenOne.ShouldBe("keep", "its expression could not be built: no value, the property is not touched");
        dto.BrokenTwo.ShouldBe("keep");
    }

    [Fact]
    public async Task With_ThrowIfException_all_the_errors_are_in_one_exception()
    {
        using var h = Create(throwIfException: true);

        var error = await Should.ThrowAsync<DistributedMapException.ExpressionsFailed>(() =>
            h.Map(new ExpressionErrorDto { UserId = "u1" }));

        error.Errors.Select(e => e.Expression).OrderBy(x => x, StringComparer.Ordinal).ShouldBe(["Broken.One", "Broken:two"]);
        error.Message.ShouldContain("Broken.One");
        error.Message.ShouldContain("Broken:two");
        error.Message.ShouldContain("2 expression(s)");
    }

    [Fact]
    public async Task The_errors_are_reported_even_when_no_row_matches()
    {
        using var h = MappingHarness.Create(c => c.ThrowIfException(), r =>
        {
            r.Setup<UserKey>((_, _) => FakeRemote.Absent, exists: _ => false);
            r.FailedExpressions = key => key == typeof(UserKey) ? Broken : [];
        });

        var error = await Should.ThrowAsync<DistributedMapException.ExpressionsFailed>(() =>
            h.Map(new ExpressionErrorDto { UserId = "nobody" }));

        error.Errors.Length.ShouldBe(2);
    }

    [Fact]
    public async Task A_response_without_errors_maps_as_before()
    {
        using var h = MappingHarness.Create(c => c.ThrowIfException());
        var dto = new ExpressionErrorDto { UserId = "u1" };

        await h.Map(dto);

        dto.UserName.ShouldBe("user-name:u1");
    }

    [Fact]
    public void The_errors_survive_the_json_of_the_transports_and_an_old_response_has_none()
    {
        var response = new ItemsResponse<DataResponse>([new DataResponse { Id = "1", Values = [] }],
            [new ExpressionError("a", "boom"), new ExpressionError("b", "bang")]);

        var json = JsonSerializer.Serialize(response);
        var back = JsonSerializer.Deserialize<ItemsResponse<DataResponse>>(json)!;

        back.Items.ShouldHaveSingleItem().Id.ShouldBe("1");
        back.Errors.ShouldBe([new ExpressionError("a", "boom"), new ExpressionError("b", "bang")]);

        // a response written by a version without errors
        var old = JsonSerializer.Deserialize<ItemsResponse<DataResponse>>("""{"Items":[{"Id":"2","Values":[]}]}""")!;
        old.Items.ShouldHaveSingleItem().Id.ShouldBe("2");
        old.Errors.ShouldBeEmpty();
    }
}
