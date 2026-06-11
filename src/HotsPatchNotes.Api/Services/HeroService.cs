using System.Text.Json;
using System.Text.RegularExpressions;
using HotsPatchNotes.Api.Repositories;
using HotsPatchNotes.Shared;
using HotsPatchNotes.Shared.DTOs;
using HotsPatchNotes.Shared.Models;

namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Service implementation for Hero business operations.
/// </summary>
public sealed partial class HeroService(
    IHeroRepository heroRepository,
    IPatchRepository patchRepository,
    IBuildRepository buildRepository) : IHeroService
{
    public async Task<List<HeroSummaryDto>> GetHeroesAsync(
        string? role = null,
        string? type = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var heroes = await heroRepository.GetAllAsync(role, type, search, cancellationToken);

        return heroes.Select(MapToSummaryDto).ToList();
    }

    public async Task<HeroDetailDto?> GetHeroAsync(string shortName, CancellationToken cancellationToken = default)
    {
        var hero = await heroRepository.GetByShortNameAsync(shortName, cancellationToken);
        if (hero is null)
        {
            return null;
        }

        return MapToDetailDto(hero);
    }

    public async Task<List<string>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        return await heroRepository.GetRolesAsync(cancellationToken);
    }

    public async Task<List<HeroPatchDto>> GetHeroPatchesAsync(string shortName, CancellationToken cancellationToken = default)
    {
        var hero = await heroRepository.GetByShortNameAsync(shortName, cancellationToken);
        if (hero is null)
        {
            return [];
        }

        var sections = await patchRepository.GetSectionsForHeroAsync(hero.Id, hero.Name, cancellationToken);

        return sections.Select(s => new HeroPatchDto
        {
            PatchId = s.PatchId,
            InternalId = s.Patch.InternalId,
            PatchName = s.Patch.PatchName,
            PatchType = s.Patch.PatchType,
            LiveDate = s.Patch.LiveDate,
            OfficialLink = s.Patch.OfficialLink,
            Content = s.Content,
            Classification = s.Classification,
            ShortSummary = s.ShortSummary,
            SectionType = s.SectionType,
            HeadingLevel = s.HeadingLevel,
            Order = s.Order
        }).ToList();
    }

    public async Task<List<HeroBuildDto>> GetHeroBuildsAsync(string shortName, CancellationToken cancellationToken = default)
    {
        var hero = await heroRepository.GetByShortNameAsync(shortName, cancellationToken);
        if (hero is null)
        {
            return [];
        }

        var builds = await buildRepository.GetByHeroIdAsync(hero.Id, cancellationToken);

        return builds.Select(b => new HeroBuildDto
        {
            Id = b.Id,
            HeroId = b.HeroId,
            HeroShortName = hero.ShortName,
            Name = b.Name,
            TalentCode = b.TalentCode,
            Description = b.Description,
            Source = b.Source,
            CreatedAt = b.CreatedAt,
            ViewCount = b.ViewCount
        }).ToList();
    }

    public async Task<HeroBuildDto?> CreateBuildAsync(string shortName, CreateBuildDto request, CancellationToken cancellationToken = default)
    {
        var hero = await heroRepository.GetByShortNameAsync(shortName, cancellationToken);
        if (hero is null)
        {
            return null;
        }

        if (!IsValidTalentCode(request.TalentCode))
        {
            return null;
        }

        var build = new HeroBuild
        {
            HeroId = hero.Id,
            Name = request.Name,
            TalentCode = request.TalentCode,
            Description = request.Description,
            Source = "user",
            CreatedAt = DateTime.UtcNow
        };

        await buildRepository.CreateAsync(build, cancellationToken);

        return new HeroBuildDto
        {
            Id = build.Id,
            HeroId = build.HeroId,
            HeroShortName = hero.ShortName,
            Name = build.Name,
            TalentCode = build.TalentCode,
            Description = build.Description,
            Source = build.Source,
            CreatedAt = build.CreatedAt,
            ViewCount = build.ViewCount
        };
    }

    public ParsedBuildResult? ParseBuildCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        // Parse format [T1234567,heroname]
        var match = BuildCodeRegex().Match(code);
        if (match.Success)
        {
            return new ParsedBuildResult(
                match.Groups[1].Value,
                match.Groups[2].Value.ToLowerInvariant(),
                ParseTalentCode(match.Groups[1].Value));
        }

        // Try just the talent code
        if (IsValidTalentCode(code))
        {
            return new ParsedBuildResult(code, null, ParseTalentCode(code));
        }

        return null;
    }

    private static bool IsValidTalentCode(string? code)
    {
        return !string.IsNullOrWhiteSpace(code) &&
               code.Length == Constants.Talents.TierCount &&
               code.All(c => c >= Constants.Talents.MinChoice && c <= Constants.Talents.MaxChoice);
    }

    private static Dictionary<int, int> ParseTalentCode(string code)
    {
        var result = new Dictionary<int, int>();

        for (int i = 0; i < code.Length && i < Constants.Talents.TierLevels.Length; i++)
        {
            result[Constants.Talents.TierLevels[i]] = code[i] - '0';
        }

        return result;
    }

    private static HeroSummaryDto MapToSummaryDto(Hero hero)
    {
        return new HeroSummaryDto
        {
            Id = hero.Id,
            ShortName = hero.ShortName,
            Name = hero.Name,
            Icon = hero.Icon,
            Role = hero.Role,
            ExpandedRole = hero.ExpandedRole,
            Type = hero.Type,
            ReleaseDate = hero.ReleaseDate,
            Tags = DeserializeJsonList(hero.TagsJson),
            Title = hero.Title,
            Universe = hero.Universe,
            Difficulty = hero.Difficulty
        };
    }

    private static HeroDetailDto MapToDetailDto(Hero hero)
    {
        var dto = new HeroDetailDto
        {
            Id = hero.Id,
            ShortName = hero.ShortName,
            HyperlinkId = hero.HyperlinkId,
            AttributeId = hero.AttributeId,
            Name = hero.Name,
            Icon = hero.Icon,
            Role = hero.Role,
            ExpandedRole = hero.ExpandedRole,
            Type = hero.Type,
            ReleaseDate = hero.ReleaseDate,
            ReleasePatch = hero.ReleasePatch,
            Tags = DeserializeJsonList(hero.TagsJson),
            Title = hero.Title,
            Universe = hero.Universe,
            Difficulty = hero.Difficulty,
            Description = hero.Description,
            Lore = hero.Lore,
            Tips = DeserializeJsonList(hero.TipsJson),
            Counters = DeserializeJsonList(hero.CountersJson),
            CounteredBy = DeserializeJsonList(hero.CounteredByJson),
            Synergies = DeserializeJsonList(hero.SynergiesJson),
            WikiUrl = hero.WikiUrl,
            SplashArtUrl = hero.SplashArtUrl,
            BaseHealth = hero.BaseHealth,
            HealthRegen = hero.HealthRegen,
            BaseMana = hero.BaseMana,
            ManaRegen = hero.ManaRegen,
            BaseAttackDamage = hero.BaseAttackDamage,
            AttackSpeed = hero.AttackSpeed,
            AttackRange = hero.AttackRange,
            // Extended stats from heroes-data
            LifeMax = hero.LifeMax,
            LifeRegenRate = hero.LifeRegenRate,
            LifeScaling = hero.LifeScaling,
            LifeRegenRateScaling = hero.LifeRegenRateScaling,
            EnergyMax = hero.EnergyMax,
            EnergyRegenRate = hero.EnergyRegenRate,
            ShieldMax = hero.ShieldMax,
            ShieldRegenRate = hero.ShieldRegenRate,
            ShieldRegenDelay = hero.ShieldRegenDelay,
            AttackDamageScaling = hero.AttackDamageScaling,
            Speed = hero.Speed,
            SightRadius = hero.SightRadius
        };

        dto.Abilities = hero.Abilities
            .GroupBy(a => a.FormName ?? hero.Name)
            .ToDictionary(
                g => g.Key,
                g => g.Select(a => new AbilityDto
                {
                    Uid = a.Uid,
                    Name = a.Name,
                    Description = a.Description,
                    Hotkey = a.Hotkey,
                    AbilityId = a.AbilityId,
                    Cooldown = a.Cooldown,
                    ManaCost = a.ManaCost,
                    Icon = a.Icon,
                    Type = a.Type,
                    IsTrait = a.IsTrait,
                    Scaling = a.Scaling,
                    CastTime = a.CastTime,
                    Range = a.Range,
                    AreaOfEffect = a.AreaOfEffect,
                    // Extended fields from heroes-data
                    IsPassive = a.IsPassive,
                    LifeCost = a.LifeCost,
                    Charges = a.Charges,
                    ChargesMax = a.ChargesMax,
                    RechargeTime = a.RechargeTime,
                    IsToggle = a.IsToggle
                }).ToList()
            );

        dto.Talents = hero.Talents
            .GroupBy(t => t.Level)
            .OrderBy(g => g.Key)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(t => t.Sort).Select(t => new TalentDto
                {
                    TooltipId = t.TooltipId,
                    TalentTreeId = t.TalentTreeId,
                    Name = t.Name,
                    Description = t.Description,
                    Icon = t.Icon,
                    Type = t.Type,
                    Sort = t.Sort,
                    Cooldown = t.Cooldown,
                    AbilityId = t.AbilityId,
                    AbilityLinks = DeserializeJsonList(t.AbilityLinksJson),
                    LinkedAbilityName = t.LinkedAbilityName,
                    Properties = t.Properties,
                    // Extended fields from heroes-data
                    IsQuest = t.IsQuest,
                    QuestRequirement = t.QuestRequirement,
                    QuestReward = t.QuestReward,
                    AbilityTalentLinkIds = DeserializeJsonList(t.AbilityTalentLinkIdsJson),
                    IsStackable = t.IsStackable
                }).ToList()
            );

        return dto;
    }

    private static List<string> DeserializeJsonList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    [GeneratedRegex(@"\[T(\d{7}),(\w+)\]", RegexOptions.IgnoreCase)]
    private static partial Regex BuildCodeRegex();
}
