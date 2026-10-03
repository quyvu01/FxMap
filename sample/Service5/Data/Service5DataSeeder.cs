using Microsoft.EntityFrameworkCore;
using Service5.Contexts;
using Service5.Models;

namespace Service5.Data;

/// <summary>Creates the database when it does not exist and seeds patients. MRN0004 has no visit in Service4.</summary>
public static class Service5DataSeeder
{
    public static async Task SeedAsync(Service5Context context, CancellationToken cancellationToken = default)
    {
        await context.Database.EnsureCreatedAsync(cancellationToken);
        if (await context.Patients.AnyAsync(cancellationToken)) return;

        context.Patients.AddRange(
            new Patient { Mrn = "MRN0001", Name = "Alice Nguyen", BirthDate = new DateTime(1985, 3, 14, 0, 0, 0, DateTimeKind.Utc) },
            new Patient { Mrn = "MRN0002", Name = "Bob Tran", BirthDate = new DateTime(1972, 11, 2, 0, 0, 0, DateTimeKind.Utc) },
            new Patient { Mrn = "MRN0003", Name = "Carol Le", BirthDate = new DateTime(1999, 7, 25, 0, 0, 0, DateTimeKind.Utc) },
            new Patient { Mrn = "MRN0004", Name = "Dan Pham", BirthDate = new DateTime(1990, 1, 30, 0, 0, 0, DateTimeKind.Utc) });
        await context.SaveChangesAsync(cancellationToken);
    }
}
