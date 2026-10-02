using FxMap.Abstractions;
using FxMap.EntityFrameworkCore.Extensions;
using FxMap.Extensions;
using FxMap.Fluent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxMap.Tests.IntegrationTests.EfCore;

public sealed class NuItemKey : IDistributedKey;

public sealed class NuGroupKey : IDistributedKey;

/// <summary>Item 1 has code A; items 2 and 3 share code B, so Code is a non-unique lookup key.</summary>
public class NuItem
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

/// <summary>Same idea with a Guid lookup key, to check the spellings of the key.</summary>
public class NuGroupRow
{
    public int Id { get; set; }
    public Guid GroupId { get; set; }
    public string Label { get; set; } = "";
}

public class NuElement
{
    public int ItemId { get; set; }
    public string? Name { get; set; }
}

public class NuView
{
    public string Code { get; set; } = "";
    public string? Name { get; set; }
    public List<NuElement>? Items { get; set; }
}

public class NuGroupView
{
    public string GroupId { get; set; } = "";
    public List<NuElement>? Rows { get; set; }
}

internal sealed class NuItemConfig : EntityConfigureOf<NuItem>
{
    protected override void Configure()
    {
        Id(x => x.Code);
        DefaultProperty(x => x.Name);
        UseDistributedKey<NuItemKey>();
    }
}

internal sealed class NuGroupRowConfig : EntityConfigureOf<NuGroupRow>
{
    protected override void Configure()
    {
        Id(x => x.GroupId);
        DefaultProperty(x => x.Label);
        UseDistributedKey<NuGroupKey>();
    }
}

internal sealed class NuViewProfile : ProfileOf<NuView>
{
    protected override void Configure() =>
        UseDistributedKey<NuItemKey>().Of(x => x.Code)
            .For(x => x.Name, "Name")
            .Collection(x => x.Items, i => i.For(x => x.ItemId, "Id").For(x => x.Name, "Name"));
}

internal sealed class NuGroupViewProfile : ProfileOf<NuGroupView>
{
    protected override void Configure() =>
        UseDistributedKey<NuGroupKey>().Of(x => x.GroupId)
            .Collection(x => x.Rows, i => i.For(x => x.ItemId, "Id").For(x => x.Name, "Label"));
}

public class NuDbContext(DbContextOptions<NuDbContext> options) : DbContext(options)
{
    public DbSet<NuItem> Items { get; set; } = null!;
    public DbSet<NuGroupRow> GroupRows { get; set; } = null!;
}

/// <summary>A key that matches several rows, end to end on EF Core InMemory: one element per row.</summary>
[Collection("EfCore Sequential")]
public class CollectionMappingEfCoreTests : IDisposable
{
    private static readonly Guid GroupG = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid GroupH = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private readonly ServiceProvider _provider;

    public CollectionMappingEfCoreTests()
    {
        var services = new ServiceCollection();
        var dbName = $"nu_{Guid.NewGuid()}";
        services.AddDbContext<NuDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddFxMap(cfg =>
            {
                cfg.AddEntityConfigs(typeof(NuItemConfig), typeof(NuGroupRowConfig));
                cfg.AddProfileConfigs(typeof(NuViewProfile), typeof(NuGroupViewProfile));
            })
            .AddEntityFrameworkCore(c => c.AddDbContexts(typeof(NuDbContext)));
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NuDbContext>();
        db.Items.AddRange(
            new NuItem { Id = 1, Code = "A", Name = "item-1" },
            new NuItem { Id = 2, Code = "B", Name = "item-2" },
            new NuItem { Id = 3, Code = "B", Name = "item-3" });
        db.GroupRows.AddRange(
            new NuGroupRow { Id = 10, GroupId = GroupG, Label = "g-1" },
            new NuGroupRow { Id = 11, GroupId = GroupH, Label = "h-1" },
            new NuGroupRow { Id = 12, GroupId = GroupG, Label = "g-2" },
            new NuGroupRow { Id = 13, GroupId = GroupG, Label = "g-3" });
        db.SaveChanges();
    }

    private async Task Map(object value)
    {
        await using var scope = _provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDistributedMapper>().MapDataAsync(value);
    }

    [Fact]
    public async Task Code_with_two_rows_gives_two_elements_and_code_with_one_row_gives_one()
    {
        var views = new List<NuView> { new() { Code = "A" }, new() { Code = "B" } };

        await Map(views);

        views[0].Items!.Select(i => (i.ItemId, i.Name)).ShouldBe([(1, "item-1")]);
        views[1].Items!.Select(i => (i.ItemId, i.Name)).ShouldBe([(2, "item-2"), (3, "item-3")]);
    }

    [Fact]
    public async Task A_plain_rule_on_the_same_key_gets_the_first_row()
    {
        var views = new List<NuView> { new() { Code = "A" }, new() { Code = "B" } };

        await Map(views);

        views[0].Name.ShouldBe("item-1");
        views[1].Name.ShouldBe("item-2");
    }

    [Fact]
    public async Task Several_objects_with_the_same_code_each_get_all_the_rows()
    {
        var views = new List<NuView> { new() { Code = "B" }, new() { Code = "B" }, new() { Code = "A" } };

        await Map(views);

        views[0].Items!.Count.ShouldBe(2);
        views[1].Items!.Count.ShouldBe(2);
        views[0].Items.ShouldNotBeSameAs(views[1].Items);
        views[2].Items!.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_code_without_rows_leaves_the_collection_untouched()
    {
        var view = new NuView { Code = "nope" };

        await Map(view);

        view.Items.ShouldBeNull();
        view.Name.ShouldBeNull();
    }

    [Fact]
    public async Task The_server_returns_one_response_per_row()
    {
        await using var scope = _provider.CreateAsyncScope();
        var mapper = scope.ServiceProvider.GetRequiredService<IDistributedMapper>();

        var response = await mapper.FetchDataAsync<NuItemKey>(new FxMap.Models.DistributedMapRequest(["A", "B"],
            ["Name"]));

        response.Items.Select(i => i.Id).ShouldBe(["A", "B", "B"]);
    }

    [Fact]
    public async Task A_guid_key_in_other_spellings_gets_all_of_its_rows()
    {
        var views = new List<NuGroupView>
        {
            new() { GroupId = GroupG.ToString() },
            new() { GroupId = GroupG.ToString().ToUpperInvariant() },
            new() { GroupId = GroupG.ToString("N") },
            new() { GroupId = GroupH.ToString("B") }
        };

        await Map(views);

        views.Take(3).ShouldAllBe(v => v.Rows!.Select(r => r.Name).SequenceEqual(new[] { "g-1", "g-2", "g-3" }));
        views[3].Rows!.Select(r => r.Name).ShouldBe(["h-1"]);
    }

    public void Dispose() => _provider.Dispose();
}
