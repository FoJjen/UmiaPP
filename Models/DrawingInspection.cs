namespace UmiaPP.Models;

public class DrawingInspection
{
    public int Id { get; set; }
    public int DrawingId { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public string CustomerReference { get; set; } = string.Empty;
    public string PreparedBy { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public string Revision { get; set; } = "1";
    public string WorkTask { get; set; } = string.Empty;
    public string BuildingPart { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SectionNumber { get; set; } = 1;
}
