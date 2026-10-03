using FxMap.Fluent;
using Shared.DistributedKeys;

namespace Service5.Responses;

/// <summary>One visit: an element of a collection, filled from one row of Service4.</summary>
public class VisitResponse
{
    public int VisitId { get; set; }
    public DateTime VisitedAt { get; set; }
    public string Facility { get; set; }
    public string Status { get; set; }
    public string Reason { get; set; }
}

/// <summary>
/// A patient of Service5 enriched with the visits of Service4. The key (Mrn) matches several rows, so every
/// <c>Collection</c> below becomes a list with one element per row.
/// </summary>
public class PatientResponse
{
    public string Mrn { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>Plain rule on a key with many rows: takes the FIRST row (in the order the database returns it).</summary>
    public string FirstRowStatus { get; set; }

    /// <summary>The three most recent visits (ordered and cut by the server for each Mrn).</summary>
    public List<VisitResponse>? LatestVisits { get; set; }

    /// <summary>The oldest visit (order ascending, limit 1).</summary>
    public List<VisitResponse>? FirstVisit { get; set; }

    /// <summary>Every visit, in the order of the database (no order, no limit).</summary>
    public List<VisitResponse>? AllVisits { get; set; }
}

public class PatientResponseProfile : ProfileOf<PatientResponse>
{
    protected override void Configure() =>
        UseDistributedKey<VisitDistributedKey>()
            .Of(x => x.Mrn)
            .For(x => x.FirstRowStatus, "Status")
            .Collection(x => x.LatestVisits, v => v
                .For(x => x.VisitId, "Id")
                .For(x => x.VisitedAt, "VisitedAt")
                .For(x => x.Facility, "Facility")
                .For(x => x.Status, "Status")
                .OrderByDescending("VisitedAt")
                .ThenBy("Id")
                .Limit(3))
            .Collection(x => x.FirstVisit, v => v
                .For(x => x.VisitId, "Id")
                .For(x => x.VisitedAt, "VisitedAt")
                .For(x => x.Reason, "Reason")
                .OrderBy("VisitedAt")
                .ThenBy("Id")
                .Limit(1))
            .Collection(x => x.AllVisits, v => v
                .For(x => x.VisitId, "Id")
                .For(x => x.VisitedAt, "VisitedAt")
                .For(x => x.Facility, "Facility")
                .For(x => x.Status, "Status")
                .For(x => x.Reason, "Reason"));
}
