using UmiaPP.Models;

namespace UmiaPP.ViewModels;

public partial class LubeCalculationTabViewModel
{
    public async Task<(int Added, int Merged)> AddOrMergeFromPurchaseArticlesAsync(
        IReadOnlyList<PurchaseArticle> purchaseArticles)
    {
        if (purchaseArticles.Count == 0)
            return (0, 0);

        var added = 0;
        var merged = 0;

        foreach (var article in purchaseArticles)
        {
            var beteckning = article.Type?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(beteckning))
                continue;

            var antal = article.PurchaseQuantity;
            if (antal <= 0)
                continue;

            var existing = SakvarorMangder.FirstOrDefault(row =>
                !row.IsQuoteSummaryRow &&
                string.Equals((row.Beteckning ?? string.Empty).Trim(), beteckning, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.Antal += antal;
                if (string.IsNullOrWhiteSpace(existing.Grupp)) existing.Grupp = article.Category ?? string.Empty;
                if (string.IsNullOrWhiteSpace(existing.Dim1)) existing.Dim1 = article.Dimension ?? string.Empty;
                if (string.IsNullOrWhiteSpace(existing.Enhet)) existing.Enhet = article.PurchaseUnit;
                if (string.IsNullOrWhiteSpace(existing.Produktkod)) existing.Produktkod = article.ProductCode ?? string.Empty;
                if (string.IsNullOrWhiteSpace(existing.Leverantor)) existing.Leverantor = article.Supplier ?? string.Empty;

                await SaveSakvaraSpecificationRowAsync(existing);
                merged++;
                continue;
            }

            var newRow = new LubeSakvaraMangd
            {
                ProjectId = Project.Id,
                Grupp = article.Category ?? string.Empty,
                Beteckning = beteckning,
                Dim1 = article.Dimension ?? string.Empty,
                Dim2 = string.Empty,
                Antal = antal,
                Enhet = article.PurchaseUnit,
                Produkt = article.Type ?? string.Empty,
                Produktkod = article.ProductCode ?? string.Empty,
                Leverantor = article.Supplier ?? string.Empty,
                IsUserAdded = true,
                HasOriginalArticleIdentity = true,
                OriginalProduktkod = article.ProductCode ?? string.Empty,
                OriginalLeverantor = article.Supplier ?? string.Empty
            };

            SakvarorMangder.Add(newRow);
            Project.LubeSakvarorMangder ??= [];
            Project.LubeSakvarorMangder.Add(newRow);
            await SaveSakvaraSpecificationRowAsync(newRow);
            added++;
        }

        RefreshPackageQuoteLabels();
        return (added, merged);
    }
}
