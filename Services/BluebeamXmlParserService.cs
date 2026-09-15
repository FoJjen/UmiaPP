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
                DimensionOrCode = StandardVentilationArticleRules.FormatDetailDimension(group.Key.Dimension),
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

        // Fallback: Inkludera alla övriga artiklar som inte redan sammanfattats
        var summarizedItems = new HashSet<BluebeamMarkupItem>(summaries.SelectMany(s => items.Where(i => i.Category == s.Category && i.Subject == s.Type && i.Dimension == s.DimensionOrCode)));
        var fallbackItems = items.Where(i => !summarizedItems.Contains(i)).ToList();
        foreach (var item in fallbackItems)
        {
            var dim = string.IsNullOrWhiteSpace(item.Dimension) ? "Ospecificerad dim" : item.Dimension;
            var qty = item.LengthMeters + item.VerticalHeightMeters > 0 ? item.LengthMeters + item.VerticalHeightMeters : item.Count;
            var unit = item.LengthMeters + item.VerticalHeightMeters > 0 ? "m" : "st";

            // Explicit manual category handling
            if (item.Category.Equals("Sakvara/Don", StringComparison.OrdinalIgnoreCase))
            {
                summaries.Add(new PurchaseSummaryItem
                {
                    Category = "Sakvara/Don",
                    Type = item.Subject,
                    DimensionOrCode = dim,
                    Quantity = Math.Round(item.LengthMeters + item.VerticalHeightMeters > 0 ? qty : item.Count, unit == "m" ? 1 : 0),
                    Unit = item.LengthMeters + item.VerticalHeightMeters > 0 ? "m" : "st",
                    SheetReferences = item.SheetLabel
                });
            }
            else
            {
                summaries.Add(new PurchaseSummaryItem
                {
                    Category = "Ej klassificerade",
                    Type = item.Subject,
                    DimensionOrCode = dim,
                    Quantity = Math.Round(qty, unit == "m" ? 1 : 0),
                    Unit = unit,
                    SheetReferences = item.SheetLabel
                });
            }
        }

        return summaries.OrderBy(s => s.Category).ThenBy(s => s.Type).ThenBy(s => s.DimensionOrCode).ToList();
    }

    private (string Category, string Dimension) CategorizeAndExtractDimension(string subject, string layer)
    {
        var text = $"{subject} {layer}".ToLowerInvariant();

        if (StandardVentilationArticleRules.TryRecognize(subject, out var standardCategory, out var standardDimension))
            return (standardCategory, standardDimension);

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

        // Kategori igenkänning för Sadelavstickor och T-rör
        if (Regex.IsMatch(subject, @"sadel\s*avstick", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline) ||
            Regex.IsMatch(subject, @"sadelavstick", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline))
        {
            // Hitta och normalisera dimensioner för sadelavstick
            var sadelDimMatch = Regex.Match(subject, @"(?:sadel\s*avstick|sadelavstick)[^\d]*(\d{2,4})[^\d]*(\d{2,4})(?:[^\d]*(\d{2,4}))?", RegexOptions.IgnoreCase);
            if (sadelDimMatch.Success)
            {
                var dim1 = sadelDimMatch.Groups[1].Value;
                var dim2 = sadelDimMatch.Groups[2].Value;
                var dim3 = sadelDimMatch.Groups[3].Value;

                // Återge som två eller tre dimensioner åtskilda av bindestreck
                dim = !string.IsNullOrWhiteSpace(dim3) ? $"{dim1}-{dim2}-{dim3}" : $"{dim1}-{dim2}";
            }

            return ("Detalj", dim);
        }

        if (Regex.IsMatch(subject, @"t[\s-]?r[\s-]?ö[r]?|trör", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline))
        {
            // Hitta och normalisera dimensioner för t-rör
            var trorDimMatch = Regex.Match(subject, @"(?:t[\s-]?r[\s-]?ö[r]?|trör)[^\d]*(\d{2,4})[^\d]*(\d{2,4})?", RegexOptions.IgnoreCase);
            if (trorDimMatch.Success)
            {
                var dim1 = trorDimMatch.Groups[1].Value;
                var dim2 = trorDimMatch.Groups[2].Value;

                dim = dim1 == dim2 ? dim1 : $"{dim1}-{dim2}";
            }

            return ("Detalj", dim);
        }

        // Identifiera kategori baserat på välkända namn och mönster
        // Kan utökas med fler regler vid behov
        if (Regex.IsMatch(subject, @"\b(?:s\r?\n?bak(?:ventil)?|frånlufts(?:don)?|z?\s*rör)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return ("Ventilation", dim);
        if (Regex.IsMatch(subject, @"\b(?:s\b avsnitt|frånlufts(?:don)?|regel(?:ventil)?|juster(?:bar)?|spjäll|filter)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return ("Övrig VVS", dim);
        if (Regex.IsMatch(subject, @"\b(?:el(?:materiel)?|belysn(?:ing|ingsarmatur)|armatur|stänger?|sladdar?|kontakter?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return ("El", dim);
        if (Regex.IsMatch(subject, @"\bvattentät(?:a|t)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return ("Vattentätning", dim);
        if (Regex.IsMatch(subject, @"\b(?:gips|durok|cementskivor?|skivmaterial(?:\s*med)?\s*hydro)?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return ("Byggmaterial", dim);

        return ("Okänd", dim);
    }

    private static string GetElementValue(XElement element, params string[] names)
    {
        foreach (var name in names)
        {
            var child = element.Elements().FirstOrDefault(candidate =>
                string.Equals(candidate.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));
            if (child != null && !string.IsNullOrWhiteSpace(child.Value))
                return child.Value.Trim();
        }
        return string.Empty;
    }

    private static double ParseDouble(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        // XML measurements can include units, decimal commas, and grouping spaces.
        var match = Regex.Match(text, @"[-+]?\d[\d\s\u00A0\u202F.,]*");
        if (!match.Success) return 0;
        var number = Regex.Replace(match.Value, @"[\s\u00A0\u202F]", string.Empty);
        var comma = number.LastIndexOf(',');
        var dot = number.LastIndexOf('.');
        if (comma >= 0 && dot >= 0)
            number = comma > dot ? number.Replace(".", string.Empty).Replace(',', '.')
                                : number.Replace(",", string.Empty);
        else if (comma >= 0)
            number = number.Replace(',', '.');
        return double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && double.IsFinite(value) ? value : 0;
    }

    private static string CleanDetailType(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject)) return "Detalj";
        if (StandardVentilationArticleRules.TryGetDetailType(subject, out var standardBendType))
            return standardBendType;
        if (Regex.IsMatch(subject, @"\bsadel\s*avstick\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return "Sadelavstick";
        if (Regex.IsMatch(subject, @"\bt\s*[-–]?\s*r[öo]r\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return "T-rör";
        // Remove the leading diameter only. Keep bend angles and other distinguishing
        // text so different fitting types are not merged into one purchase article.
        var cleaned = Regex.Replace(subject.Trim(), @"^\s*[Øø]?\s*\d{2,4}(?:[xX*]\d{2,4})?\b\s*[-–]?\s*", string.Empty);
        return string.IsNullOrWhiteSpace(cleaned) ? subject.Trim() : cleaned.Trim();
    }

    private static string CleanSubjectCode(string subject)
    {
        // Preserve the complete product code; hyphens and digits are meaningful.
        return Regex.Replace(subject ?? string.Empty, @"\s+", " ").Trim();
    }
}
