using CommunityToolkit.Mvvm.ComponentModel;
using UmiaPP.Models;

namespace UmiaPP.ViewModels;

public partial class OverviewTabViewModel : ObservableObject
{
    [ObservableProperty]
    private Project _project;

    [ObservableProperty]
    private double _totalHours;

    [ObservableProperty]
    private double _totalMaterialCost;

    [ObservableProperty]
    private int _drawingCount;

    [ObservableProperty]
    private int _componentCount;

    public OverviewTabViewModel(Project project)
    {
        _project = project;
        RefreshMetrics();
    }

    public void UpdateProject(Project project)
    {
        Project = project;
        RefreshMetrics();
    }

    public void RefreshMetrics()
    {
        DrawingCount = Project.Drawings?.Count ?? 0;
        TotalHours = Project.LubeKalkylPoster?.Sum(x => x.TotaltMontageTimmar) ?? 0;
        TotalMaterialCost = Project.LubeKalkylPoster?.Sum(x => x.TotaltMaterial) ?? 0;
        ComponentCount = (Project.BluebeamMarkups?.Count ?? 0) + (Project.LubeSakvarorMangder?.Count ?? 0);
    }
}
