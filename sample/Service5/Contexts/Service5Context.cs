using Microsoft.EntityFrameworkCore;
using Service5.Models;

namespace Service5.Contexts;

public class Service5Context(DbContextOptions<Service5Context> options) : DbContext(options)
{
    public DbSet<Patient> Patients { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Patient>().HasKey(x => x.Mrn);
        base.OnModelCreating(modelBuilder);
    }
}
