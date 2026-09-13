using System.Windows.Controls;
using UmiaPP.Models;
using UmiaPP.ViewModels;

namespace UmiaPP.Views;

public partial class PurchaseTabView : UserControl
{
    public PurchaseTabView() => InitializeComponent();

    private void Articles_RowEditEnding(object? sender, DataGridRowEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit || sender is not DataGrid grid ||
            e.Row.Item is not PurchaseArticle row || DataContext is not PurchaseTabViewModel viewModel) return;
        grid.RowEditEnding -= Articles_RowEditEnding;
        bool committed;
        try { committed = grid.CommitEdit(DataGridEditingUnit.Row, true); }
        finally { grid.RowEditEnding += Articles_RowEditEnding; }
        if (committed)
            Dispatcher.BeginInvoke(new Action(() => _ = viewModel.SavePurchaseArticleAsync(row)));
    }

    private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {

    }
}
