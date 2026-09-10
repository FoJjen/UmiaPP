using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UmiaPP.Data;
using UmiaPP.Models;
using UmiaPP.Services;

namespace UmiaPP.ViewModels;

public partial class LubeCalculationTabViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly ILubeImportService _lubeImportService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private Project _project;

    [ObservableProperty]
    private ObservableCollection<LubeKalkylPost> _kalkylPoster = [];

    [ObservableProperty]
    private ObservableCollection<LubeKalkylPost> _filteredKalkylPoster = [];

    [ObservableProperty]
    private ObservableCollection<LubeProjektanpassningPost> _projektanpassningar = [];

    [ObservableProperty]
    private ObservableCollection<LubeSakvaraMangd> _sakvarorMangder = [];

    [ObservableProperty]
    private ObservableCollection<LubeSakvaraOffert> _sakvarorOfferter = [];

    [ObservableProperty]
    private ObservableCollection<LubeSakvaraLeveranspris> _sakvarorLeveranspriser = [];

    [ObservableProperty]
    private ObservableCollection<LubeSakvaraMontage> _sakvarorMontage = [];

    [ObservableProperty]
    private ObservableCollection<LubeEjMangdbaserad> _ejMangdbaserade = [];

    [ObservableProperty]
    private double _sumTotalMaterial;

    [ObservableProperty]
    private double _sumTotalTimmar;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private bool _isImporting;

    public LubeCalculationTabViewModel(
        Project project,
        IDataService dataService,
        ILubeImportService lubeImportService,
        IDialogService dialogService)
    {
        _project = project;
        _dataService = dataService;
        _lubeImportService = lubeImportService;
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
        KalkylPoster = new ObservableCollection<LubeKalkylPost>(Project.LubeKalkylPoster ?? []);
        Projektanpassningar = new ObservableCollection<LubeProjektanpassningPost>(Project.LubeProjektanpassningar ?? []);
        SakvarorMangder = new ObservableCollection<LubeSakvaraMangd>(Project.LubeSakvarorMangder ?? []);
        SakvarorOfferter = new ObservableCollection<LubeSakvaraOffert>(Project.LubeSakvarorOfferter ?? []);
        SakvarorLeveranspriser = new ObservableCollection<LubeSakvaraLeveranspris>(Project.LubeSakvarorLeveranspriser ?? []);
        SakvarorMontage = new ObservableCollection<LubeSakvaraMontage>(Project.LubeSakvarorMontagePoster ?? []);
        EjMangdbaserade = new ObservableCollection<LubeEjMangdbaserad>(Project.LubeEjMangdbaseradePoster ?? []);

        FilterKalkyl();
        CalculateTotals();
    }

    partial void OnFilterTextChanged(string value)
    {
        FilterKalkyl();
    }

    private void FilterKalkyl()
    {
        if (string.IsNullOrWhiteSpace(FilterText))
        {
            FilteredKalkylPoster = new ObservableCollection<LubeKalkylPost>(KalkylPoster);
            return;
        }

        var q = FilterText.Trim().ToLowerInvariant();
        var list = KalkylPoster.Where(x =>
            (x.Kod?.ToLowerInvariant().Contains(q) ?? false) ||
            (x.Benamning?.ToLowerInvariant().Contains(q) ?? false)).ToList();

        FilteredKalkylPoster = new ObservableCollection<LubeKalkylPost>(list);
    }

    private void CalculateTotals()
    {
        SumTotalMaterial = KalkylPoster.Sum(x => x.TotaltMaterial);
        SumTotalTimmar = KalkylPoster.Sum(x => x.TotaltMontageTimmar);
    }

    [RelayCommand]
    private async Task ImportLubeExcelAsync()
    {
        var filePath = _dialogService.OpenFileDialog(
            "Excel-filer (*.xlsx;*.xls)|*.xlsx;*.xls|Alla filer (*.*)|*.*",
            "Välj Lube-kalkylfil");

        if (string.IsNullOrEmpty(filePath)) return;

        IsImporting = true;
        try
        {
            var res = await _lubeImportService.ImportAsync(filePath);
            if (!res.Success)
            {
                _dialogService.ShowError(res.ErrorMessage ?? "Misslyckades att importera Lube-filen.", "Importfel");
                return;
            }

            // Spara i databasen
            await _dataService.Projects.SaveLubeCalculationDataAsync(
                Project.Id,
                res.KalkylPoster,
                res.Anpassningar,
                res.SakvarorMangder,
                res.SakvarorOfferter,
                res.SakvarorLeveranspriser,
                res.SakvarorMontagePoster,
                res.EjMangdbaseradePoster);

            // Uppdatera lokalt
            KalkylPoster = new ObservableCollection<LubeKalkylPost>(res.KalkylPoster);
            Projektanpassningar = new ObservableCollection<LubeProjektanpassningPost>(res.Anpassningar);
            SakvarorMangder = new ObservableCollection<LubeSakvaraMangd>(res.SakvarorMangder);
            SakvarorOfferter = new ObservableCollection<LubeSakvaraOffert>(res.SakvarorOfferter);
            SakvarorLeveranspriser = new ObservableCollection<LubeSakvaraLeveranspris>(res.SakvarorLeveranspriser);
            SakvarorMontage = new ObservableCollection<LubeSakvaraMontage>(res.SakvarorMontagePoster);
            EjMangdbaserade = new ObservableCollection<LubeEjMangdbaserad>(res.EjMangdbaseradePoster);

            FilterKalkyl();
            CalculateTotals();

            _dialogService.ShowMessage($"Lube-kalkylen har importerats framgångsrikt!\nAntal kalkylrader: {res.KalkylPoster.Count}\nSakvaror mängder: {res.SakvarorMangder.Count}\nMontageposter: {res.SakvarorMontagePoster.Count}", "Import slutförd");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Oväntat fel: {ex.Message}", "Importfel");
        }
        finally
        {
            IsImporting = false;
        }
    }
}
