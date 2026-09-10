using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UmiaPP.Data;
using UmiaPP.Models;
using UmiaPP.Services;

namespace UmiaPP.ViewModels;

public partial class ProjectListViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly ILocalizationService _localization;
    private readonly IDialogService _dialogService;
    private readonly Action<Project> _onOpenProject;

    [ObservableProperty]
    private ObservableCollection<Project> _projects = [];

    [ObservableProperty]
    private ObservableCollection<Project> _filteredProjects = [];

    [ObservableProperty]
    private Project? _selectedProject;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    public ProjectListViewModel(
        IDataService dataService,
        ILocalizationService localization,
        IDialogService dialogService,
        Action<Project> onOpenProject)
    {
        _dataService = dataService;
        _localization = localization;
        _dialogService = dialogService;
        _onOpenProject = onOpenProject;
    }

    public async Task LoadProjectsAsync()
    {
        IsLoading = true;
        try
        {
            var list = await _dataService.Projects.GetAllAsync();
            Projects = new ObservableCollection<Project>(list);
            FilterProjects();
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        FilterProjects();
    }

    private void FilterProjects()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            FilteredProjects = new ObservableCollection<Project>(Projects);
            return;
        }

        var q = SearchText.Trim().ToLowerInvariant();
        var filtered = Projects.Where(p =>
            (p.Name?.ToLowerInvariant().Contains(q) ?? false) ||
            (p.ProjectNumber?.ToLowerInvariant().Contains(q) ?? false) ||
            (p.Client?.ToLowerInvariant().Contains(q) ?? false)).ToList();

        FilteredProjects = new ObservableCollection<Project>(filtered);
    }

    [RelayCommand]
    private async Task NewProjectAsync()
    {
        var vm = new ProjectDialogViewModel(_localization);
        var dialog = new Views.ProjectEditDialog(vm);
        if (dialog.ShowDialog() == true && vm.IsConfirmed)
        {
            var project = new Project();
            vm.ApplyToProject(project);
            await _dataService.Projects.CreateAsync(project);
            await LoadProjectsAsync();
        }
    }

    [RelayCommand]
    private async Task EditProjectAsync(Project? project)
    {
        var target = project ?? SelectedProject;
        if (target == null) return;

        var vm = new ProjectDialogViewModel(_localization, target);
        var dialog = new Views.ProjectEditDialog(vm);
        if (dialog.ShowDialog() == true && vm.IsConfirmed)
        {
            vm.ApplyToProject(target);
            await _dataService.Projects.UpdateAsync(target);
            await LoadProjectsAsync();
        }
    }

    [RelayCommand]
    private async Task DeleteProjectAsync(Project? project)
    {
        var target = project ?? SelectedProject;
        if (target == null) return;

        var message = string.Format(_localization.GetString("DeleteConfirmMessage"), target.Name);
        var title = _localization.GetString("DeleteConfirmTitle");

        if (_dialogService.Confirm(message, title))
        {
            await _dataService.Projects.DeleteAsync(target.Id);
            await LoadProjectsAsync();
        }
    }

    [RelayCommand]
    private void OpenProject(Project? project)
    {
        var target = project ?? SelectedProject;
        if (target != null)
        {
            _onOpenProject(target);
        }
    }
}
