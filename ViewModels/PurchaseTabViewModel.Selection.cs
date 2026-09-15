using System.Collections.Generic;
using System.Linq;
using UmiaPP.Models;

namespace UmiaPP.ViewModels;

public partial class PurchaseTabViewModel
{
    private bool _isCombinedSelection;

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
        if (selected.Count <= 1)
        {
            SelectPurchaseList(selected.FirstOrDefault());
            return;
        }

        var sourceRows = selected.SelectMany(LoadListRows).ToList();
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
        var combined = sourceRows
            .GroupBy(row => new
            {
                row.Category, row.Type, row.Dimension, row.Unit,
                row.ProductCode, row.Supplier
            })
            .Select(group => new PurchaseArticle
            {
                Category = group.Key.Category,
                Type = group.Key.Type,
                Dimension = group.Key.Dimension,
                Unit = group.Key.Unit,
                ProductCode = group.Key.ProductCode,
                Supplier = group.Key.Supplier,
                Quantity = group.Sum(row => row.Quantity)
            })
            .ToList();

        // These rows are detached copies: they must never be saved to a drawing.
        SelectPurchaseList(null, combined);
        ListStatus = $"{selected.Count} listor valda: {combined.Count} summerade artikelrader. " +
            "Sammanställningen är skrivskyddad. Välj en lista för att redigera. " +
            "Inköpsantal för kanaler i meter avrundas uppåt efter summering (3 m per kanal).";
    }
}
