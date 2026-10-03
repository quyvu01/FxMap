namespace Service5.Models;

/// <summary>A patient of Service5's own database. Its visits live in Service4.</summary>
public class Patient
{
    public string Mrn { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime BirthDate { get; set; }
}
