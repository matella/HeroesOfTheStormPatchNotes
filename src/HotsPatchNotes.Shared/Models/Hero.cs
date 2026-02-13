namespace HotsPatchNotes.Shared.Models;

/// <summary>
/// Represents a hero in Heroes of the Storm.
/// </summary>
public class Hero
{
    public int Id { get; set; }

    /// <summary>
    /// The hero's name with periods, dashes, apostrophes, spaces, and capitalization removed.
    /// Used as identifier in URLs and file names.
    /// </summary>
    public string ShortName { get; set; } = string.Empty;

    /// <summary>
    /// Internal hyperlink identifier used in game data.
    /// </summary>
    public string? HyperlinkId { get; set; }

    /// <summary>
    /// Four-character attribute identifier.
    /// </summary>
    public string? AttributeId { get; set; }

    /// <summary>
    /// Display name of the hero.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Icon file name for the hero.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Legacy role classification (Assassin, Warrior, Support, Specialist).
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// Modern expanded role classification (Tank, Bruiser, Healer, Support, Melee Assassin, Ranged Assassin).
    /// </summary>
    public string? ExpandedRole { get; set; }

    /// <summary>
    /// Attack type: Melee or Ranged.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Date the hero was released.
    /// </summary>
    public DateTime? ReleaseDate { get; set; }

    /// <summary>
    /// Patch version when the hero was released.
    /// </summary>
    public string? ReleasePatch { get; set; }

    /// <summary>
    /// Tags/categories for the hero (stored as JSON array).
    /// </summary>
    public string? TagsJson { get; set; }

    // --- Enriched fields from Fandom Wiki ---

    /// <summary>
    /// Hero's title/epithet (e.g., "Lord of the Scourge" for Arthas)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Universe/franchise the hero belongs to (Warcraft, Diablo, StarCraft, Overwatch, Nexus)
    /// </summary>
    public string? Universe { get; set; }

    /// <summary>
    /// Difficulty rating (Easy, Medium, Hard, Very Hard)
    /// </summary>
    public string? Difficulty { get; set; }

    /// <summary>
    /// In-game description/lore text
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Additional lore/backstory
    /// </summary>
    public string? Lore { get; set; }

    /// <summary>
    /// Tips for playing as this hero (JSON array of strings)
    /// </summary>
    public string? TipsJson { get; set; }

    /// <summary>
    /// Heroes that counter this hero (JSON array of short names)
    /// </summary>
    public string? CountersJson { get; set; }

    /// <summary>
    /// Heroes that this hero counters (JSON array of short names)
    /// </summary>
    public string? CounteredByJson { get; set; }

    /// <summary>
    /// Heroes that synergize well with this hero (JSON array of short names)
    /// </summary>
    public string? SynergiesJson { get; set; }

    /// <summary>
    /// URL to Fandom Wiki hero page
    /// </summary>
    public string? WikiUrl { get; set; }

    /// <summary>
    /// URL to hero's splash art image
    /// </summary>
    public string? SplashArtUrl { get; set; }

    /// <summary>
    /// Base health at level 1
    /// </summary>
    public int? BaseHealth { get; set; }

    /// <summary>
    /// Base attack damage at level 1
    /// </summary>
    public double? BaseAttackDamage { get; set; }

    /// <summary>
    /// Attack speed in attacks per second
    /// </summary>
    public double? AttackSpeed { get; set; }

    /// <summary>
    /// Attack range
    /// </summary>
    public double? AttackRange { get; set; }

    // --- Navigation properties ---

    /// <summary>
    /// Navigation property for abilities.
    /// </summary>
    public virtual ICollection<Ability> Abilities { get; set; } = [];

    /// <summary>
    /// Navigation property for talents.
    /// </summary>
    public virtual ICollection<Talent> Talents { get; set; } = [];

    /// <summary>
    /// Timestamp when this hero data was last synced from GitHub.
    /// </summary>
    public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;
}
