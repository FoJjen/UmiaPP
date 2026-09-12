using System.Windows.Controls;
using UmiaPP.Models;
using UmiaPP.ViewModels;

namespace UmiaPP.Views;

public partial class LubeCalculationTabView : UserControl
{
    public LubeCalculationTabView() => InitializeComponent();

    private void SakvarorGrid_RowEditEnding(object? sender, DataGridRowEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit)
            return;

        if (sender is DataGrid grid)
        {
            grid.RowEditEnding -= SakvarorGrid_RowEditEnding;
            grid.CommitEdit(DataGridEditingUnit.Row, true);
            grid.RowEditEnding += SakvarorGrid_RowEditEnding;
        }

        if (e.Row.Item is LubeSakvaraMangd item && DataContext is LubeCalculationTabViewModel viewModel)
            _ = viewModel.SaveSakvaraSpecificationRowAsync(item);
    }

    private void LubeSummaryReportView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {

    }
}
