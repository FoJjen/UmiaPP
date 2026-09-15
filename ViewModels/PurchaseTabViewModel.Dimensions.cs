using System;
using System.Linq;
using System.Threading.Tasks;
using UmiaPP.Models;
using UmiaPP.Services;

namespace UmiaPP.ViewModels;

public partial class PurchaseTabViewModel
{
    private async Task<BluebeamImportResult> ParsePurchaseMarkupXmlAsync(string filePath)
    {
        var result = await _bluebeamParser.ParseMarkupSummaryXmlAsync(filePath);
        if (!result.Success) return result;

        // Resolve the source items before grouping so unrelated unknown
        // dimensions are not permanently merged into one purchase article.
        var unresolved = result.Items.Where(RequiresPurchaseImportReview).ToList();

        if (unresolved.Count > 0)
        {
            // Cancellation deliberately continues importing the original items.
            _dialogService.ReviewPurchaseImport(unresolved);
            result.SummaryItems = _bluebeamParser.GroupAndSummarize(result.Items);
        }

        return result;
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
