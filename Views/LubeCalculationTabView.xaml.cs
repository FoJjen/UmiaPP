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
        {
            AttachQuoteInteractions(specificationGrid);
            UnlockUserEditableColumns(specificationGrid);
        }
        if (sender is not DataGrid grid || grid.DataContext is not LubeCalculationTabViewModel viewModel)
            return;
        viewModel.SetSelectedSakvaror(grid.SelectedItems.OfType<LubeSakvaraMangd>());
        viewModel.RefreshPackageQuoteLabels();
    }

    private static void UnlockUserEditableColumns(DataGrid grid)
    {
        foreach (var column in grid.Columns)
        {
            if (IsImportedLockedColumn(column))
                column.IsReadOnly = false;
        }
    }

    private void SakvarorGrid_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not DataGrid grid || e.NewValue is not LubeCalculationTabViewModel viewModel)
            return;
        viewModel.SetSelectedSakvaror(grid.SelectedItems.OfType<LubeSakvaraMangd>());
    }

    private static readonly HashSet<string> ImportedLockedFields =
    [
        nameof(LubeSakvaraMangd.Grupp),
        nameof(LubeSakvaraMangd.Beteckning),
        nameof(LubeSakvaraMangd.Dim1),
        nameof(LubeSakvaraMangd.Dim2),
        nameof(LubeSakvaraMangd.Antal),
        nameof(LubeSakvaraMangd.Enhet),
        nameof(LubeSakvaraMangd.Produkt)
    ];

    private void SakvarorGrid_BeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
    {
        if (e.Row.Item is not LubeSakvaraMangd article)
            return;

        if (article.IsQuoteSummaryRow)
        {
            e.Cancel = true;
            return;
        }

        if (!article.IsUserAdded &&
            string.Equals(article.Beteckning, "Ny rad", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(article.Produkt, "Ny rad", StringComparison.OrdinalIgnoreCase))
            article.IsUserAdded = true;

        if (!article.CanEditAllFields && IsImportedLockedColumn(e.Column))
        {
            e.Cancel = true;
            return;
        }

        if (!article.HasOriginalArticleIdentity && article.Id > 0 && !article.IsUserAdded)
        {
            article.OriginalProduktkod = article.Produktkod;
            article.OriginalLeverantor = article.Leverantor;
            article.HasOriginalArticleIdentity = true;
            article.NotifyArticleIdentityChanged();
        }
    }

    private static bool IsImportedLockedColumn(DataGridColumn column)
    {
        var path = column.SortMemberPath;
        if (string.IsNullOrWhiteSpace(path) && column is DataGridBoundColumn bound &&
            bound.Binding is System.Windows.Data.Binding binding)
            path = binding.Path?.Path;
        return !string.IsNullOrWhiteSpace(path) && ImportedLockedFields.Contains(path);
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
