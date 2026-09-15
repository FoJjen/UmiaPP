using Microsoft.Win32;
using System.Windows;
using UmiaPP.Models;
using UmiaPP.Views;

namespace UmiaPP.Services;

public interface IDialogService
{
    bool Confirm(string message, string title);
    void ShowMessage(string message, string title);
    void ShowError(string message, string title);
    string? OpenFileDialog(string filter, string title);
    string? SaveFileDialog(string defaultExt, string filter, string defaultFileName);
    bool ResolveMissingDimensions(IReadOnlyList<BluebeamMarkupItem> items);
    bool ReviewPurchaseImport(IReadOnlyList<BluebeamMarkupItem> items);
}

public class DialogService : IDialogService
{
    public bool ReviewPurchaseImport(IReadOnlyList<BluebeamMarkupItem> items)
    {
        var dialog = new PurchaseImportReviewDialog(items)
        {
            Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive)
                ?? Application.Current?.MainWindow
        };
        return dialog.ShowDialog() == true;
    }

    public bool ResolveMissingDimensions(IReadOnlyList<BluebeamMarkupItem> items)
    {
        var dialog = new DimensionResolutionDialog(items)
        {
            Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive)
                ?? Application.Current?.MainWindow
        };
        return dialog.ShowDialog() == true;
    }

    public bool Confirm(string message, string title)
    {
        return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    public void ShowMessage(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowError(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public string? OpenFileDialog(string filter, string title)
    {
        var dlg = new OpenFileDialog
        {
            Filter = filter,
            Title = title
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? SaveFileDialog(string defaultExt, string filter, string defaultFileName)
    {
        var dlg = new SaveFileDialog
        {
            DefaultExt = defaultExt,
            Filter = filter,
            FileName = defaultFileName
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
