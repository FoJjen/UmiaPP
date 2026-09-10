using UmiaPP.Models;

namespace UmiaPP.Data;

public interface IProjectRepository
{
    Task<List<Project>> GetAllAsync();
    Task<Project?> GetByIdAsync(int id);
    Task<Project> CreateAsync(Project project);
    Task<Project> UpdateAsync(Project project);
    Task<bool> DeleteAsync(int id);

    // Ritningar
    Task<List<Drawing>> GetDrawingsAsync(int projectId);
    Task<Drawing> AddDrawingAsync(Drawing drawing);
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
        List<LubeEjMangdbaserad> ejMangdbaserade);

    // Bluebeam markups
    Task<List<BluebeamMarkupItem>> GetBluebeamMarkupsAsync(int projectId);
    Task SaveBluebeamMarkupsAsync(int projectId, List<BluebeamMarkupItem> markups);
}
