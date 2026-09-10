using Microsoft.EntityFrameworkCore;

namespace UmiaPP.Data;

public class LocalDataService : IDataService
{
    private readonly IProjectRepository _projectRepository;
    private readonly Func<AppDbContext> _contextFactory;

    public IProjectRepository Projects => _projectRepository;
    public bool IsCloudConnected => false;

    public LocalDataService(IProjectRepository? projectRepository = null, Func<AppDbContext>? contextFactory = null)
    {
        _contextFactory = contextFactory ?? (() => new AppDbContext());
        _projectRepository = projectRepository ?? new ProjectRepository(_contextFactory);
    }

    public async Task InitializeAsync()
    {
        await using var context = _contextFactory();
        await SqliteSchemaSynchronizer.SynchronizeAsync(context);

        // Skapa initial demodata om inga projekt finns
        if (!await context.Projects.AnyAsync())
        {
            var demoProject = new Models.Project
            {
                Name = "Kv. Fläkten - Nybyggnad kontor",
                ProjectNumber = "V-2025-001",
                Client = "Fastighets AB Ventil",
                ContractType = Models.ContractType.Totalentreprenad,
                Status = Models.ProjectStatus.Pagaende,
                Discipline = Models.DisciplineType.Ventilation,
                CreatedDate = DateTime.Now.AddDays(-14),
                Description = "Nybyggnation av kontorsfastighet med FTX-system och VAV-styrning."
            };

            demoProject.Drawings.Add(new Models.Drawing
            {
                DrawingNumber = "V-40.1-101",
                SheetTitle = "Plan 1 - Till- och frånluft",
                Revision = "A",
                Date = DateTime.Now.AddDays(-10),
                FilePath = "C:\\Ritningar\\V-40.1-101.pdf"
            });

            demoProject.Drawings.Add(new Models.Drawing
            {
                DrawingNumber = "V-40.1-102",
                SheetTitle = "Plan 2 - Till- och frånluft",
                Revision = "B",
                Date = DateTime.Now.AddDays(-5),
                FilePath = "C:\\Ritningar\\V-40.1-102.pdf"
            });

            context.Projects.Add(demoProject);
            await context.SaveChangesAsync();
        }
    }

    public async Task SyncWithCloudAsync()
    {
        // Förberedd för framtida synkroniseringsmodul mot REST API / Azure
        await Task.Delay(300); // Simulera synk-anrop
    }
}
