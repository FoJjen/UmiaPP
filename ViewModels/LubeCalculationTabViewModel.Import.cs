using CommunityToolkit.Mvvm.Input;
using UmiaPP.Models;

namespace UmiaPP.ViewModels;

public partial class LubeCalculationTabViewModel
{
    [RelayCommand]
    private async Task ImportLubeExcelAsync()
    {
        var path = _dialogService.OpenFileDialog(
            "Lubekalkyl (*.xlsx;*.xls)|*.xlsx;*.xls",
            "Importera Lubekalkyl");
        if (string.IsNullOrWhiteSpace(path)) return;

        IsImporting = true;
        try
        {
            var result = await _lubeImportService.ImportAsync(path);
            if (!result.Success)
            {
                _dialogService.ShowError(result.ErrorMessage ?? "Importen misslyckades.", "Lubekalkyl");
                return;
            }

            await _dataService.Projects.SaveLubeCalculationDataAsync(
                Project.Id,
                result.KalkylPoster,
                result.Anpassningar,
                result.SakvarorMangder,
                result.SakvarorOfferter,
                result.SakvarorLeveranspriser,
                result.SakvarorMontagePoster,
                result.EjMangdbaseradePoster,
                result.Summary);

            var project = await _dataService.Projects.GetByIdAsync(Project.Id);
            if (project != null)
                UpdateProject(project);

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                _dialogService.ShowMessage(result.ErrorMessage, "Lubekalkyl");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Kunde inte importera kalkylen:\n{ex.Message}", "Lubekalkyl");
        }
        finally
        {
            IsImporting = false;
        }
    }
}
