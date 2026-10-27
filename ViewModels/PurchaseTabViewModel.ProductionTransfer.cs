using System.Collections;
using CommunityToolkit.Mvvm.Input;
using UmiaPP.Models;

namespace UmiaPP.ViewModels;

public partial class PurchaseTabViewModel
{
    public Func<IReadOnlyList<PurchaseArticle>, Task<(int Added, int Merged)>>? AddToProductionCalculationAsync { get; set; }

    [RelayCommand]
    private async Task AddSelectedToProductionCalculationAsync(IList? selected)
    {
        var chosen = (selected ?? Array.Empty<object>())
            .OfType<PurchaseArticle>()
            .Distinct()
            .ToList();

        if (chosen.Count == 0)
        {
            _dialogService.ShowMessage("Markera minst en artikelrad i inköpslistan.", "Inköp");
            return;
        }

        if (AddToProductionCalculationAsync == null)
        {
            _dialogService.ShowError("Kopplingen till Produktionskalkyl saknas.", "Inköp");
            return;
        }

        try
        {
            var rows = chosen.Select(article => new PurchaseArticle
            {
                Category = article.Category,
                Type = article.Type,
                Dimension = article.Dimension,
                Quantity = article.Quantity,
                Unit = article.Unit,
                ProductCode = article.ProductCode,
                Supplier = article.Supplier
            }).ToArray();

            var result = await AddToProductionCalculationAsync(rows);
            _dialogService.ShowMessage(
                $"Produktionskalkyl uppdaterad. Nya rader: {result.Added}, uppdaterade rader: {result.Merged}.",
                "Inköp");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Kunde inte lägga till artiklar i produktionskalkylen:\n{ex.Message}", "Inköp");
        }
    }
}
