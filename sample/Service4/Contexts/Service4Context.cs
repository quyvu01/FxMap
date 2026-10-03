using Microsoft.EntityFrameworkCore;
using Service4.Models;

namespace Service4.Contexts;

public class Service4Context(DbContextOptions<Service4Context> options) : DbContext(options)
{
    public DbSet<Item> Items { get; set; } = null!;
    public DbSet<Visit> Visits { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // The lookup columns are indexed but deliberately not unique.
        modelBuilder.Entity<Item>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code);
        });
        modelBuilder.Entity<Visit>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Mrn);
        });
        base.OnModelCreating(modelBuilder);
    }
}
