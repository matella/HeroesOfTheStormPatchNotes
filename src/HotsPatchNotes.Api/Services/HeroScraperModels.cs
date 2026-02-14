namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Detailed hero information scraped from the Fandom wiki.
/// </summary>
public class HeroWikiDetailInfo
{
    public string? Title { get; set; }
    public string? Role { get; set; }
    public string? Difficulty { get; set; }
    public string? Universe { get; set; }
    public string? Description { get; set; }
    public string? Lore { get; set; }
    public int? BaseHealth { get; set; }
    public double? HealthRegen { get; set; }
    public int? BaseMana { get; set; }
    public double? ManaRegen { get; set; }
    public double? BaseAttackDamage { get; set; }
    public double? AttackSpeed { get; set; }
    public double? AttackRange { get; set; }
    public string? SplashArtUrl { get; set; }
    public List<WikiAbilityInfo> Abilities { get; set; } = [];
    public List<WikiTalentInfo> Talents { get; set; } = [];
}

/// <summary>
/// Ability information scraped from wiki.
/// </summary>
public class WikiAbilityInfo
{
    public string Name { get; set; } = string.Empty;
    public string? Scaling { get; set; }
    public string? CastTime { get; set; }
    public string? Range { get; set; }
    public string? AreaOfEffect { get; set; }
}

/// <summary>
/// Talent information scraped from wiki.
/// </summary>
public class WikiTalentInfo
{
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public string? LinkedAbilityName { get; set; }
    public string? Properties { get; set; }
}
