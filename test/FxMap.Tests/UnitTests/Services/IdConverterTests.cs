using FxMap.Abstractions;
using FxMap.Exceptions;
using FxMap.Services;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Services;

/// <summary>IIdConverter&lt;TId&gt; turns the id texts of a request into a typed List&lt;TId&gt;.</summary>
public class IdConverterTests
{
    private static readonly IServiceProvider Empty = new ServiceCollection().BuildServiceProvider();

    private static IIdConverter<TId> ConverterOf<TId>(IServiceProvider sp = null) =>
        new IdConverter<TId>(sp ?? Empty);

    [Fact]
    public void Strings_are_returned_as_they_are()
    {
        List<string> ids = ConverterOf<string>().ConvertIds(["a", "B", "a"]);

        ids.ShouldBe(["a", "B", "a"]);
    }

    [Fact]
    public void The_result_is_a_new_list_not_the_input()
    {
        var input = new[] { "a", "b" };

        var ids = ConverterOf<string>().ConvertIds(input);
        ids.Add("c");

        input.ShouldBe(["a", "b"]);
    }

    [Fact]
    public void Guids_are_parsed_in_any_format_and_texts_that_do_not_parse_are_left_out()
    {
        var id = Guid.NewGuid();

        List<Guid> ids = ConverterOf<Guid>().ConvertIds([id.ToString("N"), "nope", id.ToString().ToUpperInvariant(), ""]);

        ids.ShouldBe([id, id]);
    }

    [Fact]
    public void Numbers_are_parsed()
    {
        ConverterOf<int>().ConvertIds(["7", "007", " 8 ", "x", "-1"]).ShouldBe([7, 7, 8, -1]);
        ConverterOf<long>().ConvertIds(["5000000001"]).ShouldBe([5_000_000_001L]);
        ConverterOf<decimal>().ConvertIds(["15"]).ShouldBe([15m]);
        ConverterOf<byte>().ConvertIds(["255", "256"]).ShouldBe([(byte)255]);
    }

    [Fact]
    public void Nullable_ids_are_returned_as_a_list_of_nullable()
    {
        List<Guid?> guids = ConverterOf<Guid?>().ConvertIds([Guid.Empty.ToString(), "x"]);
        List<int?> ints = ConverterOf<int?>().ConvertIds(["3", "y"]);

        guids.ShouldBe([Guid.Empty]);
        ints.ShouldBe([3]);
    }

    [Fact]
    public void No_input_gives_an_empty_list()
    {
        ConverterOf<int>().ConvertIds([]).ShouldBeEmpty();
        ConverterOf<Guid>().ConvertIds(null!).ShouldBeEmpty();
    }

    private readonly record struct OrderNo(int Number);

    private sealed class OrderNoConverter : IStronglyTypeConverter<OrderNo>
    {
        public bool CanConvert(string input) => input.StartsWith("ORD-") && int.TryParse(input[4..], out _);

        public OrderNo Convert(string input) => new(int.Parse(input[4..]));
    }

    [Fact]
    public void A_strongly_typed_id_goes_through_its_converter_and_skips_what_it_cannot_convert()
    {
        var sp = new ServiceCollection().AddSingleton<IStronglyTypeConverter<OrderNo>, OrderNoConverter>()
            .BuildServiceProvider();

        List<OrderNo> ids = ConverterOf<OrderNo>(sp).ConvertIds(["ORD-1", "bad", "ORD-22"]);

        ids.ShouldBe([new OrderNo(1), new OrderNo(22)]);
    }

    [Fact]
    public void A_type_without_a_converter_is_reported()
    {
        Should.Throw<DistributedMapException.CurrentIdTypeWasNotSupported>(() =>
            ConverterOf<OrderNo>().ConvertIds(["ORD-1"]));
    }

    [Fact]
    public void The_container_hands_out_the_converter_for_the_requested_id_type()
    {
        var sp = new ServiceCollection().AddSingleton(typeof(IIdConverter<>), typeof(IdConverter<>))
            .BuildServiceProvider();

        sp.GetRequiredService<IIdConverter<Guid>>().ShouldBeOfType<IdConverter<Guid>>();
        sp.GetRequiredService<IIdConverter<string>>().ConvertIds(["a"]).ShouldBe(["a"]);
    }
}
