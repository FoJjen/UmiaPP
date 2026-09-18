namespace UmiaPP.Models;

public class DrawingInspectionItem
{
    public int Id { get; set; }
    public int DrawingId { get; set; }
    public int TemplateId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
