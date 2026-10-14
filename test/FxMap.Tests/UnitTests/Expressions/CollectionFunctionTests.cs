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

    private static Entry One(string expression)
    {
        var builder = new ProjectionBuilder<Owner>((Expression<Func<Owner, string>>)(x => x.Id), null!, Accessors);
        return (Entry)builder.Build([expression]).Compile()(Data)[1];
    }

    [Theory]
    [InlineData("Entries:first", "c")]
    [InlineData("Entries:last", "d")]
    [InlineData("Entries:orderBy(Name):first", "a")]
    [InlineData("Entries:orderBy(Name):last", "d")]
    [InlineData("Entries:orderBy(Day desc, Name):first", "d")]
    [InlineData("Entries:orderBy(Day, By desc):last", "d")]
    [InlineData("Entries:offset(1):first", "a")]
    [InlineData("Entries:limit(2):last", "a")]
    public void First_and_last_give_one_item(string expression, string expected)
    {
        One(expression).Name.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Entries:first")]
    [InlineData("Entries:last")]
    public void First_and_last_of_an_empty_collection_are_null(string expression)
    {
        var empty = new Owner { Id = "O2" };
        var builder = new ProjectionBuilder<Owner>((Expression<Func<Owner, string>>)(x => x.Id), null!, Accessors);

        builder.Build([expression]).Compile()(empty)[1].ShouldBeNull();
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
    [InlineData("Entries:first(Name)")]
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

    private static string Json(string expression)
    {
        var builder = new ProjectionBuilder<Owner>((Expression<Func<Owner, string>>)(x => x.Id), null!, Accessors);
        return System.Text.Json.JsonSerializer.Serialize(builder.Build([expression]).Compile()(Data)[1]);
    }

    // Data, in the order of the list: c(Day 2, z), a(Day 2, y), b(Day 1, x), d(Day 3, w)
    [Theory]
    // order, page, then project the page
    [InlineData("Entries:orderBy(Day, By desc):offset(1):limit(2).{Name, Day}",
        """[{"Name":"c","Day":2},{"Name":"a","Day":2}]""")]
    [InlineData("Entries:orderBy(Name):limit(2).{Name}", """[{"Name":"a"},{"Name":"b"}]""")]
    // one item, then project it or read one property of it
    [InlineData("Entries:orderBy(Name desc):first.{Name, Day}", """{"Name":"d","Day":3}""")]
    [InlineData("Entries:orderBy(Name):first.{Name}", """{"Name":"a"}""")]
    [InlineData("Entries:orderBy(Name):first.Name", "\"a\"")]
    // a filter before the order
    [InlineData("Entries(Day > 1):orderBy(Name desc):limit(1)", """[{"Name":"d","Day":3,"By":"w"}]""")]
    // a filter before the order, then the projection or a property of the item
    [InlineData("Entries(Day > 1):orderBy(Name desc):first.{Name}", """{"Name":"d"}""")]
    [InlineData("Entries(Day > 1):orderBy(Name desc):first.{Name, Day}", """{"Name":"d","Day":3}""")]
    [InlineData("Entries(Day > 1):orderBy(Name):limit(2).{Name}", """[{"Name":"a"},{"Name":"c"}]""")]
    [InlineData("Entries(Day > 1):first.{Name}", """{"Name":"c"}""")]
    [InlineData("Entries(Day > 1):orderBy(Name desc):first.Name", "\"d\"")]
    // aggregate or distinct values of a page
    [InlineData("Entries:orderBy(Name):limit(2):count", "2")]
    [InlineData("Entries:orderBy(Name):limit(3):sum(Day)", "5")]
    [InlineData("Entries:orderBy(Name):offset(1):limit(2):distinct(By)", """["x","z"]""")]
    public void Functions_chain_with_projections_and_aggregates(string expression, string expected)
    {
        Json(expression).ShouldBe(expected);
    }

    [Theory]
    [InlineData("Entries(Day > 1):orderBy(Name) garbage")]
    [InlineData("Entries:first )")]
    [InlineData("Entries:limit(2) Name")]
    [InlineData("Name }")]
    public void Text_that_is_left_after_the_expression_is_an_error(string expression)
    {
        Should.Throw<ExpressionParseException>(() => ExpressionParser.Parse(expression));
    }

    // The indexer [skip take asc|desc Property] together with the functions.
    // Data, ordered by Name: a(Day 2), b(1), c(2), d(3); ordered by Day desc (stable): d, c, a, b
    [Theory]
    // indexer first, then the functions on its result
    [InlineData("Entries[asc Name]:offset(1):limit(2)",
        """[{"Name":"b","Day":1,"By":"x"},{"Name":"c","Day":2,"By":"z"}]""")]
    [InlineData("Entries[desc Name]:limit(2).{Name}", """[{"Name":"d"},{"Name":"c"}]""")]
    [InlineData("Entries[0 3 desc Day]:count", "3")]
    [InlineData("Entries[0 3 desc Day]:first.{Name}", """{"Name":"d"}""")]
    [InlineData("Entries[0 3 desc Day]:orderBy(Name):limit(2).{Name}", """[{"Name":"a"},{"Name":"c"}]""")]
    [InlineData("Entries[1 2 asc Name]:last", """{"Name":"c","Day":2,"By":"z"}""")]
    // a filter, the indexer, then the functions or the projection
    [InlineData("Entries(Day > 1)[0 asc Name].{Name}", """{"Name":"a"}""")]
    [InlineData("Entries(Day > 1)[asc Name]:limit(1).{Name}", """[{"Name":"a"}]""")]
    // an item of the indexer, projected or read
    [InlineData("Entries[0 asc Name].{Name, Day}", """{"Name":"a","Day":2}""")]
    [InlineData("Entries[-1 asc Name].Name", "\"d\"")]
    // functions first, then the indexer, which orders again by its own property
    [InlineData("Entries:offset(1)[0 asc Name].{Name}", """{"Name":"a"}""")]
    [InlineData("Entries:orderBy(Day desc, Name)[0 asc Name]", """{"Name":"a","Day":2,"By":"y"}""")]
    [InlineData("Entries:orderBy(Name):limit(3)[-1 asc Day].{Name, Day}", """{"Name":"c","Day":2}""")]
    public void Functions_and_the_indexer_chain(string expression, string expected)
    {
        Json(expression).ShouldBe(expected);
    }

    [Fact]
    public void A_function_of_a_collection_after_an_indexer_of_one_item_is_rejected()
    {
        // Entries[2 asc Name] is one Entry, not a collection
        Should.Throw<InvalidOperationException>(() => Json("Entries[2 asc Name]:orderBy(Day)"))
            .Message.ShouldContain("non-collection");
    }
}
