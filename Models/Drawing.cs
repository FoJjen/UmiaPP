namespace UmiaPP.Models;

public class Drawing
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string DrawingNumber { get; set; } = string.Empty; // Ritningsnummer
    public string SheetTitle { get; set; } = string.Empty;   // Sidetikett / Titel
    public string Revision { get; set; } = "A";              // Revision
    public DateTime Date { get; set; } = DateTime.Today;     // Datum
    public string FilePath { get; set; } = string.Empty;     // Länk / Filväg till PDF
    public string? Notes { get; set; }

    public string PurchaseItemsJson { get; set; } = string.Empty;
    public string PurchaseImportSource { get; set; } = string.Empty;
    public bool IsInspectionList { get; set; }

    public Project? Project { get; set; }
}
