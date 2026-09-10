using Microsoft.Win32;
using System.Windows;

namespace UmiaPP.Services;

public interface IDialogService
{
    bool Confirm(string message, string title);
    void ShowMessage(string message, string title);
    void ShowError(string message, string title);
    string? OpenFileDialog(string filter, string title);
    string? SaveFileDialog(string defaultExt, string filter, string defaultFileName);
}

public class DialogService : IDialogService
{
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
