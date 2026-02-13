namespace HotsPatchNotes.Shared.Models;

/// <summary>
/// Represents a saved hero talent build.
/// Format: [T1331221,Cassia] where numbers represent talent choices at each tier.
/// </summary>
public class HeroBuild
{
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the hero.
    /// </summary>
    public int HeroId { get; set; }

    /// <summary>
    /// Navigation property to the hero.
    /// </summary>
    public virtual Hero Hero { get; set; } = null!;

    /// <summary>
    /// Display name for the build (e.g., "Standard Q Build", "PvE Splitpush").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Talent code representing selected talents at each tier.
    /// Format: "1331221" where each digit is the talent choice (1-4) at tiers 1,4,7,10,13,16,20.
    /// </summary>
    public string TalentCode { get; set; } = string.Empty;

    /// <summary>
    /// Optional description or notes about the build.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Source of the build: "user", "popular", "pro".
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// When this build was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of times this build has been viewed/used.
    /// </summary>
    public int ViewCount { get; set; }
}
