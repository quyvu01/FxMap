using FxMap.Abstractions;
using FxMap.EntityFrameworkCore.Extensions;
using FxMap.Extensions;
using FxMap.Fluent;
using FxMap.Fluent.Rules;
using FxMap.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxMap.Tests.IntegrationTests.EfCore;

public sealed class NuVisitKey : IDistributedKey;

public class NuVisit
{
    public int Id { get; set; }
    public string Mrn { get; set; } = "";
    public DateTime VisitedAt { get; set; }
    public string Status { get; set; } = "";
}

public class NuVisitView
{
    public int VisitId { get; set; }
    public DateTime When { get; set; }
    public string Status { get; set; }
}

public class NuPatient
{
    public string Mrn { get; set; } = "";
    public List<NuVisitView>? Latest { get; set; }
    public List<NuVisitView>? Oldest { get; set; }
    public List<NuVisitView>? ByStatus { get; set; }
    public List<NuVisitView>? Some { get; set; }
    public List<NuVisitView>? All { get; set; }
}

internal sealed class NuVisitConfig : EntityConfigureOf<NuVisit>
{
    protected override void Configure()
    {
        Id(x => x.Mrn);
        DefaultProperty(x => x.Status);
        ExposedName(x => x.VisitedAt, "When");
        UseDistributedKey<NuVisitKey>();
    }
}

internal sealed class NuPatientProfile : ProfileOf<NuPatient>
{
    protected override void Configure() =>
        UseDistributedKey<NuVisitKey>().Of(x => x.Mrn)
            .Collection(x => x.Latest, v => v.For(x => x.VisitId, "Id").For(x => x.When, "When")
                .OrderByDescending("When").ThenBy("Id").Limit(2))
            .Collection(x => x.Oldest, v => v.For(x => x.VisitId, "Id").OrderBy("When").ThenBy("Id").Limit(1))
            .Collection(x => x.ByStatus, v => v.For(x => x.VisitId, "Id").For(x => x.Status, "Status")
                .OrderBy("Status").ThenByDescending("When").ThenBy("Id"))
            .Collection(x => x.Some, v => v.For(x => x.VisitId, "Id").Limit(3))
            .Collection(x => x.All, v => v.For(x => x.VisitId, "Id"));
}

internal sealed class NuPatientBadOrderProfile : ProfileOf<NuBadPatient>
{
    protected override void Configure() =>
        UseDistributedKey<NuVisitKey>().Of(x => x.Mrn)
            .Collection(x => x.Visits, v => v.For(x => x.VisitId, "Id").OrderBy("NoSuchProperty"));
}

public class NuBadPatient
{
    public string Mrn { get; set; } = "";
    public List<NuVisitView>? Visits { get; set; }
}

public class NuVisitDbContext(DbContextOptions<NuVisitDbContext> options) : DbContext(options)
{
    public DbSet<NuVisit> Visits { get; set; } = null!;
}

/// <summary>The server cuts and sorts the rows of each key (EF Core InMemory), and the mapper builds the collections from them.</summary>
[Collection("EfCore Sequential")]
public class CollectionOrderAndLimitEfCoreTests : IDisposable
{
    private readonly List<NuVisit> _visits = [];
    private readonly ServiceProvider _provider;
    private readonly ServiceProvider _badOrderProvider;

    private static readonly DateTime Day = new(2026, 1, 1);

    public CollectionOrderAndLimitEfCoreTests()
    {
        // M1: five visits with two on the same day (ties); M2: two; M9: three; M3: none
        _visits.AddRange(
        [
            new NuVisit { Id = 1, Mrn = "M1", VisitedAt = Day.AddDays(3), Status = "Open" },
            new NuVisit { Id = 2, Mrn = "M1", VisitedAt = Day.AddDays(9), Status = "Done" },
            new NuVisit { Id = 3, Mrn = "M1", VisitedAt = Day.AddDays(5), Status = "Open" },
            new NuVisit { Id = 4, Mrn = "M1", VisitedAt = Day.AddDays(9), Status = "Cancelled" },
            new NuVisit { Id = 5, Mrn = "M1", VisitedAt = Day.AddDays(1), Status = "Done" },
            new NuVisit { Id = 6, Mrn = "M2", VisitedAt = Day.AddDays(2), Status = "Open" },
            new NuVisit { Id = 7, Mrn = "M2", VisitedAt = Day.AddDays(7), Status = "Done" },
            new NuVisit { Id = 8, Mrn = "M9", VisitedAt = Day.AddDays(4), Status = "Open" },
            new NuVisit { Id = 9, Mrn = "M9", VisitedAt = Day.AddDays(6), Status = "Open" },
            new NuVisit { Id = 10, Mrn = "M9", VisitedAt = Day.AddDays(8), Status = "Done" }
        ]);
        _provider = Build(typeof(NuPatientProfile), false);
        _badOrderProvider = Build(typeof(NuPatientBadOrderProfile), true);
    }

    private ServiceProvider Build(Type profile, bool throwIfException)
    {
        var services = new ServiceCollection();
        var dbName = $"nuvisit_{Guid.NewGuid()}";
        services.AddDbContext<NuVisitDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddFxMap(cfg =>
            {
                cfg.AddEntityConfigs(typeof(NuVisitConfig));
                cfg.AddProfileConfigs(profile);
                if (throwIfException) cfg.ThrowIfException();
            })
            .AddEntityFrameworkCore(c => c.AddDbContexts(typeof(NuVisitDbContext)));
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NuVisitDbContext>();
        db.Visits.AddRange(_visits.Select(v => new NuVisit
            { Id = v.Id, Mrn = v.Mrn, VisitedAt = v.VisitedAt, Status = v.Status }));
        db.SaveChanges();
        return provider;
    }

    private static async Task Map(ServiceProvider provider, object value)
    {
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDistributedMapper>().MapDataAsync(value);
    }

    private IEnumerable<NuVisit> VisitsOf(string mrn) => _visits.Where(v => v.Mrn == mrn);

    #region End to end

    [Theory]
    [InlineData("M1")]
    [InlineData("M2")]
    [InlineData("M9")]
    public async Task Every_collection_gets_its_own_order_and_limit_for_each_mrn(string mrn)
    {
        var patient = new NuPatient { Mrn = mrn };

        await Map(_provider, patient);

        var visits = VisitsOf(mrn).ToList();
        patient.Latest!.Select(v => v.VisitId)
            .ShouldBe(visits.OrderByDescending(v => v.VisitedAt).ThenBy(v => v.Id).Take(2).Select(v => v.Id));
        patient.Oldest!.Select(v => v.VisitId)
            .ShouldBe(visits.OrderBy(v => v.VisitedAt).ThenBy(v => v.Id).Take(1).Select(v => v.Id));
        patient.ByStatus!.Select(v => v.VisitId).ShouldBe(visits
            .OrderBy(v => v.Status, StringComparer.Ordinal).ThenByDescending(v => v.VisitedAt).ThenBy(v => v.Id)
            .Select(v => v.Id));
        patient.All!.Select(v => v.VisitId).OrderBy(x => x).ShouldBe(visits.Select(v => v.Id).OrderBy(x => x));
        patient.Some!.Count.ShouldBe(Math.Min(3, visits.Count));
    }

    [Fact]
    public async Task The_limit_is_applied_to_each_mrn_not_to_the_whole_result()
    {
        var patients = new[] { new NuPatient { Mrn = "M1" }, new NuPatient { Mrn = "M2" }, new NuPatient { Mrn = "M9" } };

        await Map(_provider, patients);

        patients.Select(p => p.Latest!.Count).ShouldBe([2, 2, 2]);
        patients.Select(p => p.Oldest!.Count).ShouldBe([1, 1, 1]);
        patients[0].Latest!.Select(v => v.VisitId).ShouldBe([2, 4], "ties on the date are broken by Id");
    }

    [Fact]
    public async Task An_mrn_without_visits_leaves_every_collection_untouched()
    {
        var patient = new NuPatient { Mrn = "M3" };

        await Map(_provider, patient);

        patient.Latest.ShouldBeNull();
        patient.All.ShouldBeNull();
    }

    [Fact]
    public async Task The_exposed_name_works_as_an_order_property_and_the_date_is_mapped()
    {
        var patient = new NuPatient { Mrn = "M1" };

        await Map(_provider, patient);

        patient.Latest!.Select(v => v.When).ShouldBe([Day.AddDays(9), Day.AddDays(9)]);
        patient.Oldest!.Single().When.ShouldBe(default, "Oldest does not map When; only its Id");
    }

    #endregion

    #region The server cuts the rows itself

    [Fact]
    public async Task The_server_returns_only_the_limited_rows_in_the_requested_order()
    {
        await using var scope = _provider.CreateAsyncScope();
        var mapper = scope.ServiceProvider.GetRequiredService<IDistributedMapper>();
        var options = new CollectionOptions([new CollectionOrder("When", true), new CollectionOrder("Id", false)], 2);

        var response = await mapper.FetchDataAsync<NuVisitKey>(
            new DistributedMapRequest(["M1", "M2", "M9"], ["Id"]) { Collection = options });

        response.Items.Select(i => (i.Id, i.Values.Single().Value)).OrderBy(x => x.Id).ThenBy(x => x.Item2).ShouldBe(
        [
            ("M1", "2"), ("M1", "4"), ("M2", "6"), ("M2", "7"), ("M9", "10"), ("M9", "9")
        ]);
        // M1: ids 2 and 4 (both on day 9), M2: both rows, M9: the two latest
        response.Items.Count(i => i.Id == "M1").ShouldBe(2);
    }

    [Fact]
    public async Task Without_options_the_server_returns_every_row()
    {
        await using var scope = _provider.CreateAsyncScope();
        var mapper = scope.ServiceProvider.GetRequiredService<IDistributedMapper>();

        var response = await mapper.FetchDataAsync<NuVisitKey>(new DistributedMapRequest(["M1"], ["Id"]));

        response.Items.Length.ShouldBe(5);
    }

    [Fact]
    public async Task Rows_come_back_in_the_requested_direction_for_a_descending_single_key()
    {
        await using var scope = _provider.CreateAsyncScope();
        var mapper = scope.ServiceProvider.GetRequiredService<IDistributedMapper>();
        var options = new CollectionOptions([new CollectionOrder("Id", true)], null);

        var response = await mapper.FetchDataAsync<NuVisitKey>(
            new DistributedMapRequest(["M1"], ["Id"]) { Collection = options });

        response.Items.Select(i => i.Values.Single().Value).ShouldBe(["5", "4", "3", "2", "1"]);
    }

    #endregion

    #region Errors

    [Fact]
    public async Task An_unknown_order_property_is_reported_when_configured_to_throw()
    {
        var patient = new NuBadPatient { Mrn = "M1" };

        var error = await Should.ThrowAsync<InvalidOperationException>(() => Map(_badOrderProvider, patient));

        error.Message.ShouldContain("NoSuchProperty");
    }

    #endregion

    public void Dispose()
    {
        _provider.Dispose();
        _badOrderProvider.Dispose();
    }
}
