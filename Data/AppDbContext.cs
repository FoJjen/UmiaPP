using Microsoft.EntityFrameworkCore;
using UmiaPP.Models;
using System.IO;

namespace UmiaPP.Data;

public class AppDbContext : DbContext
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Drawing> Drawings => Set<Drawing>();
    public DbSet<LubeKalkylPost> LubeKalkylPoster => Set<LubeKalkylPost>();
    public DbSet<LubeProjektanpassningPost> LubeProjektanpassningar => Set<LubeProjektanpassningPost>();
    public DbSet<LubeSakvaraMangd> LubeSakvarorMangder => Set<LubeSakvaraMangd>();
    public DbSet<LubeSakvaraOffert> LubeSakvarorOfferter => Set<LubeSakvaraOffert>();
    public DbSet<LubeSakvaraLeveranspris> LubeSakvarorLeveranspriser => Set<LubeSakvaraLeveranspris>();
    public DbSet<LubeSakvaraMontage> LubeSakvarorMontagePoster => Set<LubeSakvaraMontage>();
    public DbSet<LubeEjMangdbaserad> LubeEjMangdbaseradePoster => Set<LubeEjMangdbaserad>();
    public DbSet<BluebeamMarkupItem> BluebeamMarkups => Set<BluebeamMarkupItem>();
    public DbSet<InspectionPointTemplate> InspectionPointTemplates => Set<InspectionPointTemplate>();
    public DbSet<DrawingInspectionItem> DrawingInspectionItems => Set<DrawingInspectionItem>();
    public DbSet<DrawingInspection> DrawingInspections => Set<DrawingInspection>();

    private readonly string _dbPath;

    public AppDbContext()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var umiaFolder = Path.Combine(appData, "UmiaPP");
        if (!Directory.Exists(umiaFolder))
        {
            Directory.CreateDirectory(umiaFolder);
        }
        _dbPath = Path.Combine(umiaFolder, "umia_vent.db");
    }

    public AppDbContext(string dbPath)
    {
        _dbPath = dbPath;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite($"Data Source={_dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.Property(p => p.ProjectNumber).IsRequired().HasMaxLength(50);
            entity.Property(p => p.Client).HasMaxLength(200);
        });

        modelBuilder.Entity<Drawing>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.HasOne(d => d.Project)
                  .WithMany(p => p.Drawings)
                  .HasForeignKey(d => d.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LubeKalkylPost>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Project)
                  .WithMany(p => p.LubeKalkylPoster)
                  .HasForeignKey(x => x.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LubeProjektanpassningPost>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Project)
                  .WithMany(p => p.LubeProjektanpassningar)
                  .HasForeignKey(x => x.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LubeSakvaraMangd>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Ignore(x => x.Differens);
            entity.Ignore(x => x.EffectiveOffertpris);
            entity.Ignore(x => x.EffectiveSlutpris);
            entity.Ignore(x => x.IsOffertprisFallback);
            entity.Ignore(x => x.IsSlutprisFallback);
            entity.Ignore(x => x.HasPackageQuote);
            entity.Ignore(x => x.PackageQuoteLabel);
            entity.HasOne(x => x.Project)
                  .WithMany(p => p.LubeSakvarorMangder)
                  .HasForeignKey(x => x.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LubeSakvaraOffert>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Ignore(x => x.EffectiveAmount);
            entity.HasOne(x => x.Project)
                  .WithMany(p => p.LubeSakvarorOfferter)
                  .HasForeignKey(x => x.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LubeSakvaraLeveranspris>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Project)
                  .WithMany(p => p.LubeSakvarorLeveranspriser)
                  .HasForeignKey(x => x.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LubeSakvaraMontage>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Project)
                  .WithMany(p => p.LubeSakvarorMontagePoster)
                  .HasForeignKey(x => x.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LubeEjMangdbaserad>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Project)
                  .WithMany(p => p.LubeEjMangdbaseradePoster)
                  .HasForeignKey(x => x.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BluebeamMarkupItem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Project)
                  .WithMany(p => p.BluebeamMarkups)
                  .HasForeignKey(x => x.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InspectionPointTemplate>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(2000);
        });

        modelBuilder.Entity<DrawingInspectionItem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.HasOne<Drawing>()
                  .WithMany()
                  .HasForeignKey(x => x.DrawingId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DrawingInspection>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.DrawingId).IsUnique();
            entity.HasOne<Drawing>()
                  .WithMany()
                  .HasForeignKey(x => x.DrawingId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
