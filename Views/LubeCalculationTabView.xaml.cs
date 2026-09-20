using System.Windows;
using System.Windows.Controls;
using UmiaPP.Models;
using UmiaPP.ViewModels;

namespace UmiaPP.Views;

public partial class LubeCalculationTabView : UserControl
{
    public LubeCalculationTabView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is LubeCalculationTabViewModel viewModel)
            viewModel.RefreshPackageQuoteLabels();
    }

    private void SakvarorGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid specificationGrid)
            AttachQuoteInteractions(specificationGrid);
        if (sender is not DataGrid grid || grid.DataContext is not LubeCalculationTabViewModel viewModel)
            return;
        viewModel.SetSelectedSakvaror(grid.SelectedItems.OfType<LubeSakvaraMangd>());
        viewModel.RefreshPackageQuoteLabels();
    }

    private void SakvarorGrid_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not DataGrid grid || e.NewValue is not LubeCalculationTabViewModel viewModel)
            return;
        viewModel.SetSelectedSakvaror(grid.SelectedItems.OfType<LubeSakvaraMangd>());
    }

    private void SakvarorGrid_BeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
    {
        if (e.Row.Item is LubeSakvaraMangd { IsQuoteSummaryRow: true })
            e.Cancel = true;
        else if (e.Row.Item is LubeSakvaraMangd { HasOriginalArticleIdentity: false, Id: > 0 } article)
        {
            article.OriginalProduktkod = article.Produktkod;
            article.OriginalLeverantor = article.Leverantor;
            article.HasOriginalArticleIdentity = true;
            article.NotifyArticleIdentityChanged();
        }
    }

    private void SakvarorGrid_RowEditEnding(object? sender, DataGridRowEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit ||
            e.Row.Item is LubeSakvaraMangd { IsQuoteSummaryRow: true })
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

    private void SakvarorGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not DataGrid grid || grid.DataContext is not LubeCalculationTabViewModel viewModel)
            return;
        viewModel.SetSelectedSakvaror(grid.SelectedItems.OfType<LubeSakvaraMangd>());
    }

    private void LubeSummaryReportView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {

    }
}
