using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using HotsPatchNotes.Api.Data;
using HotsPatchNotes.Shared.DTOs;
using HotsPatchNotes.Shared.Models;

namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Primary sync service for hero data from HeroesToolChest/heroes-data repository.
/// Creates and fully replaces heroes, abilities, and talents on each sync.
/// Gamestrings are applied to abilities and talents during sync for up-to-date names/descriptions.
/// </summary>
public sealed partial class HeroesDataSyncService(
    HotsDbContext dbContext,
    HttpClient httpClient,
    IGamestringsParser gamestringsParser,
    ILogger<HeroesDataSyncService> logger) : IHeroesDataSyncService
{
    private const string HeroesDataDirApiUrl = "https://api.github.com/repos/HeroesToolChest/heroes-data/contents/heroesdata";
    private const string HeroesDataRawBaseUrl = "https://raw.githubusercontent.com/HeroesToolChest/heroes-data/main";

    // Ability categories from heroes-data JSON to create abilities from
    private static readonly HashSet<string> AbilityCategoriesToProcess =
        new(StringComparer.OrdinalIgnoreCase) { "basic", "heroic", "trait", "mount", "active", "activable" };

    // Maps heroes-data abilityType to the Hotkey field
    private static readonly Dictionary<string, string> AbilityTypeToHotkey =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Q"] = "Q",
            ["W"] = "W",
            ["E"] = "E",
            ["Heroic"] = "R",
            ["Trait"] = "D",
            ["Z"] = "Z",
            ["Mount"] = "Z",
        };

    // Maps heroes-data level key to integer tier level
    private static readonly Dictionary<string, int> LevelKeyToInt =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["level1"] = 1,
            ["level4"] = 4,
            ["level7"] = 7,
            ["level10"] = 10,
            ["level13"] = 13,
            ["level16"] = 16,
            ["level20"] = 20,
        };

    // Overrides for heroes where camelCase→kebab derivation is incorrect
    private static readonly Dictionary<string, string> ShortNameOverrides =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["DVa"] = "d.va",
            ["LostVikings"] = "the-lost-vikings",
        };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<SyncResultDto> SyncHeroesDataAsync(CancellationToken cancellationToken = default)
    {
        var result = new SyncResultDto { SyncedAt = DateTime.UtcNow };

        try
        {
            logger.LogInformation("Starting heroes-data primary sync");

            var latest = await FindLatestBuildAsync(cancellationToken);
            if (latest is null)
            {
                result.Errors.Add("No build directories found in heroes-data repository");
                logger.LogWarning("No build directories found in heroes-data repository");
                return result;
            }

            var (dirName, fileUrl) = latest.Value;
            logger.LogInformation("Found latest heroes-data build: {DirName}", dirName);

            var jsonContent = await httpClient.GetStringAsync(fileUrl, cancellationToken);
            var heroesData = JsonSerializer.Deserialize<Dictionary<string, HeroesDataHero>>(jsonContent, JsonOptions);

            if (heroesData is null || heroesData.Count == 0)
            {
                result.Errors.Add("Failed to parse heroes-data JSON or no heroes found");
                return result;
            }

            logger.LogInformation("Parsed {Count} heroes from heroes-data", heroesData.Count);

            // Fetch gamestrings (graceful degradation — sync continues without them)
            Dictionary<string, GamestringEntry>? gamestrings = null;
            try
            {
                gamestrings = await gamestringsParser.FetchAndParseGamestringsAsync(
                    dirName,
                    "enus",
                    cancellationToken);

                if (gamestrings is not null)
                    logger.LogInformation("Fetched {Count} gamestring entries for {DirName}", gamestrings.Count, dirName);
                else
                    logger.LogWarning("No gamestrings fetched for {DirName}, continuing without them", dirName);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch gamestrings, continuing without them");
            }

            var syncStartTime = DateTime.UtcNow;

            foreach (var (heroId, heroData) in heroesData)
            {
                try
                {
                    await CreateOrUpdateHeroAsync(heroId, heroData, gamestrings, cancellationToken);
                    result.HeroesUpdated++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to sync hero: {HeroId}", heroId);
                    result.Errors.Add($"Failed to sync {heroId}: {ex.Message}");
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            // Warn about heroes in DB that were not present in heroes-data
            await WarnAboutStaleHeroesAsync(syncStartTime, cancellationToken);

            result.Success = true;
            result.Message = $"Synced {result.HeroesUpdated} heroes from heroes-data";
            logger.LogInformation("Heroes-data sync complete: {Count} heroes synced", result.HeroesUpdated);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync heroes-data");
            result.Errors.Add($"Heroes-data sync failed: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Finds the latest build directory and constructs the herodata file URL.
    /// Returns (dirName, fileUrl) for the newest build, or null if none found.
    /// </summary>
    private async Task<(string DirName, string FileUrl)?> FindLatestBuildAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.GetStringAsync(HeroesDataDirApiUrl, cancellationToken);
            var items = JsonSerializer.Deserialize<List<GitHubFileInfo>>(response, JsonOptions);

            if (items is null) return null;

            // Select directories, sort by numeric build number descending
            var buildDirs = items
                .Where(i => i.Type == "dir")
                .Select(i => (DirName: i.Name, BuildNum: ParseBuildNum(i.Name)))
                .Where(x => x.BuildNum > 0)
                .OrderByDescending(x => x.BuildNum)
                .ToList();

            var latest = buildDirs.FirstOrDefault();
            if (latest == default) return null;

            var fileUrl = $"{HeroesDataRawBaseUrl}/heroesdata/{latest.DirName}/data/herodata_{latest.DirName}_localized.json";
            return (latest.DirName, fileUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to find latest heroes-data build directory");
            return null;
        }
    }

    /// <summary>
    /// Creates or fully replaces a hero and all its abilities and talents.
    /// Existing abilities and talents are cleared and repopulated from heroes-data.
    /// Gamestrings are applied to update names and descriptions.
    /// </summary>
    private async Task CreateOrUpdateHeroAsync(
        string heroId,
        HeroesDataHero heroData,
        Dictionary<string, GamestringEntry>? gamestrings,
        CancellationToken cancellationToken)
    {
        var hyperlinkId = heroData.HyperlinkId ?? heroId;
        var shortName = DeriveShortName(hyperlinkId);

        var hero = await dbContext.Heroes
            .Include(h => h.Abilities)
            .Include(h => h.Talents)
            .FirstOrDefaultAsync(h =>
                h.HyperlinkId == hyperlinkId ||
                h.ShortName == shortName,
                cancellationToken);

        if (hero is null)
        {
            hero = new Hero
            {
                ShortName = shortName,
                HyperlinkId = hyperlinkId,
                Name = heroData.Name ?? hyperlinkId,
            };
            dbContext.Heroes.Add(hero);
            logger.LogDebug("Creating new hero: {Name} ({ShortName})", hero.Name, hero.ShortName);
        }
        else
        {
            // Full replace strategy: clear existing abilities and talents
            dbContext.Abilities.RemoveRange(hero.Abilities);
            dbContext.Talents.RemoveRange(hero.Talents);
            hero.Abilities.Clear();
            hero.Talents.Clear();
            logger.LogDebug("Updating hero: {Name} — cleared abilities and talents for repopulation", hero.Name);
        }

        // Update hero fields (preserve existing non-null values that we can't derive from heroes-data)
        if (!string.IsNullOrWhiteSpace(heroData.Name))
            hero.Name = heroData.Name;

        hero.HyperlinkId ??= hyperlinkId;
        hero.AttributeId ??= heroData.AttributeId;

        if (!string.IsNullOrWhiteSpace(heroData.Franchise))
            hero.Universe ??= heroData.Franchise;

        if (!string.IsNullOrWhiteSpace(heroData.ReleaseDate) && hero.ReleaseDate is null &&
            DateTime.TryParse(heroData.ReleaseDate, out var releaseDate))
        {
            hero.ReleaseDate = releaseDate;
        }

        if (heroData.Descriptors is not null && heroData.Descriptors.Count > 0)
            hero.TagsJson ??= JsonSerializer.Serialize(heroData.Descriptors);

        // Stats (null-coalescing — not overwriting values set by other enrichment)
        if (heroData.Life is not null)
        {
            hero.LifeMax ??= heroData.Life.LifeMax;
            hero.LifeRegenRate ??= heroData.Life.LifeRegenRate;
            hero.LifeScaling ??= heroData.Life.LifeScaling;
            hero.LifeRegenRateScaling ??= heroData.Life.LifeRegenRateScaling;
        }

        if (heroData.Energy is not null)
        {
            hero.EnergyMax ??= heroData.Energy.EnergyMax;
            hero.EnergyRegenRate ??= heroData.Energy.EnergyRegenRate;
        }

        if (heroData.Shield is not null)
        {
            hero.ShieldMax ??= heroData.Shield.ShieldMax;
            hero.ShieldRegenRate ??= heroData.Shield.ShieldRegenRate;
            hero.ShieldRegenDelay ??= heroData.Shield.ShieldRegenDelay;
        }

        hero.Speed ??= heroData.Speed;
        hero.SightRadius ??= heroData.Sight;

        var primaryWeapon = heroData.Weapons?.FirstOrDefault();
        if (primaryWeapon is not null)
        {
            hero.AttackDamageScaling ??= primaryWeapon.DamageScaling;
            hero.AttackRange ??= primaryWeapon.Range;
            hero.AttackSpeed ??= primaryWeapon.AttackSpeed;
        }

        // Create abilities from heroes-data categories
        if (heroData.Abilities is not null)
        {
            foreach (var (category, abilitiesList) in heroData.Abilities)
            {
                if (!AbilityCategoriesToProcess.Contains(category))
                    continue;

                foreach (var abilityData in abilitiesList)
                {
                    if (string.IsNullOrWhiteSpace(abilityData.NameId))
                        continue;

                    var isTrait = string.Equals(abilityData.AbilityType, "Trait", StringComparison.OrdinalIgnoreCase);
                    AbilityTypeToHotkey.TryGetValue(abilityData.AbilityType ?? string.Empty, out var hotkey);

                    var ability = new Ability
                    {
                        Hero = hero,
                        AbilityId = abilityData.NameId,
                        Uid = abilityData.ButtonId ?? abilityData.NameId,
                        Name = abilityData.NameId, // Overwritten by gamestrings below
                        Icon = abilityData.Icon,
                        Type = category,
                        Hotkey = hotkey,
                        IsTrait = isTrait,
                        IsPassive = abilityData.IsPassive,
                        IsToggle = abilityData.IsToggle,
                        LifeCost = abilityData.LifeCost,
                        ChargesMax = abilityData.Charges?.CountMax,
                        RechargeTime = abilityData.Charges?.RecastCooldown,
                    };

                    hero.Abilities.Add(ability);
                }
            }
        }

        // Create talents from heroes-data level keys
        if (heroData.Talents is not null)
        {
            foreach (var (levelKey, talentsList) in heroData.Talents)
            {
                if (!LevelKeyToInt.TryGetValue(levelKey, out var level))
                    continue;

                for (var i = 0; i < talentsList.Count; i++)
                {
                    var talentData = talentsList[i];
                    if (string.IsNullOrWhiteSpace(talentData.NameId))
                        continue;

                    var talent = new Talent
                    {
                        Hero = hero,
                        TalentTreeId = talentData.NameId,
                        TooltipId = talentData.ButtonId ?? talentData.NameId,
                        Name = talentData.NameId, // Overwritten by gamestrings below
                        Icon = talentData.Icon,
                        Type = talentData.AbilityType,
                        Level = level,
                        Sort = talentData.Sort ?? i,
                        IsQuest = talentData.IsQuest,
                        IsStackable = talentData.IsStackable,
                    };

                    if (talentData.AbilityTalentLinkIds is not null && talentData.AbilityTalentLinkIds.Count > 0)
                    {
                        talent.AbilityLinksJson = JsonSerializer.Serialize(talentData.AbilityTalentLinkIds);
                        talent.AbilityTalentLinkIdsJson = talent.AbilityLinksJson;
                    }

                    hero.Talents.Add(talent);
                }
            }
        }

        // Apply gamestrings to overwrite placeholder names/descriptions
        if (gamestrings is not null)
        {
            if (hero.Abilities.Count > 0)
            {
                var abilityMatches = gamestringsParser.ApplyGamestringsToAbilities(hero.Abilities, gamestrings);
                logger.LogDebug("Matched {Count}/{Total} abilities to gamestrings for {Hero}",
                    abilityMatches, hero.Abilities.Count, hero.Name);
            }

            if (hero.Talents.Count > 0)
            {
                var talentMatches = gamestringsParser.ApplyGamestringsToTalents(hero.Talents, gamestrings);
                logger.LogDebug("Matched {Count}/{Total} talents to gamestrings for {Hero}",
                    talentMatches, hero.Talents.Count, hero.Name);
            }
        }

        hero.LastSyncedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Logs a warning for heroes in the database that were not present in the current heroes-data JSON.
    /// These are heroes that were not updated during this sync — they may be stale.
    /// </summary>
    private async Task WarnAboutStaleHeroesAsync(DateTime syncStartTime, CancellationToken cancellationToken)
    {
        try
        {
            var staleHeroes = await dbContext.Heroes
                .Where(h => h.LastSyncedAt < syncStartTime)
                .Select(h => h.Name)
                .ToListAsync(cancellationToken);

            if (staleHeroes.Count > 0)
            {
                logger.LogWarning("The following {Count} heroes were not found in heroes-data and were NOT updated: {Heroes}",
                    staleHeroes.Count, string.Join(", ", staleHeroes));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to check for stale heroes");
        }
    }

    /// <summary>
    /// Derives a URL-friendly ShortName from a camelCase HyperlinkId.
    /// For example: "LiMing" → "li-ming", "DVa" → "d.va" (via override table).
    /// </summary>
    private static string DeriveShortName(string hyperlinkId)
    {
        if (ShortNameOverrides.TryGetValue(hyperlinkId, out var known))
            return known;

        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < hyperlinkId.Length; i++)
        {
            if (i > 0 && char.IsUpper(hyperlinkId[i]) && char.IsLower(hyperlinkId[i - 1]))
                sb.Append('-');
            sb.Append(char.ToLowerInvariant(hyperlinkId[i]));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Extracts the numeric build number from a version directory name.
    /// Example: "2.55.15.96477" → 96477
    /// </summary>
    private static int ParseBuildNum(string dirName)
    {
        var parts = dirName.Split('.');
        return int.TryParse(parts[^1], out var n) ? n : 0;
    }

    #region Data Models for heroes-data JSON

    private sealed class GitHubFileInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    private sealed class HeroesDataHero
    {
        public string? Name { get; set; }
        public string? HyperlinkId { get; set; }
        public string? AttributeId { get; set; }
        public string? Franchise { get; set; }
        public string? ReleaseDate { get; set; }
        public List<string>? Descriptors { get; set; }
        public HeroesDataLife? Life { get; set; }
        public HeroesDataEnergy? Energy { get; set; }
        public HeroesDataShield? Shield { get; set; }
        public double? Speed { get; set; }
        public double? Sight { get; set; }
        public List<HeroesDataWeapon>? Weapons { get; set; }
        public Dictionary<string, List<HeroesDataAbility>>? Abilities { get; set; }
        public Dictionary<string, List<HeroesDataTalent>>? Talents { get; set; }
    }

    private sealed class HeroesDataLife
    {
        public double? LifeMax { get; set; }
        public double? LifeRegenRate { get; set; }
        public double? LifeScaling { get; set; }
        public double? LifeRegenRateScaling { get; set; }
    }

    private sealed class HeroesDataEnergy
    {
        public double? EnergyMax { get; set; }
        public double? EnergyRegenRate { get; set; }
    }

    private sealed class HeroesDataShield
    {
        public double? ShieldMax { get; set; }
        public double? ShieldRegenRate { get; set; }
        public double? ShieldRegenDelay { get; set; }
    }

    private sealed class HeroesDataWeapon
    {
        public double? Range { get; set; }
        public double? DamageScaling { get; set; }
        public double? AttackSpeed { get; set; }
    }

    private sealed class HeroesDataAbility
    {
        public string? NameId { get; set; }
        public string? ButtonId { get; set; }
        public string? Icon { get; set; }
        public string? AbilityType { get; set; }
        public bool? IsPassive { get; set; }
        public bool? IsToggle { get; set; }
        public double? LifeCost { get; set; }
        public HeroesDataCharges? Charges { get; set; }
    }

    private sealed class HeroesDataTalent
    {
        public string? NameId { get; set; }
        public string? ButtonId { get; set; }
        public string? Icon { get; set; }
        public string? AbilityType { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsQuest { get; set; }
        public int? Sort { get; set; }
        public List<string>? AbilityTalentLinkIds { get; set; }
        public bool? IsStackable { get; set; }
    }

    private sealed class HeroesDataCharges
    {
        public int? CountMax { get; set; }
        public double? RecastCooldown { get; set; }
    }

    #endregion
}
