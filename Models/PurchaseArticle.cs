using CommunityToolkit.Mvvm.ComponentModel;

namespace UmiaPP.Models;

public partial class PurchaseArticle : ObservableObject
{
    public string RowId { get; set; } = Guid.NewGuid().ToString("N");
    public bool IsManual { get; set; }
    [ObservableProperty] private string _category = string.Empty;
    [ObservableProperty] private string _type = string.Empty;
    [ObservableProperty] private string _dimension = string.Empty;
    [ObservableProperty] private double _quantity;
    [ObservableProperty] private string _unit = "st";
    [ObservableProperty] private string _productCode = string.Empty;
    [ObservableProperty] private string _supplier = string.Empty;
}
