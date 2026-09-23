using System.ComponentModel.DataAnnotations.Schema;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UmiaPP.Models;

public class LubeKalkylPost
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Benamning { get; set; } = string.Empty;
    public string Enhet { get; set; } = string.Empty;
    public double Mangd { get; set; }
    public double MaterialKostnad { get; set; }
    public double MontageTimmar { get; set; }
    public double TotaltMaterial { get; set; }
    public double TotaltMontageTimmar { get; set; }

    public Project? Project { get; set; }
}

public class LubeProjektanpassningPost
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Parameter { get; set; } = string.Empty;
    public string Beskrivning { get; set; } = string.Empty;
    public double Varde { get; set; }
    public string Enhet { get; set; } = string.Empty; // %, timmar, kr

    public Project? Project { get; set; }
}

public partial class LubeSakvaraMangd : ObservableObject
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Beteckning { get; set; } = string.Empty;
    public string Dim1 { get; set; } = string.Empty;
    public string Dim2 { get; set; } = string.Empty;
    private double _antal;
    public double Antal
    {
        get => _antal;
        set => SetProperty(ref _antal, value);
    }
    public string Enhet { get; set; } = "st";

    public string Grupp { get; set; } = string.Empty;
    public string Produkt { get; set; } = string.Empty;
    private string _produktkod = string.Empty;
    public string Produktkod
    {
        get => _produktkod;
        set { if (SetProperty(ref _produktkod, value)) NotifyArticleIdentityChanged(); }
    }
    private string _leverantor = string.Empty;
    public string Leverantor
    {
        get => _leverantor;
        set { if (SetProperty(ref _leverantor, value)) NotifyArticleIdentityChanged(); }
    }

    public bool HasOriginalArticleIdentity { get; set; }
    public bool IsUserAdded { get; set; }
    public string OriginalProduktkod { get; set; } = string.Empty;
    public string OriginalLeverantor { get; set; } = string.Empty;

    [NotMapped]
    public bool CanEditAllFields => IsUserAdded && !IsQuoteSummaryRow;

    [NotMapped]
    public bool IsProduktkodChanged => HasOriginalArticleIdentity && Produktkod != OriginalProduktkod;
    [NotMapped]
    public bool IsLeverantorChanged => HasOriginalArticleIdentity && Leverantor != OriginalLeverantor;

    public void NotifyArticleIdentityChanged()
    {
        OnPropertyChanged(nameof(IsProduktkodChanged));
        OnPropertyChanged(nameof(IsLeverantorChanged));
        OnPropertyChanged(nameof(OriginalProduktkod));
        OnPropertyChanged(nameof(OriginalLeverantor));
    }

    public double PrisBto { get; set; }
    public double RabattSats { get; set; }
    private double _prisNto;
    public double PrisNto
    {
        get => _prisNto;
        set
        {
            if (SetProperty(ref _prisNto, value)) NotifyEffectivePricesChanged();
        }
    }

    [ObservableProperty]
    private double _offertpris;

    [ObservableProperty]
    private double _slutpris;

    [ObservableProperty]
    private bool _arKopt;

    [ObservableProperty]
    private string _anteckningar = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPackageQuote))]
    [NotifyPropertyChangedFor(nameof(PackageQuoteLabel))]
    [NotifyPropertyChangedFor(nameof(EffectiveOffertpris))]
    [NotifyPropertyChangedFor(nameof(EffectiveSlutpris))]
    [NotifyPropertyChangedFor(nameof(IsOffertprisFallback))]
    [NotifyPropertyChangedFor(nameof(IsSlutprisFallback))]
    [NotifyPropertyChangedFor(nameof(Differens))]
    private int? _packageQuoteId;

    [NotMapped]
    public bool HasPackageQuote => PackageQuoteId is > 0;

    [NotMapped]
    public string PackageQuoteLabel { get; set; } = string.Empty;

    // Display-only rows must never be persisted as articles or included in article totals.
    [NotMapped]
    public bool IsQuoteSummaryRow { get; init; }

    // Separate from PackageQuoteId: a summary displays its own amount rather
    // than behaving like an article whose price is included in a package.
    [NotMapped]
    public int? SummaryQuoteId { get; init; }

    [NotMapped]
    public double EffectiveOffertpris => IsQuoteSummaryRow ? Offertpris
        : HasPackageQuote ? 0 : Offertpris != 0 ? Offertpris : PrisNto;

    [NotMapped]
    public double EffectiveSlutpris => IsQuoteSummaryRow ? Slutpris
        : HasPackageQuote ? 0 : Slutpris != 0 ? Slutpris : EffectiveOffertpris;

    [NotMapped]
    public bool IsOffertprisFallback => !IsQuoteSummaryRow && !HasPackageQuote && Offertpris == 0;

    [NotMapped]
    public bool IsSlutprisFallback => !IsQuoteSummaryRow && !HasPackageQuote && Slutpris == 0;

    [NotMapped]
    public double Differens => EffectiveSlutpris - EffectiveOffertpris;

    public Project? Project { get; set; }

    private void NotifyEffectivePricesChanged()
    {
        OnPropertyChanged(nameof(EffectiveOffertpris));
        OnPropertyChanged(nameof(EffectiveSlutpris));
        OnPropertyChanged(nameof(IsOffertprisFallback));
        OnPropertyChanged(nameof(IsSlutprisFallback));
        OnPropertyChanged(nameof(Differens));
        OnPropertyChanged(nameof(HasPackageQuote));
        OnPropertyChanged(nameof(PackageQuoteLabel));
    }

    partial void OnOffertprisChanged(double value) => NotifyEffectivePricesChanged();
    partial void OnSlutprisChanged(double value) => NotifyEffectivePricesChanged();

    public void NotifyQuoteChanged() => NotifyEffectivePricesChanged();
}

public class LubeSakvaraOffert
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Leverantor { get; set; } = string.Empty;
    public string Produktgrupp { get; set; } = string.Empty;
    public double OffereratPris { get; set; }
    public string Valuta { get; set; } = "SEK";
    public string? Kommentar { get; set; }
    public DateTime QuoteDate { get; set; } = DateTime.Today;
    public double FinalAmount { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public bool IsPackageQuote { get; set; }

    [NotMapped]
    public double EffectiveAmount => FinalAmount != 0 ? FinalAmount : OffereratPris;

    public Project? Project { get; set; }
}

public class LubeSakvaraLeveranspris
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Produkt { get; set; } = string.Empty;
    public string Leverantor { get; set; } = string.Empty;
    public double BruttoPris { get; set; }
    public double RabattSats { get; set; }
    public double NettoPris { get; set; }

    public Project? Project { get; set; }
}

public class LubeSakvaraMontage
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string ProduktKategori { get; set; } = string.Empty; // Kanalfläktar, galler, etc.
    public string Beteckning { get; set; } = string.Empty;
    public double MontageTimmarPerEnhet { get; set; }
    public double InjusteringTimmarPerEnhet { get; set; }
    public double Antal { get; set; }
    public double SummaMontageTimmar => MontageTimmarPerEnhet * Antal;
    public double SummaInjusteringTimmar => InjusteringTimmarPerEnhet * Antal;

    public Project? Project { get; set; }
}

public class LubeEjMangdbaserad
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Moment { get; set; } = string.Empty; // OVK, CE-märkning, täthetsprovning etc.
    public double Timmar { get; set; }
    public double Kostnad { get; set; }
    public string? Kommentar { get; set; }

    public Project? Project { get; set; }
}
