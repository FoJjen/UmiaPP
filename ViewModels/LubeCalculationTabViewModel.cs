using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json;
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
    private readonly HashSet<LubeSakvaraMangd> _totalRows = [];

    public double SumSlutpris => SakvarorMangder.Sum(x => x.Slutpris);

    partial void OnSakvarorMangderChanged(ObservableCollection<LubeSakvaraMangd> oldValue, ObservableCollection<LubeSakvaraMangd> newValue)
    {
        oldValue.CollectionChanged -= OnSakvarorCollectionChanged;
        newValue.CollectionChanged += OnSakvarorCollectionChanged;
        TrackTotalRows();
    }

    private void OnSakvarorCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => TrackTotalRows();

    private void TrackTotalRows()
    {
        foreach (var row in _totalRows)
            row.PropertyChanged -= OnSakvaraPriceChanged;
        _totalRows.Clear();
        foreach (var row in SakvarorMangder)
        {
            if (_totalRows.Add(row))
                row.PropertyChanged += OnSakvaraPriceChanged;
        }
        OnPropertyChanged(nameof(SumSlutpris));
    }

    private void OnSakvaraPriceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LubeSakvaraMangd.Slutpris))
            OnPropertyChanged(nameof(SumSlutpris));
    }

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
    private LubeCalculationSummary _summary = new();

    [ObservableProperty]
    private string _lubeProjektNummer = string.Empty;

    [ObservableProperty]
    private string _lubePdelRitning = string.Empty;

    [ObservableProperty]
    private string _lubeHandling = string.Empty;

    [ObservableProperty]
    private string _lubeDatum = string.Empty;

    [ObservableProperty]
    private ObservableCollection<LubeSakvaraMangd> _filteredSakvaror = [];

    [ObservableProperty]
    private double _sumTotalMaterial;

    [ObservableProperty]
    private double _sumTotalTimmar;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private bool _isImporting;

    [ObservableProperty]
    private bool _isTabPickerOpen;

    [ObservableProperty]
    private int _selectedLubeTabIndex;

    public ObservableCollection<LubeTabOption> TabOptions { get; } = [];
    public LubeTabOption SammandragTab { get; }
    public LubeTabOption SakvarorSpecTab { get; }
    public LubeTabOption KalkylTab { get; }
    public LubeTabOption AnpassningTab { get; }
    public LubeTabOption SakvarorMangdTab { get; }
    public LubeTabOption SakvarorOffertTab { get; }
    public LubeTabOption LeveransTab { get; }
    public LubeTabOption MontageTab { get; }
    public LubeTabOption EjMangdTab { get; }

    private readonly ILocalizationService _settingsService = new LocalizationService();

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

        var hidden = _settingsService.LoadSettings().HiddenLubeTabs ?? [];
        SammandragTab = CreateTab("sammandrag", "SubTabSammandrag", canHide: false, hidden);
        SakvarorSpecTab = CreateTab("sakvarorSpec", "SubTabSakvarorSpec", true, hidden);
        KalkylTab = CreateTab("kalkyl", "SubTabKalkyl", true, hidden);
        AnpassningTab = CreateTab("anpassning", "SubTabAnpassning", true, hidden);
        SakvarorMangdTab = CreateTab("sakvarorMangd", "SubTabSakvarorMangd", true, hidden);
        SakvarorOffertTab = CreateTab("sakvarorOffert", "SubTabSakvarorOffert", true, hidden);
        LeveransTab = CreateTab("leverans", "SubTabSakvarorLeverans", true, hidden);
        MontageTab = CreateTab("montage", "SubTabSakvarorMontage", true, hidden);
        EjMangdTab = CreateTab("ejMangd", "SubTabEjMangd", true, hidden);

        LoadFromProject();
    }

    private LubeTabOption CreateTab(string key, string titleKey, bool canHide, List<string> hidden)
    {
        var option = new LubeTabOption(
            key,
            ResolveTitle(titleKey),
            canHide,
            isVisible: !hidden.Contains(key, StringComparer.OrdinalIgnoreCase),
            OnTabVisibilityChanged);
        TabOptions.Add(option);
        return option;
    }

    private static string ResolveTitle(string resourceKey)
    {
        if (System.Windows.Application.Current?.TryFindResource(resourceKey) is string title)
            return title;
        return resourceKey;
    }

    private void OnTabVisibilityChanged(LubeTabOption option)
    {
        var index = TabOptions.IndexOf(option);
        if (!option.IsVisible && SelectedLubeTabIndex == index)
            SelectedLubeTabIndex = 0;

        var settings = _settingsService.LoadSettings();
        settings.HiddenLubeTabs = TabOptions
            .Where(tab => tab.CanHide && !tab.IsVisible)
            .Select(tab => tab.Key)
            .ToList();
        _settingsService.SaveSettings(settings);
    }

    public void UpdateProject(Project project)
    {
        Project = project;
        LoadFromProject();
    }

    private void LoadFromProject()
    {
        Summary = new LubeCalculationSummary();
        if (!string.IsNullOrWhiteSpace(Project.LubeSummaryJson))
        {
            try
            {
                Summary = JsonSerializer.Deserialize<LubeCalculationSummary>(Project.LubeSummaryJson)
                    ?? new LubeCalculationSummary();
            }
            catch (JsonException ex)
            {
                _dialogService.ShowError($"Kunde inte läsa det sparade kalkylsammandraget: {ex.Message}", "Läsfel");
            }
        }

        KalkylPoster = new ObservableCollection<LubeKalkylPost>(Project.LubeKalkylPoster ?? []);
        Projektanpassningar = new ObservableCollection<LubeProjektanpassningPost>(Project.LubeProjektanpassningar ?? []);
        SakvarorMangder = new ObservableCollection<LubeSakvaraMangd>(Project.LubeSakvarorMangder ?? []);
        SakvarorOfferter = new ObservableCollection<LubeSakvaraOffert>(Project.LubeSakvarorOfferter ?? []);
        SakvarorLeveranspriser = new ObservableCollection<LubeSakvaraLeveranspris>(Project.LubeSakvarorLeveranspriser ?? []);
        SakvarorMontage = new ObservableCollection<LubeSakvaraMontage>(Project.LubeSakvarorMontagePoster ?? []);
        EjMangdbaserade = new ObservableCollection<LubeEjMangdbaserad>(Project.LubeEjMangdbaseradePoster ?? []);

        FilterKalkyl();
        FilterSakvaror();
        CalculateTotals();
    }

    partial void OnFilterTextChanged(string value)
    {
        FilterKalkyl();
        FilterSakvaror();
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

    private void FilterSakvaror()
    {
        if (string.IsNullOrWhiteSpace(FilterText))
        {
            FilteredSakvaror = new ObservableCollection<LubeSakvaraMangd>(SakvarorMangder);
            return;
        }

        var q = FilterText.Trim().ToLowerInvariant();
        var list = SakvarorMangder.Where(x =>
            (x.Beteckning?.ToLowerInvariant().Contains(q) ?? false) ||
            (x.Grupp?.ToLowerInvariant().Contains(q) ?? false) ||
            (x.Produkt?.ToLowerInvariant().Contains(q) ?? false) ||
            (x.Produktkod?.ToLowerInvariant().Contains(q) ?? false) ||
            (x.Leverantor?.ToLowerInvariant().Contains(q) ?? false)).ToList();

        FilteredSakvaror = new ObservableCollection<LubeSakvaraMangd>(list);
    }

    private void CalculateTotals()
    {
        SumTotalMaterial = KalkylPoster.Sum(x => x.TotaltMaterial);
        SumTotalTimmar = KalkylPoster.Sum(x => x.TotaltMontageTimmar);
    }

    public async Task SaveSakvaraPricesAsync(LubeSakvaraMangd item)
    {
        try
        {
            await _dataService.Projects.UpdateSakvaraPricesAsync(item);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Kunde inte spara priset: {ex.Message}", "Sparfel");
        }
    }

    [RelayCommand]
    private async Task MarkeraKoptAsync(LubeSakvaraMangd? item)
    {
        if (item == null)
            return;

        item.ArKopt = !item.ArKopt;
        await SaveSakvaraPricesAsync(item);
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
                res.EjMangdbaseradePoster,
                res.Summary);

            // Uppdatera lokalt
            KalkylPoster = new ObservableCollection<LubeKalkylPost>(res.KalkylPoster);
            Projektanpassningar = new ObservableCollection<LubeProjektanpassningPost>(res.Anpassningar);
            SakvarorMangder = new ObservableCollection<LubeSakvaraMangd>(res.SakvarorMangder);
            SakvarorOfferter = new ObservableCollection<LubeSakvaraOffert>(res.SakvarorOfferter);
            SakvarorLeveranspriser = new ObservableCollection<LubeSakvaraLeveranspris>(res.SakvarorLeveranspriser);
            SakvarorMontage = new ObservableCollection<LubeSakvaraMontage>(res.SakvarorMontagePoster);
            EjMangdbaserade = new ObservableCollection<LubeEjMangdbaserad>(res.EjMangdbaseradePoster);

            Summary = res.Summary;
            Project.LubeSummaryJson = JsonSerializer.Serialize(res.Summary);
            LubeProjektNummer = res.ProjektNummer ?? Project.ProjectNumber;
            LubePdelRitning = res.PdelRitning ?? "Alla";
            LubeHandling = res.Handling ?? "Alla h";
            LubeDatum = res.Datum ?? DateTime.Now.ToString("yyyy-MM-dd");

            FilterKalkyl();
            FilterSakvaror();
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
