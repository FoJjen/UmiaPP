using Microsoft.EntityFrameworkCore;
using UmiaPP.Models;

namespace UmiaPP.Data;

public class ProjectRepository : IProjectRepository
{
    private readonly Func<AppDbContext> _contextFactory;

    public ProjectRepository(Func<AppDbContext>? contextFactory = null)
    {
        _contextFactory = contextFactory ?? (() => new AppDbContext());
    }

    public async Task<List<Project>> GetAllAsync()
    {
        await using var context = _contextFactory();
        return await context.Projects
            .OrderByDescending(p => p.CreatedDate)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Project?> GetByIdAsync(int id)
    {
        await using var context = _contextFactory();
        return await context.Projects
            .Include(p => p.Drawings)
            .Include(p => p.LubeKalkylPoster)
            .Include(p => p.LubeProjektanpassningar)
            .Include(p => p.LubeSakvarorMangder)
            .Include(p => p.LubeSakvarorOfferter)
            .Include(p => p.LubeSakvarorLeveranspriser)
            .Include(p => p.LubeSakvarorMontagePoster)
            .Include(p => p.LubeEjMangdbaseradePoster)
            .Include(p => p.BluebeamMarkups)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Project> CreateAsync(Project project)
    {
        await using var context = _contextFactory();
        context.Projects.Add(project);
        await context.SaveChangesAsync();
        return project;
    }

    public async Task<Project> UpdateAsync(Project project)
    {
        await using var context = _contextFactory();
        var existing = await context.Projects.FirstOrDefaultAsync(p => p.Id == project.Id);
        if (existing == null)
            throw new InvalidOperationException($"Projektet med ID {project.Id} hittades inte.");

        existing.Name = project.Name;
        existing.ProjectNumber = project.ProjectNumber;
        existing.Client = project.Client;
        existing.ContractType = project.ContractType;
        existing.Status = project.Status;
        existing.Discipline = project.Discipline;
        existing.Description = project.Description;

        await context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var context = _contextFactory();
        var project = await context.Projects.FindAsync(id);
        if (project == null) return false;

        context.Projects.Remove(project);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<List<Drawing>> GetDrawingsAsync(int projectId)
    {
        await using var context = _contextFactory();
        return await context.Drawings
            .Where(d => d.ProjectId == projectId)
            .OrderBy(d => d.DrawingNumber)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Drawing> AddDrawingAsync(Drawing drawing)
    {
        await using var context = _contextFactory();
        context.Drawings.Add(drawing);
        await context.SaveChangesAsync();
        return drawing;
    }

    public async Task<bool> DeleteDrawingAsync(int drawingId)
    {
        await using var context = _contextFactory();
        var d = await context.Drawings.FindAsync(drawingId);
        if (d == null) return false;

        context.Drawings.Remove(d);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task SaveLubeCalculationDataAsync(
        int projectId,
        List<LubeKalkylPost> kalkyl,
        List<LubeProjektanpassningPost> anpassningar,
        List<LubeSakvaraMangd> sakvarorMangder,
        List<LubeSakvaraOffert> offerter,
        List<LubeSakvaraLeveranspris> leveranspriser,
        List<LubeSakvaraMontage> montage,
        List<LubeEjMangdbaserad> ejMangdbaserade)
    {
        await using var context = _contextFactory();

        // Rensa gamla kalkylrader för projektet
        var oldKalkyl = await context.LubeKalkylPoster.Where(x => x.ProjectId == projectId).ToListAsync();
        context.LubeKalkylPoster.RemoveRange(oldKalkyl);

        var oldAnpassning = await context.LubeProjektanpassningar.Where(x => x.ProjectId == projectId).ToListAsync();
        context.LubeProjektanpassningar.RemoveRange(oldAnpassning);

        var oldMangder = await context.LubeSakvarorMangder.Where(x => x.ProjectId == projectId).ToListAsync();
        context.LubeSakvarorMangder.RemoveRange(oldMangder);

        var oldOfferter = await context.LubeSakvarorOfferter.Where(x => x.ProjectId == projectId).ToListAsync();
        context.LubeSakvarorOfferter.RemoveRange(oldOfferter);

        var oldLeverans = await context.LubeSakvarorLeveranspriser.Where(x => x.ProjectId == projectId).ToListAsync();
        context.LubeSakvarorLeveranspriser.RemoveRange(oldLeverans);

        var oldMontage = await context.LubeSakvarorMontagePoster.Where(x => x.ProjectId == projectId).ToListAsync();
        context.LubeSakvarorMontagePoster.RemoveRange(oldMontage);

        var oldEjMangd = await context.LubeEjMangdbaseradePoster.Where(x => x.ProjectId == projectId).ToListAsync();
        context.LubeEjMangdbaseradePoster.RemoveRange(oldEjMangd);

        // Koppla nya rader
        kalkyl.ForEach(x => x.ProjectId = projectId);
        anpassningar.ForEach(x => x.ProjectId = projectId);
        sakvarorMangder.ForEach(x => x.ProjectId = projectId);
        offerter.ForEach(x => x.ProjectId = projectId);
        leveranspriser.ForEach(x => x.ProjectId = projectId);
        montage.ForEach(x => x.ProjectId = projectId);
        ejMangdbaserade.ForEach(x => x.ProjectId = projectId);

        await context.LubeKalkylPoster.AddRangeAsync(kalkyl);
        await context.LubeProjektanpassningar.AddRangeAsync(anpassningar);
        await context.LubeSakvarorMangder.AddRangeAsync(sakvarorMangder);
        await context.LubeSakvarorOfferter.AddRangeAsync(offerter);
        await context.LubeSakvarorLeveranspriser.AddRangeAsync(leveranspriser);
        await context.LubeSakvarorMontagePoster.AddRangeAsync(montage);
        await context.LubeEjMangdbaseradePoster.AddRangeAsync(ejMangdbaserade);

        await context.SaveChangesAsync();
    }

    public async Task<List<BluebeamMarkupItem>> GetBluebeamMarkupsAsync(int projectId)
    {
        await using var context = _contextFactory();
        return await context.BluebeamMarkups
            .Where(b => b.ProjectId == projectId)
            .OrderBy(b => b.Category)
            .ThenBy(b => b.Subject)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task SaveBluebeamMarkupsAsync(int projectId, List<BluebeamMarkupItem> markups)
    {
        await using var context = _contextFactory();

        var existing = await context.BluebeamMarkups.Where(b => b.ProjectId == projectId).ToListAsync();
        context.BluebeamMarkups.RemoveRange(existing);

        markups.ForEach(m => m.ProjectId = projectId);
        await context.BluebeamMarkups.AddRangeAsync(markups);

        await context.SaveChangesAsync();
    }
}
