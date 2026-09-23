using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UmiaPP.Data;
using UmiaPP.Models;
using UmiaPP.Services;
using UmiaPP.Views;

namespace UmiaPP.ViewModels;

public partial class LubeCalculationTabViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly ILubeImportService _lubeImportService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _settingsService;
    private readonly HashSet<LubeSakvaraMangd> _totalRows = [];
    private readonly SemaphoreSlim _summarySaveLock = new(1, 1);
    private bool _ataLoadFailed;
    private bool _updatingSpecificationOutcome;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Outcome))]
    private AtaSummary? _ataSummary;

    [ObservableProperty]
    private string _ataStatus = string.Empty;

    [ObservableProperty]
    private Project _project = null!;

    [ObservableProperty]
    private LubeCalculationSummary _summary = new();

    [ObservableProperty]
    private ObservableCollection<LubeKalkylPost> _kalkylPoster = [];

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
    private ObservableCollection<LubeKalkylPost> _filteredKalkylPoster = [];

    [ObservableProperty]
    private ObservableCollection<LubeSakvaraMangd> _filteredSakvaror = [];

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private int _selectedLubeTabIndex;

    [ObservableProperty]
    private bool _isImporting;

    [ObservableProperty]
    private string _lubeProjektNummer = string.Empty;

    [ObservableProperty]
    private string _lubePdelRitning = string.Empty;

    [ObservableProperty]
    private string _lubeHandling = string.Empty;

    [ObservableProperty]
    private string _lubeDatum = string.Empty;

    public AtaOutcome Outcome => new(AtaSummary,
        _ataLoadFailed ? null : Project.SalesPrice, _ataLoadFailed ? null : GetBaseOutcomeCost(), Project.Discipline);

    public double SumSlutpris => UnlinkedRows.Sum(x => x.EffectiveSlutpris)
        + PackageQuotes.Sum(q => q.EffectiveAmount);

    public double SumPrisNto => SakvarorMangder.Sum(x => x.PrisNto);
    public double SumOffertpris => UnlinkedRows.Sum(x => x.EffectiveOffertpris)
        + PackageQuotes.Sum(q => q.OffereratPris);
    public double SumDifferens => SumSlutpris - SumOffertpris;
    public double SumTotalMaterial => KalkylPoster.Sum(x => x.TotaltMaterial);
    public double SumTotalTimmar => KalkylPoster.Sum(x => x.TotaltMontageTimmar);

    public IEnumerable<LubeSakvaraMangd> UnlinkedRows => SakvarorMangder.Where(x => !x.HasPackageQuote);
    public IEnumerable<LubeSakvaraOffert> PackageQuotes => SakvarorOfferter.Where(q => q.IsPackageQuote);
    public ObservableCollection<LubeSakvaraMangd> SelectedSakvaror { get; } = [];
    public ObservableCollection<LubeSakvaraMangd> SpecificationRows { get; } = [];
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

    public LubeCalculationTabViewModel(
        Project project,
        IDataService dataService,
        ILubeImportService lubeImportService,
        IDialogService dialogService)
    {
        _dataService = dataService;
        _lubeImportService = lubeImportService;
        _dialogService = dialogService;
        _settingsService = new LocalizationService();
        Project = project;

        SammandragTab = CreateTab("sammandrag", "Kalkylsammandrag", false);
        SakvarorSpecTab = CreateTab("specifikation", "Specifikation", false);
        KalkylTab = CreateTab("kalkyl", "Kalkyl", true);
        AnpassningTab = CreateTab("anpassning", "Projektanpassning", true);
        SakvarorMangdTab = CreateTab("sakvarormangd", "Sakvaror mängd", true);
        SakvarorOffertTab = CreateTab("sakvaroroffert", "Sakvaror offerter", true);
        LeveransTab = CreateTab("leverans", "Leveranspris", true);
        MontageTab = CreateTab("montage", "Montage", true);
        EjMangdTab = CreateTab("ejmangd", "Ej mängdbaserade", true);
        LoadFromProject(project);
        MarkLegacyUserAddedRows();
        RefreshPackageQuoteLabels();
    }

    public void UpdateProject(Project project)
    {
        Project = project;
        LoadFromProject(project);
        MarkLegacyUserAddedRows();
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

    public void SetSelectedSakvaror(IEnumerable<LubeSakvaraMangd> selected)
    {
        var articles = selected.Where(row => !row.IsQuoteSummaryRow).Distinct().ToArray();
        SelectedSakvaror.Clear();
        foreach (var row in articles)
            SelectedSakvaror.Add(row);
    }

    private void MarkLegacyUserAddedRows()
    {
        foreach (var row in SakvarorMangder.Where(item =>
                     !item.IsQuoteSummaryRow &&
                     !item.IsUserAdded &&
                     string.Equals(item.Beteckning, "Ny rad", StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(item.Produkt, "Ny rad", StringComparison.OrdinalIgnoreCase)))
        {
            row.IsUserAdded = true;
        }
    }

    public void RefreshPackageQuoteLabels()
    {
        foreach (var row in SakvarorMangder)
        {
            var quote = row.PackageQuoteId is int id
                ? SakvarorOfferter.FirstOrDefault(q => q.Id == id)
                : null;
            row.PackageQuoteLabel = quote == null
                ? row.HasPackageQuote ? $"Ingår i offert #{row.PackageQuoteId}" : string.Empty
                : $"Ingår i offert {GetPackageQuoteReference(quote)}";
            row.NotifyQuoteChanged();
        }

        FilterSakvaror();
        RefreshSpecificationTotals();
    }

    public void RefreshSpecificationTotals()
    {
        NotifyQuoteTotals();
        UpdateSpecificationOutcome();
    }

    public async Task SaveSakvaraSpecificationRowAsync(LubeSakvaraMangd item)
    {
        try
        {
            item.ProjectId = Project.Id;
            await _dataService.Projects.SaveSakvaraSpecificationRowAsync(item);
            RefreshSpecificationTotals();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Kunde inte spara raden: {ex.Message}", "Sparfel");
        }
    }

    private decimal? GetBaseOutcomeCost()
    {
        if (string.IsNullOrWhiteSpace(Project.LubeSummaryJson)) return null;
        var cost = Summary.ActualProductionCost;
        if (!double.IsFinite(cost)) return null;
        try { return checked((decimal)cost); }
        catch (OverflowException) { return null; }
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
        report.AppendLine("Rad | ID | Beteckning | Produkt | Inmatat offertpris | Visat offertpris | Paketoffert");
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            if (row.Offertpris == 0 && row.EffectiveOffertpris == 0 && !row.HasPackageQuote) continue;
            report.AppendLine($"{index + 1} | {row.Id} | {row.Beteckning} | {row.Produkt} | {row.Offertpris.ToString("G17", culture)} | {row.EffectiveOffertpris.ToString("G17", culture)} | {row.PackageQuoteLabel}");
        }
        var total = UnlinkedRows.Sum(row => row.EffectiveOffertpris) + PackageQuotes.Sum(q => q.OffereratPris);
        report.AppendLine();
        report.AppendLine($"Summa inmatade offertpriser: {rows.Sum(row => row.Offertpris).ToString("N2", culture)} kr");
        report.AppendLine($"Summa Offertpris inklusive paketofferter: {total.ToString("N2", culture)} kr");
        report.AppendLine($"Summa före visningsavrundning: {total.ToString("G17", culture)}");
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
            FontSize = 12,
            Margin = new System.Windows.Thickness(12)
        };
        var offerWindow = new System.Windows.Window
        {
            Title = "Kontrollera offertsumma",
            Width = 920,
            Height = 560,
            Content = text,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner
        };
        if (System.Windows.Application.Current?.MainWindow is { } offerOwner)
            offerWindow.Owner = offerOwner;
        offerWindow.ShowDialog();
    }

    [RelayCommand]
    private async Task LinkSelectedToQuoteAsync(System.Collections.IList? selectedItems)
    {
        if (selectedItems == null)
        {
            _dialogService.ShowError("Markeringen kunde inte läsas från artikeltabellen.", "Offert");
            return;
        }

        // Snapshot the grid's selection before opening a dialog or awaiting a save.
        var selected = selectedItems.OfType<LubeSakvaraMangd>()
            .Where(row => !row.IsQuoteSummaryRow).Distinct().ToArray();
        if (selected.Length == 0)
        {
            _dialogService.ShowError("Markera minst en artikelrad i specifikationen.", "Offert");
            return;
        }

        if (selected.Any(row => row.Id == 0))
        {
            _dialogService.ShowError("En eller flera markerade artikelrader är inte sparade. Spara raderna innan du kopplar dem till en offert.", "Offert");
            return;
        }

        if (selected.Any(r => r.HasPackageQuote) &&
            !_dialogService.Confirm("En eller flera rader ingår redan i en offert. Vill du flytta dem till den nya offerten?", "Offert"))
            return;

        var dialog = new LinkQuoteDialogViewModel(selected);
        var quoteWindow = new LinkQuoteDialog(dialog);
        if (System.Windows.Application.Current?.MainWindow is { } owner)
            quoteWindow.Owner = owner;
        if (quoteWindow.ShowDialog() != true) return;

        var quote = new LubeSakvaraOffert
        {
            ProjectId = Project.Id,
            Leverantor = dialog.Supplier.Trim(),
            Produktgrupp = string.IsNullOrWhiteSpace(dialog.QuoteNumber)
                ? $"Paketoffert {dialog.Supplier}".Trim()
                : dialog.QuoteNumber.Trim(),
            OffereratPris = dialog.QuotedAmount,
            FinalAmount = dialog.FinalAmount,
            QuoteDate = dialog.QuoteDate,
            FilePath = dialog.FilePath,
            IsPackageQuote = true,
            Kommentar = string.Join(", ", selected.Select(r => r.Beteckning).Where(s => !string.IsNullOrWhiteSpace(s)))
        };

        try
        {
            await _dataService.Projects.SavePackageQuoteAsync(quote, selected.Select(r => r.Id).ToArray());
            if (SakvarorOfferter.All(q => q.Id != quote.Id))
                SakvarorOfferter.Add(quote);
            foreach (var row in selected)
            {
                row.PackageQuoteId = quote.Id;
                if (string.IsNullOrWhiteSpace(row.Leverantor))
                    row.Leverantor = quote.Leverantor;
            }
            RefreshPackageQuoteLabels();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Kunde inte spara offerten: {ex.Message}", "Offert");
        }
    }

    [RelayCommand]
    private async Task AddSakvaraRowAsync()
    {
        var item = new LubeSakvaraMangd { ProjectId = Project.Id, Enhet = "st" };
        try
        {
            await _dataService.Projects.SaveSakvaraSpecificationRowAsync(item);
            SakvarorMangder.Add(item);
            FilterSakvaror();
            RefreshSpecificationTotals();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Kunde inte lägga till raden: {ex.Message}", "Sparfel");
        }
    }

    private void FilterSakvaror()
    {
        FilteredSakvaror =
            string.IsNullOrWhiteSpace(FilterText)
            ? new ObservableCollection<LubeSakvaraMangd>(SakvarorMangder)
            : new ObservableCollection<LubeSakvaraMangd>(SakvarorMangder.Where(MatchesFilter));
        RefreshSpecificationRows();
    }

    private void RefreshSpecificationRows()
    {
        var rows = FilteredSakvaror.ToList();
        var visibleQuoteIds = rows.Where(row => row.HasPackageQuote)
            .Select(row => row.PackageQuoteId!.Value).ToHashSet();

        foreach (var quote in PackageQuotes)
        {
            var summary = new LubeSakvaraMangd
            {
                IsQuoteSummaryRow = true,
                SummaryQuoteId = quote.Id,
                ProjectId = Project.Id,
                Grupp = "Offert",
                Beteckning = $"Offert {GetPackageQuoteReference(quote)}",
                Produkt = "Paketoffert – sammanställning",
                Leverantor = quote.Leverantor,
                Antal = 1,
                Enhet = "paket",
                PrisNto = GetCalculatedQuoteNetAmount(quote.Id),
                Offertpris = quote.OffereratPris,
                Slutpris = quote.EffectiveAmount,
                Anteckningar = quote.Kommentar ?? string.Empty
            };
            if (string.IsNullOrWhiteSpace(FilterText) || visibleQuoteIds.Contains(quote.Id) || MatchesFilter(summary))
                rows.Add(summary);
        }

        // Preserve the actual article instances and their selection; summaries
        // exist only in this view collection, never in SakvarorMangder.
        var desired = rows.ToHashSet();
        for (var index = SpecificationRows.Count - 1; index >= 0; index--)
            if (!desired.Contains(SpecificationRows[index]))
                SpecificationRows.RemoveAt(index);

        for (var index = 0; index < rows.Count; index++)
        {
            if (index < SpecificationRows.Count && ReferenceEquals(SpecificationRows[index], rows[index]))
                continue;
            var previousIndex = SpecificationRows.IndexOf(rows[index]);
            if (previousIndex >= 0)
                SpecificationRows.Move(previousIndex, index);
            else
                SpecificationRows.Insert(index, rows[index]);
        }
    }

    private bool MatchesFilter(LubeKalkylPost item)
        => ContainsFilter(item.Kod) || ContainsFilter(item.Benamning);

    private bool MatchesFilter(LubeSakvaraMangd item)
        => ContainsFilter(item.Beteckning) || ContainsFilter(item.Produkt) || ContainsFilter(item.Produktkod)
            || ContainsFilter(item.Leverantor) || ContainsFilter(item.Grupp) || ContainsFilter(item.PackageQuoteLabel);

    private bool ContainsFilter(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Contains(FilterText.Trim(), StringComparison.CurrentCultureIgnoreCase);

    private void NotifyQuoteTotals()
    {
        OnPropertyChanged(nameof(SumPrisNto));
        OnPropertyChanged(nameof(SumOffertpris));
        OnPropertyChanged(nameof(SumSlutpris));
        OnPropertyChanged(nameof(SumDifferens));
        OnPropertyChanged(nameof(UnlinkedRows));
        OnPropertyChanged(nameof(PackageQuotes));
    }

    private void UpdateSpecificationOutcome()
    {
        if (_updatingSpecificationOutcome) return;
        var hasSpecification = SakvarorMangder.Count > 0 || PackageQuotes.Any();
        var wasManaged = Summary.ReportSections.SelectMany(section => section.Rows)
            .Any(row => row.Label.Trim().Equals("Sakvaror", StringComparison.OrdinalIgnoreCase)
                && row.List == "Specifikation");
        if (!hasSpecification && !wasManaged) return;
        _updatingSpecificationOutcome = true;
        try
        {
            Summary.ApplySpecificationOutcome(SumSlutpris);
        }
        finally
        {
            _updatingSpecificationOutcome = false;
        }
        OnPropertyChanged(nameof(Outcome));
    }

    private void LoadFromProject(Project project)
    {
        Project = project;
        KalkylPoster = new ObservableCollection<LubeKalkylPost>(project.LubeKalkylPoster ?? []);
        Projektanpassningar = new ObservableCollection<LubeProjektanpassningPost>(project.LubeProjektanpassningar ?? []);
        SakvarorOfferter = new ObservableCollection<LubeSakvaraOffert>(project.LubeSakvarorOfferter ?? []);
        SakvarorMangder = new ObservableCollection<LubeSakvaraMangd>(project.LubeSakvarorMangder ?? []);
        SakvarorLeveranspriser = new ObservableCollection<LubeSakvaraLeveranspris>(project.LubeSakvarorLeveranspriser ?? []);
        SakvarorMontage = new ObservableCollection<LubeSakvaraMontage>(project.LubeSakvarorMontagePoster ?? []);
        EjMangdbaserade = new ObservableCollection<LubeEjMangdbaserad>(project.LubeEjMangdbaseradePoster ?? []);
        LubeProjektNummer = project.ProjectNumber;
        Summary = LoadSummary(project.LubeSummaryJson);
        FilterKalkyl();
        FilterSakvaror();
        RefreshAtaSummary();
        RefreshPackageQuoteLabels();
        OnPropertyChanged(nameof(SumTotalMaterial));
        OnPropertyChanged(nameof(SumTotalTimmar));
        OnPropertyChanged(nameof(SumTotalMaterial));
                OnPropertyChanged(nameof(SumTotalTimmar));
                OnPropertyChanged(nameof(Outcome));
                OnPropertyChanged(nameof(SumPrisNto));
                OnPropertyChanged(nameof(SumOffertpris));
                OnPropertyChanged(nameof(SumSlutpris));
                OnPropertyChanged(nameof(SumDifferens));
    }

    private static LubeCalculationSummary LoadSummary(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new LubeCalculationSummary();
        try
        {
            return JsonSerializer.Deserialize<LubeCalculationSummary>(json) ?? new LubeCalculationSummary();
        }
        catch (JsonException)
        {
            return new LubeCalculationSummary();
        }
    }

    private LubeTabOption CreateTab(string key, string title, bool canHide)
    {
        var hidden = _settingsService.LoadSettings().HiddenLubeTabs
            .Contains(key, StringComparer.OrdinalIgnoreCase);
        var option = new LubeTabOption(key, title, canHide, !hidden, OnTabVisibilityChanged);
        TabOptions.Add(option);
        return option;
    }

    private void OnTabVisibilityChanged(LubeTabOption option)
    {
        var settings = _settingsService.LoadSettings();
        settings.HiddenLubeTabs = TabOptions
            .Where(tab => tab.CanHide && !tab.IsVisible)
            .Select(tab => tab.Key)
            .ToList();
        _settingsService.SaveSettings(settings);
        if (SelectedLubeTabIndex >= 0 && SelectedLubeTabIndex < TabOptions.Count && !TabOptions[SelectedLubeTabIndex].IsVisible)
            SelectedLubeTabIndex = 0;
    }

    [RelayCommand]
    public async Task CreateQuoteForArticlesAsync(IReadOnlyList<LubeSakvaraMangd> articles)
    {
        if (articles.Count == 0) return;
        if (articles.Any(a => a.Id == 0))
        {
            _dialogService.ShowError("En eller flera av de valda artiklarna är inte sparade. Spara artiklarna innan du skapar en offert.", "Offert");
            return;
        }

        var distinctArticles = articles.Distinct().ToArray();
        var dialog = new LinkQuoteDialogViewModel(distinctArticles);
        var quoteWindow = new LinkQuoteDialog(dialog);
        if (System.Windows.Application.Current?.MainWindow is { } owner)
            quoteWindow.Owner = owner;
        if (quoteWindow.ShowDialog() != true) return;

        var quote = new LubeSakvaraOffert
        {
            ProjectId = Project.Id,
            Leverantor = dialog.Supplier.Trim(),
            Produktgrupp = string.IsNullOrWhiteSpace(dialog.QuoteNumber)
                ? $"Paketoffert {dialog.Supplier}".Trim()
                : dialog.QuoteNumber.Trim(),
            OffereratPris = dialog.QuotedAmount,
            FinalAmount = dialog.FinalAmount,
            QuoteDate = dialog.QuoteDate,
            FilePath = dialog.FilePath,
            IsPackageQuote = true,
            Kommentar = string.Join(", ", distinctArticles.Select(r => r.Beteckning).Where(s => !string.IsNullOrWhiteSpace(s)))
        };

        try
        {
            await _dataService.Projects.SavePackageQuoteAsync(quote, distinctArticles.Select(r => r.Id).ToArray());
            if (SakvarorOfferter.All(q => q.Id != quote.Id))
                SakvarorOfferter.Add(quote);
            foreach (var row in distinctArticles)
            {
                row.PackageQuoteId = quote.Id;
                if (string.IsNullOrWhiteSpace(row.Leverantor))
                    row.Leverantor = quote.Leverantor;
            }
            RefreshPackageQuoteLabels();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Kunde inte spara offerten: {ex.Message}", "Offert");
        }
    }

    public async Task AddArticlesToQuoteAsync(int quoteId, IReadOnlyList<LubeSakvaraMangd> articles)
    {
        if (articles.Count == 0) return;
        if (articles.Any(a => a.Id == 0))
        {
            _dialogService.ShowError("En eller flera av de valda artiklarna är inte sparade. Spara artiklarna innan du lägger till dem i offerten.", "Offert");
            return;
        }

        var existingQuote = SakvarorOfferter.FirstOrDefault(q => q.Id == quoteId);
        if (existingQuote == null)
        {
            _dialogService.ShowError("Den angivna offerten kunde inte hittas.", "Offert");
            return;
        }

        if (articles.Any(r => r.HasPackageQuote && r.PackageQuoteId != quoteId) &&
            !_dialogService.Confirm("En eller flera artiklar ingår redan i en annan offert. Vill du flytta dem till den valda offerten?", "Offert"))
            return;

        var articleIds = articles.Select(a => a.Id).ToArray();
        try
        {
            await _dataService.Projects.SavePackageQuoteAsync(existingQuote, articleIds);
            foreach (var row in articles)
            {
                row.PackageQuoteId = quoteId;
                if (string.IsNullOrWhiteSpace(row.Leverantor))
                    row.Leverantor = existingQuote.Leverantor;
            }
            RefreshPackageQuoteLabels();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Kunde inte uppdatera offerten: {ex.Message}", "Offert");
        }
    }

    public async Task RemoveArticlesFromQuoteAsync(IReadOnlyList<LubeSakvaraMangd> articles)
    {
        if (articles.Count == 0) return;

        var articleIds = articles.Select(a => a.Id).ToArray();
        try
        {
            await _dataService.Projects.SetArticleQuoteLinksAsync(Project.Id, articleIds, null);
            foreach (var row in articles)
                row.PackageQuoteId = null;
            RefreshPackageQuoteLabels();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Kunde inte ta bort artiklarna från offerten: {ex.Message}", "Offert");
        }
    }
}
