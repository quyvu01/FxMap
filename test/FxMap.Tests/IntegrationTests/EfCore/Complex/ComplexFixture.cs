using FxMap.Abstractions;
using FxMap.EntityFrameworkCore.Extensions;
using FxMap.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FxMap.Tests.IntegrationTests.EfCore.Complex;

/// <summary>A seeded EF Core InMemory database wired to the real FxMap EF handlers.</summary>
public sealed class ComplexFixture : IDisposable
{
    private static readonly Type[] EntityConfigs =
    [
        typeof(CountryConfig), typeof(ProvinceConfig), typeof(CityConfig), typeof(CustomerConfig),
        typeof(CategoryConfig), typeof(ProductConfig), typeof(OrderConfig), typeof(TagConfig)
    ];

    private static readonly Type[] Profiles =
    [
        typeof(OrderViewProfile), typeof(CustomerSummaryViewProfile), typeof(OrderItemViewProfile),
        typeof(CustomerStatsViewProfile), typeof(CountryReportViewProfile), typeof(CategoryTreeViewProfile),
        typeof(ShipmentViewProfile), typeof(TagCardViewProfile), typeof(ProductCardViewProfile),
        typeof(OrderDateViewProfile), typeof(LooseIdViewProfile)
    ];

    private readonly ServiceProvider _provider;

    public DomainData Data { get; }
    public DisplayMode Mode { get; }

    private ComplexFixture(DomainData data, DisplayMode mode, ServiceProvider provider)
    {
        Data = data;
        Mode = mode;
        _provider = provider;
    }

    public static ComplexFixture Create(int seed, int orderCount, Action<FxMap.Registries.MapConfigurator> configure = null)
    {
        var data = DomainData.Generate(seed, orderCount);
        var mode = new DisplayMode();
        var services = new ServiceCollection();
        services.AddSingleton(mode);
        var dbName = $"Complex_{seed}_{orderCount}_{Guid.NewGuid()}";
        services.AddDbContext<ComplexDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddFxMap(cfg =>
            {
                cfg.AddEntityConfigs(EntityConfigs);
                cfg.AddProfileConfigs(Profiles);
                configure?.Invoke(cfg);
            })
            .AddEntityFrameworkCore(cfg => cfg.AddDbContexts(typeof(ComplexDbContext)));
        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ComplexDbContext>();
            db.AddRange(data.Countries);
            db.AddRange(data.Provinces);
            db.AddRange(data.Cities);
            db.AddRange(data.Categories);
            db.AddRange(data.Tags);
            db.AddRange(data.Products);
            db.AddRange(data.Customers);
            db.AddRange(data.Reviews);
            db.AddRange(data.Orders);
            db.SaveChanges();
        }

        return new ComplexFixture(data, mode, provider);
    }

    /// <summary>Runs <paramref name="action"/> inside a fresh scope, the way a request would.</summary>
    public async Task<T> InScope<T>(Func<IServiceProvider, IDistributedMapper, Task<T>> action)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await action(scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<IDistributedMapper>());
    }

    /// <summary>Loads orders as the "plain" DTOs a service would produce before enrichment (scalars and keys only).</summary>
    public Task<List<OrderView>> LoadOrderViews() => InScope(async (sp, _) =>
        await sp.GetRequiredService<ComplexDbContext>().Orders.AsNoTracking().OrderBy(o => o.OrderDate)
            .Select(o => new OrderView
            {
                Id = o.Id, CustomerId = o.CustomerId, Status = o.Status, Total = o.Total, OrderDate = o.OrderDate,
                Items = o.Items.Select(i => new OrderItemView
                    { Id = i.Id, ProductId = i.ProductId, Quantity = i.Quantity, UnitPrice = i.UnitPrice }).ToList()
            }).ToListAsync());

    public void Dispose() => _provider.Dispose();
}
