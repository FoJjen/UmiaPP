using System.IO;
using CommunityToolkit.Mvvm.Input;
using UmiaPP.Models;
using UmiaPP.Services;

namespace UmiaPP.ViewModels;

public partial class PurchaseTabViewModel
{
    [RelayCommand]
    private async Task ExportAllListsAsync()
    {
        if (IsListsBusy) return;
        if (Drawings.Count == 0)
        {
            _dialogService.ShowMessage("Det finns inga listor att exportera.", "Exportera alla listor");
            return;
        }

        IsListsBusy = true;
        try
        {
            // Snapshot all project lists, independently of selection and filters.
            // The export worker must not enumerate live UI collections.
            var lists = Drawings.Select(drawing => new PurchaseExportList(
                string.IsNullOrWhiteSpace(drawing.DrawingNumber)
                    ? (string.IsNullOrWhiteSpace(drawing.SheetTitle) ? "Lista" : drawing.SheetTitle)
                    : drawing.DrawingNumber,
                LoadListRows(drawing).Select(article => new PurchaseArticle
                {
                    Category = article.Category,
                    Type = article.Type,
                    Dimension = article.Dimension,
                    Quantity = article.Quantity,
                    Unit = article.Unit,
                    ProductCode = article.ProductCode,
                    Supplier = article.Supplier
                }).ToList())).ToList();

            if (Drawings.Any(_unreadableLists.Contains))
            {
                _dialogService.ShowError(
                    "En eller flera listor kan inte läsas. Exporten avbröts för att undvika en ofullständig sammanfattning.",
                    "Exportera alla listor");
                return;
            }

            var projectName = Project.Name;
            var safeName = string.Concat(projectName.Select(character =>
                Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
            var path = _dialogService.SaveFileDialog(".xlsx", "Excel-arbetsbok (*.xlsx)|*.xlsx",
                $"Inköp_{safeName}_alla_listor.xlsx");
            if (string.IsNullOrWhiteSpace(path)) return;

            try
            {
                await PurchaseWorkbookExporter.ExportAsync(lists, projectName, path);
            }
            catch (IOException ex) when (
                ex.HResult == unchecked((int)0x80070020) || // ERROR_SHARING_VIOLATION
                ex.HResult == unchecked((int)0x80070021))   // ERROR_LOCK_VIOLATION
            {
                _dialogService.ShowError(
                    $"Filen används eller är låst och kunde inte sparas:\n{path}\n\n" +
                    "Stäng filen i Excel eller det program som använder den och försök exportera igen. " +
                    "Om filen ligger på en delad enhet kan en annan användare ha den öppen. " +
                    "Du kan också exportera igen och välja ett annat filnamn.",
                    "Filen används");
                return;
            }
            _dialogService.ShowMessage(
                $"Exporten är klar: {lists.Count} listor och ett sammanfattningsblad.\n{path}",
                "Exportera alla listor");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Kunde inte exportera alla listor:\n{ex.Message}", "Exportera alla listor");
        }
        finally
        {
            IsListsBusy = false;
        }
    }
}
