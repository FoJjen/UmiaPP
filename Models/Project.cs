using System.Collections.ObjectModel;

namespace UmiaPP.Models;

public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProjectNumber { get; set; } = string.Empty;
    public string Client { get; set; } = string.Empty; // Beställare
    public ContractType ContractType { get; set; } = ContractType.Totalentreprenad;
    public ProjectStatus Status { get; set; } = ProjectStatus.Kalkyl;
    public DisciplineType Discipline { get; set; } = DisciplineType.Ventilation;
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public string? Description { get; set; }
    public string? LubeSummaryJson { get; set; }
    public string? AtaSummaryJson { get; set; }
    public decimal? AtaOwnCost { get; set; }
    public decimal? SalesPrice { get; set; }
    public decimal? BudgetedCost { get; set; }

    // Navigation properties
    public List<Drawing> Drawings { get; set; } = [];
    public List<LubeKalkylPost> LubeKalkylPoster { get; set; } = [];
    public List<LubeProjektanpassningPost> LubeProjektanpassningar { get; set; } = [];
    public List<LubeSakvaraMangd> LubeSakvarorMangder { get; set; } = [];
    public List<LubeSakvaraOffert> LubeSakvarorOfferter { get; set; } = [];
    public List<LubeSakvaraLeveranspris> LubeSakvarorLeveranspriser { get; set; } = [];
    public List<LubeSakvaraMontage> LubeSakvarorMontagePoster { get; set; } = [];
    public List<LubeEjMangdbaserad> LubeEjMangdbaseradePoster { get; set; } = [];
    public List<BluebeamMarkupItem> BluebeamMarkups { get; set; } = [];
}
