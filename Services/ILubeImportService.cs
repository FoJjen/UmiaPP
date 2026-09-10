using UmiaPP.Models;

namespace UmiaPP.Services;

public class LubeImportResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<LubeKalkylPost> KalkylPoster { get; set; } = [];
    public List<LubeProjektanpassningPost> Anpassningar { get; set; } = [];
    public List<LubeSakvaraMangd> SakvarorMangder { get; set; } = [];
    public List<LubeSakvaraOffert> SakvarorOfferter { get; set; } = [];
    public List<LubeSakvaraLeveranspris> SakvarorLeveranspriser { get; set; } = [];
    public List<LubeSakvaraMontage> SakvarorMontagePoster { get; set; } = [];
    public List<LubeEjMangdbaserad> EjMangdbaseradePoster { get; set; } = [];

    public double TotalMaterialKostnad => KalkylPoster.Sum(x => x.TotaltMaterial);
    public double TotalMontageTimmar => KalkylPoster.Sum(x => x.TotaltMontageTimmar);
}

public interface ILubeImportService
{
    Task<LubeImportResult> ImportAsync(string filePath);
}
