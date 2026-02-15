using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using HotsPatchNotes.Api.Data;
using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Service for syncing comprehensive hero data from HeroesToolChest/heroes-data repository.
/// Supplements and enhances the base hero data from heroes-talents with detailed game mechanics.
/// </summary>
public sealed partial class HeroesDataSyncService(
    HotsDbContext dbContext,
    HttpClient httpClient,
    IGamestringsParser gamestringsParser,
    ILogger<HeroesDataSyncService> logger) : IHeroesDataSyncService
{
    private const string HeroesDataRepoUrl = "https://api.github.com/repos/HeroesToolChest/heroes-data/contents";
    private const string HeroesDataRawBaseUrl = "https://raw.githubusercontent.com/HeroesToolChest/heroes-data/main";

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
            logger.LogInformation("Starting heroes-data sync from HeroesToolChest repository");

            // Find the latest herodata localized file
            var latestFile = await FindLatestHeroDataFileAsync(cancellationToken);
            if (string.IsNullOrEmpty(latestFile))
            {
                result.Errors.Add("No herodata files found in heroes-data repository");
                logger.LogWarning("No herodata files found");
                return result;
            }

            logger.LogInformation("Found latest heroes-data file: {FileName}", latestFile);

            // Download and parse the file
            var fileUrl = $"{HeroesDataRawBaseUrl}/{latestFile}";
            var jsonContent = await httpClient.GetStringAsync(fileUrl, cancellationToken);
            var heroesData = JsonSerializer.Deserialize<Dictionary<string, HeroesDataHero>>(jsonContent, JsonOptions);

            if (heroesData is null || heroesData.Count == 0)
            {
                result.Errors.Add("Failed to parse heroes-data JSON or no heroes found");
                return result;
            }

            logger.LogInformation("Parsed {Count} heroes from heroes-data", heroesData.Count);

            // Extract build number and fetch gamestrings
            Dictionary<string, GamestringEntry>? gamestrings = null;
            try
            {
                var buildNumber = ExtractBuildNumber(latestFile);
                logger.LogDebug("Extracted build number: {Build}", buildNumber);

                gamestrings = await gamestringsParser.FetchAndParseGamestringsAsync(
                    buildNumber,
                    "enus",
                    cancellationToken);

                if (gamestrings is not null)
                {
                    logger.LogInformation("Fetched {Count} gamestring entries for build {Build}",
                        gamestrings.Count, buildNumber);
                }
                else
                {
                    logger.LogWarning("No gamestrings fetched for build {Build}, continuing without them", buildNumber);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch gamestrings, continuing without them");
            }

            // Enrich existing heroes with heroes-data
            foreach (var (heroId, heroData) in heroesData)
            {
                try
                {
                    await EnrichHeroWithHeroesDataAsync(heroId, heroData, gamestrings, cancellationToken);
                    result.HeroesUpdated++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to enrich hero with heroes-data: {HeroId}", heroId);
                    result.Errors.Add($"Failed to enrich {heroId}: {ex.Message}");
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            result.Success = true;
            result.Message = $"Enriched {result.HeroesUpdated} heroes with heroes-data";
            logger.LogInformation("Heroes-data sync complete: {Count} heroes enriched", result.HeroesUpdated);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync heroes-data");
            result.Errors.Add($"Heroes-data sync failed: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Finds the latest herodata localized JSON file in the repository.
    /// </summary>
    private async Task<string?> FindLatestHeroDataFileAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.GetStringAsync(HeroesDataRepoUrl, cancellationToken);
            var files = JsonSerializer.Deserialize<List<GitHubFileInfo>>(response, JsonOptions);

            if (files is null) return null;

            // Find files matching pattern: herodata_<version>_localized.json
            var heroDataFiles = files
                .Where(f => f.Name.StartsWith("herodata_") && f.Name.EndsWith("_localized.json"))
                .OrderByDescending(f => f.Name) // Latest version should sort last
                .ToList();

            return heroDataFiles.FirstOrDefault()?.Name;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list heroes-data files");
            return null;
        }
    }

    /// <summary>
    /// Enriches an existing hero in the database with comprehensive data from heroes-data.
    /// Only updates fields that are null or need enhancement - doesn't overwrite existing data.
    /// </summary>
    private async Task EnrichHeroWithHeroesDataAsync(
        string heroId,
        HeroesDataHero heroData,
        Dictionary<string, GamestringEntry>? gamestrings,
        CancellationToken cancellationToken)
    {
        // Try to match hero by HyperlinkId or ShortName
        var hero = await dbContext.Heroes
            .Include(h => h.Abilities)
            .Include(h => h.Talents)
            .FirstOrDefaultAsync(h =>
                h.HyperlinkId == heroId ||
                h.ShortName == heroId.ToLowerInvariant(),
                cancellationToken);

        if (hero is null)
        {
            logger.LogDebug("Hero not found for heroes-data ID: {HeroId} (skipping)", heroId);
            return;
        }

        logger.LogDebug("Enriching hero {HeroName} with heroes-data", hero.Name);

        // Enrich hero-level stats
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

        // Enrich weapon/attack data
        if (heroData.Weapons is not null && heroData.Weapons.Count > 0)
        {
            var primaryWeapon = heroData.Weapons.FirstOrDefault().Value;
            if (primaryWeapon is not null)
            {
                hero.AttackDamageScaling ??= primaryWeapon.DamageScaling;
                hero.AttackRange ??= primaryWeapon.Range;
                hero.AttackSpeed ??= primaryWeapon.AttackSpeed;
            }
        }

        // Enrich abilities with heroes-data
        if (heroData.Abilities is not null)
        {
            foreach (var (abilityId, abilityData) in heroData.Abilities)
            {
                var matchingAbility = hero.Abilities.FirstOrDefault(a =>
                    a.AbilityId == abilityId ||
                    a.Uid == abilityId ||
                    a.Name.Equals(abilityData.Name, StringComparison.OrdinalIgnoreCase));

                if (matchingAbility is not null)
                {
                    matchingAbility.IsPassive ??= abilityData.IsPassive;
                    matchingAbility.LifeCost ??= abilityData.LifeCost;
                    matchingAbility.IsToggle ??= abilityData.IsToggle;

                    if (abilityData.Charges is not null)
                    {
                        matchingAbility.ChargesMax ??= abilityData.Charges.CountMax;
                        matchingAbility.RechargeTime ??= abilityData.Charges.RecastCooldown;
                    }
                }
            }
        }

        // Enrich talents with heroes-data
        if (heroData.Talents is not null)
        {
            foreach (var (talentId, talentData) in heroData.Talents)
            {
                var matchingTalent = hero.Talents.FirstOrDefault(t =>
                    t.TalentTreeId == talentId ||
                    t.TooltipId == talentId ||
                    t.Name.Equals(talentData.Name, StringComparison.OrdinalIgnoreCase));

                if (matchingTalent is not null)
                {
                    matchingTalent.IsQuest ??= talentData.IsQuest;

                    if (talentData.AbilityTalentLinkIds is not null && talentData.AbilityTalentLinkIds.Count > 0)
                    {
                        matchingTalent.AbilityTalentLinkIdsJson ??= JsonSerializer.Serialize(talentData.AbilityTalentLinkIds);
                    }

                    matchingTalent.IsStackable ??= talentData.IsStackable;
                }
            }
        }

        // Apply gamestrings to talents (after heroes-data enrichment)
        if (gamestrings is not null && hero.Talents.Any())
        {
            try
            {
                var matchCount = gamestringsParser.ApplyGamestringsToTalents(
                    hero.Talents,
                    gamestrings);

                logger.LogDebug("Matched {Count}/{Total} talents to gamestrings for {Hero}",
                    matchCount, hero.Talents.Count, hero.Name);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to apply gamestrings to {Hero}", hero.Name);
            }
        }

        hero.LastSyncedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Extracts the build number from a herodata file name.
    /// Example: "herodata_76003_localized.json" → "76003"
    /// </summary>
    [GeneratedRegex(@"_(\d+)_")]
    private static partial Regex BuildNumberRegex();

    private static string ExtractBuildNumber(string fileName)
    {
        var match = BuildNumberRegex().Match(fileName);
        if (!match.Success)
        {
            throw new ArgumentException($"Invalid herodata file name format: {fileName}", nameof(fileName));
        }

        return match.Groups[1].Value;
    }

    #region Data Models for heroes-data JSON

    private class GitHubFileInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    private class HeroesDataHero
    {
        public string? Name { get; set; }
        public string? HyperlinkId { get; set; }
        public HeroesDataLife? Life { get; set; }
        public HeroesDataEnergy? Energy { get; set; }
        public HeroesDataShield? Shield { get; set; }
        public double? Speed { get; set; }
        public double? Sight { get; set; }
        public Dictionary<string, HeroesDataWeapon>? Weapons { get; set; }
        public Dictionary<string, HeroesDataAbility>? Abilities { get; set; }
        public Dictionary<string, HeroesDataTalent>? Talents { get; set; }
    }

    private class HeroesDataLife
    {
        public double? LifeMax { get; set; }
        public double? LifeRegenRate { get; set; }
        public double? LifeScaling { get; set; }
        public double? LifeRegenRateScaling { get; set; }
    }

    private class HeroesDataEnergy
    {
        public double? EnergyMax { get; set; }
        public double? EnergyRegenRate { get; set; }
    }

    private class HeroesDataShield
    {
        public double? ShieldMax { get; set; }
        public double? ShieldRegenRate { get; set; }
        public double? ShieldRegenDelay { get; set; }
    }

    private class HeroesDataWeapon
    {
        public double? Range { get; set; }
        public double? DamageScaling { get; set; }
        public double? AttackSpeed { get; set; }
    }

    private class HeroesDataAbility
    {
        public string? Name { get; set; }
        public bool? IsPassive { get; set; }
        public double? LifeCost { get; set; }
        public bool? IsToggle { get; set; }
        public HeroesDataCharges? Charges { get; set; }
    }

    private class HeroesDataCharges
    {
        public int? CountMax { get; set; }
        public double? RecastCooldown { get; set; }
    }

    private class HeroesDataTalent
    {
        public string? Name { get; set; }
        public bool? IsQuest { get; set; }
        public List<string>? AbilityTalentLinkIds { get; set; }
        public bool? IsStackable { get; set; }
    }

    #endregion
}
