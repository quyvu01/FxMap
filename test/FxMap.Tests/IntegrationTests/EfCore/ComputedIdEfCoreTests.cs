using FxMap.Abstractions;
using FxMap.EntityFrameworkCore.Extensions;
using FxMap.Extensions;
using FxMap.Fluent;
using FxMap.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxMap.Tests.IntegrationTests.EfCore;

public sealed class StaffKey : IDistributedKey;

public class StaffRow
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Facility { get; set; } = "";
    public string Name { get; set; } = "";
    public int Level { get; set; }
}

public class StaffView
{
    public string StaffId { get; set; } = "";
    public string Name { get; set; }
    public string Facility { get; set; }
}

/// <summary>The id is a computed value: Code and Facility together.</summary>
internal sealed class StaffConfig : EntityConfigureOf<StaffRow>
{
    protected override void Configure()
    {
        Id(x => x.Code + ":" + x.Facility);
        DefaultProperty(x => x.Name + " (" + x.Level + ")");
        UseDistributedKey<StaffKey>();
    }
}

internal sealed class StaffViewProfile : ProfileOf<StaffView>
{
    protected override void Configure() =>
        UseDistributedKey<StaffKey>().Of(x => x.StaffId)
            .For(x => x.Name)
            .For(x => x.Facility, "Facility");
}

public class StaffDbContext(DbContextOptions<StaffDbContext> options) : DbContext(options)
{
    public DbSet<StaffRow> Staff { get; set; } = null!;
}

/// <summary>Id and DefaultProperty declared as lambdas, including computed ones, on EF Core InMemory.</summary>
[Collection("EfCore Sequential")]
public class ComputedIdEfCoreTests : IDisposable
{
    private readonly ServiceProvider _provider;

    public ComputedIdEfCoreTests()
    {
        var services = new ServiceCollection();
        var dbName = $"staff_{Guid.NewGuid()}";
        services.AddDbContext<StaffDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddFxMap(cfg =>
            {
                cfg.AddEntityConfigs(typeof(StaffConfig));
                cfg.AddProfileConfigs(typeof(StaffViewProfile));
                cfg.ThrowIfException();
            })
            .AddEntityFrameworkCore(c => c.AddDbContexts(typeof(StaffDbContext)));
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StaffDbContext>();
        db.Staff.AddRange(
            new StaffRow { Id = 1, Code = "A", Facility = "F1", Name = "Anna", Level = 3 },
            new StaffRow { Id = 2, Code = "A", Facility = "F2", Name = "Anh", Level = 2 },
            new StaffRow { Id = 3, Code = "B", Facility = "F1", Name = "Bao", Level = 3 });
        db.SaveChanges();
    }

    private async Task Map(object value)
    {
        await using var scope = _provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDistributedMapper>().MapDataAsync(value);
    }

    [Fact]
    public async Task A_computed_id_finds_the_row_with_that_combination()
    {
        var views = new[]
        {
            new StaffView { StaffId = "A:F1" }, new StaffView { StaffId = "A:F2" }, new StaffView { StaffId = "B:F1" },
            new StaffView { StaffId = "B:F2" }
        };

        await Map(views);

        views.Select(v => v.Facility).ShouldBe(["F1", "F2", "F1", null]);
        views[0].Name.ShouldBe("Anna (3)", "the default property is computed as well");
        views[1].Name.ShouldBe("Anh (2)");
        views[3].Name.ShouldBeNull("no row has that combination");
    }

    [Fact]
    public async Task The_response_ids_are_the_computed_values()
    {
        await using var scope = _provider.CreateAsyncScope();
        var mapper = scope.ServiceProvider.GetRequiredService<IDistributedMapper>();

        var response = await mapper.FetchDataAsync<StaffKey>(new DistributedMapRequest(["A:F1", "B:F1"], ["Name"]));

        response.Items.Select(i => i.Id).OrderBy(x => x).ShouldBe(["A:F1", "B:F1"]);
    }

    public void Dispose() => _provider.Dispose();
}
