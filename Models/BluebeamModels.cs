namespace UmiaPP.Models;

public class BluebeamMarkupItem
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Subject { get; set; } = string.Empty;       // <Ämne> (t.ex. "160-90 Böj", "250 Kanal", "BGS101-200")
    public string Layer { get; set; } = string.Empty;         // <Lager> (Kanaler, Brandspjäll, Don, Ljuddämpare)
    public double LengthMeters { get; set; }                  // <Längd unit="m">
    public double Count { get; set; } = 1;                    // <Antal_Luft> eller <Kommentarer> eller default 1
    public double VerticalHeightMeters { get; set; }          // <Total_vertikalhöjd_luft>
    public string SheetLabel { get; set; } = string.Empty;    // <Sidetikett>
    public string? Comments { get; set; }                     // <Kommentarer>
    public string Category { get; set; } = string.Empty;      // Extraherad kategori: Kanal, Detalj, Sakvara/Don
    public string Dimension { get; set; } = string.Empty;     // T.ex. "160", "250", etc.

    public Project? Project { get; set; }
}

public class PurchaseSummaryItem
{
    public string Category { get; set; } = string.Empty;      // "Kanal", "Detalj", "Sakvara/Don"
    public string Type { get; set; } = string.Empty;          // Raka kanaler, 90-böj, T-rör, Brandspjäll
    public string DimensionOrCode { get; set; } = string.Empty; // "160", "250", "BGS101-200"
    public double Quantity { get; set; }                     // Total längd (m) eller antal (st)
    public string Unit { get; set; } = "st";                  // "m" eller "st"
    public string SheetReferences { get; set; } = string.Empty; // "V-01, V-02"
}
