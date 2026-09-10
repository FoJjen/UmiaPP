using System.Windows.Controls;
using System.Windows.Input;
using UmiaPP.ViewModels;

namespace UmiaPP.Views;

public partial class ProjectListView : UserControl
{
    public ProjectListView() => InitializeComponent();

    private void ProjectGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ProjectListViewModel viewModel && viewModel.SelectedProject != null)
        {
            viewModel.OpenProjectCommand.Execute(viewModel.SelectedProject);
        }
    }
}
