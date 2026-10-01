using FxMap.Abstractions;
using FxMap.Fluent;
using Microsoft.EntityFrameworkCore;

namespace FxMap.Benchmark.Projection;

public sealed class CountryKey : IDistributedKey;

public sealed class ProvinceKey : IDistributedKey;

public sealed class CustomerKey : IDistributedKey;

public sealed class CategoryKey : IDistributedKey;

public sealed class ProductKey : IDistributedKey;

public class Country
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public class Province
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string CountryId { get; set; } = "";
    public Country Country { get; set; } = null!;
}

public class Customer
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string ProvinceId { get; set; } = "";
    public Province Province { get; set; } = null!;
}

public class Category
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public class Product
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string CategoryId { get; set; } = "";
    public Category Category { get; set; } = null!;
}

public class Order
{
    public string Id { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal Total { get; set; }
    public DateTime OrderDate { get; set; }
    public Customer Customer { get; set; } = null!;
    public List<OrderItem> Items { get; set; } = [];
}

public class OrderItem
{
    public string Id { get; set; } = "";
    public string OrderId { get; set; } = "";
    public string ProductId { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public Product Product { get; set; } = null!;
}

public class CountryConfig : EntityConfigureOf<Country>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Name);
        UseDistributedKey<CountryKey>();
    }
}

public class ProvinceConfig : EntityConfigureOf<Province>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Name);
        UseDistributedKey<ProvinceKey>();
    }
}

public class CustomerConfig : EntityConfigureOf<Customer>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Name);
        UseDistributedKey<CustomerKey>();
    }
}

public class CategoryConfig : EntityConfigureOf<Category>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Name);
        UseDistributedKey<CategoryKey>();
    }
}

public class ProductConfig : EntityConfigureOf<Product>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Name);
        UseDistributedKey<ProductKey>();
    }
}

public class ProjectionDbContext(DbContextOptions<ProjectionDbContext> options) : DbContext(options)
{
    public DbSet<Country> Countries { get; set; } = null!;
    public DbSet<Province> Provinces { get; set; } = null!;
    public DbSet<Customer> Customers { get; set; } = null!;
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<OrderItem> OrderItems { get; set; } = null!;
}
