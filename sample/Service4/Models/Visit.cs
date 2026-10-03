namespace Service4.Models;

/// <summary>A visit of a patient. A patient (Mrn) has many visits.</summary>
public class Visit
{
    public int Id { get; set; }
    public string Mrn { get; set; } = "";
    public DateTime VisitedAt { get; set; }
    public string Facility { get; set; } = "";
    public string Status { get; set; } = "";
    public string Reason { get; set; } = "";
}
