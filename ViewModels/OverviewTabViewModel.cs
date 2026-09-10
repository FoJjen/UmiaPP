using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using System.Text.Json;
using UmiaPP.Data;
using UmiaPP.Models;
using UmiaPP.Services;

namespace UmiaPP.ViewModels;

public partial class OverviewTabViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveSalesCalculationCommand))]
    private string _salesPriceInput = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveSalesCalculationCommand))]
    private string _budgetedCostInput = string.Empty;

    [ObservableProperty]
    private decimal? _currentTg;

    [ObservableProperty]
    private decimal? _productionCost;

    private static bool TryAmount(string input, out decimal amount) =>
        decimal.TryParse(input, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
            CultureInfo.CurrentCulture, out amount) && amount >= 0;

    private bool CanSaveSalesCalculation() => TryAmount(SalesPriceInput, out _) && TryAmount(BudgetedCostInput, out _);

    [RelayCommand(CanExecute = nameof(CanSaveSalesCalculation))]
    private async Task SaveSalesCalculationAsync()
    {
        if (!TryAmount(SalesPriceInput, out var sales) || !TryAmount(BudgetedCostInput, out var budget)) return;
        try
        {
            await _dataService.Projects.SaveSalesCalculationAsync(Project.Id, sales, budget);
            Project.SalesPrice = sales;
            Project.BudgetedCost = budget;
            RefreshMetrics();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message, "UmiaPP");
        }
    }

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

    public OverviewTabViewModel(Project project, IDataService dataService, IDialogService dialogService)
    {
        _dataService = dataService;
        _dialogService = dialogService;
        _project = project;
        LoadSalesInputs();
        RefreshMetrics();
    }

    public void UpdateProject(Project project)
    {
        Project = project;
        LoadSalesInputs();
        RefreshMetrics();
    }

    private void LoadSalesInputs()
    {
        SalesPriceInput = Project.SalesPrice?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
        BudgetedCostInput = Project.BudgetedCost?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
    }

    public void RefreshMetrics()
    {
        ProductionCost = null;
        CurrentTg = null;
        if (!string.IsNullOrWhiteSpace(Project.LubeSummaryJson))
        {
            try
            {
                var summary = JsonSerializer.Deserialize<LubeCalculationSummary>(Project.LubeSummaryJson);
                if (summary != null && (summary.ReportSections.Count > 0 || summary.MontageGrupper.Count > 0 || summary.LeveransGrupper.Count > 0))
                {
                    var cost = summary.TotalProduktionskostnad;
                    if (double.IsFinite(cost) && cost >= 0)
                    {
                        ProductionCost = (decimal)cost;
                        if (Project.SalesPrice is > 0)
                            CurrentTg = (1 - ProductionCost.Value / Project.SalesPrice.Value) * 100;
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException or OverflowException)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                ProductionCost = null;
                CurrentTg = null;
            }
        }
        DrawingCount = Project.Drawings?.Count ?? 0;
        TotalHours = Project.LubeKalkylPoster?.Sum(x => x.TotaltMontageTimmar) ?? 0;
        TotalMaterialCost = Project.LubeKalkylPoster?.Sum(x => x.TotaltMaterial) ?? 0;
        ComponentCount = (Project.BluebeamMarkups?.Count ?? 0) + (Project.LubeSakvarorMangder?.Count ?? 0);
    }
}
