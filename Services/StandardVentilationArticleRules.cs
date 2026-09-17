using System.Globalization;
using System.Text.RegularExpressions;

namespace UmiaPP.Services;

/// <summary>Recognition and formatting for standard ventilation article names.</summary>
internal static class StandardVentilationArticleRules
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly Regex BendName = new(@"\bb[öo]j(?:ar)?\b", Options);
    private static readonly Regex SaddleName = new(@"\bsadel\s*avstick\b", Options);
    private static readonly Regex TeeName = new(@"\bt\s*[-–]?\s*r[öo]r\b", Options);
    private static readonly Regex ReducerName = new(
        @"\b(?:dim|reducering|reduktion|excentrik|excentrisk|exentrik|exentrisk)\b", Options);
    private static readonly Regex SingleConnectionFitting = new(
        @"^\s*(?:[Øø]?\s*(?<dimension>\d{2,4})\s*(?<type>plan\s*avstick|nippel)|(?<type>plan\s*avstick|nippel)\s*[Øø]?\s*(?<dimension>\d{2,4}))(?<suffix>\s+.*)?\s*$",
        Options);
    private static readonly Regex ConnectionSizes = new(
        @"(?<![\d.,])[Øø]?\s*(?<sizes>\d{2,4}(?:\s*[-–]\s*[Øø]?\s*\d{2,4}){1,2})(?![\d.,]|\s*[-–]\s*[Øø]?\s*\d)",
        Options);
    private static readonly Regex BendSizes = new(
        @"(?<![\d.,])[Øø]?\s*(?<diameter>\d{2,4})\s*[-–]\s*(?<angle>\d{1,3}(?:[.,]\d+)?)\s*°?(?![\d.,])",
        Options);
    private static readonly Regex StraightDuct = new(
        @"^\s*(?:[Øø]?\s*(?<dimension>\d{2,4}(?:\s*[xX*]\s*\d{2,4})?)\s*(?:vertikal(?:a)?\s+)?kanal(?:er)?|(?:vertikal(?:a)?\s+)?kanal(?:er)?\s*[Øø]?\s*(?<dimension>\d{2,4}(?:\s*[xX*]\s*\d{2,4})?))\s*$",
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

        if (TryReadSingleConnectionFitting(subject, out var fittingDimension, out _))
        {
            category = "Detalj";
            dimension = fittingDimension;
            return true;
        }

        // Match complete standard duct names, not fittings mentioning a duct.
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

        // Diameter and angle have different meanings: 100-90 is a diameter
        // of 100 and an angle of 90, not two connection diameters.
        type = $"Böj {angle.ToString("0.###", CultureInfo.InvariantCulture)}°";
        var remainder = BendName.Replace(BendSizes.Replace(subject!, " "), " ");
        remainder = Regex.Replace(remainder, @"\s+", " ").Trim();
        if (remainder.Length > 0) type += " " + remainder;
        return true;
    }

    public static bool TryGetDetailType(string? subject, out string type)
    {
        if (TryReadSingleConnectionFitting(subject, out _, out type)) return true;
        if (!TryReadConnectionFitting(subject, out _))
            return TryGetBendType(subject, out type);

        // Keep GG/Nippel-Nippel and eccentric variants in the description.
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

        return "Ø" + text.TrimStart('Ø', 'ø').Trim();
    }

    private static bool TryReadSingleConnectionFitting(string? subject, out string dimension, out string type)
    {
        dimension = string.Empty;
        type = string.Empty;
        if (string.IsNullOrWhiteSpace(subject)) return false;
        var match = SingleConnectionFitting.Match(subject);
        if (!match.Success) return false;

        dimension = match.Groups["dimension"].Value;
        var name = Regex.Replace(match.Groups["type"].Value, @"\s+", string.Empty);
        type = name.Equals("nippel", StringComparison.OrdinalIgnoreCase) ? "Nippel" : "Planavstick";
        // Retain descriptive variants instead of merging distinct fittings.
        var suffix = Regex.Replace(match.Groups["suffix"].Value, @"\s+", " ").Trim();
        if (suffix.Length > 0) type += " " + suffix;
        return true;
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
