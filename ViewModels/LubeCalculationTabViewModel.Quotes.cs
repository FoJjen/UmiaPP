using System;
using System.Linq;
using System.Windows;
using UmiaPP.Views;

namespace UmiaPP.ViewModels;

public partial class LubeCalculationTabViewModel
{
    public void OpenPackageQuoteDetails(int quoteId)
    {
        var quote = PackageQuotes.FirstOrDefault(item => item.Id == quoteId && item.ProjectId == Project.Id);
        if (quote == null)
        {
            _dialogService.ShowError("Offerten finns inte längre i projektet.", "Offert");
            return;
        }

        var articles = SakvarorMangder.Where(item => item.PackageQuoteId == quote.Id).ToArray();
        using var editor = new PackageQuoteEditorViewModel(quote, articles, async draft =>
        {
            // An empty ID list updates quote details only; existing article links
            // are preserved by SavePackageQuoteAsync.
            await _dataService.Projects.SavePackageQuoteAsync(draft, Array.Empty<int>());

            // Do not mutate the live quote until persistence has succeeded.
            quote.Leverantor = draft.Leverantor;
            quote.Produktgrupp = draft.Produktgrupp;
            quote.QuoteDate = draft.QuoteDate;
            quote.OffereratPris = draft.OffereratPris;
            quote.FinalAmount = draft.FinalAmount;
            quote.FilePath = draft.FilePath;
            quote.Kommentar = draft.Kommentar;
            quote.Valuta = draft.Valuta;
            var index = SakvarorOfferter.IndexOf(quote);
            if (index >= 0)
                SakvarorOfferter[index] = quote;
            RefreshPackageQuoteLabels();
        }, delete: () => DeletePackageQuoteAsync(quote.Id),
            confirmDelete: () => _dialogService.Confirm(
                $"Vill du ta bort offerten {quote.Produktgrupp} (#{quote.Id})?\n\n" +
                "Kopplade artiklar behålls och kopplas loss från offerten. " +
                "Deras egna priser används igen och utfallet räknas om.\n" +
                "Offertfilen på disk tas inte bort. Ändringen kan inte ångras.",
                "Ta bort offert"));
        var dialog = new PackageQuoteDetailsDialog(editor);
        if (Application.Current?.MainWindow is { } owner)
            dialog.Owner = owner;
        dialog.ShowDialog();
    }

    public async System.Threading.Tasks.Task DeletePackageQuoteAsync(int quoteId)
    {
        var quote = PackageQuotes.FirstOrDefault(item => item.Id == quoteId && item.ProjectId == Project.Id)
            ?? throw new InvalidOperationException("Offerten finns inte längre i projektet.");

        // Do not change the live state until deletion and unlinking have committed.
        await _dataService.Projects.DeletePackageQuoteAsync(Project.Id, quoteId);

        var wasUpdating = _updatingSpecificationOutcome;
        _updatingSpecificationOutcome = true;
        try
        {
            foreach (var row in SakvarorMangder.Where(item => item.PackageQuoteId == quoteId))
                row.PackageQuoteId = null;
            foreach (var row in Project.LubeSakvarorMangder.Where(item => item.PackageQuoteId == quoteId))
                row.PackageQuoteId = null;
            SakvarorOfferter.Remove(quote);
            Project.LubeSakvarorOfferter.RemoveAll(item => item.Id == quoteId);
        }
        finally
        {
            _updatingSpecificationOutcome = wasUpdating;
        }
        // Rebuild display-only quote rows, notify totals, and save the new outcome
        // through the existing ActualCostsChanged handler.
        RefreshPackageQuoteLabels();
    }

    private double GetCalculatedQuoteNetAmount(int quoteId)
        => SakvarorMangder.Where(row => row.PackageQuoteId == quoteId).Sum(row => row.PrisNto);

    private void RefreshQuoteSummaryNetAmounts()
    {
        var amounts = SakvarorMangder.Where(row => row.HasPackageQuote)
            .GroupBy(row => row.PackageQuoteId!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(row => row.PrisNto));

        // Update existing display rows in place; do not rebuild the grid while
        // an article's net price is being edited.
        foreach (var summary in SpecificationRows.Where(row => row.IsQuoteSummaryRow))
            summary.PrisNto = summary.SummaryQuoteId is int id && amounts.TryGetValue(id, out var amount)
                ? amount : 0;
    }
}
