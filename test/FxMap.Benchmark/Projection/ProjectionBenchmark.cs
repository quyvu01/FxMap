using System.Text.Json;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using BenchmarkDotNet.Attributes;
using FxMap.Abstractions;
using FxMap.EntityFrameworkCore.Extensions;
using FxMap.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FxMap.Benchmark.Projection;

/// <summary>
/// End-to-end: EF Core InMemory -> OrderDto (4 levels: Order > Customer > Province > Country, Order > Items > Product > Category).
/// AutoMapper does a single ProjectTo; FxMap loads the scalar DTO then enriches it locally (no transport).
/// </summary>
public abstract class ProjectionBenchmarkBase
{
    protected ServiceProvider _provider = null!;
    protected MapperConfiguration _mapperConfig = null!;
    public abstract int OrderCount { get; set; }

    protected virtual void Configure() { }

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        var dbName = $"Projection_{Guid.NewGuid()}";
        services.AddDbContext<ProjectionDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddFxMap(cfg =>
            {
                cfg.AddEntitiesFromAssemblyContaining<IBenchmarkAssemblyMarker>();
                cfg.AddProfilesFromAssemblyContaining<IBenchmarkAssemblyMarker>();
            })
            .AddEntityFrameworkCore(cfg => cfg.AddDbContexts(typeof(ProjectionDbContext)));
        _provider = services.BuildServiceProvider();

        _mapperConfig = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<OrderItem, OrderItemDto>()
                .ForMember(d => d.ProductName, o => o.MapFrom(s => s.Product.Name))
                .ForMember(d => d.CategoryId, o => o.MapFrom(s => s.Product.CategoryId))
                .ForMember(d => d.CategoryName, o => o.MapFrom(s => s.Product.Category.Name));
            cfg.CreateMap<Order, OrderDto>()
                .ForMember(d => d.CustomerName, o => o.MapFrom(s => s.Customer.Name))
                .ForMember(d => d.CustomerEmail, o => o.MapFrom(s => s.Customer.Email))
                .ForMember(d => d.ProvinceId, o => o.MapFrom(s => s.Customer.ProvinceId))
                .ForMember(d => d.ProvinceName, o => o.MapFrom(s => s.Customer.Province.Name))
                .ForMember(d => d.CountryName, o => o.MapFrom(s => s.Customer.Province.Country.Name));
        });

        Seed();
        Configure();
    }

    protected virtual Task CleanupCore() => Task.CompletedTask;

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await CleanupCore();
        _provider.Dispose();
    }

    private void Seed()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjectionDbContext>();
        var rnd = new Random(42);

        var countries = Enumerable.Range(0, 5)
            .Select(i => new Country { Id = $"co-{i}", Name = $"Country {i}" }).ToList();
        var provinces = Enumerable.Range(0, 20)
            .Select(i => new Province { Id = $"pr-{i}", Name = $"Province {i}", CountryId = countries[i % 5].Id })
            .ToList();
        var customers = Enumerable.Range(0, 50).Select(i => new Customer
        {
            Id = $"cu-{i}", Name = $"Customer {i}", Email = $"c{i}@x.com", ProvinceId = provinces[i % 20].Id
        }).ToList();
        var categories = Enumerable.Range(0, 10)
            .Select(i => new Category { Id = $"ca-{i}", Name = $"Category {i}" }).ToList();
        var products = Enumerable.Range(0, 100).Select(i => new Product
        {
            Id = $"po-{i}", Name = $"Product {i}", CategoryId = categories[i % 10].Id
        }).ToList();

        var orders = new List<Order>();
        for (var i = 0; i < OrderCount; i++)
        {
            var order = new Order
            {
                Id = $"or-{i}", CustomerId = customers[rnd.Next(customers.Count)].Id,
                Status = i % 2 == 0 ? "Done" : "Pending", OrderDate = new DateTime(2026, 1, 1).AddDays(i % 300)
            };
            var itemCount = rnd.Next(3, 6);
            for (var j = 0; j < itemCount; j++)
            {
                var item = new OrderItem
                {
                    Id = $"it-{i}-{j}", OrderId = order.Id, ProductId = products[rnd.Next(products.Count)].Id,
                    Quantity = rnd.Next(1, 5), Price = rnd.Next(1, 100)
                };
                order.Items.Add(item);
                order.Total += item.Quantity * item.Price;
            }

            orders.Add(order);
        }

        db.AddRange(countries);
        db.AddRange(provinces);
        db.AddRange(customers);
        db.AddRange(categories);
        db.AddRange(products);
        db.AddRange(orders);
        db.SaveChanges();
    }

}

[MemoryDiagnoser]
public class ProjectionBenchmark : ProjectionBenchmarkBase
{
    [Params(10, 100, 1000)] public override int OrderCount { get; set; }

    protected override void Configure() => VerifyEquivalent();

    private void VerifyEquivalent()
    {
        var a = Serialize(AutoMapperProjectTo().GetAwaiter().GetResult());
        var b = Serialize(FxMapLoadAndEnrich().GetAwaiter().GetResult());
        var c = Serialize(ManualSelect().GetAwaiter().GetResult());
        if (a != b) throw new InvalidOperationException("AutoMapper and FxMap results differ");
        if (a != c) throw new InvalidOperationException("AutoMapper and manual Select results differ");
    }

    private static string Serialize(List<OrderDto> dtos) =>
        JsonSerializer.Serialize(dtos.OrderBy(x => x.Id, StringComparer.Ordinal).ToList());

    [Benchmark(Baseline = true)]
    public async Task<List<OrderDto>> AutoMapperProjectTo()
    {
        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjectionDbContext>();
        return await db.Orders.AsNoTracking().ProjectTo<OrderDto>(_mapperConfig).ToListAsync();
    }

    [Benchmark]
    public async Task<List<OrderDto>> FxMapLoadAndEnrich()
    {
        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjectionDbContext>();
        var dtos = await ScalarProjection(db).ToListAsync();
        await scope.ServiceProvider.GetRequiredService<IDistributedMapper>().MapDataAsync(dtos);
        return dtos;
    }

    /// <summary>Lower bound for FxMap: only the scalar query, no enrichment (output is incomplete).</summary>
    [Benchmark]
    public async Task<List<OrderDto>> FxMapBaseQueryOnly()
    {
        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjectionDbContext>();
        return await ScalarProjection(db).ToListAsync();
    }

    /// <summary>Hand-written EF Select: the floor for the "single query" approach.</summary>
    [Benchmark]
    public async Task<List<OrderDto>> ManualSelect()
    {
        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjectionDbContext>();
        return await db.Orders.AsNoTracking().Select(o => new OrderDto
        {
            Id = o.Id, Status = o.Status, Total = o.Total, OrderDate = o.OrderDate,
            CustomerId = o.CustomerId, CustomerName = o.Customer.Name, CustomerEmail = o.Customer.Email,
            ProvinceId = o.Customer.ProvinceId, ProvinceName = o.Customer.Province.Name,
            CountryName = o.Customer.Province.Country.Name,
            Items = o.Items.Select(i => new OrderItemDto
            {
                Id = i.Id, Quantity = i.Quantity, Price = i.Price, ProductId = i.ProductId,
                ProductName = i.Product.Name, CategoryId = i.Product.CategoryId,
                CategoryName = i.Product.Category.Name
            }).ToList()
        }).ToListAsync();
    }

    private static IQueryable<OrderDto> ScalarProjection(ProjectionDbContext db) =>
        db.Orders.AsNoTracking().Select(o => new OrderDto
        {
            Id = o.Id, Status = o.Status, Total = o.Total, OrderDate = o.OrderDate, CustomerId = o.CustomerId,
            Items = o.Items.Select(i => new OrderItemDto
                { Id = i.Id, Quantity = i.Quantity, Price = i.Price, ProductId = i.ProductId }).ToList()
        });
}
