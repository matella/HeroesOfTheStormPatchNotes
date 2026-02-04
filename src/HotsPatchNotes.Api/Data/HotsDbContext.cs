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
        });
    }
}
