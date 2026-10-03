namespace Service4.Models;

/// <summary>An item identified by Id, looked up by Code. Several items can have the same Code.</summary>
public class Item
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}
