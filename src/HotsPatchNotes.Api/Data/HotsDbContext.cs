using Microsoft.EntityFrameworkCore;
using HotsPatchNotes.Shared.Models;

namespace HotsPatchNotes.Api.Data;

public class HotsDbContext : DbContext
{
    public HotsDbContext(DbContextOptions<HotsDbContext> options) : base(options)
    {
    }

    public DbSet<Hero> Heroes => Set<Hero>();
    public DbSet<Ability> Abilities => Set<Ability>();
    public DbSet<Talent> Talents => Set<Talent>();
    public DbSet<Patch> Patches => Set<Patch>();
    public DbSet<PatchSection> PatchSections => Set<PatchSection>();
    public DbSet<HeroBuild> HeroBuilds => Set<HeroBuild>();
    public DbSet<Battleground> Battlegrounds => Set<Battleground>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Hero configuration
        modelBuilder.Entity<Hero>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ShortName).IsUnique();
            entity.Property(e => e.ShortName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Icon).HasMaxLength(255);
            entity.Property(e => e.Role).HasMaxLength(100);
            entity.Property(e => e.ExpandedRole).HasMaxLength(100);
            entity.Property(e => e.Type).HasMaxLength(50);
            entity.Property(e => e.HyperlinkId).HasMaxLength(100);
            entity.Property(e => e.AttributeId).HasMaxLength(10);
            entity.Property(e => e.ReleasePatch).HasMaxLength(50);
            // Enriched fields
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Universe).HasMaxLength(50);
            entity.Property(e => e.Difficulty).HasMaxLength(50);
            entity.Property(e => e.WikiUrl).HasMaxLength(500);
            entity.Property(e => e.SplashArtUrl).HasMaxLength(500);
        });

        // Ability configuration
        modelBuilder.Entity<Ability>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.HeroId, e.AbilityId });
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Uid).HasMaxLength(50);
            entity.Property(e => e.Hotkey).HasMaxLength(10);
            entity.Property(e => e.AbilityId).HasMaxLength(100);
            entity.Property(e => e.ManaCost).HasMaxLength(50);
            entity.Property(e => e.Icon).HasMaxLength(255);
            entity.Property(e => e.Type).HasMaxLength(50);
            entity.Property(e => e.FormName).HasMaxLength(100);
            entity.Property(e => e.Scaling).HasMaxLength(100);
            entity.Property(e => e.CastTime).HasMaxLength(100);
            entity.Property(e => e.Range).HasMaxLength(100);
            entity.Property(e => e.AreaOfEffect).HasMaxLength(200);

            entity.HasOne(e => e.Hero)
                .WithMany(h => h.Abilities)
                .HasForeignKey(e => e.HeroId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Talent configuration
        modelBuilder.Entity<Talent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.HeroId, e.Level, e.Sort });
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.TooltipId).HasMaxLength(200);
            entity.Property(e => e.TalentTreeId).HasMaxLength(200);
            entity.Property(e => e.Icon).HasMaxLength(255);
            entity.Property(e => e.Type).HasMaxLength(50);
            entity.Property(e => e.AbilityId).HasMaxLength(100);
            entity.Property(e => e.LinkedAbilityName).HasMaxLength(200);
            entity.Property(e => e.Properties).HasMaxLength(500);

            entity.HasOne(e => e.Hero)
                .WithMany(h => h.Talents)
                .HasForeignKey(e => e.HeroId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Patch configuration
        modelBuilder.Entity<Patch>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.InternalId).IsUnique();
            entity.HasIndex(e => e.LiveDate);
            entity.Property(e => e.InternalId).IsRequired().HasMaxLength(200);
            entity.Property(e => e.PatchName).HasMaxLength(200);
            entity.Property(e => e.PatchType).HasMaxLength(50);
            entity.Property(e => e.GameVersion).HasMaxLength(50);
            entity.Property(e => e.FullVersion).HasMaxLength(50);
            entity.Property(e => e.OfficialLink).HasMaxLength(500);
            entity.Property(e => e.AlternateLink).HasMaxLength(500);
            entity.Property(e => e.LiveBuild).HasMaxLength(50);
            entity.Property(e => e.PtrOfficialLink).HasMaxLength(500);
            entity.Property(e => e.PtrBuild).HasMaxLength(50);
        });

        // PatchSection configuration
        modelBuilder.Entity<PatchSection>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.PatchId, e.Order });
            entity.HasIndex(e => new { e.PatchId, e.SectionType, e.EntityName });
            entity.HasIndex(e => e.HeroId);
            entity.Property(e => e.SectionType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EntityName).IsRequired().HasMaxLength(200);

            entity.HasOne(e => e.Patch)
                .WithMany(p => p.Sections)
                .HasForeignKey(e => e.PatchId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Hero)
                .WithMany()
                .HasForeignKey(e => e.HeroId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ParentSection)
                .WithMany(e => e.ChildSections)
                .HasForeignKey(e => e.ParentSectionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // HeroBuild configuration
        modelBuilder.Entity<HeroBuild>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.HeroId, e.TalentCode });
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.TalentCode).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Source).HasMaxLength(50);

            entity.HasOne(e => e.Hero)
                .WithMany()
                .HasForeignKey(e => e.HeroId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Battleground configuration
        modelBuilder.Entity<Battleground>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ShortName).IsUnique();
            entity.Property(e => e.ShortName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.MapType).HasMaxLength(50);
            entity.Property(e => e.ImageUrl).HasMaxLength(500);
            entity.Property(e => e.Event).HasMaxLength(200);
            entity.Property(e => e.Universe).HasMaxLength(100);
        });
    }
}
