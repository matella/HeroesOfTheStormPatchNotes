namespace HotsPatchNotes.Shared.DTOs;

/// <summary>
/// Summary DTO for hero list views.
/// </summary>
public class HeroSummaryDto
{
    public int Id { get; set; }
    public string ShortName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Role { get; set; }
    public string? ExpandedRole { get; set; }
    public string? Type { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public List<string> Tags { get; set; } = [];

    // Enriched summary fields
    public string? Title { get; set; }
    public string? Universe { get; set; }
    public string? Difficulty { get; set; }
}

/// <summary>
/// Detailed DTO for hero detail views including abilities and talents.
/// </summary>
public class HeroDetailDto
{
    public int Id { get; set; }
    public string ShortName { get; set; } = string.Empty;
    public string? HyperlinkId { get; set; }
    public string? AttributeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Role { get; set; }
    public string? ExpandedRole { get; set; }
    public string? Type { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public string? ReleasePatch { get; set; }
    public List<string> Tags { get; set; } = [];

    // Enriched fields
    public string? Title { get; set; }
    public string? Universe { get; set; }
    public string? Difficulty { get; set; }
    public string? Description { get; set; }
    public string? Lore { get; set; }
    public List<string> Tips { get; set; } = [];
    public List<string> Counters { get; set; } = [];
    public List<string> CounteredBy { get; set; } = [];
    public List<string> Synergies { get; set; } = [];
    public string? WikiUrl { get; set; }
    public string? SplashArtUrl { get; set; }
    public int? BaseHealth { get; set; }
    public double? HealthRegen { get; set; }
    public int? BaseMana { get; set; }
    public double? ManaRegen { get; set; }
    public double? BaseAttackDamage { get; set; }
    public double? AttackSpeed { get; set; }
    public double? AttackRange { get; set; }

    /// <summary>
    /// Abilities grouped by form (e.g., "Abathur", "AbathurSymbiote").
    /// </summary>
    public Dictionary<string, List<AbilityDto>> Abilities { get; set; } = [];

    /// <summary>
    /// Talents grouped by level (1, 4, 7, 10, 13, 16, 20).
    /// </summary>
    public Dictionary<int, List<TalentDto>> Talents { get; set; } = [];
}

/// <summary>
/// DTO for ability data.
/// </summary>
public class AbilityDto
{
    public string? Uid { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Hotkey { get; set; }
    public string? AbilityId { get; set; }
    public double? Cooldown { get; set; }
    public string? ManaCost { get; set; }
    public string? Icon { get; set; }
    public string? Type { get; set; }
    public bool IsTrait { get; set; }
    public string? Scaling { get; set; }
    public string? CastTime { get; set; }
    public string? Range { get; set; }
    public string? AreaOfEffect { get; set; }
}

/// <summary>
/// DTO for talent data.
/// </summary>
public class TalentDto
{
    public string? TooltipId { get; set; }
    public string? TalentTreeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string? Type { get; set; }
    public int Sort { get; set; }
    public double? Cooldown { get; set; }
    public string? AbilityId { get; set; }
    public List<string> AbilityLinks { get; set; } = [];
    public string? LinkedAbilityName { get; set; }
    public string? Properties { get; set; }
}

/// <summary>
/// DTO for hero build data.
/// </summary>
public class HeroBuildDto
{
    public int Id { get; set; }
    public int HeroId { get; set; }
    public string HeroShortName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TalentCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Source { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ViewCount { get; set; }

    /// <summary>
    /// Full build code in format [T1331221,HeroName]
    /// </summary>
    public string FullCode => $"[T{TalentCode},{HeroShortName}]";
}

/// <summary>
/// Request DTO for creating/updating a build.
/// </summary>
public class CreateBuildDto
{
    public string Name { get; set; } = string.Empty;
    public string TalentCode { get; set; } = string.Empty;
    public string? Description { get; set; }
}
