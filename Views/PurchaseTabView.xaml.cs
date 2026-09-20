using System.Windows.Controls;
using UmiaPP.Models;
using UmiaPP.ViewModels;

namespace UmiaPP.Views;

public partial class PurchaseTabView : UserControl
{
    private bool _changingDrawingSelection;
    private Drawing[] _acceptedDrawings = [];

    public PurchaseTabView() => InitializeComponent();

    private void Articles_BeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
    {
        if (DataContext is not PurchaseTabViewModel { IsCombinedSelection: true }) return;
        var property = (e.Column as DataGridBoundColumn)?.Binding as System.Windows.Data.Binding;
        e.Cancel = property?.Path?.Path is not (nameof(PurchaseArticle.ProductCode) or nameof(PurchaseArticle.Supplier));
    }

    private void Articles_RowEditEnding(object? sender, DataGridRowEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit) return;
        if (sender is not DataGrid grid) return;
        if (e.Row.Item is not PurchaseArticle row) return;
        if (DataContext is not PurchaseTabViewModel viewModel) return;
        grid.RowEditEnding -= Articles_RowEditEnding;
        bool committed;
        try { committed = grid.CommitEdit(DataGridEditingUnit.Row, true); }
        finally { grid.RowEditEnding += Articles_RowEditEnding; }
        if (committed)
        {
            if (viewModel.IsCombinedSelection)
                _ = viewModel.SaveCombinedPurchaseArticleAsync(row);
            else
                Dispatcher.BeginInvoke(new Action(() => _ = viewModel.SavePurchaseArticleAsync(row)));
        }
    }

    private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {

    }

    private void Drawings_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_changingDrawingSelection || sender is not DataGrid grid ||
            DataContext is not PurchaseTabViewModel viewModel) return;

        var selected = grid.SelectedItems.Cast<Drawing>().ToArray();
        var selectedDrawing = grid.SelectedItem as Drawing;
        var previousDrawing = viewModel.SelectedDrawing;
        _changingDrawingSelection = true;
        try
        {
            // SelectedItem is OneWay: keep the outgoing list and combined-row
            // mappings intact until the cell and row have passed validation.
            if (ArticlesGrid != null &&
                (!ArticlesGrid.CommitEdit(DataGridEditingUnit.Cell, true) ||
                 !ArticlesGrid.CommitEdit(DataGridEditingUnit.Row, true)))
            {
                grid.SelectedItems.Clear();
                if (previousDrawing != null && grid.Items.Contains(previousDrawing))
                    grid.SelectedItem = previousDrawing;
                foreach (var drawing in _acceptedDrawings)
                {
                    if (grid.Items.Contains(drawing) && !grid.SelectedItems.Contains(drawing))
                        grid.SelectedItems.Add(drawing);
                }
                viewModel.ListStatus = "Rätta valideringsfelen i artikelraden innan du byter lista.";
                return;
            }

            viewModel.SelectedDrawing = selectedDrawing;
            viewModel.SetSelectedPurchaseLists(selected);
            _acceptedDrawings = selected;
        }
        finally
        {
            _changingDrawingSelection = false;
        }
    }

    private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {

    }
}
