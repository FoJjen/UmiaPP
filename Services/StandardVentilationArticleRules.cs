using System.Globalization;
using System.Text.RegularExpressions;

namespace UmiaPP.Services;

/// <summary>Unambiguous standard names that must not require manual import review.</summary>
internal static class StandardVentilationArticleRules
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly Regex BendName = new(@"\bb[öo]j(?:ar)?\b", Options);
    private static readonly Regex SaddleName = new(@"\bsadel\s*avstick\b", Options);
    private static readonly Regex TeeName = new(@"\bt\s*[-–]?\s*r[öo]r\b", Options);
    private static readonly Regex ReducerName = new(
        @"\b(?:dim|reducering|reduktion|excentrik|excentrisk|exentrik|exentrisk)\b", Options);
    private static readonly Regex ConnectionSizes = new(
        @"(?<![\d.,])[Øø]?\s*(?<sizes>\d{2,4}(?:\s*[-–]\s*[Øø]?\s*\d{2,4}){1,2})(?![\d.,]|\s*[-–]\s*[Øø]?\s*\d)",
        Options);
    private static readonly Regex BendSizes = new(
        @"(?<![\d.,])[Øø]?\s*(?<diameter>\d{2,4})\s*[-–]\s*(?<angle>\d{1,3}(?:[.,]\d+)?)\s*°?(?![\d.,])",
        Options);
    private static readonly Regex StraightDuct = new(
        @"^\s*(?:[Øø]?\s*(?<dimension>\d{2,4}(?:\s*[xX*]\s*\d{2,4})?)\s*kanal(?:er)?|kanal(?:er)?\s*[Øø]?\s*(?<dimension>\d{2,4}(?:\s*[xX*]\s*\d{2,4})?))\s*$",
        Options);

    public static bool TryRecognize(string? subject, out string category, out string dimension)
    {
        category = string.Empty;
        dimension = string.Empty;
        if (string.IsNullOrWhiteSpace(subject)) return false;

        if (TryReadBend(subject, out var diameter, out _))
        {
            category = "Detalj";
            dimension = diameter;
            return true;
        }

        if (TryReadConnectionFitting(subject, out var connectionDimension))
        {
            category = "Detalj";
            dimension = connectionDimension;
            return true;
        }

        // Anchoring prevents a fitting whose description mentions a duct from
        // being mistaken for a straight duct.
        var duct = StraightDuct.Match(subject);
        if (!duct.Success) return false;
        category = "Kanal";
        dimension = Regex.Replace(duct.Groups["dimension"].Value, @"\s+", string.Empty)
            .Replace('X', 'x').Replace('*', 'x');
        return true;
    }

    public static bool TryGetBendType(string? subject, out string type)
    {
        type = string.Empty;
        if (!TryReadBend(subject, out _, out var angle)) return false;
        // The angle belongs in the type, not the diameter. This keeps 45-degree
        // and 90-degree bends separate even when their diameters are identical.
        type = $"Böj {angle.ToString("0.###", CultureInfo.InvariantCulture)}°";
        // Preserve additional descriptors so insulated/special bends are not
        // merged with otherwise similar standard bends.
        var remainder = BendName.Replace(BendSizes.Replace(subject!, " "), " ");
        remainder = Regex.Replace(remainder, @"\s+", " ").Trim();
        if (remainder.Length > 0) type += " " + remainder;
        return true;
    }

    public static bool TryGetDetailType(string? subject, out string type)
    {
        if (!TryReadConnectionFitting(subject, out _))
            return TryGetBendType(subject, out type);

        // Sizes belong in Dimension / Beteckning; retain variant descriptors
        // such as GG, Nippel/Nippel, and eccentric in Typ / Artikel.
        var description = ConnectionSizes.Replace(subject!, " ");
        description = SaddleName.Replace(description, "Sadelavstick");
        description = TeeName.Replace(description, "T-rör");
        type = Regex.Replace(description, @"\s+", " ").Trim();
        return true;
    }

    public static string FormatDetailDimension(string? dimension)
    {
        if (string.IsNullOrWhiteSpace(dimension) || dimension.Trim() == "-") return "-";
        var text = dimension.Trim();
        var connections = ConnectionSizes.Match(text);
        if (connections.Success && connections.Index == 0 && connections.Length == text.Length)
            return NormalizeConnectionSizes(connections.Groups["sizes"].Value);

        // Single-diameter details, including bends, keep their diameter symbol.
        return "Ø" + text.TrimStart('Ø', 'ø').Trim();
    }

    private static bool TryReadConnectionFitting(string? subject, out string dimension)
    {
        dimension = string.Empty;
        if (string.IsNullOrWhiteSpace(subject) || BendName.IsMatch(subject)) return false;
        if (!SaddleName.IsMatch(subject) && !TeeName.IsMatch(subject) && !ReducerName.IsMatch(subject))
            return false;
        var match = ConnectionSizes.Match(subject);
        if (!match.Success) return false;
        dimension = NormalizeConnectionSizes(match.Groups["sizes"].Value);
        return true;
    }

    private static string NormalizeConnectionSizes(string text) =>
        Regex.Replace(text, @"[\sØø]", string.Empty).Replace('–', '-');

    private static bool TryReadBend(string? subject, out string diameter, out double angle)
    {
        diameter = string.Empty;
        angle = 0;
        if (string.IsNullOrWhiteSpace(subject) || !BendName.IsMatch(subject)) return false;
        var match = BendSizes.Match(subject);
        if (!match.Success || !double.TryParse(match.Groups["angle"].Value.Replace(',', '.'),
                NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out angle) ||
            !double.IsFinite(angle) || angle <= 0 || angle > 180)
            return false;

        diameter = match.Groups["diameter"].Value;
        return true;
    }
}
