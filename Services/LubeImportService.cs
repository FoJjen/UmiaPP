using ClosedXML.Excel;
using UmiaPP.Models;
using System.IO;

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
                using var workbook = new XLWorkbook(filePath);

                // 1. Kalkyl (Sammandrag, timmar, material och montage)
                ReadKalkylSheet(workbook, result);

                // 2. Projektanpassning (Tidsjusteringar, procentpåslag, etableringstimmar)
                ReadProjektanpassningSheet(workbook, result);

                // 3. SakvarorMängder (Beteckning, Dim1, Dim2, Antal/st)
                ReadSakvarorMangderSheet(workbook, result);

                // 4. SakvarorOfferter (Leverantörer, offererade priser)
                ReadSakvarorOfferterSheet(workbook, result);

                // 5. SakvarorLeveranspris (Produkt, leverantör, brutto/nettopris)
                ReadSakvarorLeveransprisSheet(workbook, result);

                // 6. SakvarorMontage (Kanalfläktar, galler, montage- och injusteringstimmar per enhet)
                ReadSakvarorMontageSheet(workbook, result);

                // 7. EjMängdbaserade (Postbaserade timmar och kostnader som OVK, CE-märkning, täthetsprovning)
                ReadEjMangdbaseradeSheet(workbook, result);

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Ett fel uppstod vid inläsning av Lube-filen: {ex.Message}";
            }

            return result;
        });
    }

    private IXLWorksheet? FindSheet(XLWorkbook wb, params string[] names)
    {
        foreach (var name in names)
        {
            var sheet = wb.Worksheets.FirstOrDefault(s =>
                string.Equals(s.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase) ||
                s.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
            if (sheet != null) return sheet;
        }
        return null;
    }

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
        var sheet = FindSheet(wb, "SakvarorMängder", "SakvarorMangder", "Sakvaror Mängder");
        if (sheet == null) return;

        var rows = sheet.RowsUsed().Skip(1);
        foreach (var row in rows)
        {
            var beteckning = row.Cell(1).GetString().Trim();
            if (string.IsNullOrWhiteSpace(beteckning)) continue;

            var dim1 = row.Cell(2).GetString().Trim();
            var dim2 = row.Cell(3).GetString().Trim();
            double.TryParse(row.Cell(4).GetString(), out var antal);
            var enhet = row.Cell(5).GetString().Trim();

            result.SakvarorMangder.Add(new LubeSakvaraMangd
            {
                Beteckning = beteckning,
                Dim1 = dim1,
                Dim2 = dim2,
                Antal = antal <= 0 ? 1 : antal,
                Enhet = string.IsNullOrEmpty(enhet) ? "st" : enhet
            });
        }
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

        var rows = sheet.RowsUsed().Skip(1);
        foreach (var row in rows)
        {
            var prod = row.Cell(1).GetString().Trim();
            var lev = row.Cell(2).GetString().Trim();
            if (string.IsNullOrWhiteSpace(prod) && string.IsNullOrWhiteSpace(lev)) continue;

            double.TryParse(row.Cell(3).GetString(), out var brutto);
            double.TryParse(row.Cell(4).GetString(), out var rabatt);
            double.TryParse(row.Cell(5).GetString(), out var netto);

            if (netto == 0 && brutto > 0)
            {
                netto = brutto * (1.0 - (rabatt / 100.0));
            }

            result.SakvarorLeveranspriser.Add(new LubeSakvaraLeveranspris
            {
                Produkt = prod,
                Leverantor = lev,
                BruttoPris = brutto,
                RabattSats = rabatt,
                NettoPris = netto
            });
        }
    }

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
