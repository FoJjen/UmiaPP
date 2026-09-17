using System;
using System.Linq;
using System.Threading.Tasks;
using UmiaPP.Models;
using UmiaPP.Services;

namespace UmiaPP.ViewModels;

public partial class PurchaseTabViewModel
{
    private readonly Dictionary<BluebeamMarkupItem, string> _purchaseImportOriginalSubjects = [];

    private async Task<BluebeamImportResult> ParsePurchaseMarkupXmlAsync(string filePath)
    {
        _pendingPurchaseCategories.Clear();
        var result = await _bluebeamParser.ParseMarkupSummaryXmlAsync(filePath);
        if (!result.Success) return result;

        // Capture original subjects once per XML import, before any corrections.
        _purchaseImportOriginalSubjects.Clear();
        foreach (var item in result.Items)
            _purchaseImportOriginalSubjects[item] = item.Subject ?? string.Empty;

        // Explicit user choices override automatic classification, but never
        // replace dimensions, quantities, source descriptions, or drawing IDs.
        ApplyRememberedPurchaseCategories(result.Items);

        // Resolve the source items before grouping so unrelated unknown
        // dimensions are not permanently merged into one purchase article.
        var unresolved = result.Items.Where(RequiresPurchaseImportReview).ToList();

        if (unresolved.Count > 0)
        {
            // Include all matching original subjects across drawings, even if
            // only some of those rows originally required review.
            var subjects = unresolved.Select(item => _purchaseImportOriginalSubjects[item])
                .Where(subject => !string.IsNullOrWhiteSpace(subject)).ToHashSet(StringComparer.Ordinal);
            var unresolvedItems = unresolved.ToHashSet();
            var reviewItems = result.Items.Where(item => unresolvedItems.Contains(item) ||
                subjects.Contains(_purchaseImportOriginalSubjects[item])).ToList();
            // Cancellation deliberately continues importing the original items.
            ReviewImportedPurchaseItems(reviewItems);
        }

        result.SummaryItems = _bluebeamParser.GroupAndSummarize(result.Items);
        return result;
    }

    private bool ReviewImportedPurchaseItems(IReadOnlyList<BluebeamMarkupItem> items)
    {
        if (!_dialogService.ReviewPurchaseImport(items, _purchaseImportOriginalSubjects, _pendingPurchaseCategories))
            return false;
        foreach (var original in _purchaseImportOriginalSubjects)
            if (_pendingPurchaseCategories.TryGetValue(original.Value, out var category))
                original.Key.Category = category;
        return true;
    }

    private bool RequiresPurchaseImportReview(BluebeamMarkupItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Category) ||
            string.Equals(item.Category, "Okänd", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Category, "Ej klassificerade", StringComparison.OrdinalIgnoreCase))
            return true;

        if ((item.Category == "Kanal" || item.Category == "Detalj") &&
            (string.IsNullOrWhiteSpace(item.Dimension) || item.Dimension.Trim() == "-" ||
             string.Equals(item.Dimension.Trim(), "Ospecificerad dim", StringComparison.OrdinalIgnoreCase)))
            return true;

        // Also catch unsupported categories that the summary retains as fallback
        // rows, rather than assuming every nonempty category is understood.
        var summaries = _bluebeamParser.GroupAndSummarize([item]);
        return summaries.Count == 0 || summaries.Any(summary =>
            string.Equals(summary.Category, "Ej klassificerade", StringComparison.OrdinalIgnoreCase));
    }
}
