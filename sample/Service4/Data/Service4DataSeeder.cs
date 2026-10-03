using Microsoft.EntityFrameworkCore;
using Service4.Contexts;
using Service4.Models;

namespace Service4.Data;

/// <summary>
/// Creates the database when it does not exist and seeds the two non-unique lookups:
/// items (Code A once, Code B twice) and visits (MRN0001 six times, MRN0002 three times, MRN0003 once, MRN0004 never).
/// </summary>
public static class Service4DataSeeder
{
    private static DateTime Utc(int month, int day, int hour = 9, int minute = 0) =>
        new(2026, month, day, hour, minute, 0, DateTimeKind.Utc);

    public static async Task SeedAsync(Service4Context context, CancellationToken cancellationToken = default)
    {
        await context.Database.EnsureCreatedAsync(cancellationToken);

        if (!await context.Items.AnyAsync(cancellationToken))
            context.Items.AddRange(
                new Item { Code = "A", Name = "item-1" },
                new Item { Code = "B", Name = "item-2" },
                new Item { Code = "B", Name = "item-3" });

        if (!await context.Visits.AnyAsync(cancellationToken))
            context.Visits.AddRange(
                // MRN0001: six visits, two of them on the same day and hour (ties) to show the order and the tie-break
                V("MRN0001", Utc(1, 5), "North Clinic", "Completed", "Check-up"),
                V("MRN0001", Utc(2, 14, 10, 30), "City Hospital", "Completed", "Blood test"),
                V("MRN0001", Utc(3, 2), "North Clinic", "Cancelled", "Follow-up"),
                V("MRN0001", Utc(4, 20, 15), "Eastside Lab", "Completed", "X-ray"),
                V("MRN0001", Utc(4, 20, 15), "City Hospital", "Scheduled", "X-ray review"),
                V("MRN0001", Utc(6, 1, 8, 45), "North Clinic", "Scheduled", "Annual visit"),
                // MRN0002: three visits
                V("MRN0002", Utc(1, 18, 11), "City Hospital", "Completed", "Consultation"),
                V("MRN0002", Utc(3, 9, 16), "City Hospital", "Completed", "Surgery"),
                V("MRN0002", Utc(5, 12, 9, 30), "North Clinic", "Scheduled", "Post-op"),
                // MRN0003: a single visit
                V("MRN0003", Utc(2, 3, 13), "Eastside Lab", "Completed", "Lab work"));
        // MRN0004 deliberately has no visit.

        await context.SaveChangesAsync(cancellationToken);
    }

    private static Visit V(string mrn, DateTime at, string facility, string status, string reason) => new()
        { Mrn = mrn, VisitedAt = at, Facility = facility, Status = status, Reason = reason };
}
