using CommunityToolkit.Mvvm.Input;
using UmiaPP.Services;

namespace UmiaPP.ViewModels;

public partial class PurchaseTabViewModel
{
    [RelayCommand]
    private void ShowImportFormatHelp()
    {
        _dialogService.ShowMessage(PurchaseImportFormatHelp.Text, PurchaseImportFormatHelp.Title);
    }
}
