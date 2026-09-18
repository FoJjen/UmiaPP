using ClosedXML.Excel;
using UmiaPP.Models;

namespace UmiaPP.Services;

internal static class InspectionWorkbookExporter
{
    public static Task ExportAsync(
        Project project,
        Drawing drawing,
        DrawingInspection header,
        IReadOnlyList<DrawingInspectionItem> items,
        string filePath)
    {
        return Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Egenkontroll");
            var workOrder = DeliveryMarking.Format(project.ProjectNumber, project.Discipline);
            var discipline = DeliveryMarking.ShortName(project.Discipline);
            var date = header.Date == default ? DateTime.Today : header.Date;
            var revision = string.IsNullOrWhiteSpace(header.Revision) ? drawing.Revision : header.Revision;

            sheet.Column(1).Width = 22;
            sheet.Column(2).Width = 22;
            sheet.Column(3).Width = 18;
            sheet.Column(4).Width = 16;
            sheet.Column(5).Width = 16;
            sheet.Column(6).Width = 18;
            sheet.Column(7).Width = 12;
            sheet.Column(8).Width = 12;

            sheet.Cell(1, 1).Value = "umia";
            sheet.Range(1, 1, 1, 3).Merge();
            sheet.Cell(1, 1).Style.Font.FontSize = 28;
            sheet.Cell(1, 1).Style.Font.Bold = true;
            sheet.Cell(1, 1).Style.Font.FontColor = XLColor.FromArgb(227, 82, 5);

            sheet.Cell(1, 6).Value = $"Projektnummer: {workOrder}";
            sheet.Range(1, 6, 1, 8).Merge();
            sheet.Cell(1, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            sheet.Cell(2, 6).Value = $"Projektnamn: {project.Name}";
            sheet.Range(2, 6, 2, 8).Merge();
            sheet.Cell(2, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            sheet.Cell(3, 1).Value = "CHECKLISTA EGENKONTROLL";
            sheet.Range(3, 1, 3, 8).Merge();
            sheet.Cell(3, 1).Style.Font.Bold = true;
            sheet.Cell(3, 1).Style.Font.FontSize = 16;
            sheet.Cell(3, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            Label(sheet, 5, 1, "Projekt");
            Value(sheet, 5, 2, 2, project.Name);
            Label(sheet, 5, 4, "Arbetsorder");
            Value(sheet, 5, 5, 1, workOrder);
            Label(sheet, 5, 6, "Verksamhet");
            Value(sheet, 5, 7, 1, discipline);
            Label(sheet, 5, 8, "Datum");
            Value(sheet, 6, 8, 1, date.ToString("yyyy-MM-dd"));
            Value(sheet, 6, 2, 2, drawing.SheetTitle);

            Label(sheet, 7, 1, "Beställare/Kund");
            Value(sheet, 7, 2, 4, project.Client);
            Label(sheet, 7, 6, "Beställare/Kund ref.nr");
            Value(sheet, 7, 7, 2, header.CustomerReference);

            Label(sheet, 8, 1, "Framställd av");
            Value(sheet, 8, 2, 1, header.PreparedBy);
            Label(sheet, 8, 3, "Godkänd av");
            Value(sheet, 8, 4, 2, header.ApprovedBy);
            Label(sheet, 8, 6, "Dok nr");
            Value(sheet, 8, 7, 1, header.DocumentNumber);
            Label(sheet, 8, 8, "Rev");
            sheet.Cell(8, 8).Value = revision;
            sheet.Cell(8, 8).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            sheet.Cell(10, 1).Value = $"Nr {Math.Max(1, header.SectionNumber)}";
            sheet.Cell(10, 1).Style.Font.Bold = true;

            Stack(sheet, 11, "Arbetsmoment", header.WorkTask);
            Stack(sheet, 12, "Byggdel", header.BuildingPart);
            Stack(sheet, 13, "Ritningar", drawing.DrawingNumber);
            Stack(sheet, 14, "Beskrivning", header.Description);

            sheet.Range(16, 1, 17, 3).Merge().Value = "Checklista för egenkontroll";
            sheet.Range(16, 4, 17, 6).Merge().Value = "Krav/information";
            sheet.Range(16, 7, 16, 8).Merge().Value = "Godkänd";
            sheet.Cell(17, 7).Value = "Datum";
            sheet.Cell(17, 8).Value = "Sign";
            var tableHeader = sheet.Range(16, 1, 17, 8);
            tableHeader.Style.Font.Bold = true;
            tableHeader.Style.Fill.BackgroundColor = XLColor.FromArgb(242, 242, 242);
            tableHeader.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            tableHeader.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            tableHeader.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            tableHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            tableHeader.Style.Alignment.WrapText = true;

            var row = 18;
            foreach (var item in items)
            {
                sheet.Range(row, 1, row, 3).Merge().Value = item.Title ?? string.Empty;
                sheet.Range(row, 4, row, 6).Merge().Value = item.Description ?? string.Empty;
                sheet.Range(row, 1, row, 8).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                sheet.Range(row, 1, row, 8).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                sheet.Range(row, 1, row, 8).Style.Alignment.WrapText = true;
                sheet.Range(row, 1, row, 8).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                sheet.Row(row).Height = 28;
                row++;
            }

            var observations = sheet.Range(row, 1, row, 8);
            observations.Merge();
            observations.Value = "Observationer/Avvikelser";
            observations.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            observations.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            observations.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            observations.Style.Alignment.WrapText = true;
            sheet.Row(row).Height = 90;

            sheet.PageSetup.PageOrientation = XLPageOrientation.Portrait;
            sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
            sheet.PageSetup.FitToPages(1, 0);
            workbook.SaveAs(filePath);
        });
    }

    private static void Label(IXLWorksheet sheet, int row, int column, string text)
    {
        sheet.Cell(row, column).Value = text;
        sheet.Cell(row, column).Style.Font.FontSize = 9;
        sheet.Cell(row, column).Style.Fill.BackgroundColor = XLColor.FromArgb(242, 242, 242);
        sheet.Cell(row, column).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
    }

    private static void Value(IXLWorksheet sheet, int row, int column, int span, string? value)
    {
        sheet.Cell(row, column).Value = value ?? string.Empty;
        if (span > 1)
            sheet.Range(row, column, row, column + span - 1).Merge();
        sheet.Range(row, column, row, column + span - 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.Cell(row, column).Style.Alignment.WrapText = true;
    }

    private static void Stack(IXLWorksheet sheet, int row, string label, string? value)
    {
        Label(sheet, row, 1, label);
        Value(sheet, row, 2, 7, value);
    }
}
