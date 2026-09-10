using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UmiaPP.Models;
using UmiaPP.Services;

namespace UmiaPP.ViewModels;

public partial class ProjectDialogViewModel : ObservableObject
{
    private readonly ILocalizationService _localization;

    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _projectNumber = string.Empty;

    [ObservableProperty]
    private string _client = string.Empty;

    [ObservableProperty]
    private ContractType _contractType = ContractType.Totalentreprenad;

    [ObservableProperty]
    private ProjectStatus _status = ProjectStatus.Kalkyl;

    [ObservableProperty]
    private DisciplineType _discipline = DisciplineType.Ventilation;

    [ObservableProperty]
    private DateTime _createdDate = DateTime.Now;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _title = "Nytt projekt";

    public bool IsConfirmed { get; private set; }

    public Array ContractTypes => Enum.GetValues(typeof(ContractType));
    public Array ProjectStatuses => Enum.GetValues(typeof(ProjectStatus));
    public Array Disciplines => Enum.GetValues(typeof(DisciplineType));

    public Action? CloseAction { get; set; }

    public ProjectDialogViewModel(ILocalizationService localization, Project? existing = null)
    {
        _localization = localization;
        if (existing != null)
        {
            Title = _localization.GetString("EditProject");
            Id = existing.Id;
            Name = existing.Name;
            ProjectNumber = existing.ProjectNumber;
            Client = existing.Client;
            ContractType = existing.ContractType;
            Status = existing.Status;
            Discipline = existing.Discipline;
            CreatedDate = existing.CreatedDate;
            Description = existing.Description ?? string.Empty;
        }
        else
        {
            Title = _localization.GetString("NewProject");
            CreatedDate = DateTime.Today;
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(ProjectNumber))
        {
            MessageBox.Show("Vänligen fyll i projektnamn och projektnummer.", "Validering", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        IsConfirmed = true;
        CloseAction?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        IsConfirmed = false;
        CloseAction?.Invoke();
    }

    public void ApplyToProject(Project project)
    {
        project.Name = Name.Trim();
        project.ProjectNumber = ProjectNumber.Trim();
        project.Client = Client.Trim();
        project.ContractType = ContractType;
        project.Status = Status;
        project.Discipline = Discipline;
        project.CreatedDate = CreatedDate;
        project.Description = Description;
    }
}
