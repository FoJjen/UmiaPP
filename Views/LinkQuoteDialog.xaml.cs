using System.Windows;
using UmiaPP.ViewModels;

namespace UmiaPP.Views;

public partial class LinkQuoteDialog : Window
{
    public LinkQuoteDialog(LinkQuoteDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseAction = () => DialogResult = viewModel.IsConfirmed;
        viewModel.BrowseFile = filter =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = filter, Title = "Välj offert-PDF" };
            if (dialog.ShowDialog(this) == true)
                viewModel.FilePath = dialog.FileName;
        };
    }
}
