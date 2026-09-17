using System.Collections.Generic;
using System.Linq;
using UmiaPP.Models;

namespace UmiaPP.ViewModels;

public partial class PurchaseTabViewModel
{
    private bool _isCombinedSelection;
    private readonly Dictionary<PurchaseArticle, List<(Drawing Drawing, PurchaseArticle Article)>> _combinedRowSources = [];
    private List<Drawing> _combinedDrawings = [];

    public bool IsCombinedSelection
    {
        get => _isCombinedSelection;
        private set
        {
            if (SetProperty(ref _isCombinedSelection, value))
                OnPropertyChanged(nameof(CanEditSelectedList));
        }
    }

    public bool CanEditSelectedList => !IsCombinedSelection;

    public void SetSelectedPurchaseLists(IEnumerable<Drawing> drawings)
    {
        var selected = drawings.Where(Drawings.Contains).Distinct().ToList();
        _combinedRowSources.Clear();
        _combinedDrawings = [];
        if (selected.Count <= 1)
        {
            SelectPurchaseList(selected.FirstOrDefault());
            return;
        }

        var sourceRows = selected.SelectMany(drawing =>
            LoadListRows(drawing).Select(article => (Drawing: drawing, Article: article))).ToList();
        if (selected.Any(_unreadableLists.Contains))
        {
            // Never present a partial total as a complete purchase quantity.
            SelectPurchaseList(null, new List<PurchaseArticle>());
            ListStatus = "En eller flera valda listor kan inte läsas. Sammanställningen visas inte för att undvika ofullständiga inköpsmängder.";
            return;
        }

        // Sum the original quantities, not the rounded purchase quantities.
        // Product codes and suppliers remain separate to avoid combining
        // different purchasable articles with the same description.
        var combined = new List<PurchaseArticle>();
        foreach (var group in sourceRows.GroupBy(entry => new
            {
                entry.Article.Category, entry.Article.Type, entry.Article.Dimension,
                entry.Article.Unit, entry.Article.ProductCode, entry.Article.Supplier
            }))
        {
            var summary = new PurchaseArticle
            {
                Category = group.Key.Category,
                Type = group.Key.Type,
                Dimension = group.Key.Dimension,
                Unit = group.Key.Unit,
                ProductCode = group.Key.ProductCode,
                Supplier = group.Key.Supplier,
                Quantity = group.Sum(entry => entry.Article.Quantity)
            };
            combined.Add(summary);
            _combinedRowSources[summary] = group.ToList();
        }

        // The mapping identifies editable summary rows. Metadata is saved by
        // Type across the whole project, never using summed quantities.
        _combinedDrawings = selected;
        SelectPurchaseList(null, combined);
        ListStatus = $"{selected.Count} listor valda: {combined.Count} summerade artikelrader. " +
            "Produktkod och Leverantör kan ändras och sparas på alla artiklar med samma Typ / Artikel i hela projektet när raden bekräftas, även i ritningar som inte är valda. Övriga fält är skrivskyddade. " +
            "Inköpsantal för kanaler i meter avrundas uppåt efter summering (3 m per kanal).";
    }
}
