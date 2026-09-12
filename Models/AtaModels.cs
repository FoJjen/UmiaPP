using System.Text.Json.Serialization;

namespace UmiaPP.Models;

public class AtaSummaryRow
{
    public string Number { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string Discipline { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal ReportedAmount { get; set; }
    public decimal ApprovedAmount { get; set; }
    public decimal InvoicedAmount { get; set; }
    public decimal WorkHours { get; set; }
    public decimal ManagementHours { get; set; }
    public decimal OtherHours { get; set; }
    public decimal MaterialAmount { get; set; }
    public string Remarks { get; set; } = string.Empty;
    public string Approval { get; set; } = string.Empty;

    [JsonIgnore]
    public decimal TotalHours => WorkHours + ManagementHours + OtherHours;

    public bool MatchesDiscipline(DisciplineType discipline)
    {
        if (string.IsNullOrWhiteSpace(Discipline)) return false;
        var code = Discipline.Trim().ToUpperInvariant();
        return discipline switch
        {
            DisciplineType.Ventilation => code is "VE" or "V" or "VENTILATION",
            DisciplineType.Piping      => code is "VS" or "VVS" or "RÖR" or "ROR" or "R",
            DisciplineType.Electrical  => code is "EL" or "E",
            DisciplineType.Control     => code is "SÖE" or "SOE" or "STYR" or "AUTOMATION",
            _ => false
        };
    }
}

public class AtaSummary
{
    public string FilePath { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectNumber { get; set; } = string.Empty;
    public string Business { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string RevisionDate { get; set; } = string.Empty;
    public string Customer { get; set; } = string.Empty;
    public string CustomerReference { get; set; } = string.Empty;
    public string ProjectLeader { get; set; } = string.Empty;
    public string ProjectManager { get; set; } = string.Empty;
    public List<AtaSummaryRow> Rows { get; set; } = [];

    [JsonIgnore]
    public AtaSummaryRow Totals => new()
    {
        Description = "Summor",
        ReportedAmount = Rows.Sum(r => r.ReportedAmount),
        ApprovedAmount = Rows.Sum(r => r.ApprovedAmount),
        InvoicedAmount = Rows.Sum(r => r.InvoicedAmount),
        WorkHours = Rows.Sum(r => r.WorkHours),
        ManagementHours = Rows.Sum(r => r.ManagementHours),
        OtherHours = Rows.Sum(r => r.OtherHours),
        MaterialAmount = Rows.Sum(r => r.MaterialAmount)
    };

    public AtaSummaryRow GetTotalsForDiscipline(DisciplineType discipline)
    {
        var filtered = Rows.Where(r => r.MatchesDiscipline(discipline)).ToList();
        return new AtaSummaryRow
        {
            Description = $"Summor ({GetDisciplineCode(discipline)})",
            ReportedAmount = filtered.Sum(r => r.ReportedAmount),
            ApprovedAmount = filtered.Sum(r => r.ApprovedAmount),
            InvoicedAmount = filtered.Sum(r => r.InvoicedAmount),
            WorkHours = filtered.Sum(r => r.WorkHours),
            ManagementHours = filtered.Sum(r => r.ManagementHours),
            OtherHours = filtered.Sum(r => r.OtherHours),
            MaterialAmount = filtered.Sum(r => r.MaterialAmount)
        };
    }

    public static string GetDisciplineCode(DisciplineType discipline) => discipline switch
    {
        DisciplineType.Ventilation => "VE",
        DisciplineType.Piping      => "VS",
        DisciplineType.Electrical  => "EL",
        DisciplineType.Control     => "SÖE",
        _ => discipline.ToString()
    };
}

public sealed class AtaOutcome
{
    public AtaSummaryRow Totals { get; }
    public decimal? BaseRevenue { get; }
    public decimal? BaseCost { get; }
    public decimal? Revenue => BaseRevenue + Totals.ApprovedAmount;
    public decimal? Cost => BaseCost;
    public decimal? Profit => Revenue - Cost;
    public decimal? Margin => Revenue is > 0 ? Profit / Revenue * 100 : null;

    public AtaOutcome(AtaSummary? summary, decimal? baseRevenue, decimal? baseCost, DisciplineType? discipline = null)
    {
        Totals = discipline.HasValue && summary != null
            ? summary.GetTotalsForDiscipline(discipline.Value)
            : summary?.Totals ?? new AtaSummaryRow();
        BaseRevenue = baseRevenue;
        BaseCost = baseCost;
    }
}
