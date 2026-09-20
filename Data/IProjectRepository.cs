using UmiaPP.Models;

namespace UmiaPP.Data;

public interface IProjectRepository
{
    Task<List<Project>> GetAllAsync();
    Task<Project?> GetByIdAsync(int id);
    Task<Project> CreateAsync(Project project);
    Task<Project> UpdateAsync(Project project);
    Task SaveSalesCalculationAsync(int projectId, decimal salesPrice, decimal budgetedCost);
    Task SaveAtaSummaryAsync(int projectId, string summaryJson);
    Task SaveAtaOwnCostAsync(int projectId, decimal? ownCost);
    Task<bool> DeleteAsync(int id);

    // Ritningar
    Task<List<Drawing>> GetDrawingsAsync(int projectId);
    Task<Drawing> AddDrawingAsync(Drawing drawing);
    Task SavePurchaseListsAsync(int projectId, List<Drawing> drawings);
    Task<bool> DeleteDrawingAsync(int drawingId);

    // Lube kalkyl
    Task SaveLubeCalculationDataAsync(
        int projectId,
        List<LubeKalkylPost> kalkyl,
        List<LubeProjektanpassningPost> anpassningar,
        List<LubeSakvaraMangd> sakvarorMangder,
        List<LubeSakvaraOffert> offerter,
        List<LubeSakvaraLeveranspris> leveranspriser,
        List<LubeSakvaraMontage> montage,
        List<LubeEjMangdbaserad> ejMangdbaserade,
        LubeCalculationSummary summary);

    Task UpdateSakvaraPricesAsync(LubeSakvaraMangd item);

    Task SaveSakvaraSpecificationRowAsync(LubeSakvaraMangd item);

    Task SavePackageQuoteAsync(LubeSakvaraOffert quote, IReadOnlyList<int> articleIds);

    Task DeletePackageQuoteAsync(int projectId, int quoteId);

    Task SetArticleQuoteLinksAsync(int projectId, IReadOnlyList<int> articleIds, int? quoteId);

    Task SaveLubeSummaryAsync(int projectId, string summaryJson);

    // Bluebeam markups
    Task<List<BluebeamMarkupItem>> GetBluebeamMarkupsAsync(int projectId);
    Task SaveBluebeamMarkupsAsync(int projectId, List<BluebeamMarkupItem> markups);

    Task<List<InspectionPointTemplate>> GetInspectionTemplatesAsync();
    Task<InspectionPointTemplate> SaveInspectionTemplateAsync(InspectionPointTemplate template);
    Task DeleteInspectionTemplateAsync(int templateId);
    Task<List<DrawingInspectionItem>> GetDrawingInspectionItemsAsync(int drawingId);
    Task SaveDrawingInspectionItemsAsync(int drawingId, List<DrawingInspectionItem> items);
    Task<DrawingInspection> GetDrawingInspectionAsync(int drawingId);
    Task SaveDrawingInspectionAsync(DrawingInspection inspection);
}
