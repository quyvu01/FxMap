using Microsoft.EntityFrameworkCore;

namespace FxMap.Tests.ProviderTests.Postgres;

public sealed class ProviderDbContext(DbContextOptions<ProviderDbContext> options) : DbContext(options)
{
    public DbSet<ProviderCustomer> Customers { get; set; } = null!;
    public DbSet<ProviderDevice> Devices { get; set; } = null!;
    public DbSet<ProviderTicket> Tickets { get; set; } = null!;
    public DbSet<ProviderLab> Labs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProviderCustomer>(e =>
        {
            e.HasKey(x => x.Id);
            e.OwnsOne(x => x.Address);
            e.OwnsMany(x => x.Orders, o => o.Property(x => x.Total).HasPrecision(18, 2));
        });
        modelBuilder.Entity<ProviderDevice>(e => e.HasKey(x => x.Id));
        modelBuilder.Entity<ProviderTicket>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
        });
        modelBuilder.Entity<ProviderLab>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Value).HasPrecision(18, 2);
        });
    }
}
