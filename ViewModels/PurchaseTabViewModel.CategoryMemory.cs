using System.IO;
using System.Text.Json;
using UmiaPP.Models;
using UmiaPP.Services;

namespace UmiaPP.ViewModels;

public partial class PurchaseTabViewModel
{
    private readonly PurchaseCategoryMemory _purchaseCategoryMemory = new();
    private readonly Dictionary<string, string> _pendingPurchaseCategories = new(StringComparer.Ordinal);

    private void ApplyRememberedPurchaseCategories(IEnumerable<BluebeamMarkupItem> items)
    {
        try
        {
            var remembered = _purchaseCategoryMemory.Load();
            foreach (var item in items)
            {
                if (!string.IsNullOrWhiteSpace(item.Subject) && remembered.TryGetValue(item.Subject, out var category))
                    item.Category = category;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _dialogService.ShowError(
                $"Sparade kategorival kunde inte läsas. Importen fortsätter utan dem.\n{ex.Message}",
                "Sparade kategorival");
        }
    }

    private void RememberConfirmedPurchaseCategories()
    {
        try
        {
            // Called only after the project import has been saved successfully.
            _purchaseCategoryMemory.SaveChoices(_pendingPurchaseCategories);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _dialogService.ShowMessage(
                $"Inköpslistorna sparades, men kategorivalen kunde inte sparas till nästa import.\n{ex.Message}",
                "Sparade kategorival");
        }
        finally
        {
            _pendingPurchaseCategories.Clear();
        }
    }
}
