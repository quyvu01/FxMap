using FxMap.Abstractions;
using FxMap.Fluent;
using Microsoft.EntityFrameworkCore;

namespace FxMap.Tests.IntegrationTests.EfCore.Complex;

// Entity configs and profiles are INTERNAL: the scanners of the other tests only look at exported types.

#region Distributed keys

public sealed class KCountry : IDistributedKey;

public sealed class KProvince : IDistributedKey;

public sealed class KCity : IDistributedKey;

public sealed class KCustomer : IDistributedKey;

public sealed class KCategory : IDistributedKey;

public sealed class KProduct : IDistributedKey;

public sealed class KOrder : IDistributedKey;

public sealed class KTag : IDistributedKey;

#endregion

public enum Tier
{
    Basic = 0,
    Silver = 1,
    Gold = 2
}

// Id types are deliberately mixed: int, Guid, string, long, Guid?, int?.
public class DCountry
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<DProvince> Provinces { get; set; } = [];
}

public class DProvince
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public long Area { get; set; }
    public int CountryId { get; set; }
    public DCountry Country { get; set; } = null!;
    public List<DCity> Cities { get; set; } = [];
}

public class DCity
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Population { get; set; }
    public Guid ProvinceId { get; set; }
    public DProvince Province { get; set; } = null!;
}

public class DCustomer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public Tier Tier { get; set; }
    public bool IsVip { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CityId { get; set; } = "";
    public DCity City { get; set; } = null!;
    public Guid? ManagerId { get; set; }
    public DCustomer? Manager { get; set; }
    public List<DOrder> Orders { get; set; } = [];
}

public class DCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int? ParentId { get; set; }
    public DCategory? Parent { get; set; }
    public List<DCategory> Children { get; set; } = [];
}

public class DTag
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<DProduct> Products { get; set; } = [];
}

public class DProduct
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
    public DCategory Category { get; set; } = null!;
    public List<DTag> Tags { get; set; } = [];
    public List<DReview> Reviews { get; set; } = [];
}

public class DReview
{
    public int Id { get; set; }
    public long ProductId { get; set; }
    public Guid CustomerId { get; set; }
    public int Rating { get; set; }
}

public class DOrder
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public DCustomer Customer { get; set; } = null!;
    public string Status { get; set; } = "";
    public decimal Total { get; set; }
    public DateTime OrderDate { get; set; }
    public List<DOrderItem> Items { get; set; } = [];
}

public class DOrderItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public long ProductId { get; set; }
    public DProduct Product { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class ComplexDbContext(DbContextOptions<ComplexDbContext> options) : DbContext(options)
{
    public DbSet<DCountry> Countries { get; set; } = null!;
    public DbSet<DProvince> Provinces { get; set; } = null!;
    public DbSet<DCity> Cities { get; set; } = null!;
    public DbSet<DCustomer> Customers { get; set; } = null!;
    public DbSet<DCategory> Categories { get; set; } = null!;
    public DbSet<DTag> Tags { get; set; } = null!;
    public DbSet<DProduct> Products { get; set; } = null!;
    public DbSet<DReview> Reviews { get; set; } = null!;
    public DbSet<DOrder> Orders { get; set; } = null!;
    public DbSet<DOrderItem> OrderItems { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<DCategory>().HasOne(c => c.Parent).WithMany(c => c.Children).HasForeignKey(c => c.ParentId);
        b.Entity<DCustomer>().HasOne(c => c.Manager).WithMany().HasForeignKey(c => c.ManagerId);
        b.Entity<DProduct>().HasMany(p => p.Tags).WithMany(t => t.Products);
        b.Entity<DProduct>().HasMany(p => p.Reviews).WithOne().HasForeignKey(r => r.ProductId);
        b.Entity<DOrder>().HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId);
    }
}

#region Entity configs

internal sealed class CountryConfig : EntityConfigureOf<DCountry>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Name);
        UseDistributedKey<KCountry>();
    }
}

internal sealed class ProvinceConfig : EntityConfigureOf<DProvince>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Name);
        UseDistributedKey<KProvince>();
    }
}

internal sealed class CityConfig : EntityConfigureOf<DCity>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Name);
        UseDistributedKey<KCity>();
    }
}

internal sealed class CustomerConfig : EntityConfigureOf<DCustomer>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Name);
        UseDistributedKey<KCustomer>();
    }
}

internal sealed class CategoryConfig : EntityConfigureOf<DCategory>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Name);
        UseDistributedKey<KCategory>();
    }
}

internal sealed class ProductConfig : EntityConfigureOf<DProduct>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Name);
        ExposedName(x => x.Price, "Cost");
        UseDistributedKey<KProduct>();
    }
}

/// <summary>A key that is a plain string (full decoupling between services) instead of a CLR type.</summary>
internal sealed class TagConfig : EntityConfigureOf<DTag>
{
    public const string Key = "FxMap.Tests.IntegrationTests.EfCore.Complex.TagStringKey";

    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Name);
        UseDistributedKey(Key);
    }
}

internal sealed class OrderConfig : EntityConfigureOf<DOrder>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Status);
        UseDistributedKey<KOrder>();
    }
}

#endregion
