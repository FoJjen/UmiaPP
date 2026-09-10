using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UmiaPP.Data;
using UmiaPP.Models;
using UmiaPP.Services;

namespace UmiaPP.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly ILocalizationService _localization;
    private readonly IDialogService _dialogService;
    private readonly ILubeImportService _lubeImport;
    private readonly IBluebeamXmlParserService _bluebeamParser;
    private readonly IExportService _exportService;

    [ObservableProperty]
    private string _windowTitle = "UmiaPP - Ventilationsentreprenad";

    [ObservableProperty]
    private string _activeProjectTitle = "Inget projekt valt";

    [ObservableProperty]
    private bool _hasActiveProject;

    [ObservableProperty]
    private Project? _currentProject;

    [ObservableProperty]
    private object? _currentView;

    public ProjectListViewModel ProjectListVm { get; }
    public ActiveProjectViewModel? ActiveProjectVm { get; private set; }

    public MainViewModel(
        IDataService dataService,
        ILocalizationService localization,
        IDialogService dialogService,
        ILubeImportService lubeImport,
        IBluebeamXmlParserService bluebeamParser,
        IExportService exportService)
    {
        _dataService = dataService;
        _localization = localization;
        _dialogService = dialogService;
        _lubeImport = lubeImport;
        _bluebeamParser = bluebeamParser;
        _exportService = exportService;

        ProjectListVm = new ProjectListViewModel(_dataService, _localization, _dialogService, OpenProject);
        CurrentView = ProjectListVm;
        UpdateActiveProjectTitle();
    }

    public async Task InitializeAsync()
    {
        await _dataService.InitializeAsync();
        await ProjectListVm.LoadProjectsAsync();
    }

    public async void OpenProject(Project project)
    {
        var fullProject = await _dataService.Projects.GetByIdAsync(project.Id) ?? project;

        CurrentProject = fullProject;
        HasActiveProject = true;
        UpdateActiveProjectTitle();

        ActiveProjectVm = new ActiveProjectViewModel(
            fullProject,
            _dataService,
            _lubeImport,
            _bluebeamParser,
            _exportService,
            _dialogService,
            BackToProjectList);

        CurrentView = ActiveProjectVm;
    }

    [RelayCommand]
    public async Task ReturnToProjectList()
    {
        HasActiveProject = false;
        CurrentProject = null;
        UpdateActiveProjectTitle();
        CurrentView = ProjectListVm;
        await ProjectListVm.LoadProjectsAsync();
    }

    private void BackToProjectList()
    {
        _ = ReturnToProjectList();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var settingsVm = new SettingsViewModel(_localization);
        var settingsDialog = new Views.SettingsDialog(settingsVm);
        settingsDialog.ShowDialog();
        UpdateActiveProjectTitle();
    }

    private void UpdateActiveProjectTitle()
    {
        ActiveProjectTitle = CurrentProject == null
            ? _localization.GetString("NoProjectSelected")
            : $"{CurrentProject.ProjectNumber} - {CurrentProject.Name}";
    }
}
