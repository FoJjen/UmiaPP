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
        AtaTab = new AtaTabViewModel(project, dialogService);
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        if (value == 0)
            OverviewTab.RefreshMetrics();
    }

    public void Refresh(Project project)
    {
        Project = project;
        OverviewTab.UpdateProject(project);
        LubeTab.UpdateProject(project);
        PurchaseTab.UpdateProject(project);
        AtaTab.UpdateProject(project);
    }

    [RelayCommand]
    private void Back()
    {
        _onBackToList();
    }
}
