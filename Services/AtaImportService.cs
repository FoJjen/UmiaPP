using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using UmiaPP.Models;

namespace UmiaPP.Services;

public interface IAtaImportService
{
    Task<AtaSummary> ImportAsync(string filePath);
}

public sealed class AtaImportService : IAtaImportService
{
    private static readonly CultureInfo Swedish = CultureInfo.GetCultureInfo("sv-SE");

    public Task<AtaSummary> ImportAsync(string filePath) => Task.Run(() => Read(filePath));

    private static AtaSummary Read(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        if (!extension.Equals(".xlsm", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Välj en .xlsm- eller .xlsx-fil med bladet Sammanställning.");

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var sheet = new SavedWorksheet(archive);
        var header = Enumerable.Range(1, 50).FirstOrDefault(r => Normalize(Text(sheet.Cell(r, 1))) == "NR"
            && Normalize(Text(sheet.Cell(r, 2))) == "DOKTYP");
        if (header == 0)
            throw new InvalidDataException("Rubrikraden för ÄTA hittades inte.");
        var expected = new Dictionary<int, string>
        {
            [3] = "Teknik", [9] = "Redovisad kostnad (kr)", [10] = "Godkänd kostnad (kr)",
            [11] = "Fakturerat (kr)", [12] = "Arbetstid (h)", [13] = "Projekt-ledning (h)",
            [14] = "Övrig tid (h)", [15] = "Material (kr)", [16] = "Anmärkning", [21] = "Godk."
        };
        foreach (var (column, label) in expected)
            if (Normalize(Text(sheet.Cell(header, column))) != Normalize(label))
                throw new InvalidDataException($"Oväntad mall: {sheet.Cell(header, column).Address} ska innehålla '{label}'.");

        var summary = new AtaSummary
        {
            FilePath = Path.GetFullPath(filePath), ImportedAt = DateTime.Now,
            ProjectName = Text(sheet.Cell("A6")), ProjectNumber = Text(sheet.Cell("G6")),
            Business = Text(sheet.Cell("I6")), Date = sheet.DateText("P6"), RevisionDate = sheet.DateText("R6"),
            Customer = Text(sheet.Cell("A8")), CustomerReference = Text(sheet.Cell("J8")),
            ProjectLeader = Text(sheet.Cell("A10")), ProjectManager = Text(sheet.Cell("A12"))
        };
        foreach (var row in sheet.Rows.Where(r => r > header).Order())
        {
            var number = Text(sheet.Cell(row, 1));
            var description = Text(sheet.Cell(row, 4));
            if (Normalize(number) is "SUMMA" or "SUMMOR" || Normalize(description) is "SUMMA" or "SUMMOR")
                continue;
            var item = new AtaSummaryRow
            {
                Number = number, DocumentType = Text(sheet.Cell(row, 2)), Discipline = Text(sheet.Cell(row, 3)),
                Description = description, ReportedAmount = Amount(sheet.Cell(row, 9)),
                ApprovedAmount = Amount(sheet.Cell(row, 10)), InvoicedAmount = Amount(sheet.Cell(row, 11)),
                WorkHours = Amount(sheet.Cell(row, 12)), ManagementHours = Amount(sheet.Cell(row, 13)),
                OtherHours = Amount(sheet.Cell(row, 14)), MaterialAmount = Amount(sheet.Cell(row, 15)),
                Remarks = Text(sheet.Cell(row, 16)), Approval = Text(sheet.Cell(row, 21))
            };
            if (string.IsNullOrWhiteSpace(item.Number + item.DocumentType + item.Discipline + item.Description + item.Remarks + item.Approval)
                && item.ReportedAmount == 0 && item.ApprovedAmount == 0 && item.InvoicedAmount == 0
                && item.WorkHours == 0 && item.ManagementHours == 0 && item.OtherHours == 0 && item.MaterialAmount == 0)
                continue;
            summary.Rows.Add(item);
        }
        return summary;
    }

    private static string Text(SavedCell cell) => cell.Value.Trim();

    private static decimal Amount(SavedCell cell)
    {
        var text = Text(cell);
        if (text.Length == 0) return 0;
        var numeric = cell.Type is "" or "n";
        if ((numeric || cell.Type is "s" or "str" or "inlineStr") && decimal.TryParse(text,
            numeric ? NumberStyles.Float : NumberStyles.Number, numeric ? CultureInfo.InvariantCulture : Swedish, out var amount))
            return amount;
        throw new InvalidDataException($"Cell {cell.Address} innehåller inte ett giltigt belopp eller timantal.");
    }

    private sealed record SavedCell(string Address, string Value, string Type);

    private sealed class SavedWorksheet
    {
        private static readonly XNamespace SheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private readonly Dictionary<string, XElement> _cells;
        private readonly string[] _strings;
        private readonly bool _date1904;
        public int[] Rows { get; }

        public SavedWorksheet(ZipArchive archive)
        {
            var workbook = ReadXml(archive, "xl/workbook.xml");
            var relationships = ReadXml(archive, "xl/_rels/workbook.xml.rels");
            var sheet = workbook.Descendants(SheetNs + "sheet").FirstOrDefault(s =>
                string.Equals((string?)s.Attribute("name"), "Sammanställning", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException("Filen saknar bladet Sammanställning.");
            var relation = relationships.Root!.Elements().Single(r => (string?)r.Attribute("Id") == (string?)sheet.Attribute(RelNs + "id"));
            var data = ReadXml(archive, PartPath(relation));
            var sharedStrings = relationships.Root.Elements().FirstOrDefault(r =>
                ((string?)r.Attribute("Type"))?.EndsWith("/sharedStrings", StringComparison.Ordinal) == true);
            _strings = sharedStrings == null ? [] : ReadXml(archive, PartPath(sharedStrings))
                .Descendants(SheetNs + "si").Select(StringValue).ToArray();
            _cells = data.Descendants(SheetNs + "sheetData").Elements(SheetNs + "row").Elements(SheetNs + "c")
                .ToDictionary(c => (string)c.Attribute("r")!, StringComparer.OrdinalIgnoreCase);
            Rows = data.Descendants(SheetNs + "sheetData").Elements(SheetNs + "row")
                .Select(r => (int)r.Attribute("r")!).ToArray();
            _date1904 = (string?)workbook.Root?.Element(SheetNs + "workbookPr")?.Attribute("date1904") is "1" or "true";
        }

        public SavedCell Cell(int row, int column) => Cell($"{(char)('A' + column - 1)}{row}");

        public SavedCell Cell(string address)
        {
            if (!_cells.TryGetValue(address, out var cell)) return new(address, "", "");
            var type = (string?)cell.Attribute("t") ?? "";
            var value = cell.Element(SheetNs + "v");
            if (type == "e" || (cell.Element(SheetNs + "f") != null && (value == null || (value.Value.Length == 0 && type != "str"))))
                throw new InvalidDataException($"Cell {address} saknar ett giltigt sparat värde. Beräkna och spara filen i Excel före import.");
            var text = value?.Value ?? "";
            if (type == "s")
            {
                if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var index) || index < 0 || index >= _strings.Length)
                    throw new InvalidDataException($"Cell {address} innehåller en ogiltig textreferens.");
                text = _strings[index];
            }
            else if (type == "inlineStr") text = StringValue(cell.Element(SheetNs + "is"));
            return new(address, text, type);
        }

        public string DateText(string address)
        {
            var cell = Cell(address);
            if (cell.Type is "" or "n" && double.TryParse(cell.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial))
                return DateTime.FromOADate(serial + (_date1904 ? 1462 : 0)).ToString("yyyy-MM-dd", Swedish);
            if (cell.Type == "d" && DateTime.TryParse(cell.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return date.ToString("yyyy-MM-dd", Swedish);
            return Text(cell);
        }

        private static string StringValue(XElement? element) => element == null ? ""
            : string.Concat(element.Elements(SheetNs + "t").Concat(element.Elements(SheetNs + "r").Elements(SheetNs + "t")).Select(t => t.Value));

        private static string PartPath(XElement relationship)
        {
            if ((string?)relationship.Attribute("TargetMode") == "External")
                throw new InvalidDataException("Externa blad stöds inte i ÄTA-sammanställningen.");
            var target = (string?)relationship.Attribute("Target") ?? throw new InvalidDataException("Bladets sökväg saknas.");
            return new Uri(new Uri("http://package/xl/workbook.xml"), target).AbsolutePath.TrimStart('/');
        }

        private static XDocument ReadXml(ZipArchive archive, string path)
        {
            var entry = archive.GetEntry(path) ?? throw new InvalidDataException($"Excel-filen saknar {path}.");
            using var stream = entry.Open();
            return XDocument.Load(stream);
        }
    }

    private static string Normalize(string text) => string.Concat(text.Where(c => !char.IsWhiteSpace(c) && c != '-')).ToUpperInvariant();
}
