using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using ClosedXML.Excel;
using ExcelDataReader;
using UmiaPP.Models;

namespace UmiaPP.Services;

public class LubeImportService : ILubeImportService
{
    public async Task<LubeImportResult> ImportAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            var result = new LubeImportResult();

            if (!File.Exists(filePath))
            {
                result.Success = false;
                result.ErrorMessage = $"Filen hittades inte: {filePath}";
                return result;
            }

            try
            {
                using var workbook = OpenWorkbook(filePath);
                var warnings = new List<string>();
                void Step(string name, Action action)
                {
                    try { action(); }
                    catch (Exception stepEx)
                    {
                        warnings.Add($"{name}: {stepEx.Message}");
                        System.Diagnostics.Debug.WriteLine($"[LubeImport] {name} misslyckades: {stepEx}");
                    }
                }

                Step("Kalkyl", () => ReadKalkylSheet(workbook, result));
                Step("Projektanpassning", () => ReadProjektanpassningSheet(workbook, result));
                Step("SakvarorMängder", () => ReadSakvarorMangderSheet(workbook, result));
                Step("SakvarorOfferter", () => ReadSakvarorOfferterSheet(workbook, result));
                Step("SakvarorLeveranspris", () => ReadSakvarorLeveransprisSheet(workbook, result));
                Step("SakvarorMontage", () => ReadSakvarorMontageSheet(workbook, result));
                Step("EjMängdbaserade", () => ReadEjMangdbaseradeSheet(workbook, result));

                if (result.SakvarorLeveranspriser.Count == 0 && result.SakvarorMangder.Count == 0)
                    Step("Sakvaror leveranspris (reserv)", () => ReadSakvarorLeveransprisByTitle(workbook, result));

                if (result.SakvarorLeveranspriser.Count == 0 && result.SakvarorMangder.Count == 0)
                    warnings.Add("SakvarorLeveranspris: inga rader hittades. Flikar: " + string.Join(", ", workbook.Worksheets.Select(w => w.Name)));

                if (result.SakvarorLeveranspriser.Count == 0 && result.SakvarorMangder.Count > 0)
                    result.SakvarorLeveranspriser.AddRange(result.SakvarorMangder.Select(ToLeveranspris));

                Step("Kalkylsammandrag", () => result.Summary = LubeSummaryParser.Parse(workbook));
                Step("Slå ihop dubbletter", () => MergeDuplicateSakvaror(result));

                result.Success = true;
                if (warnings.Count > 0)
                    result.ErrorMessage = "Vissa flikar kunde inte läsas:\n" + string.Join("\n", warnings);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Ett fel uppstod vid inläsning av Lube-filen: {ex.Message}";
            }

            return result;
        });
    }

    private static void ReadSakvarorLeveransprisByTitle(XLWorkbook workbook, LubeImportResult result)
    {
        foreach (var sheet in workbook.Worksheets)
        {
            var used = sheet.RangeUsed();
            if (used == null) continue;
            var titleRow = 0;
            for (var row = 1; row <= Math.Min(used.LastRow().RowNumber(), 15); row++)
            {
                var text = string.Join(" ", Enumerable.Range(1, Math.Min(used.LastColumn().ColumnNumber(), 12))
                    .Select(col => sheet.Cell(row, col).GetString()));
                if (text.Contains("Sakvaror leveranspris", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("SakvarorLeveranspris", StringComparison.OrdinalIgnoreCase))
                {
                    titleRow = row;
                    break;
                }
            }
            if (titleRow == 0) continue;

            var headerRow = 0;
            for (var row = titleRow; row <= titleRow + 8 && row <= used.LastRow().RowNumber(); row++)
            {
                var text = string.Join(" ", Enumerable.Range(1, Math.Min(used.LastColumn().ColumnNumber(), 12))
                    .Select(col => sheet.Cell(row, col).GetString()));
                if (text.Contains("Beteckning", StringComparison.OrdinalIgnoreCase))
                {
                    headerRow = row;
                    break;
                }
            }
            if (headerRow == 0) headerRow = titleRow + 6;

            for (var row = headerRow + 1; row <= used.LastRow().RowNumber(); row++)
            {
                var beteckning = sheet.Cell(row, 3).GetString().Trim();
                var produkt = sheet.Cell(row, 5).GetString().Trim();
                if (string.IsNullOrWhiteSpace(beteckning) && string.IsNullOrWhiteSpace(produkt)) continue;
                if (beteckning.Contains("Summa", StringComparison.OrdinalIgnoreCase)) break;

                var item = new LubeSakvaraMangd
                {
                    Grupp = sheet.Cell(row, 2).GetString().Trim(),
                    Beteckning = beteckning,
                    Antal = sheet.Cell(row, 4).GetDouble(),
                    Produkt = produkt,
                    Produktkod = sheet.Cell(row, 6).GetString().Trim(),
                    Leverantor = sheet.Cell(row, 7).GetString().Trim(),
                    PrisBto = sheet.Cell(row, 8).GetDouble(),
                    RabattSats = sheet.Cell(row, 9).GetDouble(),
                    PrisNto = sheet.Cell(row, 10).GetDouble(),
                    Offertpris = sheet.Cell(row, 11).GetDouble(),
                    Enhet = "st"
                };
                result.SakvarorMangder.Add(item);
            }
            if (result.SakvarorMangder.Count > 0) return;
        }
    }

    private static void MergeDuplicateSakvaror(LubeImportResult result)
    {
        if (result.SakvarorMangder.Count < 2) return;

        var merged = new List<LubeSakvaraMangd>();
        foreach (var item in result.SakvarorMangder)
        {
            var key = item.Beteckning?.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                merged.Add(item);
                continue;
            }

            var existing = merged.FirstOrDefault(row =>
                string.Equals(row.Beteckning?.Trim(), key, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                merged.Add(item);
                continue;
            }

            existing.Antal += item.Antal;
            existing.PrisBto += item.PrisBto;
            existing.PrisNto += item.PrisNto;
            existing.Offertpris += item.Offertpris;
            existing.Slutpris += item.Slutpris;
            if (string.IsNullOrWhiteSpace(existing.Produkt)) existing.Produkt = item.Produkt;
            if (string.IsNullOrWhiteSpace(existing.Produktkod)) existing.Produktkod = item.Produktkod;
            if (string.IsNullOrWhiteSpace(existing.Leverantor)) existing.Leverantor = item.Leverantor;
            if (string.IsNullOrWhiteSpace(existing.Grupp)) existing.Grupp = item.Grupp;
        }

        result.SakvarorMangder.Clear();
        result.SakvarorMangder.AddRange(merged);
    }

    private static readonly object EncodingSync = new();
    private static bool _encodingProviderRegistered;

    private static XLWorkbook OpenWorkbook(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        if (extension.Equals(".xls", StringComparison.OrdinalIgnoreCase))
            return OpenLegacyXlsWorkbook(filePath);

        return new XLWorkbook(filePath);
    }

    private static XLWorkbook OpenLegacyXlsWorkbook(string filePath)
    {
        EnsureLegacyExcelEncoding();

        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
        });

        var workbook = new XLWorkbook();
        foreach (DataTable table in dataSet.Tables)
        {
            var sheetName = string.IsNullOrWhiteSpace(table.TableName) ? "Sheet1" : table.TableName.Trim();
            if (sheetName.Length > 31)
                sheetName = sheetName[..31];

            var worksheet = workbook.Worksheets.Add(sheetName);
            for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                for (var columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
                {
                    var value = table.Rows[rowIndex][columnIndex];
                    if (value == null || value is DBNull)
                        continue;

                    var cell = worksheet.Cell(rowIndex + 1, columnIndex + 1);
                    switch (value)
                    {
                        case double d:
                            cell.Value = d;
                            break;
                        case float f:
                            cell.Value = f;
                            break;
                        case decimal m:
                            cell.Value = m;
                            break;
                        case int i:
                            cell.Value = i;
                            break;
                        case long l:
                            cell.Value = l;
                            break;
                        case DateTime dt:
                            cell.Value = dt;
                            break;
                        case bool b:
                            cell.Value = b;
                            break;
                        default:
                            cell.Value = Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;
                            break;
                    }
                }
            }
        }

        try
        {
            reader.Reset();
            var sheetIndex = 1;
            do
            {
                if (sheetIndex > workbook.Worksheets.Count) break;
                var rowIndex = 1;
                while (reader.Read())
                {
                    for (var columnIndex = 0; columnIndex < reader.FieldCount; columnIndex++)
                    {
                        var format = reader.GetNumberFormatString(columnIndex);
                        if (!string.IsNullOrEmpty(format))
                            workbook.Worksheet(sheetIndex).Cell(rowIndex, columnIndex + 1).Style.NumberFormat.Format = format;
                    }
                    rowIndex++;
                }
                sheetIndex++;
            } while (reader.NextResult());
        }
        catch (Exception formatEx)
        {
            System.Diagnostics.Debug.WriteLine($"[LubeImport] Talformat kunde inte läsas: {formatEx.Message}");
        }

        if (workbook.Worksheets.Count == 0)
            workbook.Worksheets.Add("Sheet1");

        return workbook;
    }

    private static void EnsureLegacyExcelEncoding()
    {
        if (_encodingProviderRegistered)
            return;

        lock (EncodingSync)
        {
            if (_encodingProviderRegistered)
                return;

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _encodingProviderRegistered = true;
        }
    }

    private IXLWorksheet? FindSheet(XLWorkbook wb, params string[] names)
    {
        foreach (var name in names)
        {
            var needle = name.Trim();
            var compactNeedle = CompactName(needle);
            var sheet = wb.Worksheets.FirstOrDefault(s =>
                string.Equals(s.Name.Trim(), needle, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(CompactName(s.Name), compactNeedle, StringComparison.OrdinalIgnoreCase));
            if (sheet != null) return sheet;
        }

        foreach (var name in names)
        {
            var needle = name.Trim();
            if (needle.Length < 8)
                continue;

            var sheet = wb.Worksheets.FirstOrDefault(s =>
                s.Name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
            if (sheet != null) return sheet;
        }

        return null;
    }

    private static string CompactName(string value) =>
        value.Replace(" ", string.Empty).Replace("å", "a").Replace("ä", "a").Replace("ö", "o")
             .Replace("Å", "A").Replace("Ä", "A").Replace("Ö", "O");

    private void ReadKalkylSheet(XLWorkbook wb, LubeImportResult result)
    {
        var sheet = FindSheet(wb, "Kalkyl", "Sammandrag", "Kalkylblad");
        if (sheet == null) return;

        var rows = sheet.RowsUsed().Skip(1); // Hoppa över rubrik
        foreach (var row in rows)
        {
            var kod = row.Cell(1).GetString().Trim();
            var benamning = row.Cell(2).GetString().Trim();
            if (string.IsNullOrWhiteSpace(kod) && string.IsNullOrWhiteSpace(benamning))
                continue;

            var enhet = row.Cell(3).GetString().Trim();
            double.TryParse(row.Cell(4).GetString(), out var mangd);
            double.TryParse(row.Cell(5).GetString(), out var matKostnad);
            double.TryParse(row.Cell(6).GetString(), out var montTimmar);
            double.TryParse(row.Cell(7).GetString(), out var totMat);
            double.TryParse(row.Cell(8).GetString(), out var totMontTimmar);

            // Om totalkolumner inte är beräknade i arket, beräkna dem
            if (totMat == 0 && mangd > 0 && matKostnad > 0) totMat = mangd * matKostnad;
            if (totMontTimmar == 0 && mangd > 0 && montTimmar > 0) totMontTimmar = mangd * montTimmar;

            result.KalkylPoster.Add(new LubeKalkylPost
            {
                Kod = kod,
                Benamning = benamning,
                Enhet = string.IsNullOrEmpty(enhet) ? "st" : enhet,
                Mangd = mangd,
                MaterialKostnad = matKostnad,
                MontageTimmar = montTimmar,
                TotaltMaterial = totMat,
                TotaltMontageTimmar = totMontTimmar
            });
        }
    }

    private void ReadProjektanpassningSheet(XLWorkbook wb, LubeImportResult result)
    {
        var sheet = FindSheet(wb, "Projektanpassning", "Anpassning", "Tidsjusteringar");
        if (sheet == null) return;

        var rows = sheet.RowsUsed().Skip(1);
        foreach (var row in rows)
        {
            var param = row.Cell(1).GetString().Trim();
            var desc = row.Cell(2).GetString().Trim();
            if (string.IsNullOrWhiteSpace(param) && string.IsNullOrWhiteSpace(desc)) continue;

            double.TryParse(row.Cell(3).GetString(), out var varde);
            var enhet = row.Cell(4).GetString().Trim();

            result.Anpassningar.Add(new LubeProjektanpassningPost
            {
                Parameter = param,
                Beskrivning = desc,
                Varde = varde,
                Enhet = enhet
            });
        }
    }

    private void ReadSakvarorMangderSheet(XLWorkbook wb, LubeImportResult result)
    {
        var sheet = FindSheet(wb, "SakvarorMängder", "SakvarorMangder", "Sakvaror Mängder", "Sakvaror");
        if (sheet == null) return;

        ReadSakvarorHeaderMeta(sheet, result);
        result.SakvarorMangder.AddRange(ParseSakvarorRows(sheet));
    }

    private void ReadSakvarorOfferterSheet(XLWorkbook wb, LubeImportResult result)
    {
        var sheet = FindSheet(wb, "SakvarorOfferter", "Sakvaror Offerter", "Offerter");
        if (sheet == null) return;

        var rows = sheet.RowsUsed().Skip(1);
        foreach (var row in rows)
        {
            var lev = row.Cell(1).GetString().Trim();
            var prod = row.Cell(2).GetString().Trim();
            if (string.IsNullOrWhiteSpace(lev) && string.IsNullOrWhiteSpace(prod)) continue;

            double.TryParse(row.Cell(3).GetString(), out var pris);
            var valuta = row.Cell(4).GetString().Trim();
            var kommentar = row.Cell(5).GetString().Trim();

            result.SakvarorOfferter.Add(new LubeSakvaraOffert
            {
                Leverantor = lev,
                Produktgrupp = prod,
                OffereratPris = pris,
                Valuta = string.IsNullOrEmpty(valuta) ? "SEK" : valuta,
                Kommentar = kommentar
            });
        }
    }

    private void ReadSakvarorLeveransprisSheet(XLWorkbook wb, LubeImportResult result)
    {
        var sheet = FindSheet(wb, "SakvarorLeveranspris", "Sakvaror Leveranspris", "Leveranspris");
        if (sheet == null) return;

        ReadSakvarorHeaderMeta(sheet, result);
        var rows = ParseSakvarorRows(sheet);
        if (rows.Count == 0)
            return;

        result.SakvarorLeveranspriser.AddRange(rows.Select(ToLeveranspris));
        if (result.SakvarorMangder.Count == 0)
            result.SakvarorMangder.AddRange(rows);
    }

    private static LubeSakvaraLeveranspris ToLeveranspris(LubeSakvaraMangd item) => new()
    {
        Produkt = string.IsNullOrWhiteSpace(item.Produkt) ? item.Beteckning : item.Produkt,
        Leverantor = item.Leverantor,
        BruttoPris = item.PrisBto,
        RabattSats = item.RabattSats,
        NettoPris = item.PrisNto > 0 ? item.PrisNto : item.Offertpris
    };

    private static void ReadSakvarorHeaderMeta(IXLWorksheet sheet, LubeImportResult result)
    {
        var lastMetaRow = Math.Min(12, sheet.LastRowUsed()?.RowNumber() ?? 1);
        for (var r = 1; r <= lastMetaRow; r++)
        {
            var txt = sheet.Cell(r, 1).GetString().Trim();
            var value = FirstNonEmpty(sheet.Cell(r, 2).GetString(), sheet.Cell(r, 3).GetString());
            if (txt.StartsWith("Projekt", StringComparison.OrdinalIgnoreCase))
                result.ProjektNummer = string.IsNullOrWhiteSpace(result.ProjektNummer) ? value : result.ProjektNummer;
            else if (txt.StartsWith("P-del", StringComparison.OrdinalIgnoreCase))
                result.PdelRitning = value;
            else if (txt.StartsWith("Handlin", StringComparison.OrdinalIgnoreCase))
                result.Handling = value;

            if (string.IsNullOrWhiteSpace(result.Datum))
            {
                var dateText = FirstNonEmpty(sheet.Cell(r, 8).GetString(), sheet.Cell(r, 9).GetString(), sheet.Cell(r, 10).GetString());
                if (DateTime.TryParse(dateText, out var date))
                    result.Datum = date.ToString("yyyy-MM-dd");
            }
        }
    }

    private static List<LubeSakvaraMangd> ParseSakvarorRows(IXLWorksheet sheet)
    {
        var items = new List<LubeSakvaraMangd>();
        var allRows = sheet.RowsUsed().ToList();
        if (allRows.Count == 0)
            return items;

        var headerRow = allRows.FirstOrDefault(row =>
        {
            var text = string.Join(" ", row.Cells().Select(c => c.GetString().Trim()));
            return text.Contains("Beteckning", StringComparison.OrdinalIgnoreCase);
        }) ?? allRows[0];

        var map = MapSakvarorColumns(headerRow);
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();

        for (var rowNumber = headerRow.RowNumber() + 1; rowNumber <= lastRow; rowNumber++)
        {
            var row = sheet.Row(rowNumber);
            var grupp = GetMappedText(row, map, "grupp", 1);
            var beteckning = GetMappedText(row, map, "beteckning", 2);
            if (string.IsNullOrWhiteSpace(beteckning) && !string.IsNullOrWhiteSpace(grupp) && !IsMetaLabel(grupp))
            {
                beteckning = grupp;
                grupp = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(beteckning) || IsMetaLabel(beteckning) || IsMetaLabel(grupp))
                continue;

            var antal = GetMappedNumber(row, map, "antal", 3);
            var produkt = GetMappedText(row, map, "produkt", 4);
            var produktkod = GetMappedText(row, map, "produktkod", 5);
            var leverantor = GetMappedText(row, map, "leverantor", 6);
            var prisBto = GetMappedNumber(row, map, "bto", 7);
            var rabatt = GetMappedNumber(row, map, "rabatt", 8);
            var prisNto = GetMappedNumber(row, map, "nto", 9);
            var offertpris = GetMappedNumber(row, map, "offert", 10);

            if (antal <= 0 && prisBto <= 0 && prisNto <= 0 && offertpris <= 0 &&
                string.IsNullOrWhiteSpace(produkt) && string.IsNullOrWhiteSpace(leverantor))
                continue;

            if (prisNto == 0 && prisBto > 0)
            {
                var factor = (100.0 - rabatt) / 100.0;
                prisNto = (antal > 0 ? prisBto * antal : prisBto) * factor;
            }

            items.Add(new LubeSakvaraMangd
            {
                Grupp = grupp,
                Beteckning = beteckning,
                Antal = antal <= 0 ? 1 : antal,
                Produkt = produkt,
                Produktkod = produktkod,
                Leverantor = leverantor,
                PrisBto = prisBto,
                RabattSats = rabatt,
                PrisNto = prisNto,
                Offertpris = offertpris,
                Enhet = "st"
            });
        }

        return items;
    }

    private static Dictionary<string, int> MapSakvarorColumns(IXLRow headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var text = cell.GetString().Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var column = cell.Address.ColumnNumber;
            if (text.Contains("grupp") && !text.Contains("beteck"))
                map["grupp"] = column;
            else if (text.Contains("beteck"))
                map["beteckning"] = column;
            else if (text.StartsWith("antal"))
                map["antal"] = column;
            else if (text.Contains("produktkod") || text == "kod")
                map["produktkod"] = column;
            else if (text.Contains("produkt") && !text.Contains("grupp"))
                map["produkt"] = column;
            else if (text.Contains("leverant"))
                map["leverantor"] = column;
            else if (text.Contains("bto") || text.Contains("brutto"))
                map["bto"] = column;
            else if (text == "%" || text.Contains("rabatt"))
                map["rabatt"] = column;
            else if (text.Contains("nto") || text.Contains("netto"))
                map["nto"] = column;
            else if (text.Contains("offert"))
                map["offert"] = column;
        }

        return map;
    }

    private static string GetMappedText(IXLRow row, Dictionary<string, int> map, string key, int fallback) =>
        row.Cell(map.TryGetValue(key, out var column) ? column : fallback).GetString().Trim();

    private static double GetMappedNumber(IXLRow row, Dictionary<string, int> map, string key, int fallback)
    {
        var raw = GetMappedText(row, map, key, fallback);
        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out var value) && value != 0)
            return value;
        double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        return value;
    }

    private static bool IsMetaLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.StartsWith("Projekt", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("P-del", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Handlin", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Sakvar", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Lubekalk", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Grupp", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Beteckning", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Summa", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Totalt", StringComparison.OrdinalIgnoreCase);
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;

    private void ReadSakvarorMontageSheet(XLWorkbook wb, LubeImportResult result)
    {
        var sheet = FindSheet(wb, "SakvarorMontage", "Sakvaror Montage", "Montage");
        if (sheet == null) return;

        var rows = sheet.RowsUsed().Skip(1);
        foreach (var row in rows)
        {
            var kategori = row.Cell(1).GetString().Trim();
            var beteckning = row.Cell(2).GetString().Trim();
            if (string.IsNullOrWhiteSpace(kategori) && string.IsNullOrWhiteSpace(beteckning)) continue;

            double.TryParse(row.Cell(3).GetString(), out var montPerEnhet);
            double.TryParse(row.Cell(4).GetString(), out var injPerEnhet);
            double.TryParse(row.Cell(5).GetString(), out var antal);

            result.SakvarorMontagePoster.Add(new LubeSakvaraMontage
            {
                ProduktKategori = kategori,
                Beteckning = beteckning,
                MontageTimmarPerEnhet = montPerEnhet,
                InjusteringTimmarPerEnhet = injPerEnhet,
                Antal = antal <= 0 ? 1 : antal
            });
        }
    }

    private void ReadEjMangdbaseradeSheet(XLWorkbook wb, LubeImportResult result)
    {
        var sheet = FindSheet(wb, "EjMängdbaserade", "EjMangdbaserade", "Ej Mängdbaserade");
        if (sheet == null) return;

        var rows = sheet.RowsUsed().Skip(1);
        foreach (var row in rows)
        {
            var moment = row.Cell(1).GetString().Trim();
            if (string.IsNullOrWhiteSpace(moment)) continue;

            double.TryParse(row.Cell(2).GetString(), out var timmar);
            double.TryParse(row.Cell(3).GetString(), out var kostnad);
            var kommentar = row.Cell(4).GetString().Trim();

            result.EjMangdbaseradePoster.Add(new LubeEjMangdbaserad
            {
                Moment = moment,
                Timmar = timmar,
                Kostnad = kostnad,
                Kommentar = kommentar
            });
        }
    }
}
