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
    private readonly SemaphoreSlim _summarySaveLock = new(1, 1);
    private bool _ataLoadFailed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Outcome))]
    private AtaSummary? _ataSummary;

    [ObservableProperty]
    private string _ataStatus = string.Empty;

    public AtaOutcome Outcome => new(AtaSummary,
        _ataLoadFailed ? null : Project.SalesPrice, _ataLoadFailed ? null : GetBaseOutcomeCost(), Project.Discipline);

    private decimal? GetBaseOutcomeCost()
    {
        if (string.IsNullOrWhiteSpace(Project.LubeSummaryJson)) return null;
        var cost = Summary.ActualProductionCost;
        if (!double.IsFinite(cost)) return null;
        try { return checked((decimal)cost); }
        catch (OverflowException) { return null; }
    }

    public void RefreshAtaSummary()
    {
        _ataLoadFailed = false;
        try
        {
            AtaSummary = string.IsNullOrWhiteSpace(Project.AtaSummaryJson) ? null
                : JsonSerializer.Deserialize<AtaSummary>(Project.AtaSummaryJson)
                    ?? throw new JsonException("ÄTA-sammanställningen är tom.");
            AtaStatus = AtaSummary == null ? "Ingen ÄTA-fil är kopplad."
                : $"ÄTA-intäkt för teknikområde {AtaSummary.GetDisciplineCode(Project.Discipline)} ingår i utfallet nedan.";
        }
        catch (JsonException)
        {
            _ataLoadFailed = true;
            AtaSummary = null;
            AtaStatus = "Den sparade ÄTA-sammanställningen kan inte läsas. Välj filen igen under ÄTA.";
        }
        OnPropertyChanged(nameof(Outcome));
    }

    partial void OnSummaryChanged(LubeCalculationSummary? oldValue, LubeCalculationSummary newValue)
    {
        if (oldValue != null)
            oldValue.ActualCostsChanged -= OnSummaryActualCostsChanged;
        newValue.InitializeActualCosts();
        newValue.ActualCostsChanged += OnSummaryActualCostsChanged;
        UpdateSpecificationOutcome();
        OnPropertyChanged(nameof(Outcome));
    }

    private async void OnSummaryActualCostsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Outcome));
        var project = Project;
        await _summarySaveLock.WaitAsync();
        try
        {
            if (sender is not LubeCalculationSummary summary || !ReferenceEquals(summary, Summary))
                return;
            var json = JsonSerializer.Serialize(summary);
            await _dataService.Projects.SaveLubeSummaryAsync(project.Id, json);
            project.LubeSummaryJson = json;
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Kunde inte spara utfallet: {ex.Message}", "Sparfel");
        }
        finally
        {
            _summarySaveLock.Release();
        }
    }

    public double SumSlutpris => SakvarorMangder.Sum(x => x.EffectiveSlutpris);

    public double SumPrisNto => SakvarorMangder.Sum(x => x.PrisNto);
    public double SumOffertpris => SakvarorMangder.Sum(x => x.EffectiveOffertpris);
    public double SumDifferens => SakvarorMangder.Sum(x => x.Differens);

    [RelayCommand]
    private void CheckOfferTotal()
    {
        var culture = System.Globalization.CultureInfo.GetCultureInfo("sv-SE");
        var rows = SakvarorMangder.ToArray();
        var report = new System.Text.StringBuilder();
        report.AppendLine("KONTROLL AV OFFERTPRIS – ALLA PROJEKTRADER");
        report.AppendLine($"Antal rader: {rows.Length}");
        report.AppendLine("Filter påverkar inte summan. Negativa belopp ingår.");
        report.AppendLine("Belopp visas utan tusentalsavskiljare och utan avrundning.");
        report.AppendLine();
        report.AppendLine("Rad | ID | Beteckning | Produkt | Inmatat offertpris | Visat offertpris");
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            if (row.Offertpris == 0 && row.EffectiveOffertpris == 0) continue;
            report.AppendLine($"{index + 1} | {row.Id} | {row.Beteckning} | {row.Produkt} | {row.Offertpris.ToString("G17", culture)} | {row.EffectiveOffertpris.ToString("G17", culture)}");
        }
        var total = rows.Sum(row => row.EffectiveOffertpris);
        report.AppendLine();
        report.AppendLine($"Summa inmatade offertpriser: {rows.Sum(row => row.Offertpris).ToString("N2", culture)} kr");
        report.AppendLine($"Summa Offertpris inklusive Pris nto som reservvärde: {total.ToString("N2", culture)} kr");
        report.AppendLine($"Summa före visningsavrundning: {total.ToString("G17", culture)}");
        report.AppendLine($"Avvikelse från 135 910,00 kr: {(total - 135910d).ToString("N2", culture)} kr");
        var duplicateIds = rows.Where(row => row.Id != 0).GroupBy(row => row.Id)
            .Where(group => group.Count() > 1).ToArray();
        foreach (var group in duplicateIds)
            report.AppendLine($"VARNING: rad-ID {group.Key} förekommer {group.Count()} gånger i listan.");
        report.AppendLine();
        report.AppendLine("Kontrollen ändrar inga priser eller sparade uppgifter.");
        var text = new System.Windows.Controls.TextBox
        {
            Text = report.ToString(),
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = System.Windows.TextWrapping.NoWrap,
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 13,
            Margin = new System.Windows.Thickness(12)
        };
        var window = new System.Windows.Window
        {
            Title = "Kontrollera offertsumma – markera text och kopiera med Ctrl+C",
            Width = 900,
            Height = 600,
            Content = text,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner
        };
        if (System.Windows.Application.Current?.MainWindow is { } owner)
            window.Owner = owner;
        window.ShowDialog();
    }

    private void RefreshSpecificationTotals()
    {
        OnPropertyChanged(nameof(SumPrisNto));
        OnPropertyChanged(nameof(SumOffertpris));
        OnPropertyChanged(nameof(SumSlutpris));
        OnPropertyChanged(nameof(SumDifferens));
        UpdateSpecificationOutcome();
    }

    private bool _updatingSpecificationOutcome;

    private void UpdateSpecificationOutcome()
    {
        if (_updatingSpecificationOutcome || SakvarorMangder.Count == 0) return;
        static string Key(string value) => value.Trim().ToUpperInvariant();
        var deliveryRows = Summary.ReportSections
            .Where(section => Key(section.Title) == "LEVERANSER")
            .SelectMany(section => section.Rows)
            .Where(row => !row.IsHeader && !row.IsTotal && Key(row.Label) == "SAKVAROR")
            .ToArray();
        var priced = deliveryRows.Where(row => Key(row.List) is "PRISSATT" or "PRISATT").ToArray();
        var quoted = deliveryRows.Where(row => Key(row.List) == "OFFERTER").ToArray();
        // Require an unambiguous pair: do not apply a group total to multiple report rows.
        if (priced.Length != 1 || quoted.Length != 1) return;
        _updatingSpecificationOutcome = true;
        try
        {
            priced[0].ActualCost = SakvarorMangder.Where(row => row.PrisNto != 0)
                .Sum(row => row.EffectiveSlutpris);
            quoted[0].ActualCost = SakvarorMangder.Where(row => row.PrisNto == 0)
                .Sum(row => row.EffectiveSlutpris);
            priced[0].CanEditActualCost = false;
            quoted[0].CanEditActualCost = false;
            OnPropertyChanged(nameof(Outcome));
        }
        finally
        {
            _updatingSpecificationOutcome = false;
        }
    }

    [RelayCommand]
    private async Task AddSakvaraRowAsync()
    {
        var project = Project;
        var item = new LubeSakvaraMangd
        {
            ProjectId = project.Id,
            Beteckning = "Ny rad",
            Produkt = "Ny rad",
            Enhet = "st"
        };
        try
        {
            await _dataService.Projects.SaveSakvaraSpecificationRowAsync(item);
            if (!ReferenceEquals(project, Project)) return;
            SakvarorMangder.Add(item);
            FilterText = string.Empty;
            if (!FilteredSakvaror.Contains(item))
                FilteredSakvaror.Add(item);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Kunde inte skapa sakvaruraden: {ex.Message}", "Sparfel");
        }
    }

    public async Task SaveSakvaraSpecificationRowAsync(LubeSakvaraMangd item)
    {
        try
        {
            await _dataService.Projects.SaveSakvaraSpecificationRowAsync(item);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Kunde inte spara sakvaruraden: {ex.Message}", "Sparfel");
        }
    }

    partial void OnSakvarorMangderChanged(ObservableCollection<LubeSakvaraMangd>? oldValue, ObservableCollection<LubeSakvaraMangd> newValue)
    {
        if (oldValue != null)
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
        RefreshSpecificationTotals();
    }

    private void OnSakvaraPriceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) ||
            e.PropertyName is nameof(LubeSakvaraMangd.PrisNto)
                or nameof(LubeSakvaraMangd.Antal)
                or nameof(LubeSakvaraMangd.Offertpris)
                or nameof(LubeSakvaraMangd.Slutpris)
                or nameof(LubeSakvaraMangd.Differens))
            RefreshSpecificationTotals();
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
        RefreshAtaSummary();
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
        OnPropertyChanged(nameof(Outcome));
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
            await _summarySaveLock.WaitAsync();
            try
            {
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
                Summary = res.Summary;
                Project.LubeSummaryJson = JsonSerializer.Serialize(res.Summary);
            }
            finally
            {
                _summarySaveLock.Release();
            }

            // Uppdatera lokalt
            KalkylPoster = new ObservableCollection<LubeKalkylPost>(res.KalkylPoster);
            Projektanpassningar = new ObservableCollection<LubeProjektanpassningPost>(res.Anpassningar);
            SakvarorMangder = new ObservableCollection<LubeSakvaraMangd>(res.SakvarorMangder);
            SakvarorOfferter = new ObservableCollection<LubeSakvaraOffert>(res.SakvarorOfferter);
            SakvarorLeveranspriser = new ObservableCollection<LubeSakvaraLeveranspris>(res.SakvarorLeveranspriser);
            SakvarorMontage = new ObservableCollection<LubeSakvaraMontage>(res.SakvarorMontagePoster);
            EjMangdbaserade = new ObservableCollection<LubeEjMangdbaserad>(res.EjMangdbaseradePoster);

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
