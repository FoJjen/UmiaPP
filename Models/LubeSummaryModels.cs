using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UmiaPP.Models;

public class LubeSummaryItem
{
    public string Produktgrupp { get; set; } = string.Empty;
    public string Lista { get; set; } = string.Empty;
    public double Timmar { get; set; }
    public double PrisBto { get; set; }
    public double RabattSats { get; set; }
    public double PrisNto { get; set; }
}

public class LubeCalculationSummary : ObservableObject
{
    public List<LubeReportSection> ReportSections { get; set; } = [];
    public Dictionary<string, double> ImportedAmounts { get; set; } = [];

    // MONTAGE
    public List<LubeSummaryItem> MontageGrupper { get; set; } = [];
    public double SummaForeJusteringTimmar { get; set; }
    public double ProjektanpassningTimmar { get; set; }
    public double SummaEfterJusteringTimmar { get; set; }
    public double MontageTimlon { get; set; }
    public double SummaEfterJusteringKrNto => Amount(nameof(SummaEfterJusteringKrNto), SummaEfterJusteringTimmar * MontageTimlon);

    public double RestidTimmar { get; set; }
    public double RestidTimlon { get; set; }
    public double RestidKrNto => Amount(nameof(RestidKrNto), RestidTimmar * RestidTimlon);

    public double SocialaAvgifterPct { get; set; }
    public double SocialaAvgifterKr => Amount(nameof(SocialaAvgifterKr), 0);
    public double ReskostnadKm { get; set; }
    public double ReskostnadKrPerKm { get; set; }
    public double ReskostnadKrNto => Amount(nameof(ReskostnadKrNto), ReskostnadKm * ReskostnadKrPerKm);

    public double SummaMontageTotal => Amount(nameof(SummaMontageTotal), SummaEfterJusteringKrNto + RestidKrNto + SocialaAvgifterKr + ReskostnadKrNto);

    // INJUSTERING MED MERA
    public string InjusteringLista { get; set; } = string.Empty;
    public double InjusteringTimmar { get; set; }
    public double InjusteringTimlon { get; set; }
    public double InjusteringKrNto => Amount(nameof(InjusteringKrNto), InjusteringTimmar * InjusteringTimlon);
    public double SummaInjusteringTotal => Amount(nameof(SummaInjusteringTotal), InjusteringKrNto);

    // LEVERANSER
    public List<LubeSummaryItem> LeveransGrupper { get; set; } = [];
    public double LeveransSummaForeJustering { get; set; }
    public double FrakterPct { get; set; }
    public double FrakterKr => Amount(nameof(FrakterKr), LeveransSummaForeJustering * (FrakterPct / 100.0));
    public double MontagematerialPct { get; set; }
    public double MontagematerialKr => Amount(nameof(MontagematerialKr), LeveransSummaForeJustering * (MontagematerialPct / 100.0));
    public double SummaLeveransTotal => Amount(nameof(SummaLeveransTotal), LeveransSummaForeJustering + FrakterKr + MontagematerialKr);

    // UNDERENTREPRENAD
    public double SummaUnderentreprenad { get; set; }

    // TOTAL PRODUKTIONSKOSTNAD
    public double OvrigtKr { get; set; }
    public double OvrigtTbPct { get; set; }
    public double MontageOchInjusteringKr => Amount(nameof(MontageOchInjusteringKr), SummaMontageTotal + SummaInjusteringTotal);
    public double TotalProduktionskostnad => Amount(nameof(TotalProduktionskostnad), OvrigtKr + MontageOchInjusteringKr + SummaLeveransTotal + SummaUnderentreprenad);

    private double Amount(string name, double fallback) => ImportedAmounts.TryGetValue(name, out var value) ? value : fallback;

    public event EventHandler? ActualCostsChanged;

    [JsonIgnore]
    public double ActualProductionCost => TotalProduktionskostnad + Delta("montage", "injustering", "delivery", "deliveryExtra", "subcontract", "other");

    public void InitializeActualCosts()
    {
        foreach (var section in ReportSections)
        {
            var title = Normalize(section.Title);
            var mode = title == "MONTAGE" ? "montage" : title == "LEVERANSER" ? "delivery"
                : section.Rows.Any(r => Normalize(r.Label).StartsWith("INJUSTERING MED")) ? "injustering"
                : section.Rows.Any(r => Normalize(r.Label) == "TOTAL PRODUKTIONSKOSTNAD") ? "total" : "subcontract";
            foreach (var row in section.Rows)
            {
                row.PropertyChanged -= OnActualCostChanged;
                row.CostGroup = "";
                row.TotalGroups = [];
                row.CanEditActualCost = false;
                var key = Normalize(row.Label);
                if (row.IsHeader) continue;
                if (!row.NetAmount.HasValue && double.TryParse(row.Net, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
                    row.NetAmount = amount;

                if (mode == "montage" && key.StartsWith("SUMMA EFTER"))
                {
                    row.NetAmount ??= SummaEfterJusteringKrNto;
                    row.CostGroup = "montage";
                }
                else if (key.StartsWith("SUMMA MONTAGE")) row.TotalGroups = ["montage"];
                else if (mode == "injustering" && row.IsTotal) row.TotalGroups = ["injustering"];
                else if (mode == "delivery" && key.StartsWith("SUMMA FÖRE")) row.TotalGroups = ["delivery"];
                else if (key.StartsWith("SUMMA LEVERANS")) row.TotalGroups = ["delivery", "deliveryExtra"];
                else if (key.StartsWith("SUMMA UNDERENTREPRENAD")) row.TotalGroups = ["subcontract"];
                else if (mode == "total")
                {
                    if (key == "ÖVRIGT") row.CostGroup = "other";
                    else if (key.StartsWith("MONTAGE OCH INJUSTERING")) row.TotalGroups = ["montage", "injustering"];
                    else if (key == "LEVERANS") row.TotalGroups = ["delivery", "deliveryExtra"];
                    else if (key == "UNDERENTREPRENADER") row.TotalGroups = ["subcontract"];
                    else if (key == "TOTAL PRODUKTIONSKOSTNAD") row.TotalGroups = ["montage", "injustering", "delivery", "deliveryExtra", "subcontract", "other"];
                }
                else if (!row.IsTotal && row.NetAmount.HasValue)
                    row.CostGroup = mode == "delivery" && key is "FRAKTER" or "MONTAGEMATERIAL" ? "deliveryExtra" : mode;

                row.CanEditActualCost = row.CostGroup.Length > 0 && row.NetAmount.HasValue;
                row.PropertyChanged += OnActualCostChanged;
            }
        }
        RecalculateActualCosts();
    }

    public void PreserveActualCosts(LubeCalculationSummary previous)
    {
        // Match section, label and price list; queues keep repeated detail rows distinct.
        var saved = previous.ReportSections.SelectMany(s => s.Rows.Select(r => (Key: (Normalize(s.Title), Normalize(r.Label), r.List), Row: r)))
            .GroupBy(x => x.Key).ToDictionary(g => g.Key, g => new Queue<double?>(g.Select(x => x.Row.ActualCost)));
        foreach (var section in ReportSections)
            foreach (var row in section.Rows)
                if (saved.TryGetValue((Normalize(section.Title), Normalize(row.Label), row.List), out var values) && values.Count > 0)
                    row.ActualCost = values.Dequeue();
    }

    private static string Normalize(string value) => string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    private double Delta(params string[] groups) => ReportSections.SelectMany(s => s.Rows)
        .Where(r => r.CanEditActualCost && groups.Contains(r.CostGroup))
        .Sum(r => r.ActualCost.HasValue ? r.ActualCost.Value - r.NetAmount.GetValueOrDefault() : 0);

    private void OnActualCostChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LubeReportRow.ActualCost)) return;
        RecalculateActualCosts();
        ActualCostsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RecalculateActualCosts()
    {
        foreach (var row in ReportSections.SelectMany(s => s.Rows))
            row.ActualTotal = row.TotalGroups.Length > 0
                ? row.NetAmount.GetValueOrDefault() + Delta(row.TotalGroups)
                : row.CanEditActualCost ? row.ActualCost ?? row.NetAmount : null;
        OnPropertyChanged(nameof(ActualProductionCost));
    }
}

public class LubeReportSection
{
    public string Title { get; set; } = string.Empty;
    public List<LubeReportRow> Rows { get; set; } = [];

    [JsonIgnore]
    public bool CanCollapse => Title.Trim().Equals("MONTAGE", StringComparison.OrdinalIgnoreCase)
        || Title.Trim().Equals("LEVERANSER", StringComparison.OrdinalIgnoreCase);
}

public class LubeReportRow : ObservableObject
{
    public string Label { get; set; } = string.Empty;
    public string List { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
    public string Rate { get; set; } = string.Empty;
    public string Net { get; set; } = string.Empty;
    public bool IsHeader { get; set; }
    public bool IsTotal { get; set; }
    public bool IsInput { get; set; }
    public bool HasList { get; set; }
    public double? NetAmount { get; set; }

    private double? _actualCost;
    public double? ActualCost
    {
        get => _actualCost;
        set
        {
            if (value.HasValue && !double.IsFinite(value.Value))
                throw new ArgumentException("Ange ett giltigt belopp.");
            SetProperty(ref _actualCost, value);
        }
    }

    [JsonIgnore]
    public bool CanEditActualCost { get; set; }

    private double? _actualTotal;
    [JsonIgnore]
    public double? ActualTotal
    {
        get => _actualTotal;
        internal set => SetProperty(ref _actualTotal, value);
    }

    internal string CostGroup { get; set; } = "";
    internal string[] TotalGroups { get; set; } = [];
}
