using System.Globalization;
using ClosedXML.Excel;
using UmiaPP.Models;

namespace UmiaPP.Services;

internal static class LubeSummaryParser
{
    public static LubeCalculationSummary Parse(XLWorkbook workbook)
    {
        var summary = new LubeCalculationSummary();
        foreach (var sheet in workbook.Worksheets)
        {
            var heading = sheet.CellsUsed().FirstOrDefault(c => Key(c.GetString()) == "MONTAGE");
            if (heading == null || !sheet.CellsUsed().Any(c => Key(c.GetString()) == "TOTAL PRODUKTIONSKOSTNAD"))
                continue;

            var column = heading.Address.ColumnNumber;
            LubeReportSection? section = null;
            var mode = "";
            var groups = false;
            foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() >= heading.Address.RowNumber))
            {
                var label = row.Cell(column).GetString().Trim();
                var key = Key(label);
                IXLCell Cell(int offset) => row.Cell(column + offset);
                double Number(int offset) => ReadNumber(Cell(offset));
                string Text(int offset) => Display(Cell(offset));
                void Amount(string name) => summary.ImportedAmounts[name] = Number(8);

                if (key == "MONTAGE" || key == "LEVERANSER" || key.StartsWith("INJUSTERING MED"))
                {
                    mode = key == "MONTAGE" ? "montage" : key == "LEVERANSER" ? "leverans" : "injustering";
                    section = new LubeReportSection { Title = key == "INJUSTERING MED MERA" ? "" : label };
                    summary.ReportSections.Add(section);
                    groups = false;
                    if (mode != "injustering")
                        continue;
                }
                if (Key(Cell(7).GetString()) == "TB %")
                {
                    mode = "total";
                    section = new LubeReportSection();
                    summary.ReportSections.Add(section);
                    groups = false;
                }
                if (section == null)
                    continue;
                if (key.Length == 0 && Enumerable.Range(2, 7).All(i => Cell(i).IsEmpty()))
                    continue;

                var header = key == "PRODUKTGRUPP" || key.StartsWith("INJUSTERING MED") || Key(Cell(7).GetString()) is "KR/DEL" or "TB %";
                var total = key.StartsWith("SUMMA") || key.StartsWith("TOTAL PRODUKTIONS");
                if (key == "PRODUKTGRUPP") groups = true;
                if (total) groups = false;
                var quantityOffset = mode == "montage" && (key.StartsWith("SUMMA EFTER") || key is "RESTIDSERSÄTTNING" or "SOCIALA AVGIFTER" or "RESKOSTNAD") ? 4 : 5;
                var quantity = Text(quantityOffset);
                if (quantityOffset == 4 && !Cell(6).IsEmpty()) quantity += " " + Text(6);
                var reportRow = new LubeReportRow
                {
                    Label = label, List = Text(2), Quantity = quantity, Rate = Text(7), Net = Text(8),
                    NetAmount = !header && !Cell(8).IsEmpty() ? Number(8) : null,
                    IsHeader = header, IsTotal = total, IsInput = !header && !total,
                    HasList = (groups || mode == "injustering") && !total
                };
                section.Rows.Add(reportRow);

                if (groups && !header && label.Length > 0)
                {
                    var item = new LubeSummaryItem { Produktgrupp = label, Lista = Cell(2).GetString().Trim() };
                    if (mode == "montage")
                    {
                        item.Timmar = Number(5);
                        summary.MontageGrupper.Add(item);
                    }
                    else if (mode == "leverans")
                    {
                        item.PrisBto = Number(5); item.RabattSats = Number(7); item.PrisNto = Number(8);
                        summary.LeveransGrupper.Add(item);
                    }
                }
                if (mode == "montage")
                {
                    if (key.StartsWith("SUMMA FÖRE")) summary.SummaForeJusteringTimmar = Number(5);
                    else if (key == "PROJEKTANPASSNING") summary.ProjektanpassningTimmar = Number(5);
                    else if (key.StartsWith("SUMMA EFTER"))
                    {
                        summary.SummaEfterJusteringTimmar = Number(4); summary.MontageTimlon = Number(7); Amount(nameof(summary.SummaEfterJusteringKrNto));
                    }
                    else if (key == "RESTIDSERSÄTTNING")
                    {
                        summary.RestidTimmar = Number(4); summary.RestidTimlon = Number(7); Amount(nameof(summary.RestidKrNto));
                    }
                    else if (key == "SOCIALA AVGIFTER")
                    {
                        summary.SocialaAvgifterPct = Number(4); Amount(nameof(summary.SocialaAvgifterKr));
                    }
                    else if (key == "RESKOSTNAD")
                    {
                        summary.ReskostnadKm = Number(4); summary.ReskostnadKrPerKm = Number(7); Amount(nameof(summary.ReskostnadKrNto));
                    }
                    else if (key.StartsWith("SUMMA MONTAGE")) Amount(nameof(summary.SummaMontageTotal));
                }
                else if (mode == "injustering")
                {
                    if (key == "INJUSTERING")
                    {
                        summary.InjusteringLista = Cell(2).GetString().Trim(); summary.InjusteringTimmar = Number(5);
                        summary.InjusteringTimlon = Number(7); Amount(nameof(summary.InjusteringKrNto));
                    }
                    else if (key == "SUMMA") Amount(nameof(summary.SummaInjusteringTotal));
                }
                else if (mode == "leverans")
                {
                    if (key.StartsWith("SUMMA FÖRE")) summary.LeveransSummaForeJustering = Number(8);
                    else if (key == "FRAKTER") { summary.FrakterPct = Number(5); Amount(nameof(summary.FrakterKr)); }
                    else if (key == "MONTAGEMATERIAL") { summary.MontagematerialPct = Number(5); Amount(nameof(summary.MontagematerialKr)); }
                    else if (key.StartsWith("SUMMA LEVERANS"))
                    {
                        Amount(nameof(summary.SummaLeveransTotal));
                        section = new LubeReportSection();
                        summary.ReportSections.Add(section);
                        mode = "underentreprenad";
                    }
                }
                if (key.StartsWith("SUMMA UNDERENTREPRENAD")) summary.SummaUnderentreprenad = Number(8);
                if (mode == "total")
                {
                    if (key == "ÖVRIGT") { summary.OvrigtKr = Number(5); summary.OvrigtTbPct = Number(7); }
                    else if (key.StartsWith("MONTAGE OCH INJUSTERING")) Amount(nameof(summary.MontageOchInjusteringKr));
                    else if (key == "TOTAL PRODUKTIONSKOSTNAD") { Amount(nameof(summary.TotalProduktionskostnad)); break; }
                }
            }
            return summary;
        }
        return summary;
    }

    private static string Key(string text) => string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    private static double ReadNumber(IXLCell cell)
    {
        if (cell.IsEmpty()) return 0;
        if (cell.TryGetValue<double>(out var number)) return number;
        throw new FormatException($"Ogiltigt tal i kalkylsammandraget: {cell.Address} ({cell.GetString()}).");
    }

    private static string Display(IXLCell cell)
    {
        if (cell.IsEmpty()) return string.Empty;
        if (cell.DataType != XLDataType.Number) return cell.GetString().Trim();
        var format = cell.Style.NumberFormat.Format;
        var number = cell.GetDouble();
        return number.ToString(format.Contains("#,##0") ? "N0" : "0.##########", CultureInfo.InvariantCulture);
    }
}
