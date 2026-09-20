using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UmiaPP.Models;
using UmiaPP.ViewModels;

namespace UmiaPP.Views;

public partial class LubeCalculationTabView
{
    private void AttachQuoteInteractions(DataGrid grid)
    {
        grid.PreviewMouseLeftButtonDown -= Specification_PreviewMouseLeftButtonDown;
        grid.PreviewMouseLeftButtonDown += Specification_PreviewMouseLeftButtonDown;
        grid.PreviewKeyDown -= Specification_PreviewKeyDown;
        grid.PreviewKeyDown += Specification_PreviewKeyDown;
        grid.PreviewMouseRightButtonDown -= Specification_PreviewMouseRightButtonDown;
        grid.PreviewMouseRightButtonDown += Specification_PreviewMouseRightButtonDown;
        grid.ContextMenuOpening -= Specification_ContextMenuOpening;
        grid.ContextMenuOpening += Specification_ContextMenuOpening;
        grid.ContextMenu ??= new ContextMenu();
    }

    private void Specification_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid || e.OriginalSource is not DependencyObject source)
            return;
        if (ItemsControl.ContainerFromElement(grid, source) is not DataGridRow row ||
            row.Item is not LubeSakvaraMangd { IsQuoteSummaryRow: false })
        {
            e.Handled = true;
            return;
        }
        // Right-click within a multiple selection must not collapse it.
        if (!row.IsSelected)
        {
            grid.SelectedItems.Clear();
            row.IsSelected = true;
        }
    }

    private void Specification_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not DataGrid grid || grid.DataContext is not LubeCalculationTabViewModel vm)
            return;
        if (!grid.CommitEdit(DataGridEditingUnit.Cell, true) ||
            !grid.CommitEdit(DataGridEditingUnit.Row, true))
        {
            e.Handled = true;
            return;
        }
        var articles = grid.SelectedItems.OfType<LubeSakvaraMangd>()
            .Where(a => !a.IsQuoteSummaryRow && a.ProjectId == vm.Project.Id).Distinct().ToArray();
        if (articles.Length == 0)
        {
            e.Handled = true;
            return;
        }
        var menu = grid.ContextMenu ??= new ContextMenu();
        menu.Items.Clear();
        var create = new MenuItem { Header = "Lägg till i ny offert" };
        create.Click += async (_, _) => await vm.CreateQuoteForArticlesAsync(articles);
        menu.Items.Add(create);
        var existing = new MenuItem { Header = "Lägg till i befintlig offert" };
        foreach (var quote in vm.PackageQuotes.ToArray())
        {
            var quoteId = quote.Id;
            var choice = new MenuItem
            {
                Header = $"{quote.Leverantor} – {quote.Produktgrupp} ({quote.QuoteDate:d}, #{quoteId})"
            };
            choice.Click += async (_, _) => await vm.AddArticlesToQuoteAsync(quoteId, articles);
            existing.Items.Add(choice);
        }
        existing.IsEnabled = existing.Items.Count > 0;
        menu.Items.Add(existing);
        menu.Items.Add(new Separator());
        var remove = new MenuItem
        {
            Header = "Ta bort från offert",
            IsEnabled = articles.Any(a => a.HasPackageQuote)
        };
        remove.Click += async (_, _) => await vm.RemoveArticlesFromQuoteAsync(articles);
        menu.Items.Add(remove);
    }

    private void Specification_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid || e.OriginalSource is not DependencyObject source ||
            ItemsControl.ContainerFromElement(grid, source) is not DataGridRow row ||
            row.Item is not LubeSakvaraMangd { IsQuoteSummaryRow: true } summary)
            return;

        // A quote summary must receive pointer events, but its embedded editors
        // and purchase checkbox must not change on either click.
        e.Handled = true;
        grid.SelectedItem = summary;
        grid.Focus();
        if (e.ClickCount == 2 && summary.SummaryQuoteId is int quoteId &&
            grid.DataContext is LubeCalculationTabViewModel viewModel)
            viewModel.OpenPackageQuoteDetails(quoteId);
    }

    private void Specification_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not DataGrid grid)
            return;
        if (e.Key == Key.Delete && grid.SelectedItems.OfType<LubeSakvaraMangd>().Any(row => row.IsQuoteSummaryRow))
        {
            e.Handled = true;
            return;
        }
        var row = e.OriginalSource is DependencyObject source
            ? (ItemsControl.ContainerFromElement(grid, source) as DataGridRow)?.Item as LubeSakvaraMangd
            : null;
        row ??= grid.SelectedItem as LubeSakvaraMangd;
        if (row is not { IsQuoteSummaryRow: true })
            return;
        if (e.Key is Key.Enter or Key.Space or Key.F2)
        {
            e.Handled = true;
            if (e.Key == Key.Enter && row.SummaryQuoteId is int quoteId &&
                grid.DataContext is LubeCalculationTabViewModel viewModel)
                viewModel.OpenPackageQuoteDetails(quoteId);
        }
    }
}
