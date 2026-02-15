namespace HotsPatchNotes.Shared.Models;

/// <summary>
/// Represents a talent for a hero.
/// </summary>
public class Talent
{
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the hero.
    /// </summary>
    public int HeroId { get; set; }

    /// <summary>
    /// Talent tier level (1, 4, 7, 10, 13, 16, or 20).
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Tooltip identifier for the talent.
    /// </summary>
    public string? TooltipId { get; set; }

    /// <summary>
    /// Talent tree identifier (name used in replay files).
    /// </summary>
    public string? TalentTreeId { get; set; }

    /// <summary>
    /// Display name of the talent.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Full description of the talent.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Icon file name for the talent.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Type of talent (Q, W, E, R, Trait, Active, Passive, Heroic).
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Sort order within the talent tier.
    /// </summary>
    public int Sort { get; set; }

    /// <summary>
    /// Cooldown in seconds (for active talents).
    /// </summary>
    public double? Cooldown { get; set; }

    /// <summary>
    /// Internal ability identifier this talent is linked to.
    /// </summary>
    public string? AbilityId { get; set; }

    /// <summary>
    /// JSON array of ability links.
    /// </summary>
    public string? AbilityLinksJson { get; set; }

    /// <summary>
    /// Display name of the ability this talent modifies
    /// </summary>
    public string? LinkedAbilityName { get; set; }

    /// <summary>
    /// Additional properties/tags from wiki (e.g., "Spell Armor", "Spell Power")
    /// </summary>
    public string? Properties { get; set; }

    // --- Extended fields from heroes-data ---

    /// <summary>
    /// Whether this talent is a quest talent
    /// </summary>
    public bool? IsQuest { get; set; }

    /// <summary>
    /// Quest completion requirement (e.g., "Gather 30 Regeneration Globes")
    /// </summary>
    public string? QuestRequirement { get; set; }

    /// <summary>
    /// Quest completion reward description
    /// </summary>
    public string? QuestReward { get; set; }

    /// <summary>
    /// Talent tier IDs that this talent links to (JSON array from heroes-data)
    /// </summary>
    public string? AbilityTalentLinkIdsJson { get; set; }

    /// <summary>
    /// Whether this talent has multiple levels/stacks
    /// </summary>
    public bool? IsStackable { get; set; }

    /// <summary>
    /// Navigation property to the hero.
    /// </summary>
    public virtual Hero? Hero { get; set; }
}
