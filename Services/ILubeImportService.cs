using UmiaPP.Models;

namespace UmiaPP.Services;

public class LubeImportResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ProjektNummer { get; set; }
    public string? PdelRitning { get; set; }
    public string? Handling { get; set; }
    public string? Datum { get; set; }
    public LubeCalculationSummary Summary { get; set; } = new();

    public List<LubeKalkylPost> KalkylPoster { get; set; } = [];
    public List<LubeProjektanpassningPost> Anpassningar { get; set; } = [];
    public List<LubeSakvaraMangd> SakvarorMangder { get; set; } = [];
    public List<LubeSakvaraOffert> SakvarorOfferter { get; set; } = [];
    public List<LubeSakvaraLeveranspris> SakvarorLeveranspriser { get; set; } = [];
    public List<LubeSakvaraMontage> SakvarorMontagePoster { get; set; } = [];
    public List<LubeEjMangdbaserad> EjMangdbaseradePoster { get; set; } = [];

    public double TotalMaterialKostnad => Summary.SummaLeveransTotal > 0 ? Summary.SummaLeveransTotal : KalkylPoster.Sum(x => x.TotaltMaterial);
    public double TotalMontageTimmar => Summary.SummaEfterJusteringTimmar > 0 ? Summary.SummaEfterJusteringTimmar : KalkylPoster.Sum(x => x.TotaltMontageTimmar);
}

public interface ILubeImportService
{
    Task<LubeImportResult> ImportAsync(string filePath);
}
