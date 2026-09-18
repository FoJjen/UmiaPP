using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using UmiaPP.Data;
using UmiaPP.Models;
using UmiaPP.Services;

namespace UmiaPP.ViewModels;

public partial class OverviewTabViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly IDialogService _dialogService;

    private Func<AtaOutcome>? _outcomeProvider;
    private Func<(double Hours, double Quantity, int Drawings)>? _cardMetricsProvider;

    public void SetCardMetricsProvider(Func<(double Hours, double Quantity, int Drawings)> provider)
    {
        _cardMetricsProvider = provider;
        RefreshCardMetrics();
    }

    public void RefreshCardMetrics()
    {
        if (_cardMetricsProvider == null) return;
        var metrics = _cardMetricsProvider();
        TotalHours = metrics.Hours;
        ComponentCount = metrics.Quantity;
        DrawingCount = metrics.Drawings;
    }

    public event EventHandler? SalesCalculationChanged;

    [ObservableProperty]
    private decimal? _currentRevenue;

    [ObservableProperty]
    private decimal? _currentProfit;

    public void SetOutcomeProvider(Func<AtaOutcome> provider)
    {
        _outcomeProvider = provider;
        RefreshCurrentOutcome();
    }

    public void RefreshCurrentOutcome()
    {
        if (_outcomeProvider == null) return;
        var outcome = _outcomeProvider();
        CurrentRevenue = outcome.Revenue;
        ProductionCost = outcome.Cost;
        CurrentProfit = outcome.Profit;
        CurrentTg = outcome.Margin;
    }

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
            SalesCalculationChanged?.Invoke(this, EventArgs.Empty);
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
    private double _componentCount;

    public string DeliveryMark => DeliveryMarking.Format(Project.ProjectNumber, Project.Discipline);
    public string DeliveryDiscipline => DeliveryMarking.ShortName(Project.Discipline);
    public string DeliveryInfoText =>
        $"Märke: {DeliveryMark} ({DeliveryDiscipline})\n" +
        $"Leveransadress: {Project.DeliveryAddress}\n" +
        $"Kontaktperson: {Project.ContactPerson}\n" +
        $"Fakturauppgift: {Project.InvoiceDetails}";

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
        OnPropertyChanged(nameof(DeliveryMark));
        OnPropertyChanged(nameof(DeliveryDiscipline));
        OnPropertyChanged(nameof(DeliveryInfoText));
    }

    [RelayCommand]
    private void CopyDeliveryInfo()
    {
        Clipboard.SetText(DeliveryInfoText);
        _dialogService.ShowMessage("Leveransinformationen har kopierats.", "Översikt");
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
        DrawingCount = Project.Drawings?.Count(drawing => !drawing.IsInspectionList) ?? 0;
        TotalHours = Project.LubeKalkylPoster?.Sum(x => x.TotaltMontageTimmar) ?? 0;
        TotalMaterialCost = Project.LubeKalkylPoster?.Sum(x => x.TotaltMaterial) ?? 0;
        ComponentCount = (Project.BluebeamMarkups?.Count ?? 0) + (Project.LubeSakvarorMangder?.Count ?? 0);
        RefreshCardMetrics();
        RefreshCurrentOutcome();
    }
}
