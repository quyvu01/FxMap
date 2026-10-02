using FxMap.Abstractions;
using FxMap.Helpers;
using FxMap.Responses;
using FxMap.Services;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Helpers;

/// <summary>
/// Providers return rows under the canonical id text; callers match answers to their objects by the text they sent.
/// </summary>
public class RequestedIdAnswersTests
{
    private static readonly IServiceProvider EmptyServices = new ServiceCollection().BuildServiceProvider();

    private static DataResponse Row(object id, string value = "v") => new()
    {
        Id = id.ToString()!, Values = [new ValueResponse { Expression = "Name", Value = value }]
    };

    private static IIdConverter ConverterOf<TId>(IServiceProvider sp = null) =>
        new IdConverter<TId>(sp ?? EmptyServices);

    private static string[] Ids(DataResponse[] answers) => [..answers.Select(a => a.Id).OrderBy(x => x, StringComparer.Ordinal)];

    [Fact]
    public void Canonical_ids_are_returned_untouched_and_without_copying()
    {
        var id = Guid.NewGuid();
        var rows = new[] { Row(id) };

        var answers = RequestedIdAnswers.Align([id.ToString()], rows, ConverterOf<Guid>());

        answers.ShouldBeSameAs(rows);
    }

    [Fact]
    public void No_rows_means_no_answers()
    {
        var rows = Array.Empty<DataResponse>();

        RequestedIdAnswers.Align([Guid.NewGuid().ToString()], rows, ConverterOf<Guid>()).ShouldBeSameAs(rows);
    }

    [Theory]
    [InlineData("D", true)]
    [InlineData("N", false)]
    [InlineData("B", false)]
    [InlineData("P", false)]
    [InlineData("X", false)]
    public void Guid_in_any_format_is_answered_under_the_requested_text(string format, bool upper)
    {
        var id = Guid.NewGuid();
        var text = id.ToString(format);
        if (upper) text = text.ToUpperInvariant();

        var answers = RequestedIdAnswers.Align([text], [Row(id, "found")], ConverterOf<Guid>());

        var answer = answers.ShouldHaveSingleItem();
        answer.Id.ShouldBe(text);
        answer.Values.Single().Value.ShouldBe("found");
    }

    [Fact]
    public void Several_spellings_of_the_same_entity_each_get_an_answer()
    {
        var id = Guid.NewGuid();
        var texts = new[] { id.ToString(), id.ToString().ToUpperInvariant(), id.ToString("N"), id.ToString("B") };

        var answers = RequestedIdAnswers.Align(texts, [Row(id)], ConverterOf<Guid>());

        Ids(answers).ShouldBe(texts.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("007", "7")]
    [InlineData(" 7 ", "7")]
    [InlineData("+7", "7")]
    public void Int_in_another_spelling_is_answered_under_the_requested_text(string text, string canonical)
    {
        var answers = RequestedIdAnswers.Align([text], [Row(int.Parse(canonical))], ConverterOf<int>());

        answers.ShouldHaveSingleItem().Id.ShouldBe(text);
    }

    [Fact]
    public void Long_ids_are_handled_like_int_ids()
    {
        var answers = RequestedIdAnswers.Align(["0005000000001"], [Row(5_000_000_001L)], ConverterOf<long>());

        answers.ShouldHaveSingleItem().Id.ShouldBe("0005000000001");
    }

    [Fact]
    public void String_ids_are_never_rewritten()
    {
        var answers = RequestedIdAnswers.Align(["Abc", "abc"], [Row("Abc")], ConverterOf<string>());

        answers.ShouldHaveSingleItem().Id.ShouldBe("Abc");
    }

    [Fact]
    public void Mixed_canonical_and_other_spellings_are_all_answered()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var texts = new[] { a.ToString(), b.ToString("N") };

        var answers = RequestedIdAnswers.Align(texts, [Row(a, "A"), Row(b, "B")], ConverterOf<Guid>());

        answers.Single(x => x.Id == a.ToString()).Values.Single().Value.ShouldBe("A");
        answers.Single(x => x.Id == b.ToString("N")).Values.Single().Value.ShouldBe("B");
    }

    [Fact]
    public void Requested_ids_without_a_row_and_unparsable_texts_get_no_answer()
    {
        var found = Guid.NewGuid();
        var texts = new[] { found.ToString("N"), Guid.NewGuid().ToString("N"), "not-a-guid", "" };

        var answers = RequestedIdAnswers.Align(texts, [Row(found)], ConverterOf<Guid>());

        answers.ShouldHaveSingleItem().Id.ShouldBe(found.ToString("N"));
    }

    [Fact]
    public void Null_ids_and_duplicates_in_the_request_are_ignored()
    {
        var id = Guid.NewGuid();
        var text = id.ToString("N");

        var answers = RequestedIdAnswers.Align([text, null!, text, text], [Row(id)], ConverterOf<Guid>());

        answers.ShouldHaveSingleItem().Id.ShouldBe(text);
    }

    [Fact]
    public void A_row_nobody_asked_for_is_dropped()
    {
        var asked = Guid.NewGuid();
        var other = Guid.NewGuid();

        var answers = RequestedIdAnswers.Align([asked.ToString("N")], [Row(asked), Row(other)], ConverterOf<Guid>());

        answers.ShouldHaveSingleItem().Id.ShouldBe(asked.ToString("N"));
    }

    [Fact]
    public void Answers_share_the_values_of_the_row()
    {
        var id = Guid.NewGuid();
        var row = Row(id);

        var answer = RequestedIdAnswers.Align([id.ToString("N")], [row], ConverterOf<Guid>()).Single();

        answer.Values.ShouldBeSameAs(row.Values);
    }

    private readonly record struct OrderNo(int Number)
    {
        public override string ToString() => $"ORD-{Number:0000}";
    }

    private sealed class OrderNoConverter : IStronglyTypeConverter<OrderNo>
    {
        public bool CanConvert(string input) =>
            input.Trim().StartsWith("ord-", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(input.Trim()[4..], out _);

        public OrderNo Convert(string input) => new(int.Parse(input.Trim()[4..]));
    }

    [Fact]
    public void Strongly_typed_ids_use_the_registered_converter()
    {
        var services = new ServiceCollection().AddSingleton<IStronglyTypeConverter<OrderNo>, OrderNoConverter>()
            .BuildServiceProvider();

        var answers = RequestedIdAnswers.Align(["ord-7", " ORD-0007 ", "ORD-0007"], [Row(new OrderNo(7))],
            ConverterOf<OrderNo>(services));

        Ids(answers).ShouldBe([" ORD-0007 ", "ORD-0007", "ord-7"]);
    }
}
