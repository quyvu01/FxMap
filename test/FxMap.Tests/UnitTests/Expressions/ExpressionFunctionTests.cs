using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using FxMap.Accessors.TypeAccessors;
using FxMap.Delegates;
using FxMap.Expressions.Building;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Expressions;

/// <summary>
/// Every function of the expression language, evaluated in memory by the LINQ builder. The expected values are the ones that
/// docs/expression.md gives, not what the code happens to answer.
/// </summary>
public class ExpressionFunctionTests
{
    public class Line
    {
        public string Status { get; set; } = "";
        public decimal Total { get; set; }
        public int Year { get; set; }
        public int Quantity { get; set; }
    }

    public class Address
    {
        public string City { get; set; } = "";
    }

    public class Person
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Padded { get; set; } = "";
        public string Tags { get; set; } = "";
        public string Nick { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime Born { get; set; }
        public decimal Price { get; set; }
        public decimal Tax { get; set; }
        public decimal Balance { get; set; }
        public int Value { get; set; }
        public int Quantity { get; set; }
        public int Score { get; set; }
        public bool Active { get; set; }
        public Address Address { get; set; }
        public List<Line> Orders { get; set; } = [];
        public List<Line> NoOrders { get; set; } = [];
    }

    private static readonly GetTypeAccessor Accessors = type => new TypeAccessor(type, _ => null!);

    private static Person Data() => new()
    {
        Id = "P1", Name = "John", LastName = "Doe", Padded = "  john  ", Tags = "a,b,c",
        CreatedAt = new DateTime(2024, 12, 25, 14, 30, 45), // a Wednesday
        Born = DateTime.Today.AddDays(-5),
        Price = 10.456m, Tax = 2m, Balance = -100m, Value = 10, Quantity = 3, Score = 85, Active = true,
        Orders =
        [
            new Line { Status = "Done", Total = 100, Year = 2024, Quantity = 1 },
            new Line { Status = "Done", Total = 200, Year = 2024, Quantity = 2 },
            new Line { Status = "Pending", Total = 300, Year = 2025, Quantity = 5 }
        ]
    };

    private static string Json(string expression, Person person = null)
    {
        var builder = new ProjectionBuilder<Person>((Expression<Func<Person, string>>)(x => x.Id), null!, Accessors);
        var row = builder.Build([expression]).Compile()(person ?? Data());
        return JsonSerializer.Serialize(row[1]);
    }

    /// <summary>The numbers are compared by value (100 and 100.0 are the same), the rest as it is.</summary>
    private static string Canonical(JsonNode node) => node switch
    {
        null => "null",
        JsonArray a => "[" + string.Join(",", a.Select(Canonical)) + "]",
        JsonObject o => "{" + string.Join(",", o.Select(p => $"\"{p.Key}\":{Canonical(p.Value)}")) + "}",
        JsonValue v when v.TryGetValue<decimal>(out var d) && v.GetValueKind() == JsonValueKind.Number =>
            d.ToString("0.#############################", System.Globalization.CultureInfo.InvariantCulture),
        _ => node.ToJsonString()
    };

    private static void Check(string expression, string expected)
    {
        Canonical(JsonNode.Parse(Json(expression))).ShouldBe(Canonical(JsonNode.Parse(expected)), expression);
    }

    [Theory]
    [InlineData("Name:upper", "\"JOHN\"")]
    [InlineData("Name:lower", "\"john\"")]
    [InlineData("Padded:trim", "\"john\"")]
    [InlineData("Name:substring(0, 3)", "\"Joh\"")]
    [InlineData("Name:substring(1)", "\"ohn\"")]
    [InlineData("Name:substring(1, 2)", "\"oh\"")]
    [InlineData("Name:replace('o', 'a')", "\"Jahn\"")]
    [InlineData("Name:concat(' ', LastName)", "\"John Doe\"")]
    [InlineData("Name:concat('-', 'x', '-', LastName)", "\"John-x-Doe\"")]
    [InlineData("Tags:split(',')", "[\"a\",\"b\",\"c\"]")]
    public void String_functions(string expression, string expected) => Check(expression, expected);

    [Theory]
    [InlineData("Padded:trim:upper:substring(0, 3)", "\"JOH\"")]
    [InlineData("Name:upper:lower", "\"john\"")]
    [InlineData("Name:concat(' ', LastName):upper", "\"JOHN DOE\"")]
    [InlineData("Name:replace('o', '0'):upper", "\"J0HN\"")]
    public void String_functions_chain(string expression, string expected) => Check(expression, expected);

    [Theory]
    [InlineData("CreatedAt:year", "2024")]
    [InlineData("CreatedAt:month", "12")]
    [InlineData("CreatedAt:day", "25")]
    [InlineData("CreatedAt:hour", "14")]
    [InlineData("CreatedAt:minute", "30")]
    [InlineData("CreatedAt:second", "45")]
    [InlineData("CreatedAt:dayOfWeek", "3")] // Sunday is 0
    [InlineData("Born:daysAgo", "5")]
    [InlineData("CreatedAt:format('yyyy-MM-dd')", "\"2024-12-25\"")]
    [InlineData("CreatedAt:format('dd/MM/yyyy HH:mm:ss')", "\"25/12/2024 14:30:45\"")]
    [InlineData("CreatedAt:year:add(1)", "2025")]
    public void Date_functions(string expression, string expected) => Check(expression, expected);

    [Theory]
    [InlineData("Price:round(2)", "10.46")]
    [InlineData("Price:round", "10")]
    [InlineData("Price:round(0)", "10")]
    [InlineData("Price:floor", "10")]
    [InlineData("Price:ceil", "11")]
    [InlineData("Balance:abs", "100")]
    [InlineData("Price:add(Tax)", "12.456")]
    [InlineData("Price:add(1)", "11.456")]
    [InlineData("Price:subtract(Tax)", "8.456")]
    [InlineData("Price:subtract(10)", "0.456")]
    [InlineData("Price:multiply(Quantity)", "31.368")]
    [InlineData("Price:multiply(2)", "20.912")]
    [InlineData("Price:divide(Tax)", "5.228")]
    [InlineData("Price:divide(2)", "5.228")]
    [InlineData("Value:mod(3)", "1")]
    [InlineData("Value:pow(2)", "100")]
    [InlineData("Price:add(Tax):round(2)", "12.46")]
    [InlineData("Value:add(5):multiply(2)", "30")]
    public void Math_functions(string expression, string expected) => Check(expression, expected);

    [Theory]
    [InlineData("Orders:count", "3")]
    [InlineData("Orders:sum(Total)", "600")]
    [InlineData("Orders:avg(Total)", "200")]
    [InlineData("Orders:min(Total)", "100")]
    [InlineData("Orders:max(Total)", "300")]
    [InlineData("Orders:where(Status = 'Done'):count", "2")]
    [InlineData("Orders:where(Status = 'Done'):sum(Total)", "300")]
    [InlineData("Orders:where(Year = 2025):avg(Total)", "300")]
    [InlineData("NoOrders:count", "0")]
    [InlineData("NoOrders:sum(Total)", "0")]
    [InlineData("Name:count", "4")]
    public void Aggregations(string expression, string expected) => Check(expression, expected);

    [Theory]
    [InlineData("Orders:any", "true")]
    [InlineData("NoOrders:any", "false")]
    [InlineData("Orders:any(Status = 'Done')", "true")]
    [InlineData("Orders:any(Total > 1000)", "false")]
    [InlineData("Orders:all(Total > 0)", "true")]
    [InlineData("Orders:all(Total > 100)", "false")]
    [InlineData("Orders:all(Status != 'Cancelled')", "true")]
    [InlineData("NoOrders:all(Total > 0)", "true")]
    public void Boolean_functions(string expression, string expected) => Check(expression, expected);

    [Theory]
    [InlineData("Orders:where(Total >= 200 && Status = 'Done'):count", "1")]
    [InlineData("Orders:where(Status = 'Done' || Total > 250):count", "3")]
    [InlineData("Orders:where(Status = 'Done', Total > 150):count", "1")] // a comma is AND
    [InlineData("Orders:where(Status != 'Done'):count", "1")]
    [InlineData("Orders:where(Status contains 'on'):count", "2")]
    [InlineData("Orders:where(Status startswith 'Do'):count", "2")]
    [InlineData("Orders:where(Status endswith 'ing'):count", "1")]
    [InlineData("Orders:where(Total < 200):count", "1")]
    [InlineData("Orders:where(Total <= 200):count", "2")]
    [InlineData("Orders:where(Total > 100 && Total < 300):count", "1")]
    [InlineData("Orders:where(Status:lower = 'done'):count", "2")]
    [InlineData("Orders:where(Status:upper = 'PENDING'):count", "1")]
    [InlineData("Orders:where(Quantity > 1):sum(Total)", "500")]
    public void Filters(string expression, string expected) => Check(expression, expected);

    [Theory]
    [InlineData("Nick ?? Name", "\"John\"")]
    [InlineData("Nick ?? Name ?? 'x'", "\"John\"")]
    [InlineData("Nick ?? 'Anonymous'", "\"Anonymous\"")]
    [InlineData("Address?.City ?? 'Unknown'", "\"Unknown\"")]
    [InlineData("Active = true ? 'Yes' : 'No'", "\"Yes\"")]
    [InlineData("Active = false ? 'Yes' : 'No'", "\"No\"")]
    [InlineData("Score >= 90 ? 'A' : Score >= 80 ? 'B' : Score >= 70 ? 'C' : 'F'", "\"B\"")]
    [InlineData("Score >= 95 ? 'A' : Score >= 80 ? 'B' : 'F'", "\"B\"")]
    [InlineData("Orders:any(Status = 'Pending') ? 'Has' : 'None'", "\"Has\"")]
    [InlineData("Orders:any(Status = 'Cancelled') ? 'Has' : 'None'", "\"None\"")]
    [InlineData("Orders:count > 2 ? 'Many' : 'Few'", "\"Many\"")]
    [InlineData("Name = 'John' && Score > 80 ? 'Hit' : 'Miss'", "\"Hit\"")]
    [InlineData("Name = 'John' && Score > 90 ? 'Hit' : 'Miss'", "\"Miss\"")]
    [InlineData("Name = 'Jane' || Score > 80 ? 'Hit' : 'Miss'", "\"Hit\"")]
    [InlineData("Name = 'Jane' || Score > 90 ? 'Hit' : 'Miss'", "\"Miss\"")]
    [InlineData("Name = 'Jane' && Score > 80 || Active = true ? 'Hit' : 'Miss'", "\"Hit\"")]  // && binds tighter than ||
    [InlineData("Name = 'John' || Name = 'Jane' && Score > 90 ? 'Hit' : 'Miss'", "\"Hit\"")]
    public void Coalesce_and_ternary(string expression, string expected) => Check(expression, expected);

    [Theory]
    [InlineData("{Id, Name}", "{\"Id\":\"P1\",\"Name\":\"John\"}")]
    [InlineData("{Id, Name as Given}", "{\"Id\":\"P1\",\"Given\":\"John\"}")]
    [InlineData("{Name:upper as Loud, Padded:trim as Clean}", "{\"Loud\":\"JOHN\",\"Clean\":\"john\"}")]
    [InlineData("{(Nick ?? Name) as DisplayName}", "{\"DisplayName\":\"John\"}")]
    [InlineData("{(Active = true ? 'Yes' : 'No') as IsActive}", "{\"IsActive\":\"Yes\"}")]
    [InlineData("{(Score >= 80 ? 'High' : 'Low') as Tier}", "{\"Tier\":\"High\"}")]
    [InlineData("{CreatedAt:format('yyyy-MM-dd') as Date}", "{\"Date\":\"2024-12-25\"}")]
    [InlineData("{Orders:count as N, Orders:sum(Total) as Sum}", "{\"N\":3,\"Sum\":600}")]
    [InlineData("Orders.{Status, Total}", "[{\"Status\":\"Done\",\"Total\":100},{\"Status\":\"Done\",\"Total\":200},{\"Status\":\"Pending\",\"Total\":300}]")]
    [InlineData("Orders.{Status, Total:multiply(2) as Double}", "[{\"Status\":\"Done\",\"Double\":200},{\"Status\":\"Done\",\"Double\":400},{\"Status\":\"Pending\",\"Double\":600}]")]
    public void Projections(string expression, string expected) => Check(expression, expected);

    [Theory]
    [InlineData("Orders:distinct(Status)", "[\"Done\",\"Pending\"]")]
    [InlineData("Orders:distinct(Year)", "[2024,2025]")]
    [InlineData("Orders:distinct(Status):count", "2")]
    [InlineData("Orders:where(Year = 2024):distinct(Status)", "[\"Done\"]")]
    public void Distinct(string expression, string expected) => Check(expression, expected);

    [Theory]
    [InlineData("Orders:groupBy(Status).{Status, :count as Count}",
        "[{\"Status\":\"Done\",\"Count\":2},{\"Status\":\"Pending\",\"Count\":1}]")]
    [InlineData("Orders:groupBy(Status).{Status, :count as Count, :sum(Total) as Revenue}",
        "[{\"Status\":\"Done\",\"Count\":2,\"Revenue\":300},{\"Status\":\"Pending\",\"Count\":1,\"Revenue\":300}]")]
    [InlineData("Orders:groupBy(Status).{Status, :avg(Total) as Avg, :min(Total) as Min, :max(Total) as Max}",
        "[{\"Status\":\"Done\",\"Avg\":150,\"Min\":100,\"Max\":200},{\"Status\":\"Pending\",\"Avg\":300,\"Min\":300,\"Max\":300}]")]
    [InlineData("Orders:groupBy(Year, Status).{Year, Status, :count as N}",
        "[{\"Year\":2024,\"Status\":\"Done\",\"N\":2},{\"Year\":2025,\"Status\":\"Pending\",\"N\":1}]")]
    [InlineData("Orders:where(Total > 100):groupBy(Status).{Status, :count as N}",
        "[{\"Status\":\"Done\",\"N\":1},{\"Status\":\"Pending\",\"N\":1}]")]
    public void Group_by(string expression, string expected) => Check(expression, expected);

    [Fact]
    public void A_null_safe_navigation_gives_null_when_the_nested_object_is_missing()
    {
        // (Address.City without the `?` is not null-safe: LINQ over objects throws; the databases answer null)
        Check("Address?.City", "null");
        var withAddress = Data();
        withAddress.Address = new Address { City = "Hanoi" };
        Canonical(JsonNode.Parse(Json("Address.City", withAddress))).ShouldBe("\"Hanoi\"");
        Canonical(JsonNode.Parse(Json("Address?.City", withAddress))).ShouldBe("\"Hanoi\"");
        Canonical(JsonNode.Parse(Json("Address?.City ?? 'Unknown'", withAddress))).ShouldBe("\"Hanoi\"");
    }

    [Theory]
    [InlineData("Missing")]
    [InlineData("Name:nope")]
    [InlineData("Orders:sum(Nope)")]
    [InlineData("Orders:where(Nope = 1)")]
    [InlineData("Name:substring('x')")]
    public void A_wrong_expression_fails_instead_of_answering_something(string expression)
    {
        Should.Throw<Exception>(() => Json(expression));
    }
}
