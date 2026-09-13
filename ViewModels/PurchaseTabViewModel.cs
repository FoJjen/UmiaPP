using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UmiaPP.Data;
using UmiaPP.Models;
using UmiaPP.Services;

namespace UmiaPP.ViewModels;

public partial class PurchaseTabViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly IBluebeamXmlParserService _bluebeamParser;
    private readonly IExportService _exportService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private Project _project;

    // Ritningslista
    [ObservableProperty]
    private ObservableCollection<Drawing> _drawings = [];

    [ObservableProperty]
    private Drawing? _selectedDrawing;

    // Komponentlista / Markups
    [ObservableProperty]
    private ObservableCollection<BluebeamMarkupItem> _markups = [];

    // Summerad inköpslista
    [ObservableProperty]
    private ObservableCollection<PurchaseSummaryItem> _summaryItems = [];

    // Summeringar
    [ObservableProperty]
    private double _totalDuctMeters;

    [ObservableProperty]
    private double _totalDetailsCount;

    [ObservableProperty]
    private double _totalEquipmentCount;

    // Formulärfält för ny ritning
    [ObservableProperty]
    private string _newDrawingNumber = string.Empty;

    [ObservableProperty]
    private string _newSheetTitle = string.Empty;

    [ObservableProperty]
    private string _newRevision = "A";

    [ObservableProperty]
    private string _newFilePath = string.Empty;

    [ObservableProperty]
    private string _loadedPdfPath = string.Empty;

    [ObservableProperty]
    private Uri? _loadedPdfUri;

    public bool HasLoadedPdf => !string.IsNullOrWhiteSpace(LoadedPdfPath);

    public string LoadedPdfName => string.IsNullOrWhiteSpace(LoadedPdfPath)
        ? string.Empty
        : Path.GetFileName(LoadedPdfPath);

    partial void OnLoadedPdfPathChanged(string value)
    {
        OnPropertyChanged(nameof(HasLoadedPdf));
        OnPropertyChanged(nameof(LoadedPdfName));
    }

    public PurchaseTabViewModel(
        Project project,
        IDataService dataService,
        IBluebeamXmlParserService bluebeamParser,
        IExportService exportService,
        IDialogService dialogService)
    {
        _project = project;
        _dataService = dataService;
        _bluebeamParser = bluebeamParser;
        _exportService = exportService;
        _dialogService = dialogService;

        LoadFromProject();
    }

    public void UpdateProject(Project project)
    {
        Project = project;
        LoadFromProject();
    }

    private void LoadFromProject()
    {
        Drawings = new ObservableCollection<Drawing>(Project.Drawings ?? []);
        Markups = new ObservableCollection<BluebeamMarkupItem>(Project.BluebeamMarkups ?? []);

        UpdateSummary();
        InitializePurchaseLists();
    }

    private void UpdateSummary()
    {
        var summaries = _bluebeamParser.GroupAndSummarize(Markups.ToList());
        SummaryItems = new ObservableCollection<PurchaseSummaryItem>(summaries);

        TotalDuctMeters = SummaryItems.Where(s => s.Category == "Kanaler").Sum(s => s.Quantity);
        TotalDetailsCount = SummaryItems.Where(s => s.Category == "Kanal-detaljer").Sum(s => s.Quantity);
        TotalEquipmentCount = SummaryItems.Where(s => s.Category != "Kanaler" && s.Category != "Kanal-detaljer").Sum(s => s.Quantity);
    }

    [RelayCommand]
    private void BrowsePdf()
    {
        var path = _dialogService.OpenFileDialog("PDF-ritningar (*.pdf)|*.pdf|Alla filer (*.*)|*.*", "Välj ritningsfil");
        if (!string.IsNullOrEmpty(path))
        {
            NewFilePath = path;
            if (string.IsNullOrWhiteSpace(NewSheetTitle))
            {
                NewSheetTitle = Path.GetFileNameWithoutExtension(path);
            }
        }
    }

    [RelayCommand]
    private void LoadPdf()
    {
        var path = _dialogService.OpenFileDialog("PDF-filer (*.pdf)|*.pdf|Alla filer (*.*)|*.*", "Välj PDF");
        if (string.IsNullOrEmpty(path))
            return;

        ShowPdf(path);
    }

    partial void OnSelectedDrawingChanged(Drawing? value)
    {
        SelectPurchaseList(value);
    }

    private void ShowPdf(string path)
    {
        if (!File.Exists(path))
        {
            _dialogService.ShowError($"Filen kunde inte hittas:\n{path}", "Fil saknas");
            return;
        }

        LoadedPdfPath = path;
        LoadedPdfUri = new Uri(path);
    }

    [RelayCommand]
    private async Task AddDrawingAsync()
    {
        if (string.IsNullOrWhiteSpace(NewDrawingNumber))
        {
            _dialogService.ShowError("Vänligen ange ett ritningsnummer.", "Validering");
            return;
        }

        var drawing = new Drawing
        {
            ProjectId = Project.Id,
            DrawingNumber = NewDrawingNumber.Trim(),
            SheetTitle = NewSheetTitle.Trim(),
            Revision = string.IsNullOrWhiteSpace(NewRevision) ? "A" : NewRevision.Trim(),
            Date = DateTime.Today,
            FilePath = NewFilePath.Trim()
        };

        var saved = await _dataService.Projects.AddDrawingAsync(drawing);
        Drawings.Add(saved);

        // Nollställ formulär
        NewDrawingNumber = string.Empty;
        NewSheetTitle = string.Empty;
        NewRevision = "A";
        NewFilePath = string.Empty;
    }

    [RelayCommand]
    private async Task DeleteDrawingAsync(Drawing? drawing)
    {
        var target = drawing ?? SelectedDrawing;
        if (target == null) return;

        if (_dialogService.Confirm($"Vill du ta bort ritning {target.DrawingNumber}?", "Ta bort ritning"))
        {
            await _dataService.Projects.DeleteDrawingAsync(target.Id);
            Drawings.Remove(target);
        }
    }

    [RelayCommand]
    private void OpenDrawingFile(Drawing? drawing)
    {
        var target = drawing ?? SelectedDrawing;
        if (target == null || string.IsNullOrWhiteSpace(target.FilePath))
        {
            _dialogService.ShowError("Ingen giltig filväg är angiven för ritningen.", "Kunde inte öppna");
            return;
        }

        if (!File.Exists(target.FilePath))
        {
            _dialogService.ShowError($"Filen kunde inte hittas på sökvägen:\n{target.FilePath}", "Fil saknas");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target.FilePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Kunde inte öppna filen: {ex.Message}", "Fel");
        }
    }

    [RelayCommand]
    private async Task ImportBluebeamXmlAsync()
    {
        var xmlFile = _dialogService.OpenFileDialog(
            "Bluebeam XML-filer (*.xml)|*.xml|Alla filer (*.*)|*.*",
            "Välj Bluebeam Revu Markup Summary XML");

        if (string.IsNullOrEmpty(xmlFile)) return;

        try
        {
            var res = await _bluebeamParser.ParseMarkupSummaryXmlAsync(xmlFile);
            if (!res.Success)
            {
                _dialogService.ShowError(res.ErrorMessage ?? "Misslyckades med att läsa XML.", "Fel");
                return;
            }

            // Spara i databas
            await _dataService.Projects.SaveBluebeamMarkupsAsync(Project.Id, res.Items);

            Markups = new ObservableCollection<BluebeamMarkupItem>(res.Items);
            UpdateSummary();

            _dialogService.ShowMessage(
                $"Bluebeam-markeringar importerades framgångsrikt!\nTotalt antal markeringar: {res.Items.Count}\nSummerade artiklar: {SummaryItems.Count}",
                "Import slutförd");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Fel vid import: {ex.Message}", "Fel");
        }
    }

    [RelayCommand]
    private async Task ExportExcelAsync()
    {
        if (SummaryItems.Count == 0)
        {
            _dialogService.ShowError("Det finns inga inköpsposter att exportera.", "Ingen data");
            return;
        }

        var savePath = _dialogService.SaveFileDialog(
            ".xlsx",
            "Excel-filer (*.xlsx)|*.xlsx",
            $"Inköpslista_{Project.ProjectNumber}_{DateTime.Now:yyyyMMdd}.xlsx");

        if (string.IsNullOrEmpty(savePath)) return;

        try
        {
            await _exportService.ExportPurchaseListToExcelAsync(SummaryItems.ToList(), Project.Name, savePath);
            _dialogService.ShowMessage($"Inköpslistan har exporterats till:\n{savePath}", "Export lyckades");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Kunde inte exportera till Excel: {ex.Message}", "Exportfel");
        }
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (SummaryItems.Count == 0)
        {
            _dialogService.ShowError("Det finns inga inköpsposter att exportera.", "Ingen data");
            return;
        }

        var savePath = _dialogService.SaveFileDialog(
            ".csv",
            "CSV-filer (*.csv)|*.csv",
            $"Inköpslista_{Project.ProjectNumber}_{DateTime.Now:yyyyMMdd}.csv");

        if (string.IsNullOrEmpty(savePath)) return;

        try
        {
            await _exportService.ExportPurchaseListToCsvAsync(SummaryItems.ToList(), savePath);
            _dialogService.ShowMessage($"Inköpslistan har exporterats till:\n{savePath}", "Export lyckades");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Kunde inte exportera till CSV: {ex.Message}", "Exportfel");
        }
    }
}
