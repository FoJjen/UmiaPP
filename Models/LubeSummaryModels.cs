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

public class LubeCalculationSummary
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
}

public class LubeReportSection
{
    public string Title { get; set; } = string.Empty;
    public List<LubeReportRow> Rows { get; set; } = [];
}

public class LubeReportRow
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
}
