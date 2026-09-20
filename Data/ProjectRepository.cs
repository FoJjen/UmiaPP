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

        // Existing rows use their persisted values as the baseline on first edit.
        // A newly created article starts with its entered identity as the baseline.
        if (!saved.HasOriginalArticleIdentity)
        {
            saved.OriginalProduktkod = item.Id == 0 ? item.Produktkod : saved.Produktkod;
            saved.OriginalLeverantor = item.Id == 0 ? item.Leverantor : saved.Leverantor;
            saved.HasOriginalArticleIdentity = true;
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
        // Quote membership is saved by the dedicated transactional quote APIs.
        // A delayed row edit must not overwrite a newer link/unlink operation.
        await context.SaveChangesAsync();
        item.Id = saved.Id;
        item.OriginalProduktkod = saved.OriginalProduktkod;
        item.OriginalLeverantor = saved.OriginalLeverantor;
        item.HasOriginalArticleIdentity = saved.HasOriginalArticleIdentity;
        item.NotifyArticleIdentityChanged();
    }

    public async Task SavePackageQuoteAsync(LubeSakvaraOffert quote, IReadOnlyList<int> articleIds)
    {
        await using var context = _contextFactory();
        await using var transaction = await context.Database.BeginTransactionAsync();
        LubeSakvaraOffert saved;
        if (quote.Id == 0)
        {
            saved = new LubeSakvaraOffert { ProjectId = quote.ProjectId };
            context.LubeSakvarorOfferter.Add(saved);
        }
        else
        {
            saved = await context.LubeSakvarorOfferter.SingleOrDefaultAsync(x => x.Id == quote.Id && x.ProjectId == quote.ProjectId)
                ?? throw new InvalidOperationException("Offerten finns inte längre i projektet.");
        }

        saved.Leverantor = quote.Leverantor;
        saved.Produktgrupp = quote.Produktgrupp;
        saved.OffereratPris = quote.OffereratPris;
        saved.Valuta = quote.Valuta;
        saved.Kommentar = quote.Kommentar;
        saved.QuoteDate = quote.QuoteDate;
        saved.FinalAmount = quote.FinalAmount;
        saved.FilePath = quote.FilePath;
        saved.IsPackageQuote = true;
        await context.SaveChangesAsync();

        if (articleIds.Count > 0)
        {
            var articles = await context.LubeSakvarorMangder
                .Where(x => x.ProjectId == quote.ProjectId && articleIds.Contains(x.Id))
                .ToListAsync();
            if (articles.Count != articleIds.Distinct().Count())
                throw new InvalidOperationException("En eller flera artiklar finns inte längre i projektet.");

            foreach (var article in articles)
                article.PackageQuoteId = saved.Id;
            await context.SaveChangesAsync();
        }
        await transaction.CommitAsync();
        quote.Id = saved.Id;
    }

    public async Task DeletePackageQuoteAsync(int projectId, int quoteId)
    {
        await using var context = _contextFactory();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var quote = await context.LubeSakvarorOfferter.SingleOrDefaultAsync(
            q => q.Id == quoteId && q.ProjectId == projectId && q.IsPackageQuote)
            ?? throw new InvalidOperationException("Offerten finns inte i projektet.");
        var articles = await context.LubeSakvarorMangder
            .Where(row => row.ProjectId == projectId && row.PackageQuoteId == quoteId)
            .ToListAsync();
        foreach (var article in articles)
            article.PackageQuoteId = null;

        // Keep articles, their individual prices and any attached file on disk.
        context.LubeSakvarorOfferter.Remove(quote);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task SetArticleQuoteLinksAsync(int projectId, IReadOnlyList<int> articleIds, int? quoteId)
    {
        var ids = articleIds.Distinct().ToArray();
        if (ids.Length == 0) return;
        await using var context = _contextFactory();
        await using var transaction = await context.Database.BeginTransactionAsync();
        if (quoteId.HasValue && !await context.LubeSakvarorOfferter.AnyAsync(
                q => q.Id == quoteId.Value && q.ProjectId == projectId && q.IsPackageQuote))
            throw new InvalidOperationException("Offerten finns inte i projektet.");
        var articles = await context.LubeSakvarorMangder
            .Where(a => a.ProjectId == projectId && ids.Contains(a.Id)).ToListAsync();
        if (articles.Count != ids.Length)
            throw new InvalidOperationException("En eller flera artiklar finns inte i projektet.");
        foreach (var article in articles)
            article.PackageQuoteId = quoteId;
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
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
        existing.CustomerReference = project.CustomerReference;
        existing.ContractType = project.ContractType;
        existing.Status = project.Status;
        existing.Discipline = project.Discipline;
        existing.Description = project.Description;
        existing.SalesPrice = project.SalesPrice;
        existing.DeliveryAddress = project.DeliveryAddress;
        existing.ContactPerson = project.ContactPerson;
        existing.InvoiceDetails = project.InvoiceDetails;
        existing.TargetTg = project.TargetTg;

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

        var inspectionItems = context.DrawingInspectionItems.Where(item => item.DrawingId == drawingId);
        context.DrawingInspectionItems.RemoveRange(inspectionItems);
        var inspections = context.DrawingInspections.Where(item => item.DrawingId == drawingId);
        context.DrawingInspections.RemoveRange(inspections);
        context.Drawings.Remove(d);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<List<InspectionPointTemplate>> GetInspectionTemplatesAsync()
    {
        await using var context = _contextFactory();
        return await context.InspectionPointTemplates
            .OrderBy(template => template.SortOrder)
            .ThenBy(template => template.Title)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<InspectionPointTemplate> SaveInspectionTemplateAsync(InspectionPointTemplate template)
    {
        if (string.IsNullOrWhiteSpace(template.Title))
            throw new InvalidOperationException("Ange en kontrollpunkt.");

        await using var context = _contextFactory();
        InspectionPointTemplate saved;
        if (template.Id == 0)
        {
            saved = new InspectionPointTemplate { CreatedDate = DateTime.Now };
            context.InspectionPointTemplates.Add(saved);
        }
        else
        {
            saved = await context.InspectionPointTemplates.SingleOrDefaultAsync(item => item.Id == template.Id)
                ?? throw new InvalidOperationException("Kontrollpunkten finns inte längre.");
        }

        saved.Title = template.Title.Trim();
        saved.Description = template.Description?.Trim() ?? string.Empty;
        saved.SortOrder = template.SortOrder;
        if (saved.Id != 0)
        {
            var items = await context.DrawingInspectionItems.Where(item => item.TemplateId == saved.Id).ToListAsync();
            foreach (var item in items)
            {
                item.Title = saved.Title;
                item.Description = saved.Description;
            }
        }
        await context.SaveChangesAsync();
        template.Id = saved.Id;
        template.CreatedDate = saved.CreatedDate;
        return saved;
    }

    public async Task DeleteInspectionTemplateAsync(int templateId)
    {
        await using var context = _contextFactory();
        var template = await context.InspectionPointTemplates.FindAsync(templateId);
        if (template == null) return;
        context.InspectionPointTemplates.Remove(template);
        await context.SaveChangesAsync();
    }

    public async Task<List<DrawingInspectionItem>> GetDrawingInspectionItemsAsync(int drawingId)
    {
        await using var context = _contextFactory();
        return await context.DrawingInspectionItems
            .Where(item => item.DrawingId == drawingId)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Title)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task SaveDrawingInspectionItemsAsync(int drawingId, List<DrawingInspectionItem> items)
    {
        await using var context = _contextFactory();
        if (!await context.Drawings.AnyAsync(drawing => drawing.Id == drawingId))
            throw new InvalidOperationException("Ritningen finns inte längre.");

        var existing = await context.DrawingInspectionItems.Where(item => item.DrawingId == drawingId).ToListAsync();
        context.DrawingInspectionItems.RemoveRange(existing);
        foreach (var item in items)
        {
            context.DrawingInspectionItems.Add(new DrawingInspectionItem
            {
                DrawingId = drawingId,
                TemplateId = item.TemplateId,
                Title = item.Title?.Trim() ?? string.Empty,
                Description = item.Description?.Trim() ?? string.Empty,
                SortOrder = item.SortOrder
            });
        }

        await context.SaveChangesAsync();
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

    public async Task<DrawingInspection> GetDrawingInspectionAsync(int drawingId)
    {
        await using var context = _contextFactory();
        return await context.DrawingInspections.AsNoTracking()
                .FirstOrDefaultAsync(item => item.DrawingId == drawingId)
            ?? new DrawingInspection { DrawingId = drawingId, Date = DateTime.Today, Revision = "1", SectionNumber = 1 };
    }

    public async Task SaveDrawingInspectionAsync(DrawingInspection inspection)
    {
        await using var context = _contextFactory();
        if (!await context.Drawings.AnyAsync(drawing => drawing.Id == inspection.DrawingId))
            throw new InvalidOperationException("Ritningen finns inte längre.");

        var saved = await context.DrawingInspections.FirstOrDefaultAsync(item => item.DrawingId == inspection.DrawingId);
        if (saved == null)
        {
            saved = new DrawingInspection { DrawingId = inspection.DrawingId };
            context.DrawingInspections.Add(saved);
        }

        saved.Date = inspection.Date == default ? DateTime.Today : inspection.Date;
        saved.CustomerReference = inspection.CustomerReference?.Trim() ?? string.Empty;
        saved.PreparedBy = inspection.PreparedBy?.Trim() ?? string.Empty;
        saved.ApprovedBy = inspection.ApprovedBy?.Trim() ?? string.Empty;
        saved.DocumentNumber = inspection.DocumentNumber?.Trim() ?? string.Empty;
        saved.Revision = string.IsNullOrWhiteSpace(inspection.Revision) ? "1" : inspection.Revision.Trim();
        saved.WorkTask = inspection.WorkTask?.Trim() ?? string.Empty;
        saved.BuildingPart = inspection.BuildingPart?.Trim() ?? string.Empty;
        saved.Description = inspection.Description?.Trim() ?? string.Empty;
        saved.SectionNumber = inspection.SectionNumber < 1 ? 1 : inspection.SectionNumber;
        await context.SaveChangesAsync();
        inspection.Id = saved.Id;
    }
}
