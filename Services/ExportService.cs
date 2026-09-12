using ClosedXML.Excel;
using System.IO;
using System.Text;
using UmiaPP.Models;

namespace UmiaPP.Services;

public interface IExportService
{
    Task ExportPurchaseListToExcelAsync(List<PurchaseSummaryItem> items, string projectName, string filePath);
    Task ExportPurchaseListToCsvAsync(List<PurchaseSummaryItem> items, string filePath);
    Task ExportAtaInvoiceToExcelAsync(AtaSummary? summary, AtaSummaryRow row, string filePath);
    Task ExportAtaInvoiceToPdfAsync(AtaSummary? summary, AtaSummaryRow row, string filePath);
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

    public async Task ExportAtaInvoiceToExcelAsync(AtaSummary? summary, AtaSummaryRow row, string filePath)
    {
        await Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add($"ÄTA {row.Number}");

            // Rubrik och Umia Header
            sheet.Cell("A1").Value = "UMIA - FAKTURERINGS-/ÄTA-UNDERLAG";
            sheet.Cell("A1").Style.Font.Bold = true;
            sheet.Cell("A1").Style.Font.FontSize = 14;
            sheet.Cell("A1").Style.Font.FontColor = XLColor.FromArgb(227, 82, 5); // Umia Orange

            sheet.Cell("A3").Value = "Projekt:";
            sheet.Cell("B3").Value = summary?.ProjectName ?? string.Empty;
            sheet.Cell("D3").Value = "Projekt-/Ordernr:";
            sheet.Cell("E3").Value = summary?.ProjectNumber ?? string.Empty;

            sheet.Cell("A4").Value = "Beställare/kund:";
            sheet.Cell("B4").Value = summary?.Customer ?? string.Empty;
            sheet.Cell("D4").Value = "Datum:";
            sheet.Cell("E4").Value = string.IsNullOrWhiteSpace(summary?.Date) ? DateTime.Now.ToString("yyyy-MM-dd") : summary.Date;

            sheet.Cell("A5").Value = "Projektledare:";
            sheet.Cell("B5").Value = summary?.ProjectLeader ?? string.Empty;
            sheet.Cell("D5").Value = "Teknik:";
            sheet.Cell("E5").Value = row.Discipline;

            sheet.Cell("A7").Value = "ÄTA-nummer:";
            sheet.Cell("B7").Value = row.Number;
            sheet.Cell("A8").Value = "Dokumenttyp:";
            sheet.Cell("B8").Value = row.DocumentType;
            sheet.Cell("A9").Value = "Benämning:";
            sheet.Cell("B9").Value = row.Description;
            sheet.Cell("A10").Value = "Status / Godkänd:";
            sheet.Cell("B10").Value = string.IsNullOrWhiteSpace(row.Approval) ? "Ej angivet" : row.Approval;

            // Header sektion
            sheet.Range("A12:E12").Style.Fill.BackgroundColor = XLColor.FromArgb(235, 235, 235);
            sheet.Range("A12:E12").Style.Font.Bold = true;
            sheet.Cell("A12").Value = "Beskrivning / Moment";
            sheet.Cell("C12").Value = "Mängd / Timmar";
            sheet.Cell("D12").Value = "Enhet";
            sheet.Cell("E12").Value = "Belopp (kr)";

            // Rader
            int curRow = 13;
            if (row.WorkHours > 0)
            {
                sheet.Cell(curRow, 1).Value = "Arbetstid montör/tekniker";
                sheet.Cell(curRow, 3).Value = (double)row.WorkHours;
                sheet.Cell(curRow, 4).Value = "h";
                curRow++;
            }

            if (row.ManagementHours > 0)
            {
                sheet.Cell(curRow, 1).Value = "Projektledning";
                sheet.Cell(curRow, 3).Value = (double)row.ManagementHours;
                sheet.Cell(curRow, 4).Value = "h";
                curRow++;
            }

            if (row.OtherHours > 0)
            {
                sheet.Cell(curRow, 1).Value = "Övrig tid";
                sheet.Cell(curRow, 3).Value = (double)row.OtherHours;
                sheet.Cell(curRow, 4).Value = "h";
                curRow++;
            }

            if (row.MaterialAmount > 0)
            {
                sheet.Cell(curRow, 1).Value = "Material";
                sheet.Cell(curRow, 5).Value = (double)row.MaterialAmount;
                curRow++;
            }

            // Totals
            curRow++;
            sheet.Range($"A{curRow}:E{curRow}").Style.Border.TopBorder = XLBorderStyleValues.Thin;
            sheet.Cell(curRow, 1).Value = "Begärt / Offererat belopp:";
            sheet.Cell(curRow, 1).Style.Font.Bold = true;
            sheet.Cell(curRow, 5).Value = (double)row.ReportedAmount;
            sheet.Cell(curRow, 5).Style.Font.Bold = true;

            curRow++;
            sheet.Cell(curRow, 1).Value = "Godkänt belopp:";
            sheet.Cell(curRow, 1).Style.Font.Bold = true;
            sheet.Cell(curRow, 5).Value = (double)row.ApprovedAmount;
            sheet.Cell(curRow, 5).Style.Font.Bold = true;
            sheet.Cell(curRow, 5).Style.Font.FontColor = XLColor.FromArgb(227, 82, 5);

            curRow++;
            sheet.Cell(curRow, 1).Value = "Fakturerat belopp:";
            sheet.Cell(curRow, 5).Value = (double)row.InvoicedAmount;
            sheet.Cell(curRow, 5).Style.Font.Bold = true;

            sheet.Columns().AdjustToContents();
            workbook.SaveAs(filePath);
        });
    }

    public async Task ExportAtaInvoiceToPdfAsync(AtaSummary? summary, AtaSummaryRow row, string filePath)
    {
        await Task.Run(() =>
        {
            var dateStr = string.IsNullOrWhiteSpace(summary?.Date) ? DateTime.Now.ToString("yyyy-MM-dd") : summary.Date;
            var approvalStr = string.IsNullOrWhiteSpace(row.Approval) ? "Ej angivet" : row.Approval;

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"sv\">");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset=\"UTF-8\">");
            sb.AppendLine($"  <title>Faktureringsunderlag ÄTA {row.Number}</title>");
            sb.AppendLine("  <style>");
            sb.AppendLine("    body { font-family: 'Segoe UI', Arial, sans-serif; margin: 40px; color: #333; }");
            sb.AppendLine("    .header { border-bottom: 3px solid #E35205; padding-bottom: 12px; margin-bottom: 24px; }");
            sb.AppendLine("    .logo-title { font-size: 24px; font-weight: bold; color: #E35205; margin: 0; }");
            sb.AppendLine("    .doc-subtitle { font-size: 14px; color: #666; margin-top: 4px; }");
            sb.AppendLine("    .meta-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 12px 30px; margin-bottom: 24px; }");
            sb.AppendLine("    .meta-item { display: flex; justify-content: space-between; border-bottom: 1px dotted #ccc; padding-bottom: 4px; }");
            sb.AppendLine("    .meta-label { font-weight: 600; color: #555; }");
            sb.AppendLine("    table { width: 100%; border-collapse: collapse; margin-top: 20px; }");
            sb.AppendLine("    th { background-color: #244C78; color: white; text-align: left; padding: 10px; font-size: 13px; }");
            sb.AppendLine("    td { padding: 10px; border-bottom: 1px solid #ddd; font-size: 13px; }");
            sb.AppendLine("    .num { text-align: right; }");
            sb.AppendLine("    .summary-card { margin-top: 30px; border: 1px solid #ddd; background: #f9f9f9; padding: 16px; border-radius: 4px; }");
            sb.AppendLine("    .sum-row { display: flex; justify-content: space-between; padding: 6px 0; font-size: 14px; }");
            sb.AppendLine("    .sum-row.total { font-weight: bold; font-size: 16px; border-top: 2px solid #333; margin-top: 8px; padding-top: 8px; }");
            sb.AppendLine("    .sum-row.revenue { color: #E35205; }");
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            // Header
            sb.AppendLine("  <div class=\"header\">");
            sb.AppendLine("    <div class=\"logo-title\">UMIA</div>");
            sb.AppendLine($"    <div class=\"doc-subtitle\">Faktureringsunderlag – ÄTA #{row.Number}</div>");
            sb.AppendLine("  </div>");

            // Metadata
            sb.AppendLine("  <div class=\"meta-grid\">");
            sb.AppendLine($"    <div class=\"meta-item\"><span class=\"meta-label\">Projekt:</span> <span>{summary?.ProjectName ?? "-"}</span></div>");
            sb.AppendLine($"    <div class=\"meta-item\"><span class=\"meta-label\">Projekt-/Ordernr:</span> <span>{summary?.ProjectNumber ?? "-"}</span></div>");
            sb.AppendLine($"    <div class=\"meta-item\"><span class=\"meta-label\">Beställare:</span> <span>{summary?.Customer ?? "-"}</span></div>");
            sb.AppendLine($"    <div class=\"meta-item\"><span class=\"meta-label\">Datum:</span> <span>{dateStr}</span></div>");
            sb.AppendLine($"    <div class=\"meta-item\"><span class=\"meta-label\">Projektledare:</span> <span>{summary?.ProjectLeader ?? "-"}</span></div>");
            sb.AppendLine($"    <div class=\"meta-item\"><span class=\"meta-label\">Teknik:</span> <span>{row.Discipline}</span></div>");
            sb.AppendLine($"    <div class=\"meta-item\"><span class=\"meta-label\">Dokumenttyp:</span> <span>{row.DocumentType}</span></div>");
            sb.AppendLine($"    <div class=\"meta-item\"><span class=\"meta-label\">Status / Godkänd:</span> <span>{approvalStr}</span></div>");
            sb.AppendLine("  </div>");

            sb.AppendLine($"  <p><strong>Benämning / Beskrivning:</strong> {row.Description}</p>");

            // Specifikationstabell
            sb.AppendLine("  <table>");
            sb.AppendLine("    <thead>");
            sb.AppendLine("      <tr>");
            sb.AppendLine("        <th>Moment / Beskrivning</th>");
            sb.AppendLine("        <th class=\"num\">Mängd / Timmar</th>");
            sb.AppendLine("        <th>Enhet</th>");
            sb.AppendLine("        <th class=\"num\">Belopp (kr)</th>");
            sb.AppendLine("      </tr>");
            sb.AppendLine("    </thead>");
            sb.AppendLine("    <tbody>");

            if (row.WorkHours > 0)
                sb.AppendLine($"      <tr><td>Arbetstid montör/tekniker</td><td class=\"num\">{row.WorkHours:N2}</td><td>h</td><td class=\"num\">-</td></tr>");
            if (row.ManagementHours > 0)
                sb.AppendLine($"      <tr><td>Projektledning</td><td class=\"num\">{row.ManagementHours:N2}</td><td>h</td><td class=\"num\">-</td></tr>");
            if (row.OtherHours > 0)
                sb.AppendLine($"      <tr><td>Övrig tid</td><td class=\"num\">{row.OtherHours:N2}</td><td>h</td><td class=\"num\">-</td></tr>");
            if (row.MaterialAmount > 0)
                sb.AppendLine($"      <tr><td>Material</td><td class=\"num\">-</td><td>-</td><td class=\"num\">{row.MaterialAmount:N0} kr</td></tr>");

            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");

            // Summeringskort
            sb.AppendLine("  <div class=\"summary-card\">");
            sb.AppendLine($"    <div class=\"sum-row\"><span>Total arbetstid:</span> <strong>{row.TotalHours:N2} h</strong></div>");
            sb.AppendLine($"    <div class=\"sum-row\"><span>Begärt belopp:</span> <span>{row.ReportedAmount:N0} kr</span></div>");
            sb.AppendLine($"    <div class=\"sum-row total revenue\"><span>Godkänt belopp:</span> <span>{row.ApprovedAmount:N0} kr</span></div>");
            sb.AppendLine($"    <div class=\"sum-row\"><span>Fakturerat belopp:</span> <span>{row.InvoicedAmount:N0} kr</span></div>");
            sb.AppendLine("  </div>");

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        });
    }
}
