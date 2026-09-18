namespace UmiaPP.Models;

public class InspectionPointTemplate
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
