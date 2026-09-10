using System.Windows;
using UmiaPP.ViewModels;

namespace UmiaPP.Views;

public partial class ProjectEditDialog : Window
{
    public ProjectEditDialog(ProjectDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseAction = () => DialogResult = viewModel.IsConfirmed;
    }
}
