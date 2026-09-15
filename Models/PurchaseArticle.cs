using CommunityToolkit.Mvvm.ComponentModel;

namespace UmiaPP.Models;

public partial class PurchaseArticle : ObservableObject
{
    public string RowId { get; set; } = Guid.NewGuid().ToString("N");
    public bool IsManual { get; set; }
    [NotifyPropertyChangedFor(nameof(PurchaseQuantity))]
    [NotifyPropertyChangedFor(nameof(PurchaseUnit))]
    [ObservableProperty] private string _category = string.Empty;
    [ObservableProperty] private string _type = string.Empty;
    [ObservableProperty] private string _dimension = string.Empty;
    [NotifyPropertyChangedFor(nameof(PurchaseQuantity))]
    [ObservableProperty] private double _quantity;
    [NotifyPropertyChangedFor(nameof(PurchaseQuantity))]
    [NotifyPropertyChangedFor(nameof(PurchaseUnit))]
    [ObservableProperty] private string _unit = "st";
    [ObservableProperty] private string _productCode = string.Empty;
    [ObservableProperty] private string _supplier = string.Empty;

    [System.Text.Json.Serialization.JsonIgnore]
    public double PurchaseQuantity => IsThreeMeterDuct ? Math.Ceiling(Quantity / 3d) : Quantity;

    [System.Text.Json.Serialization.JsonIgnore]
    public string PurchaseUnit => IsThreeMeterDuct ? "st (3 m)" : Unit;

    private bool IsThreeMeterDuct =>
        string.Equals(Category?.Trim(), "Kanaler", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Unit?.Trim(), "m", StringComparison.OrdinalIgnoreCase);
}
