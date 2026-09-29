using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UmiaPP.Data;
using UmiaPP.Models;
using UmiaPP.Services;

namespace UmiaPP.ViewModels;

public partial class InspectionTemplateChoice : ObservableObject
{
    private readonly InspectionTabViewModel _owner;
    private bool _ready;

    public InspectionPointTemplate Template { get; }

    [ObservableProperty]
    private bool _isIncluded;

    public InspectionTemplateChoice(InspectionTabViewModel owner, InspectionPointTemplate template, bool isIncluded)
    {
        _owner = owner;
        Template = template;
        _isIncluded = isIncluded;
        _ready = true;
    }

    partial void OnIsIncludedChanged(bool value)
    {
        if (_ready)
            _ = _owner.SaveSelectedListAsync();
    }
}

public partial class InspectionTabViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly IExportService _exportService;
    private readonly IDialogService _dialogService;
    private bool _loadingChecklist;

    [ObservableProperty]
    private Project _project;

    [ObservableProperty]
    private ObservableCollection<Drawing> _drawings = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportListCommand))]
    private Drawing? _selectedDrawing;

    [ObservableProperty]
    private ObservableCollection<InspectionTemplateChoice> _templateChoices = [];

    [ObservableProperty]
    private string _newListName = string.Empty;

    [ObservableProperty]
    private string _newPointTitle = string.Empty;

    [ObservableProperty]
    private string _newPointDescription = string.Empty;

    [ObservableProperty]
    private string _status = "Skapa en egenkontrollista och markera de förinställda kontrollpunkter som ska ingå.";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty] private DateTime _inspectionDate = DateTime.Today;
    [ObservableProperty] private string _customerReference = string.Empty;
    [ObservableProperty] private string _preparedBy = string.Empty;
    [ObservableProperty] private string _approvedBy = string.Empty;
    [ObservableProperty] private string _documentNumber = string.Empty;
    [ObservableProperty] private string _revision = "1";
    [ObservableProperty] private string _workTask = string.Empty;
    [ObservableProperty] private string _buildingPart = string.Empty;
    [ObservableProperty] private string _workDescription = string.Empty;
    [ObservableProperty] private int _sectionNumber = 1;

    public InspectionTabViewModel(
        Project project,
        IDataService dataService,
        IExportService exportService,
        IDialogService dialogService)
    {
        _project = project;
        _dataService = dataService;
        _exportService = exportService;
        _dialogService = dialogService;
        CustomerReference = project.CustomerReference ?? string.Empty;
        PreparedBy = CurrentWindowsUser();
        LoadInspectionLists();
        if (SelectedDrawing == null)
            _ = LoadChecklistAsync();
    }

    public void UpdateProject(Project project)
    {
        Project = project;
        CustomerReference = project.CustomerReference ?? string.Empty;
        PreparedBy = CurrentWindowsUser();
        RefreshLists();
    }

    private static string CurrentWindowsUser()
    {
        var user = Environment.UserName;
        return string.IsNullOrWhiteSpace(user) ? string.Empty : user.Trim();
    }

    public void RefreshLists()
    {
        var selectedId = SelectedDrawing?.Id;
        LoadInspectionLists();
        if (selectedId is int id)
        {
            var match = Drawings.FirstOrDefault(drawing => drawing.Id == id);
            if (match != null && !ReferenceEquals(match, SelectedDrawing))
                SelectedDrawing = match;
        }
        if (SelectedDrawing == null)
            _ = LoadChecklistAsync();
    }

    private void LoadInspectionLists()
    {
        Drawings = new ObservableCollection<Drawing>((Project.Drawings ?? []).Where(drawing => drawing.IsInspectionList));
        if (SelectedDrawing == null || !Drawings.Any(drawing => drawing.Id == SelectedDrawing.Id))
            SelectedDrawing = Drawings.FirstOrDefault();
    }

    partial void OnSelectedDrawingChanged(Drawing? value) => _ = LoadChecklistAsync();

    [RelayCommand]
    private async Task ImportPurchaseDrawingsAsync()
    {
        var existing = Drawings
            .Select(drawing => drawing.DrawingNumber.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sources = (Project.Drawings ?? [])
            .Where(drawing => !drawing.IsInspectionList && !string.IsNullOrWhiteSpace(drawing.DrawingNumber))
            .Where(drawing => !existing.Contains(drawing.DrawingNumber.Trim()))
            .GroupBy(drawing => drawing.DrawingNumber.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        if (sources.Count == 0)
        {
            _dialogService.ShowMessage("Alla ritningar från Inköp finns redan i Egenkontroll.", "Egenkontroll");
            return;
        }

        IsBusy = true;
        try
        {
            foreach (var source in sources)
            {
                var drawing = new Drawing
                {
                    ProjectId = Project.Id,
                    DrawingNumber = source.DrawingNumber.Trim(),
                    SheetTitle = source.SheetTitle,
                    Revision = string.IsNullOrWhiteSpace(source.Revision) ? "1" : source.Revision,
                    Date = source.Date == default ? DateTime.Today : source.Date,
                    FilePath = source.FilePath,
                    IsInspectionList = true
                };
                var saved = await _dataService.Projects.AddDrawingAsync(drawing);
                Project.Drawings.Add(saved);
            }
            RefreshLists();
            Status = $"{sources.Count} ritningar hämtades från Inköp.";
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Kunde inte hämta ritningarna:\n{ex.Message}", "Egenkontroll");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportAllListsAsync()
    {
        if (Drawings.Count == 0)
        {
            _dialogService.ShowMessage("Det finns inga egenkontrollistor att exportera.", "Egenkontroll");
            return;
        }

        var path = _dialogService.SaveFileDialog("xlsx", "Excel (*.xlsx)|*.xlsx", $"{Project.ProjectNumber}-egenkontroller.xlsx");
        if (string.IsNullOrWhiteSpace(path)) return;

        IsBusy = true;
        try
        {
            var lists = new List<(Drawing Drawing, DrawingInspection Header, IReadOnlyList<DrawingInspectionItem> Items)>();
            foreach (var drawing in Drawings)
            {
                var header = await _dataService.Projects.GetDrawingInspectionAsync(drawing.Id);
                var items = (await _dataService.Projects.GetDrawingInspectionItemsAsync(drawing.Id))
                    .OrderBy(item => item.SortOrder)
                    .ToList();
                lists.Add((drawing, header, items));
            }
            await InspectionWorkbookExporter.ExportAllAsync(Project, lists, path);
            Status = $"{lists.Count} egenkontrollistor exporterades.";
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Kunde inte exportera listorna:\n{ex.Message}", "Egenkontroll");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task MovePointUpAsync(InspectionTemplateChoice? choice) => await MovePointAsync(choice, -1);

    [RelayCommand]
    private async Task MovePointDownAsync(InspectionTemplateChoice? choice) => await MovePointAsync(choice, 1);

    private async Task MovePointAsync(InspectionTemplateChoice? choice, int direction)
    {
        if (choice == null || SelectedDrawing == null) return;
        var index = TemplateChoices.IndexOf(choice);
        var target = index + direction;
        if (index < 0 || target < 0 || target >= TemplateChoices.Count) return;

        TemplateChoices.Move(index, target);
        for (var i = 0; i < TemplateChoices.Count; i++)
            TemplateChoices[i].Template.SortOrder = i + 1;
        await SaveSelectedListAsync();
    }

    private async Task LoadTemplatesAsync() => await LoadChecklistAsync();

    private async Task LoadChecklistAsync()
    {
        try
        {
            var templates = await _dataService.Projects.GetInspectionTemplatesAsync();
            await ApplyTemplatesAsync(templates);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Kunde inte läsa kontrollpunkterna:\n{ex.Message}", "Egenkontroll");
        }
    }

    private async Task ApplyTemplatesAsync(List<InspectionPointTemplate> templates)
    {
        var savedItems = SelectedDrawing == null
            ? []
            : await _dataService.Projects.GetDrawingInspectionItemsAsync(SelectedDrawing.Id);
        var includedIds = savedItems.Select(item => item.TemplateId).ToHashSet();
        var savedOrder = savedItems.OrderBy(item => item.SortOrder).Select(item => item.TemplateId).ToList();

        _loadingChecklist = true;
        try
        {
            if (SelectedDrawing != null)
            {
                var header = await _dataService.Projects.GetDrawingInspectionAsync(SelectedDrawing.Id);
                InspectionDate = header.Date == default ? DateTime.Today : header.Date;
                CustomerReference = string.IsNullOrWhiteSpace(Project.CustomerReference)
                    ? header.CustomerReference
                    : Project.CustomerReference;
                PreparedBy = CurrentWindowsUser();
                ApprovedBy = header.ApprovedBy;
                DocumentNumber = header.DocumentNumber;
                Revision = string.IsNullOrWhiteSpace(header.Revision) ? (SelectedDrawing.Revision ?? "1") : header.Revision;
                WorkTask = header.WorkTask;
                BuildingPart = header.BuildingPart;
                WorkDescription = header.Description;
                SectionNumber = header.SectionNumber < 1 ? 1 : header.SectionNumber;
            }
            var orderedTemplates = templates
                .OrderBy(template =>
                {
                    var index = savedOrder.IndexOf(template.Id);
                    return index < 0 ? int.MaxValue : index;
                })
                .ThenBy(template => template.SortOrder)
                .ThenBy(template => template.Title);
            TemplateChoices = new ObservableCollection<InspectionTemplateChoice>(
                orderedTemplates.Select(template => new InspectionTemplateChoice(this, template, includedIds.Contains(template.Id))));
            ExportListCommand.NotifyCanExecuteChanged();
            Status = SelectedDrawing == null
                ? "Skapa en egenkontrollista. Listorna är fristående från Inköp."
                : $"{SelectedDrawing.DrawingNumber}: {TemplateChoices.Count(choice => choice.IsIncluded)} kontrollpunkter valda.";
        }
        finally
        {
            _loadingChecklist = false;
        }
    }

    internal async Task SaveSelectedListAsync()
    {
        if (_loadingChecklist || SelectedDrawing == null) return;
        try
        {
            IsBusy = true;
            var items = TemplateChoices
                .Where(choice => choice.IsIncluded)
                .Select((choice, index) => new DrawingInspectionItem
                {
                    DrawingId = SelectedDrawing.Id,
                    TemplateId = choice.Template.Id,
                    Title = choice.Template.Title,
                    Description = choice.Template.Description,
                    SortOrder = index
                }).ToList();
            await _dataService.Projects.SaveDrawingInspectionItemsAsync(SelectedDrawing.Id, items);
            await _dataService.Projects.SaveDrawingInspectionAsync(CreateHeader());
            Status = $"{SelectedDrawing.DrawingNumber}: {items.Count} kontrollpunkter sparade.";
            ExportListCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Kunde inte spara egenkontrollen:\n{ex.Message}", "Egenkontroll");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateListAsync()
    {
        if (string.IsNullOrWhiteSpace(NewListName))
        {
            _dialogService.ShowError("Ange ett namn för egenkontrollistan.", "Validering");
            return;
        }

        try
        {
            IsBusy = true;
            var name = NewListName.Trim();
            var list = await _dataService.Projects.AddDrawingAsync(new Drawing
            {
                ProjectId = Project.Id,
                DrawingNumber = name,
                SheetTitle = name,
                Revision = "1",
                Date = DateTime.Today,
                IsInspectionList = true
            });
            Project.Drawings ??= [];
            Project.Drawings.Add(list);
            NewListName = string.Empty;
            LoadInspectionLists();
            SelectedDrawing = Drawings.FirstOrDefault(drawing => drawing.Id == list.Id) ?? list;
            Status = $"Listan {list.DrawingNumber} är skapad.";
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Kunde inte skapa listan:\n{ex.Message}", "Egenkontroll");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteListAsync(Drawing? list)
    {
        var target = list ?? SelectedDrawing;
        if (target == null) return;
        if (!_dialogService.Confirm($"Vill du ta bort egenkontrollistan \"{target.DrawingNumber}\"?", "Ta bort lista"))
            return;

        try
        {
            IsBusy = true;
            await _dataService.Projects.DeleteDrawingAsync(target.Id);
            Project.Drawings?.RemoveAll(drawing => drawing.Id == target.Id);
            LoadInspectionLists();
            Status = "Listan är borttagen.";
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Kunde inte ta bort listan:\n{ex.Message}", "Egenkontroll");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task AddPointFromDialogAsync(string title, string description)
    {
        NewPointTitle = title?.Trim() ?? string.Empty;
        NewPointDescription = description?.Trim() ?? string.Empty;
        await AddPointAsync();
    }

    public async Task SaveTemplateAsync(InspectionTemplateChoice choice)
    {
        if (_loadingChecklist || choice.Template.Id == 0) return;
        if (string.IsNullOrWhiteSpace(choice.Template.Title))
        {
            _dialogService.ShowError("Ange en kontrollpunkt innan den sparas.", "Validering");
            return;
        }

        try
        {
            await _dataService.Projects.SaveInspectionTemplateAsync(choice.Template);
            if (choice.IsIncluded && SelectedDrawing != null)
                await SaveSelectedListAsync();
            Status = $"Kontrollpunkten \"{choice.Template.Title}\" är uppdaterad i katalogen.";
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Kunde inte spara kontrollpunkten:\n{ex.Message}", "Egenkontroll");
        }
    }

    [RelayCommand]
    private async Task AddPointAsync()
    {
        if (string.IsNullOrWhiteSpace(NewPointTitle))
        {
            _dialogService.ShowError("Ange en kontrollpunkt innan den sparas.", "Validering");
            return;
        }

        try
        {
            IsBusy = true;
            await _dataService.Projects.SaveInspectionTemplateAsync(new InspectionPointTemplate
            {
                Title = NewPointTitle.Trim(),
                Description = NewPointDescription.Trim(),
                SortOrder = TemplateChoices.Count
            });
            NewPointTitle = string.Empty;
            NewPointDescription = string.Empty;
            await LoadTemplatesAsync();
            Status = "Kontrollpunkten är sparad och tillgänglig i alla projekt.";
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Kunde inte spara kontrollpunkten:\n{ex.Message}", "Egenkontroll");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeletePointAsync(InspectionTemplateChoice? choice)
    {
        if (choice == null) return;
        if (!_dialogService.Confirm(
                $"Vill du ta bort kontrollpunkten \"{choice.Template.Title}\" från katalogen? Befintliga listor behåller punkten tills de sparas om.",
                "Ta bort kontrollpunkt"))
            return;

        try
        {
            IsBusy = true;
            await _dataService.Projects.DeleteInspectionTemplateAsync(choice.Template.Id);
            await LoadTemplatesAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Kunde inte ta bort kontrollpunkten:\n{ex.Message}", "Egenkontroll");
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnWorkTaskChanged(string value) => _ = SaveHeaderAsync();
    partial void OnBuildingPartChanged(string value) => _ = SaveHeaderAsync();
    partial void OnPreparedByChanged(string value) => _ = SaveHeaderAsync();
    partial void OnApprovedByChanged(string value) => _ = SaveHeaderAsync();
    partial void OnDocumentNumberChanged(string value) => _ = SaveHeaderAsync();
    partial void OnRevisionChanged(string value) => _ = SaveHeaderAsync();
    partial void OnCustomerReferenceChanged(string value) => _ = SaveHeaderAsync();
    partial void OnWorkDescriptionChanged(string value) => _ = SaveHeaderAsync();
    partial void OnInspectionDateChanged(DateTime value) => _ = SaveHeaderAsync();
    partial void OnSectionNumberChanged(int value) => _ = SaveHeaderAsync();

    private async Task SaveHeaderAsync()
    {
        if (_loadingChecklist || SelectedDrawing == null) return;
        try { await _dataService.Projects.SaveDrawingInspectionAsync(CreateHeader()); }
        catch (Exception ex) { _dialogService.ShowError($"Kunde inte spara listans uppgifter:\n{ex.Message}", "Egenkontroll"); }
    }

    private DrawingInspection CreateHeader() => new()
    {
        DrawingId = SelectedDrawing?.Id ?? 0,
        Date = InspectionDate,
        CustomerReference = string.IsNullOrWhiteSpace(Project.CustomerReference) ? CustomerReference : Project.CustomerReference,
        PreparedBy = CurrentWindowsUser(),
        ApprovedBy = ApprovedBy,
        DocumentNumber = DocumentNumber,
        Revision = Revision,
        WorkTask = WorkTask,
        BuildingPart = BuildingPart,
        Description = WorkDescription,
        SectionNumber = SectionNumber
    };

    private bool CanExportList() =>
        SelectedDrawing != null && TemplateChoices.Any(choice => choice.IsIncluded);

    [RelayCommand(CanExecute = nameof(CanExportList))]
    private async Task ExportListAsync()
    {
        if (SelectedDrawing == null) return;
        var items = TemplateChoices
            .Where(choice => choice.IsIncluded)
            .Select((choice, index) => new DrawingInspectionItem
            {
                DrawingId = SelectedDrawing.Id,
                TemplateId = choice.Template.Id,
                Title = choice.Template.Title,
                Description = choice.Template.Description,
                SortOrder = index
            }).ToList();
        if (items.Count == 0)
        {
            _dialogService.ShowError("Välj minst en kontrollpunkt innan listan exporteras.", "Egenkontroll");
            return;
        }

        var path = _dialogService.SaveFileDialog(
            ".xlsx",
            "Excel-arbetsbok (*.xlsx)|*.xlsx",
            $"Egenkontroll-{SelectedDrawing.DrawingNumber}.xlsx");
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            IsBusy = true;
            await _dataService.Projects.SaveDrawingInspectionAsync(CreateHeader());
            await _exportService.ExportInspectionListToExcelAsync(Project, SelectedDrawing, CreateHeader(), items, path);
            Status = $"Egenkontrollen exporterades till {path}.";
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Kunde inte exportera listan:\n{ex.Message}", "Egenkontroll");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
