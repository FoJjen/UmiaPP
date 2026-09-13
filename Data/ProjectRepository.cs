using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using UmiaPP.Models;

namespace UmiaPP.Data;

public class ProjectRepository : IProjectRepository
{
    private readonly Func<AppDbContext> _contextFactory;

    public async Task SaveSakvaraSpecificationRowAsync(LubeSakvaraMangd item)
    {
        await using var context = _contextFactory();
        var rows = context.Set<LubeSakvaraMangd>();
        LubeSakvaraMangd saved;
        if (item.Id == 0)
        {
            saved = new LubeSakvaraMangd { ProjectId = item.ProjectId };
            rows.Add(saved);
        }
        else
        {
            saved = await rows.SingleOrDefaultAsync(x => x.Id == item.Id && x.ProjectId == item.ProjectId)
                ?? throw new InvalidOperationException("Sakvaruraden finns inte längre i projektet.");
        }

        saved.Beteckning = item.Beteckning;
        saved.Dim1 = item.Dim1;
        saved.Dim2 = item.Dim2;
        saved.Antal = item.Antal;
        saved.Enhet = item.Enhet;
        saved.Grupp = item.Grupp;
        saved.Produkt = item.Produkt;
        saved.Produktkod = item.Produktkod;
        saved.Leverantor = item.Leverantor;
        saved.PrisBto = item.PrisBto;
        saved.RabattSats = item.RabattSats;
        saved.PrisNto = item.PrisNto;
        saved.Offertpris = item.Offertpris;
        saved.Slutpris = item.Slutpris;
        saved.ArKopt = item.ArKopt;
        saved.Anteckningar = item.Anteckningar;
        await context.SaveChangesAsync();
        item.Id = saved.Id;
    }

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
            .AsSplitQuery()
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
        existing.SalesPrice = project.SalesPrice;

        await context.SaveChangesAsync();
        return existing;
    }

    public async Task SaveSalesCalculationAsync(int projectId, decimal salesPrice, decimal budgetedCost)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(salesPrice);
        ArgumentOutOfRangeException.ThrowIfNegative(budgetedCost);
        await using var context = _contextFactory();
        var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == projectId)
            ?? throw new InvalidOperationException($"Projektet med ID {projectId} hittades inte.");
        project.SalesPrice = salesPrice;
        project.BudgetedCost = budgetedCost;
        await context.SaveChangesAsync();
    }

    public async Task SaveAtaSummaryAsync(int projectId, string summaryJson)
    {
        await using var context = _contextFactory();
        var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == projectId)
            ?? throw new InvalidOperationException($"Projektet med ID {projectId} hittades inte.");
        project.AtaSummaryJson = summaryJson;
        await context.SaveChangesAsync();
    }

    public async Task SaveAtaOwnCostAsync(int projectId, decimal? ownCost)
    {
        await using var context = _contextFactory();
        var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == projectId)
            ?? throw new InvalidOperationException($"Projektet med ID {projectId} hittades inte.");
        project.AtaOwnCost = ownCost;
        await context.SaveChangesAsync();
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

    public async Task SavePurchaseListsAsync(int projectId, List<Drawing> drawings)
    {
        await using var context = _contextFactory();
        if (!await context.Projects.AnyAsync(p => p.Id == projectId))
            throw new InvalidOperationException("Projektet finns inte längre.");
        var saved = new List<(Drawing Input, Drawing Stored)>();
        foreach (var input in drawings)
        {
            if (input.ProjectId != projectId)
                throw new InvalidOperationException("Ritningen tillhör ett annat projekt.");
            Drawing stored;
            if (input.Id == 0)
            {
                stored = new Drawing { ProjectId = projectId };
                context.Drawings.Add(stored);
            }
            else
            {
                stored = await context.Drawings.SingleOrDefaultAsync(d => d.Id == input.Id && d.ProjectId == projectId)
                    ?? throw new InvalidOperationException("Ritningen finns inte längre i projektet.");
            }
            stored.DrawingNumber = input.DrawingNumber;
            stored.SheetTitle = input.SheetTitle;
            stored.Revision = input.Revision;
            stored.Date = input.Date;
            stored.FilePath = input.FilePath;
            stored.Notes = input.Notes;
            stored.PurchaseItemsJson = input.PurchaseItemsJson;
            stored.PurchaseImportSource = input.PurchaseImportSource;
            saved.Add((input, stored));
        }
        // One SaveChanges transaction: a rejected import cannot leave half the lists replaced.
        await context.SaveChangesAsync();
        foreach (var pair in saved) pair.Input.Id = pair.Stored.Id;
    }

    public async Task SaveLubeCalculationDataAsync(
        int projectId,
        List<LubeKalkylPost> kalkyl,
        List<LubeProjektanpassningPost> anpassningar,
        List<LubeSakvaraMangd> sakvarorMangder,
        List<LubeSakvaraOffert> offerter,
        List<LubeSakvaraLeveranspris> leveranspriser,
        List<LubeSakvaraMontage> montage,
        List<LubeEjMangdbaserad> ejMangdbaserade,
        LubeCalculationSummary summary)
    {
        await using var context = _contextFactory();
        var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == projectId)
            ?? throw new InvalidOperationException($"Projektet med ID {projectId} hittades inte.");
        if (!string.IsNullOrWhiteSpace(project.LubeSummaryJson))
        {
            var previous = JsonSerializer.Deserialize<LubeCalculationSummary>(project.LubeSummaryJson);
            if (previous != null)
                summary.PreserveActualCosts(previous);
        }
        project.LubeSummaryJson = JsonSerializer.Serialize(summary);

        // Rensa gamla kalkylrader för projektet
        var oldKalkyl = await context.LubeKalkylPoster.Where(x => x.ProjectId == projectId).ToListAsync();
        context.LubeKalkylPoster.RemoveRange(oldKalkyl);

        var oldAnpassning = await context.LubeProjektanpassningar.Where(x => x.ProjectId == projectId).ToListAsync();
        context.LubeProjektanpassningar.RemoveRange(oldAnpassning);

        var oldMangder = await context.LubeSakvarorMangder.Where(x => x.ProjectId == projectId).ToListAsync();
        foreach (var item in sakvarorMangder)
        {
            var existing = oldMangder.FirstOrDefault(o =>
                string.Equals(o.Beteckning, item.Beteckning, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(o.Produktkod, item.Produktkod, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(o.Leverantor, item.Leverantor, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                if (existing.Offertpris != 0)
                    item.Offertpris = existing.Offertpris;
                if (existing.Slutpris != 0)
                    item.Slutpris = existing.Slutpris;
                item.ArKopt = existing.ArKopt;
                item.Anteckningar = existing.Anteckningar;
            }
        }
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

    public async Task SaveLubeSummaryAsync(int projectId, string summaryJson)
    {
        await using var context = _contextFactory();
        var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == projectId)
            ?? throw new InvalidOperationException($"Projektet med ID {projectId} hittades inte.");
        project.LubeSummaryJson = summaryJson;
        await context.SaveChangesAsync();
    }

    public async Task UpdateSakvaraPricesAsync(LubeSakvaraMangd item)
    {
        if (item.Id <= 0)
            return;

        await using var context = _contextFactory();
        var existing = await context.LubeSakvarorMangder.FirstOrDefaultAsync(x => x.Id == item.Id);
        if (existing == null)
            return;

        existing.Offertpris = item.Offertpris;
        existing.Slutpris = item.Slutpris;
        existing.ArKopt = item.ArKopt;
        existing.Anteckningar = item.Anteckningar;
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
