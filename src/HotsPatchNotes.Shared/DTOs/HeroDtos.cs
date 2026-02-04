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
    public List<string> Tags { get; set; } = new();
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
    public List<string> Tags { get; set; } = new();
    
    /// <summary>
    /// Abilities grouped by form (e.g., "Abathur", "AbathurSymbiote").
    /// </summary>
    public Dictionary<string, List<AbilityDto>> Abilities { get; set; } = new();
    
    /// <summary>
    /// Talents grouped by level (1, 4, 7, 10, 13, 16, 20).
    /// </summary>
    public Dictionary<int, List<TalentDto>> Talents { get; set; } = new();
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
    public List<string> AbilityLinks { get; set; } = new();
}
