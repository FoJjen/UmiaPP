using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using System.IO;
using System.Text.Json;
using UmiaPP.Data;
using UmiaPP.Models;
using UmiaPP.Services;

namespace UmiaPP.ViewModels;

public partial class AtaTabViewModel : ObservableObject
{
    private readonly IDialogService _dialogService;
    private readonly IDataService _dataService;
    private readonly IAtaImportService _importService;
    private readonly IExportService _exportService;

    public event EventHandler? SummaryChanged;

    [ObservableProperty]
    private Project _project = null!;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayRows))]
    [NotifyPropertyChangedFor(nameof(DisciplineTotals))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private AtaSummary? _summary;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LinkFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _status = "Välj en ÄTA-fil för att visa sammanställningen.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayRows))]
    private bool _filterCurrentDisciplineOnly;

    public AtaSummaryRow? DisciplineTotals => Summary?.GetTotalsForDiscipline(Project.Discipline);

    public string CurrentDisciplineCode => AtaSummary.GetDisciplineCode(Project.Discipline);

    public IEnumerable<AtaSummaryRow> DisplayRows
    {
        get
        {
            if (Summary == null) return [];
            var totals = FilterCurrentDisciplineOnly ? DisciplineTotals ?? Summary.Totals : Summary.Totals;
            var rows = FilterCurrentDisciplineOnly
                ? Summary.Rows.Where(r => r.MatchesDiscipline(Project.Discipline))
                : Summary.Rows;
            return new[] { totals }.Concat(rows);
        }
    }

    public AtaTabViewModel(
        Project project,
        IDataService dataService,
        IDialogService dialogService,
        IExportService? exportService = null,
        IAtaImportService? importService = null)
    {
        _project = project;
        _dialogService = dialogService;
        _dataService = dataService;
        _exportService = exportService ?? new ExportService();
        _importService = importService ?? new AtaImportService();
        LoadSummary();
    }

    public void UpdateProject(Project project)
    {
        Project = project;
        LoadSummary();
    }

    private void LoadSummary()
    {
        Summary = null;
        Status = "Välj en ÄTA-fil för att visa sammanställningen.";
        try
        {
            if (!string.IsNullOrWhiteSpace(Project.AtaSummaryJson))
                Summary = JsonSerializer.Deserialize<AtaSummary>(Project.AtaSummaryJson);
            if (Summary != null) Status = "Visar senast sparade sammanställning. Uppdatera från fil vid behov.";
        }
        catch (JsonException ex)
        {
            Status = "Den sparade ÄTA-sammanställningen kunde inte läsas. Välj filen igen.";
            _dialogService.ShowError(ex.Message, "ÄTA");
        }
    }

    private bool CanLinkFile() => !IsBusy;
    private bool CanRefresh() => !IsBusy && !string.IsNullOrWhiteSpace(Summary?.FilePath);

    [RelayCommand(CanExecute = nameof(CanLinkFile))]
    private async Task LinkFileAsync()
    {
        var path = _dialogService.OpenFileDialog("Excel-filer (*.xlsm;*.xlsx)|*.xlsm;*.xlsx", "Välj ÄTA-sammanställning");
        if (!string.IsNullOrWhiteSpace(path)) await ImportAsync(path);
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        if (Summary != null) await ImportAsync(Summary.FilePath);
    }

    private async Task ImportAsync(string path)
    {
        var project = Project;
        IsBusy = true;
        Status = "Läser ÄTA-filen…";
        try
        {
            var summary = await _importService.ImportAsync(path);
            var json = JsonSerializer.Serialize(summary);
            await _dataService.Projects.SaveAtaSummaryAsync(project.Id, json);
            project.AtaSummaryJson = json;
            if (!ReferenceEquals(project, Project)) return;
            Summary = summary;
            Status = $"Uppdaterad: {summary.Rows.Count} rader.";
            SummaryChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Status = "Importen misslyckades. Tidigare sparad sammanställning är oförändrad.";
            _dialogService.ShowError($"Kunde inte läsa eller spara ÄTA-filen: {ex.Message}", "ÄTA-import");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateInvoiceDocumentAsync(AtaSummaryRow? row)
    {
        if (row == null || row.Description == "Summor" || row.Description.StartsWith("Summor ("))
            return;

        var defaultFileName = $"Faktureringsunderlag_ATA_{row.Number}_{Project.ProjectNumber}";
        var filePath = _dialogService.SaveFileDialog("pdf", "PDF-dokument (*.pdf)|*.pdf|Excel-kalkylblad (*.xlsx)|*.xlsx", defaultFileName);
        if (string.IsNullOrWhiteSpace(filePath)) return;

        IsBusy = true;
        try
        {
            var ext = Path.GetExtension(filePath);
            if (ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                await _exportService.ExportAtaInvoiceToExcelAsync(Summary, row, filePath);
            }
            else
            {
                await _exportService.ExportAtaInvoiceToPdfAsync(Summary, row, filePath);
            }

            _dialogService.ShowMessage($"Faktureringsunderlag skapades:\n{filePath}", "Underlag skapat");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Kunde inte exportera underlag: {ex.Message}", "Exportfel");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
