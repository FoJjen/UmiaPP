using ClosedXML.Excel;
using System.IO;
using System.Text;
using UmiaPP.Models;

namespace UmiaPP.Services;

public interface IExportService
{
    Task ExportPurchaseListToExcelAsync(List<PurchaseSummaryItem> items, string projectName, string filePath);
    Task ExportPurchaseListToCsvAsync(List<PurchaseSummaryItem> items, string filePath);
}

public class ExportService : IExportService
{
    public async Task ExportPurchaseListToExcelAsync(List<PurchaseSummaryItem> items, string projectName, string filePath)
    {
        await Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Inköpssammanställning");

            // Rubrik
            worksheet.Cell(1, 1).Value = $"Inköpslista - {projectName}";
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Cell(1, 1).Style.Font.FontSize = 14;

            worksheet.Cell(2, 1).Value = $"Skapad: {DateTime.Now:yyyy-MM-dd HH:mm}";
            worksheet.Cell(2, 1).Style.Font.Italic = true;

            // Kolumnrubriker
            var headers = new[] { "Kategori", "Typ / Komponent", "Dimension / Beteckning", "Mängd", "Enhet", "Ritningsreferenser" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = worksheet.Cell(4, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(24, 76, 120);
                cell.Style.Font.FontColor = XLColor.White;
            }

            int row = 5;
            foreach (var item in items)
            {
                worksheet.Cell(row, 1).Value = item.Category;
                worksheet.Cell(row, 2).Value = item.Type;
                worksheet.Cell(row, 3).Value = item.DimensionOrCode;
                worksheet.Cell(row, 4).Value = item.Quantity;
                worksheet.Cell(row, 5).Value = item.Unit;
                worksheet.Cell(row, 6).Value = item.SheetReferences;

                if (row % 2 == 0)
                {
                    worksheet.Range(row, 1, row, 6).Style.Fill.BackgroundColor = XLColor.FromArgb(245, 247, 250);
                }

                row++;
            }

            worksheet.Columns().AdjustToContents();
            workbook.SaveAs(filePath);
        });
    }

    public async Task ExportPurchaseListToCsvAsync(List<PurchaseSummaryItem> items, string filePath)
    {
        await Task.Run(() =>
        {
            var sb = new StringBuilder();
            sb.AppendLine("Kategori;Typ;Dimension;Mängd;Enhet;Ritningsreferenser");

            foreach (var item in items)
            {
                sb.AppendLine($"\"{item.Category}\";\"{item.Type}\";\"{item.DimensionOrCode}\";{item.Quantity};\"{item.Unit}\";\"{item.SheetReferences}\"");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        });
    }
}
