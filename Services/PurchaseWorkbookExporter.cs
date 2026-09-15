using ClosedXML.Excel;
using UmiaPP.Models;

namespace UmiaPP.Services;

internal sealed record PurchaseExportList(string Name, List<PurchaseArticle> Articles);

internal static class PurchaseWorkbookExporter
{
    public static Task ExportAsync(IReadOnlyList<PurchaseExportList> lists, string projectName, string filePath)
    {
        return Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Sammanfattning" };

            WriteSheet(workbook.Worksheets.Add("Sammanfattning"),
                $"Inköpssammanfattning - {projectName}",
                lists.SelectMany(list => list.Articles.Select(article => (list.Name, Article: article))));

            foreach (var list in lists)
            {
                var sheetName = UniqueSheetName(list.Name, usedNames);
                WriteSheet(workbook.Worksheets.Add(sheetName),
                    $"{list.Name} - {projectName}",
                    list.Articles.Select(article => (list.Name, Article: article)));
            }

            workbook.SaveAs(filePath);
        });
    }

    private static void WriteSheet(IXLWorksheet sheet, string title,
        IEnumerable<(string Name, PurchaseArticle Article)> source)
    {
        sheet.Cell(1, 1).Value = title;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        sheet.Cell(2, 1).Value = $"Skapad: {DateTime.Now:yyyy-MM-dd HH:mm}";
        sheet.Cell(3, 1).Value = "Kanaler i meter: inköpsantal = summerad längd / 3, avrundat uppåt. Originalmängder behålls.";

        string[] headers = ["Kategori", "Typ / Artikel", "Dimension / Beteckning", "Mängd", "Enhet",
            "Inköpsantal", "Inköpsenhet", "Produktkod", "Leverantör", "Ritningar / Listor"];
        for (var column = 0; column < headers.Length; column++)
            sheet.Cell(5, column + 1).Value = headers[column];
        var header = sheet.Range(5, 1, 5, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.FromArgb(24, 76, 120);

        // Use the same article identity as the purchasing multi-selection view.
        // Aggregate meters first, then derive the number of 3-meter lengths.
        var groups = source.GroupBy(entry => new
        {
            entry.Article.Category, entry.Article.Type, entry.Article.Dimension,
            entry.Article.Unit, entry.Article.ProductCode, entry.Article.Supplier
        }).OrderBy(group => group.Key.Category)
          .ThenBy(group => group.Key.Type)
          .ThenBy(group => group.Key.Dimension)
          .ThenBy(group => group.Key.ProductCode)
          .ThenBy(group => group.Key.Supplier);

        var row = 6;
        foreach (var group in groups)
        {
            var article = new PurchaseArticle
            {
                Category = group.Key.Category,
                Type = group.Key.Type,
                Dimension = group.Key.Dimension,
                Unit = group.Key.Unit,
                ProductCode = group.Key.ProductCode,
                Supplier = group.Key.Supplier,
                Quantity = group.Sum(entry => entry.Article.Quantity)
            };
            sheet.Cell(row, 1).Value = article.Category ?? string.Empty;
            sheet.Cell(row, 2).Value = article.Type ?? string.Empty;
            sheet.Cell(row, 3).Value = article.Dimension ?? string.Empty;
            sheet.Cell(row, 4).Value = article.Quantity;
            sheet.Cell(row, 5).Value = article.Unit ?? string.Empty;
            sheet.Cell(row, 6).Value = article.PurchaseQuantity;
            sheet.Cell(row, 7).Value = article.PurchaseUnit ?? string.Empty;
            sheet.Cell(row, 8).Value = article.ProductCode ?? string.Empty;
            sheet.Cell(row, 9).Value = article.Supplier ?? string.Empty;
            sheet.Cell(row, 10).Value = string.Join(", ", group.Select(entry => entry.Name).Distinct());
            if (row % 2 == 0)
                sheet.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.FromArgb(245, 247, 250);
            row++;
        }

        sheet.Column(4).Style.NumberFormat.Format = "#,##0.00";
        sheet.Column(6).Style.NumberFormat.Format = "#,##0.##";
        sheet.Range(5, 1, Math.Max(5, row - 1), headers.Length).SetAutoFilter();
        sheet.SheetView.FreezeRows(5);
        // Size using headers and data, not the long title/instructions.
        sheet.Columns(1, headers.Length).AdjustToContents(5, Math.Max(5, row - 1));
    }

    private static string UniqueSheetName(string name, HashSet<string> usedNames)
    {
        const string invalid = "[]:*?/\\";
        var clean = new string(name.Where(character => !char.IsControl(character) &&
            !invalid.Contains(character)).ToArray()).Trim().Trim('\'');
        if (string.IsNullOrWhiteSpace(clean)) clean = "Lista";
        if (string.Equals(clean, "History", StringComparison.OrdinalIgnoreCase)) clean = "History_lista";
        var basis = clean[..Math.Min(31, clean.Length)].TrimEnd('\'');
        var candidate = basis;
        for (var suffix = 2; !usedNames.Add(candidate); suffix++)
        {
            var ending = $" ({suffix})";
            candidate = basis[..Math.Min(basis.Length, 31 - ending.Length)] + ending;
        }
        return candidate;
    }
}
