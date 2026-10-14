using System.Linq.Expressions;
using FxMap.Accessors.TypeAccessors;
using FxMap.Delegates;
using FxMap.Expressions.Building;
using FxMap.Tests.ProviderTests;
using FxMap.Tests.ProviderTests.Postgres;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Expressions;

/// <summary>
/// :offset and :limit reach the database as parameters, not as numbers written in the SQL: a number in the SQL text makes a query (and a
/// plan) of its own for each value. The SQL is generated only (no connection is opened).
/// </summary>
public class PagingParametersTests
{
    private static readonly GetTypeAccessor Accessors = type => new TypeAccessor(type, _ => null!);

    private static string Sql(string expression)
    {
        var options = new DbContextOptionsBuilder<ProviderDbContext>()
            .UseNpgsql("Host=localhost;Database=never_opened").Options;
        using var db = new ProviderDbContext(options);
        var builder = new ProjectionBuilder<ProviderCustomer>(
            (Expression<Func<ProviderCustomer, string>>)(x => x.Id), null!, Accessors);

        return db.Customers.AsNoTracking().Select(builder.Build([expression])).ToQueryString();
    }

    [Fact]
    public void Offset_and_limit_are_query_parameters()
    {
        var sql = Sql("Orders:orderBy(Total):offset(7):limit(3)");

        sql.ShouldContain("@__Offset_");
        sql.ShouldContain("@__Limit_");
        // ToQueryString writes the values of the parameters in comments; the numbers must not be in the statement itself
        sql.ShouldContain("='7'");
        sql.ShouldContain("='3'");
        sql.ShouldNotContain("7 <");
        sql.ShouldNotContain("<= 3");
    }

    [Fact]
    public void Another_number_gives_the_same_statement()
    {
        static string Statement(string sql) =>
            string.Join('\n', sql.Split('\n').Where(l => !l.StartsWith("--")));

        Statement(Sql("Orders:orderBy(Total):offset(7):limit(3)"))
            .ShouldBe(Statement(Sql("Orders:orderBy(Total):offset(20):limit(50)")));
    }
}
