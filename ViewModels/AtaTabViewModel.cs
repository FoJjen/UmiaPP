using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UmiaPP.Models;
using UmiaPP.Services;

namespace UmiaPP.ViewModels;

public partial class AtaTabViewModel : ObservableObject
{
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private Project _project;

    [ObservableProperty]
    private string _heading = "Ändrings- och Tilläggsarbeten (ÄTA)";

    public AtaTabViewModel(Project project, IDialogService dialogService)
    {
        _project = project;
        _dialogService = dialogService;
    }

    public void UpdateProject(Project project)
    {
        Project = project;
    }

    [RelayCommand]
    private void RegisterAta()
    {
        _dialogService.ShowMessage(
            "ÄTA-modulen är i drift som förberedd modul. Här kommer du kunna registrera ÄTA-nummer, koppla till beställare, specificera avstämda timmar och materialpåslag.",
            "ÄTA-registrering");
    }
}
