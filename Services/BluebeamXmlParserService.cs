using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UmiaPP.Models;

namespace UmiaPP.Services;

public class BluebeamImportResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<BluebeamMarkupItem> Items { get; set; } = [];
    public List<PurchaseSummaryItem> SummaryItems { get; set; } = [];
}

public interface IBluebeamXmlParserService
{
    Task<BluebeamImportResult> ParseMarkupSummaryXmlAsync(string xmlFilePath);
    List<PurchaseSummaryItem> GroupAndSummarize(List<BluebeamMarkupItem> items);
}

public class BluebeamXmlParserService : IBluebeamXmlParserService
{
    private static readonly Regex DimensionRegex = new(@"\b(?:[dD]im\s*[:=]?\s*|[øØ]?\s*)(\d{2,4}(?:[xX*]\d{2,4})?)\b", RegexOptions.Compiled);
    private static readonly Regex NumberPrefixRegex = new(@"^(\d{2,4}(?:[xX*]\d{2,4})?)\b", RegexOptions.Compiled);

    public async Task<BluebeamImportResult> ParseMarkupSummaryXmlAsync(string xmlFilePath)
    {
        return await Task.Run(() =>
        {
            var result = new BluebeamImportResult();

            if (!File.Exists(xmlFilePath))
            {
                result.Success = false;
                result.ErrorMessage = $"Filen hittades inte: {xmlFilePath}";
                return result;
            }

            try
            {
                var doc = XDocument.Load(xmlFilePath);
                var markups = doc.Descendants().Where(e => e.Name.LocalName.Equals("Markup", StringComparison.OrdinalIgnoreCase));

                foreach (var markup in markups)
                {
                    // Läs taggar enligt specifikationen
                    var amne = GetElementValue(markup, "Ämne", "Amne", "Subject", "Label");
                    var lager = GetElementValue(markup, "Lager", "Layer");
                    var langdStr = GetElementValue(markup, "Längd", "Langd", "Length", "Measurement");
                    var antalStr = GetElementValue(markup, "Antal_Luft", "AntalLuft", "Count", "Antal");
                    var vertikalStr = GetElementValue(markup, "Total_vertikalhöjd_luft", "Total_vertikalhojd_luft", "VerticalHeight");
                    var sidetikett = GetElementValue(markup, "Sidetikett", "PageLabel", "Page", "Drawing");
                    var kommentarer = GetElementValue(markup, "Kommentarer", "Comments");

                    if (string.IsNullOrWhiteSpace(amne) && string.IsNullOrWhiteSpace(lager))
                        continue;

                    var length = ParseDouble(langdStr);
                    var count = ParseDouble(antalStr);
                    if (count <= 0)
                    {
                        // Försök tolka antal från kommentarer eller default 1
                        if (!string.IsNullOrWhiteSpace(kommentarer) && double.TryParse(kommentarer.Trim(), out var parsedCount))
                        {
                            count = parsedCount;
                        }
                        else
                        {
                            count = 1;
                        }
                    }

                    var verticalHeight = ParseDouble(vertikalStr);

                    // Extrahera dimension och kategori
                    var (category, dimension) = CategorizeAndExtractDimension(amne, lager);

                    result.Items.Add(new BluebeamMarkupItem
                    {
                        Subject = amne,
                        Layer = lager,
                        LengthMeters = length,
                        Count = count,
                        VerticalHeightMeters = verticalHeight,
                        SheetLabel = sidetikett,
                        Comments = kommentarer,
                        Category = category,
                        Dimension = dimension
                    });
                }

                result.SummaryItems = GroupAndSummarize(result.Items);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Fel vid parsing av Bluebeam XML: {ex.Message}";
            }

            return result;
        });
    }

    public List<PurchaseSummaryItem> GroupAndSummarize(List<BluebeamMarkupItem> items)
    {
        var summaries = new List<PurchaseSummaryItem>();

        // 1. Raka kanaler per dimension (löpande meter) - inkluderar horisontell längd och vertikalhöjd
        var ductItems = items.Where(i => i.Category == "Kanal").ToList();
        var ductGroups = ductItems.GroupBy(i => i.Dimension);
        foreach (var group in ductGroups)
        {
            var dim = string.IsNullOrWhiteSpace(group.Key) ? "Ospecificerad dim" : $"Ø{group.Key}";
            var totalLength = group.Sum(x => x.LengthMeters + x.VerticalHeightMeters);
            if (totalLength <= 0)
            {
                // Om längd inte angivits men Count finns (t.ex. styckvisa standardlängder 3m)
                totalLength = group.Sum(x => x.Count * 3.0);
            }

            var sheets = string.Join(", ", group.Select(x => x.SheetLabel).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());

            summaries.Add(new PurchaseSummaryItem
            {
                Category = "Kanaler",
                Type = "Raka ventilationskanaler",
                DimensionOrCode = dim,
                Quantity = Math.Round(totalLength, 1),
                Unit = "m",
                SheetReferences = sheets
            });
        }

        // 2. Detaljer (böjar, T-rör, dim, planavstick) per typ och dimension (st)
        var detailItems = items.Where(i => i.Category == "Detalj").ToList();
        var detailGroups = detailItems.GroupBy(i => new { DetailType = CleanDetailType(i.Subject), i.Dimension });
        foreach (var group in detailGroups)
        {
            var totalCount = group.Sum(x => x.Count);
            var sheets = string.Join(", ", group.Select(x => x.SheetLabel).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());

            summaries.Add(new PurchaseSummaryItem
            {
                Category = "Kanal-detaljer",
                Type = group.Key.DetailType,
                DimensionOrCode = string.IsNullOrWhiteSpace(group.Key.Dimension) ? "-" : $"Ø{group.Key.Dimension}",
                Quantity = Math.Round(totalCount, 0),
                Unit = "st",
                SheetReferences = sheets
            });
        }

        // 3. Sakvaror/Don/Brandspjäll/Ljuddämpare per beteckning (st)
        var componentItems = items.Where(i => i.Category != "Kanal" && i.Category != "Detalj").ToList();
        var compGroups = componentItems.GroupBy(i => new { i.Category, Code = CleanSubjectCode(i.Subject) });
        foreach (var group in compGroups)
        {
            var totalCount = group.Sum(x => x.Count);
            var sheets = string.Join(", ", group.Select(x => x.SheetLabel).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());

            summaries.Add(new PurchaseSummaryItem
            {
                Category = group.Key.Category,
                Type = group.Key.Category,
                DimensionOrCode = group.Key.Code,
                Quantity = Math.Round(totalCount, 0),
                Unit = "st",
                SheetReferences = sheets
            });
        }

        return summaries.OrderBy(s => s.Category).ThenBy(s => s.Type).ThenBy(s => s.DimensionOrCode).ToList();
    }

    private (string Category, string Dimension) CategorizeAndExtractDimension(string subject, string layer)
    {
        var text = $"{subject} {layer}".ToLowerInvariant();

        // Extrahera dimension (t.ex. 160, 200, 250, 315, 400, 500, etc.)
        var dim = "";
        var prefixMatch = NumberPrefixRegex.Match(subject.Trim());
        if (prefixMatch.Success)
        {
            dim = prefixMatch.Groups[1].Value;
        }
        else
        {
            var dimMatch = DimensionRegex.Match(subject);
            if (dimMatch.Success)
            {
                dim = dimMatch.Groups[1].Value;
            }
        }

        if (text.Contains("kanal") || layer.Equals("Kanaler", StringComparison.OrdinalIgnoreCase))
        {
            if (text.Contains("böj") || text.Contains("boj") || text.Contains("t-rör") || text.Contains("trör") ||
                text.Contains("reduktion") || text.Contains("avstick") || text.Contains("muff") || text.Contains("nippel") ||
                text.Contains("rens") || text.Contains("huv") || text.Contains("galler"))
            {
                return ("Detalj", dim);
            }
            return ("Kanal", dim);
        }

        if (text.Contains("böj") || text.Contains("boj") || text.Contains("t-rör") || text.Contains("trör") ||
            text.Contains("reduktion") || text.Contains("påstick") || text.Contains("planavstick"))
        {
            return ("Detalj", dim);
        }

        if (text.Contains("brandspjäll") || text.Contains("brand") || layer.Contains("brand", StringComparison.OrdinalIgnoreCase))
        {
            return ("Brandspjäll", dim);
        }

        if (text.Contains("ljuddämpare") || text.Contains("dämpare") || layer.Contains("ljuddämp", StringComparison.OrdinalIgnoreCase))
        {
            return ("Ljuddämpare", dim);
        }

        if (text.Contains("don") || text.Contains("ventil") || text.Contains("spridare") || text.Contains("galler") ||
            layer.Contains("don", StringComparison.OrdinalIgnoreCase))
        {
            return ("Don & Ventiler", dim);
        }

        if (text.Contains("fläkt") || text.Contains("kanalfläkt") || text.Contains("aggregat"))
        {
            return ("Fläktar & Aggregat", dim);
        }

        return ("Sakvaror", dim);
    }

    private string CleanDetailType(string subject)
    {
        var s = subject.ToLowerInvariant();
        if (s.Contains("90") && (s.Contains("böj") || s.Contains("boj"))) return "90° Böj";
        if (s.Contains("45") && (s.Contains("böj") || s.Contains("boj"))) return "45° Böj";
        if (s.Contains("30") && (s.Contains("böj") || s.Contains("boj"))) return "30° Böj";
        if (s.Contains("15") && (s.Contains("böj") || s.Contains("boj"))) return "15° Böj";
        if (s.Contains("böj") || s.Contains("boj")) return "Böj";
        if (s.Contains("t-rör") || s.Contains("trör")) return "T-rör";
        if (s.Contains("reduktion") || s.Contains("dim")) return "Reduktion";
        if (s.Contains("planavstick") || s.Contains("avstick")) return "Planavstick";
        if (s.Contains("gavel")) return "Gavel";
        return subject;
    }

    private string CleanSubjectCode(string subject)
    {
        return string.IsNullOrWhiteSpace(subject) ? "Ospecificerad" : subject.Trim();
    }

    private string GetElementValue(XElement parent, params string[] localNames)
    {
        foreach (var name in localNames)
        {
            var el = parent.Elements().FirstOrDefault(e => e.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (el != null && !string.IsNullOrWhiteSpace(el.Value))
                return el.Value.Trim();

            // Även kolla attribut
            var attr = parent.Attributes().FirstOrDefault(a => a.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (attr != null && !string.IsNullOrWhiteSpace(attr.Value))
                return attr.Value.Trim();
        }
        return string.Empty;
    }

    private double ParseDouble(string val)
    {
        if (string.IsNullOrWhiteSpace(val)) return 0;
        // Ta bort ev "m", "st", spaces etc
        val = Regex.Replace(val, @"[^\d,\.\-]", "");
        val = val.Replace(',', '.');
        return double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0;
    }
}
