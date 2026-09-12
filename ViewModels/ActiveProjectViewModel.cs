using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UmiaPP.Data;
using UmiaPP.Models;
using UmiaPP.Services;

namespace UmiaPP.ViewModels;

public partial class ActiveProjectViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly ILubeImportService _lubeImport;
    private readonly IBluebeamXmlParserService _bluebeamParser;
    private readonly IExportService _exportService;
    private readonly IDialogService _dialogService;
    private readonly Action _onBackToList;

    [ObservableProperty]
    private Project _project;

    [ObservableProperty]
    private int _selectedTabIndex;

    public OverviewTabViewModel OverviewTab { get; }
    public LubeCalculationTabViewModel LubeTab { get; }
    public PurchaseTabViewModel PurchaseTab { get; }
    public AtaTabViewModel AtaTab { get; }

    public ActiveProjectViewModel(
        Project project,
        IDataService dataService,
        ILubeImportService lubeImport,
        IBluebeamXmlParserService bluebeamParser,
        IExportService exportService,
        IDialogService dialogService,
        Action onBackToList)
    {
        _project = project;
        _dataService = dataService;
        _lubeImport = lubeImport;
        _bluebeamParser = bluebeamParser;
        _exportService = exportService;
        _dialogService = dialogService;
        _onBackToList = onBackToList;

        OverviewTab = new OverviewTabViewModel(project, dataService, dialogService);
        LubeTab = new LubeCalculationTabViewModel(project, dataService, lubeImport, dialogService);
        PurchaseTab = new PurchaseTabViewModel(project, dataService, bluebeamParser, exportService, dialogService);
        AtaTab = new AtaTabViewModel(project, dataService, dialogService, exportService);
        AtaTab.SummaryChanged += (_, _) => LubeTab.RefreshAtaSummary();
        OverviewTab.SetOutcomeProvider(() => LubeTab.Outcome);
        OverviewTab.SetCardMetricsProvider(() => (
            LubeTab.Summary.SummaEfterJusteringTimmar + LubeTab.Summary.InjusteringTimmar,
            LubeTab.SakvarorMangder.Sum(row => row.Antal),
            PurchaseTab.Drawings.Count));
        System.Collections.Specialized.INotifyCollectionChanged? observedRows = null;
        System.Collections.Specialized.INotifyCollectionChanged? observedDrawings = null;
        void RefreshCards(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs args)
            => OverviewTab.RefreshCardMetrics();
        void ObserveCardCollections()
        {
            if (observedRows != null) observedRows.CollectionChanged -= RefreshCards;
            if (observedDrawings != null) observedDrawings.CollectionChanged -= RefreshCards;
            observedRows = LubeTab.SakvarorMangder;
            observedDrawings = PurchaseTab.Drawings;
            observedRows.CollectionChanged += RefreshCards;
            observedDrawings.CollectionChanged += RefreshCards;
            OverviewTab.RefreshCardMetrics();
        }
        ObserveCardCollections();
        PurchaseTab.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(PurchaseTab.Drawings)) ObserveCardCollections();
        };
        LubeTab.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(LubeTab.SakvarorMangder)) ObserveCardCollections();
            OverviewTab.RefreshCardMetrics();
            if (string.IsNullOrEmpty(args.PropertyName) || args.PropertyName == nameof(LubeTab.Outcome))
                OverviewTab.RefreshCurrentOutcome();
        };
        OverviewTab.SalesCalculationChanged += (_, _) => LubeTab.RefreshAtaSummary();
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        if (value == 0)
            OverviewTab.RefreshMetrics();
        if (value == 1)
            LubeTab.RefreshAtaSummary();
    }

    public void Refresh(Project project)
    {
        Project = project;
        OverviewTab.UpdateProject(project);
        LubeTab.UpdateProject(project);
        PurchaseTab.UpdateProject(project);
        AtaTab.UpdateProject(project);
        OverviewTab.RefreshCardMetrics();
        OverviewTab.RefreshCurrentOutcome();
    }

    [RelayCommand]
    private void Back()
    {
        _onBackToList();
    }
}
