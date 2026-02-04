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
    
    /// <summary>
    /// Navigation property for abilities.
    /// </summary>
    public virtual ICollection<Ability> Abilities { get; set; } = new List<Ability>();
    
    /// <summary>
    /// Navigation property for talents.
    /// </summary>
    public virtual ICollection<Talent> Talents { get; set; } = new List<Talent>();
    
    /// <summary>
    /// Timestamp when this hero data was last synced from GitHub.
    /// </summary>
    public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;
}
