namespace HotsPatchNotes.Shared.DTOs;

/// <summary>
/// Admin DTO for editing hero records.
/// </summary>
public class AdminHeroDto
{
    public int Id { get; set; }
    public string ShortName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Role { get; set; }
    public string? ExpandedRole { get; set; }
    public string? Type { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public string? Title { get; set; }
    public string? Universe { get; set; }
    public string? Difficulty { get; set; }
    public string? Description { get; set; }
    public string? Lore { get; set; }
    public string? WikiUrl { get; set; }
    public string? SplashArtUrl { get; set; }
    public int? BaseHealth { get; set; }
    public double? HealthRegen { get; set; }
    public int? BaseMana { get; set; }
    public double? ManaRegen { get; set; }
    public double? BaseAttackDamage { get; set; }
    public double? AttackSpeed { get; set; }
    public double? AttackRange { get; set; }
    public int AbilityCount { get; set; }
    public int TalentCount { get; set; }
}

/// <summary>
/// Admin DTO for editing ability records.
/// </summary>
public class AdminAbilityDto
{
    public int Id { get; set; }
    public int HeroId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Hotkey { get; set; }
    public double? Cooldown { get; set; }
    public string? ManaCost { get; set; }
    public string? Icon { get; set; }
    public string? Type { get; set; }
    public string? FormName { get; set; }
    public string? Scaling { get; set; }
    public string? CastTime { get; set; }
    public string? Range { get; set; }
    public string? AreaOfEffect { get; set; }
}

/// <summary>
/// Admin DTO for editing talent records.
/// </summary>
public class AdminTalentDto
{
    public int Id { get; set; }
    public int HeroId { get; set; }
    public int Level { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string? Type { get; set; }
    public int Sort { get; set; }
    public double? Cooldown { get; set; }
    public string? LinkedAbilityName { get; set; }
    public string? Properties { get; set; }
}

/// <summary>
/// Admin DTO for editing patch records.
/// </summary>
public class AdminPatchDto
{
    public int Id { get; set; }
    public string InternalId { get; set; } = string.Empty;
    public string? PatchName { get; set; }
    public string? PatchType { get; set; }
    public string? GameVersion { get; set; }
    public DateTime? LiveDate { get; set; }
    public string? OfficialLink { get; set; }
    public string? AlternateLink { get; set; }
    public string? Source { get; set; }
    public bool HasContent { get; set; }
    public int SectionCount { get; set; }
}

/// <summary>
/// Admin DTO for editing battleground records.
/// </summary>
public class AdminBattlegroundDto
{
    public int Id { get; set; }
    public string ShortName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? MapType { get; set; }
    public string? Description { get; set; }
    public string? Objective { get; set; }
    public string? ObjectiveTiming { get; set; }
    public string? MercCamps { get; set; }
    public string? BossInfo { get; set; }
    public string? Tips { get; set; }
    public string? ImageUrl { get; set; }
    public string? Universe { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public bool IsInRotation { get; set; }
}

/// <summary>
/// Paginated result wrapper for admin list endpoints.
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
