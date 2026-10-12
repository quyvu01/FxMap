using FxMap.Abstractions;
using FxMap.EntityFrameworkCore.Extensions;
using FxMap.Extensions;
using FxMap.Models;
using FxMap.Registries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxMap.Tests.IntegrationTests.EfCore;

/// <summary>
/// An owner that cannot build some expressions of a request: it fails the request (the default), or answers the others and
/// reports these ones (<c>IsolateExpressionErrors()</c>). EF Core InMemory is enough: the expressions fail when they are built.
/// </summary>
[Collection("EfCore Sequential")]
public class ExpressionErrorsEfCoreTests
{
    private static ServiceProvider Create(bool isolate)
    {
        var services = new ServiceCollection();
        var dbName = $"staff_{Guid.NewGuid()}";
        services.AddDbContext<StaffDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddFxMap(cfg =>
            {
                cfg.AddEntityConfigs(typeof(StaffConfig));
                cfg.AddProfileConfigs(typeof(StaffViewProfile));
            })
            .AddEntityFrameworkCore(c =>
            {
                c.AddDbContexts(typeof(StaffDbContext));
                if (isolate) c.IsolateExpressionErrors();
            });
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StaffDbContext>();
        db.Staff.AddRange(
            new StaffRow { Id = 1, Code = "A", Facility = "F1", Name = "Anna", Level = 3 },
            new StaffRow { Id = 2, Code = "B", Facility = "F1", Name = "Bao", Level = 2 });
        db.SaveChanges();
        return provider;
    }

    private static async Task<FxMap.Responses.ItemsResponse<FxMap.Responses.DataResponse>> Fetch(ServiceProvider provider,
        string[] ids, params string[] expressions)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IDistributedMapper>()
            .FetchDataAsync<StaffKey>(new DistributedMapRequest(ids, expressions));
    }

    [Fact]
    public async Task By_default_an_expression_that_cannot_be_built_fails_the_request()
    {
        using var provider = Create(isolate: false);

        await Should.ThrowAsync<Exception>(() => Fetch(provider, ["A:F1"], "Facility", "Nope.Thing"));
    }

    [Fact]
    public async Task When_isolated_the_other_expressions_are_answered_and_every_broken_one_is_reported()
    {
        using var provider = Create(isolate: true);

        var response = await Fetch(provider, ["A:F1", "B:F1"], "Facility", "Nope.Thing", "Level", "Staff:summ(Level)");

        response.Errors.Select(e => e.Expression).OrderBy(x => x, StringComparer.Ordinal)
            .ShouldBe(["Nope.Thing", "Staff:summ(Level)"]);
        response.Errors.ShouldAllBe(e => e.Message.Contains($"Expression '{e.Expression}' failed to build"));

        response.Items.Select(i => i.Id).OrderBy(x => x).ShouldBe(["A:F1", "B:F1"]);
        var anna = response.Items.Single(i => i.Id == "A:F1");
        anna.Values.Single(v => v.Expression == "Facility").Value.ShouldBe("\"F1\"");
        anna.Values.Single(v => v.Expression == "Level").Value.ShouldBe("3");
        anna.Values.Single(v => v.Expression == "Nope.Thing").Value.ShouldBeNull("no value for an expression that failed");
        anna.Values.Single(v => v.Expression == "Staff:summ(Level)").Value.ShouldBeNull();
    }

    [Fact]
    public async Task When_isolated_the_errors_are_reported_even_if_no_row_matches()
    {
        using var provider = Create(isolate: true);

        var response = await Fetch(provider, ["Z:Z"], "Facility", "Nope.Thing");

        response.Items.ShouldBeEmpty();
        response.Errors.ShouldHaveSingleItem().Expression.ShouldBe("Nope.Thing");
    }

    [Fact]
    public async Task When_isolated_a_request_without_broken_expressions_has_no_error()
    {
        using var provider = Create(isolate: true);

        var response = await Fetch(provider, ["A:F1"], "Facility");

        response.Errors.ShouldBeEmpty();
        response.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task The_same_list_of_expressions_gives_the_same_errors_the_second_time()
    {
        using var provider = Create(isolate: true);

        var first = await Fetch(provider, ["A:F1"], "Facility", "Nope.Thing");
        var second = await Fetch(provider, ["B:F1"], "Facility", "Nope.Thing");

        second.Errors.ShouldBe(first.Errors);
        second.Items.ShouldHaveSingleItem().Id.ShouldBe("B:F1");
    }
}
