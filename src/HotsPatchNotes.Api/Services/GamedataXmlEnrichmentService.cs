using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using HotsPatchNotes.Api.Data;
using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Service for enriching hero data from jamiephan/HeroesOfTheStorm_Gamedata raw XML files.
/// Provides Role, ExpandedRole, Type (Melee/Ranged), and ability Cooldown/Range/ManaCost.
/// Follows the same graceful-degradation pattern as S2MAHeroParserService.
/// </summary>
public sealed class GamedataXmlEnrichmentService(
    HotsDbContext dbContext,
    HttpClient httpClient,
    ILogger<GamedataXmlEnrichmentService> logger) : IGamedataXmlEnrichmentService
{
    private const string HeroListApiUrl =
        "https://api.github.com/repos/jamiephan/HeroesOfTheStorm_Gamedata/contents/mods/heroesdata.stormmod/base.stormdata/gamedata/heroes";

    private const string HeroXmlUrlPattern =
        "https://raw.githubusercontent.com/jamiephan/HeroesOfTheStorm_Gamedata/master/mods/heroesdata.stormmod/base.stormdata/gamedata/heroes/{0}data/{0}data.xml";

    // Mapping for heroes where GitHub folder name differs from database ShortName
    private static readonly Dictionary<string, string> HeroFolderToShortName =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["dva"] = "d.va",
            ["liming"] = "li-ming",
        };

    public async Task<SyncResultDto> EnrichHeroesFromGamedataAsync(CancellationToken cancellationToken = default)
    {
        var result = new SyncResultDto { SyncedAt = DateTime.UtcNow };
        var errors = new List<string>();
        var enrichedCount = 0;

        try
        {
            var heroFolders = await GetHeroFolderListAsync(cancellationToken);
            if (heroFolders.Count == 0)
            {
                errors.Add("No hero folders found in Gamedata repository");
                result.Errors = errors;
                return result;
            }

            logger.LogInformation("Found {Count} hero folders to enrich from Gamedata XML", heroFolders.Count);

            foreach (var folderName in heroFolders)
            {
                try
                {
                    var xmlContent = await DownloadHeroXmlAsync(folderName, cancellationToken);
                    if (string.IsNullOrWhiteSpace(xmlContent))
                        continue;

                    var gamedataInfo = ParseHeroXml(xmlContent);

                    var shortName = HeroFolderToShortName.TryGetValue(folderName, out var mapped)
                        ? mapped
                        : folderName.ToLowerInvariant();

                    var enriched = await EnrichHeroAsync(shortName, gamedataInfo, cancellationToken);
                    if (enriched)
                        enrichedCount++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error enriching hero from Gamedata XML: {FolderName}", folderName);
                    errors.Add($"Error enriching {folderName}: {ex.Message}");
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            result.HeroesUpdated = enrichedCount;
            result.Success = true;
            result.Message = $"Enriched {enrichedCount} heroes from Gamedata XML";
            result.Errors = errors;

            logger.LogInformation("Gamedata XML enrichment complete: {Count} heroes enriched", enrichedCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Critical error during Gamedata XML enrichment");
            errors.Add($"Critical error: {ex.Message}");
            result.Errors = errors;
        }

        return result;
    }

    private async Task<List<string>> GetHeroFolderListAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.GetStringAsync(HeroListApiUrl, cancellationToken);
            var items = JsonSerializer.Deserialize<List<GitHubFileInfo>>(response) ?? [];

            // Each hero has a folder named "{hero}data" — extract the hero name prefix
            return items
                .Where(i => i.Name.EndsWith("data", StringComparison.OrdinalIgnoreCase) && i.Type == "dir")
                .Select(i => i.Name[..^4]) // Remove trailing "data" suffix
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch hero folder list from Gamedata repository");
            return [];
        }
    }

    private async Task<string?> DownloadHeroXmlAsync(string folderName, CancellationToken cancellationToken)
    {
        try
        {
            var url = string.Format(HeroXmlUrlPattern, folderName);
            return await httpClient.GetStringAsync(url, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            logger.LogDebug("Hero XML not found for folder: {FolderName}", folderName);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to download Gamedata XML for: {FolderName}", folderName);
            return null;
        }
    }

    private GamedataHeroInfo ParseHeroXml(string xmlContent)
    {
        var info = new GamedataHeroInfo();

        try
        {
            var doc = XDocument.Parse(xmlContent);

            // Extract hero type (Melee/Ranged) from CHero elements
            var heroElement = doc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "CHero");

            if (heroElement is not null)
            {
                // Try to determine attack type from unit attributes
                var unitType = heroElement.Descendants("UnitCategory")
                    .FirstOrDefault()?.Attribute("value")?.Value
                    ?? heroElement.Attribute("unitCategory")?.Value;

                if (!string.IsNullOrWhiteSpace(unitType))
                {
                    if (unitType.Contains("Melee", StringComparison.OrdinalIgnoreCase))
                        info.AttackType = "Melee";
                    else if (unitType.Contains("Ranged", StringComparison.OrdinalIgnoreCase))
                        info.AttackType = "Ranged";
                }

                // Try to read role from descriptors/flags
                var roleElement = heroElement.Descendants("Role").FirstOrDefault();
                if (roleElement is not null)
                {
                    info.Role = roleElement.Attribute("value")?.Value;
                }
            }

            // Extract ability data from CAbil* elements
            var abilityElements = doc.Descendants()
                .Where(e => e.Name.LocalName.StartsWith("CAbil", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var abilElement in abilityElements)
            {
                var abilityId = abilElement.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(abilityId))
                    continue;

                // Cooldown: <Cost><Cooldown TimeUse="30" /></Cost>
                var cooldown = ParseDouble(abilElement
                    .Descendants("Cooldown")
                    .FirstOrDefault()?.Attribute("TimeUse")?.Value);

                // Range: <Range value="500" />
                var range = ParseDouble(abilElement
                    .Descendants("Range")
                    .FirstOrDefault()?.Attribute("value")?.Value);

                // ManaCost: <Cost><Vital type="Energy" value="40" /></Cost>
                var manaCostValue = abilElement.Descendants("Vital")
                    .FirstOrDefault(v =>
                        string.Equals(v.Attribute("index")?.Value ?? v.Attribute("type")?.Value, "Energy", StringComparison.OrdinalIgnoreCase))?
                    .Attribute("value")?.Value;
                var manaCost = ParseDouble(manaCostValue);

                if (cooldown.HasValue || range.HasValue || manaCost.HasValue)
                {
                    info.Abilities.Add(new GamedataAbilityInfo
                    {
                        AbilityId = abilityId,
                        Cooldown = cooldown,
                        Range = range,
                        ManaCost = manaCost,
                    });
                }
            }

            logger.LogDebug("Parsed Gamedata XML: {Abilities} abilities with data", info.Abilities.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse Gamedata hero XML");
        }

        return info;
    }

    private async Task<bool> EnrichHeroAsync(
        string heroShortName,
        GamedataHeroInfo gamedataInfo,
        CancellationToken cancellationToken)
    {
        var hero = await dbContext.Heroes
            .Include(h => h.Abilities)
            .FirstOrDefaultAsync(h => h.ShortName == heroShortName, cancellationToken);

        if (hero is null)
        {
            logger.LogDebug("Hero not found for Gamedata enrichment: {ShortName} (skipping)", heroShortName);
            return false;
        }

        // Enrich hero-level classification fields (null-coalescing — preserve heroes-talents values)
        if (!string.IsNullOrWhiteSpace(gamedataInfo.Role))
            hero.Role ??= gamedataInfo.Role;

        if (!string.IsNullOrWhiteSpace(gamedataInfo.ExpandedRole))
            hero.ExpandedRole ??= gamedataInfo.ExpandedRole;

        if (!string.IsNullOrWhiteSpace(gamedataInfo.AttackType))
            hero.Type ??= gamedataInfo.AttackType;

        // Enrich ability mechanical fields (null-coalescing — Gamedata provides baseline values)
        foreach (var gamedataAbility in gamedataInfo.Abilities)
        {
            var dbAbility = hero.Abilities
                .FirstOrDefault(a =>
                    a.AbilityId == gamedataAbility.AbilityId ||
                    a.Uid == gamedataAbility.AbilityId);

            if (dbAbility is null)
                continue;

            if (gamedataAbility.Cooldown.HasValue)
                dbAbility.Cooldown ??= gamedataAbility.Cooldown;

            if (gamedataAbility.Range.HasValue)
                dbAbility.Range ??= gamedataAbility.Range.Value.ToString("0.##");

            if (gamedataAbility.ManaCost.HasValue)
                dbAbility.ManaCost ??= gamedataAbility.ManaCost.Value.ToString("0.##");
        }

        logger.LogDebug("Enriched {ShortName} with Gamedata XML ({Abilities} abilities processed)",
            heroShortName, gamedataInfo.Abilities.Count);

        return true;
    }

    private static double? ParseDouble(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        return double.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private sealed class GitHubFileInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    private sealed class GamedataHeroInfo
    {
        public string? Role { get; set; }
        public string? ExpandedRole { get; set; }
        public string? AttackType { get; set; }
        public List<GamedataAbilityInfo> Abilities { get; set; } = [];
    }

    private sealed class GamedataAbilityInfo
    {
        public string AbilityId { get; set; } = string.Empty;
        public double? Cooldown { get; set; }
        public double? Range { get; set; }
        public double? ManaCost { get; set; }
    }
}
