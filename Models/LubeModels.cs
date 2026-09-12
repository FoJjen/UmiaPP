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
    public string Produktkod { get; set; } = string.Empty;
    public string Leverantor { get; set; } = string.Empty;
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

    [NotMapped]
    public double EffectiveOffertpris => Offertpris != 0 ? Offertpris : PrisNto;

    [NotMapped]
    public double EffectiveSlutpris => Slutpris != 0 ? Slutpris : EffectiveOffertpris;

    [NotMapped]
    public bool IsOffertprisFallback => Offertpris == 0;

    [NotMapped]
    public bool IsSlutprisFallback => Slutpris == 0;

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
    }

    partial void OnOffertprisChanged(double value) => NotifyEffectivePricesChanged();
    partial void OnSlutprisChanged(double value) => NotifyEffectivePricesChanged();
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
