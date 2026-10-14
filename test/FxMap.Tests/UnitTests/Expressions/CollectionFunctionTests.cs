using System.Linq.Expressions;
using FxMap.Accessors.TypeAccessors;
using FxMap.Analyzers;
using FxMap.Delegates;
using FxMap.Expressions.Building;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Expressions;

/// <summary>:orderBy, :offset and :limit on a collection.</summary>
public class CollectionFunctionTests
{
    public class Entry
    {
        public string Name { get; set; } = "";
        public int Day { get; set; }
        public string By { get; set; } = "";
    }

    public class Owner
    {
        public string Id { get; set; } = "";
        public List<Entry> Entries { get; set; } = [];
    }

    private static readonly GetTypeAccessor Accessors = type => new TypeAccessor(type, _ => null!);

    private static readonly Owner Data = new()
    {
        Id = "O1",
        Entries =
        [
            new Entry { Name = "c", Day = 2, By = "z" },
            new Entry { Name = "a", Day = 2, By = "y" },
            new Entry { Name = "b", Day = 1, By = "x" },
            new Entry { Name = "d", Day = 3, By = "w" }
        ]
    };

    private static string[] Names(string expression)
    {
        var builder = new ProjectionBuilder<Owner>((Expression<Func<Owner, string>>)(x => x.Id), null!, Accessors);
        var row = builder.Build([expression]).Compile()(Data);
        return ((IEnumerable<Entry>)row[1]).Select(e => e.Name).ToArray();
    }

    [Theory]
    [InlineData("Entries:orderBy(Name)", "a,b,c,d")]
    [InlineData("Entries:orderBy(Name asc)", "a,b,c,d")]
    [InlineData("Entries:orderBy(Name desc)", "d,c,b,a")]
    [InlineData("Entries:orderBy(Day, By)", "b,a,c,d")]
    [InlineData("Entries:orderBy(Day, By desc)", "b,c,a,d")]
    [InlineData("Entries:orderBy(Day desc, Name asc)", "d,a,c,b")]
    [InlineData("Entries:orderBy(Day, Name, By desc)", "b,a,c,d")]
    public void OrderBy_orders_by_the_first_key_then_by_the_others(string expression, string expected)
    {
        string.Join(',', Names(expression)).ShouldBe(expected);
    }

    [Theory]
    [InlineData("Entries:offset(0)", "c,a,b,d")]
    [InlineData("Entries:offset(1)", "a,b,d")]
    [InlineData("Entries:offset(4)", "")]
    [InlineData("Entries:offset(10)", "")]
    [InlineData("Entries:limit(0)", "")]
    [InlineData("Entries:limit(2)", "c,a")]
    [InlineData("Entries:limit(10)", "c,a,b,d")]
    public void Offset_and_limit(string expression, string expected)
    {
        string.Join(',', Names(expression)).ShouldBe(expected);
    }

    [Fact]
    public void Offset_and_limit_are_a_page_of_the_ordered_collection()
    {
        string.Join(',', Names("Entries:orderBy(Name desc):offset(1):limit(2)")).ShouldBe("c,b");
    }

    [Fact]
    public void Offset_before_orderBy_orders_only_what_is_left()
    {
        string.Join(',', Names("Entries:offset(1):orderBy(Name)")).ShouldBe("a,b,d");
    }

    [Theory]
    [InlineData("Entries:offset(-1)")]
    [InlineData("Entries:limit(1.5)")]
    [InlineData("Entries:limit(Name)")]
    [InlineData("Entries:orderBy(Missing)")]
    public void A_wrong_argument_is_rejected(string expression)
    {
        Should.Throw<Exception>(() => Names(expression));
    }

    [Fact]
    public void OrderBy_parses_the_keys_with_their_directions()
    {
        var node = (FunctionNode)ExpressionParser.Parse("Entries:orderBy(Day, By desc)");

        node.FunctionName.ShouldBe(FunctionType.OrderBy);
        node.GetArguments().ShouldBe([
            new SortKeyNode("Day", OrderDirection.Asc), new SortKeyNode("By", OrderDirection.Desc)
        ]);
    }
}
