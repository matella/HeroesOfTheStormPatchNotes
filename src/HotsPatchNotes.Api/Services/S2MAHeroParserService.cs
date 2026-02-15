using System.Text.Json;
using System.Xml.Linq;
using HotsPatchNotes.Api.Data;
using HotsPatchNotes.Shared.DTOs;
using HotsPatchNotes.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Nmpq;

namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Service for parsing hero data directly from S2MA .stormmod MPQ archives
/// </summary>
public sealed class S2MAHeroParserService(
    HotsDbContext dbContext,
    HttpClient httpClient,
    ILogger<S2MAHeroParserService> logger) : IS2MAHeroParserService
{
    private const string S2MARepoContentsUrl = "https://api.github.com/repos/jamiephan/HeroesOfTheStorm_S2MA/contents/mods/heromods";
    private const string S2MARawBaseUrl = "https://raw.githubusercontent.com/jamiephan/HeroesOfTheStorm_S2MA/master/";

    // Mapping for heroes where S2MA filename differs from database ShortName
    private static readonly Dictionary<string, string> HeroNameMapping = new()
    {
        ["dva"] = "d.va",
        ["liming"] = "li-ming"
    };

    // Tier to level mapping (S2MA uses 0-6 for tiers, HotS uses levels 1,4,7,10,13,16,20)
    private static readonly int[] TierLevels = [1, 4, 7, 10, 13, 16, 20];

    /// <inheritdoc/>
    public async Task<SyncResultDto> SyncHeroesFromS2MAAsync(CancellationToken cancellationToken = default)
    {
        var result = new SyncResultDto();
        var errors = new List<string>();

        try
        {
            // Get list of .stormmod files from GitHub
            var stormModFiles = await GetHeroModFileListAsync(cancellationToken);
            if (stormModFiles.Count == 0)
            {
                errors.Add("No .stormmod files found in S2MA repository");
                result.Errors = errors;
                return result;
            }

            logger.LogInformation("Found {Count} .stormmod files to parse", stormModFiles.Count);

            var successCount = 0;

            // Process each .stormmod file
            foreach (var fileInfo in stormModFiles)
            {
                try
                {
                    // Extract hero short name from filename (e.g., "abathur.stormmod" -> "abathur")
                    var fileName = Path.GetFileNameWithoutExtension(fileInfo.Name);
                    var heroShortName = fileName.ToLowerInvariant();

                    // Apply name mapping if needed
                    if (HeroNameMapping.TryGetValue(heroShortName, out var mappedName))
                    {
                        heroShortName = mappedName;
                    }

                    // Download .stormmod file
                    var stormModData = await DownloadStormModAsync(fileInfo.Path, cancellationToken);
                    if (stormModData.Length == 0)
                    {
                        logger.LogWarning("Failed to download {FileName}", fileInfo.Name);
                        errors.Add($"Failed to download {fileInfo.Name}");
                        continue;
                    }

                    // Parse hero data from MPQ archive
                    var heroData = ParseHeroDataFromMpq(stormModData, heroShortName);
                    if (heroData.Abilities.Count == 0 && heroData.Talents.Count == 0)
                    {
                        logger.LogWarning("No data extracted from {FileName}", fileInfo.Name);
                        continue; // Not necessarily an error - hero might not have extractable data
                    }

                    // Enrich existing hero in database
                    await EnrichHeroWithS2MADataAsync(heroShortName, heroData, cancellationToken);
                    successCount++;

                    logger.LogDebug("Successfully processed {HeroShortName} ({Abilities} abilities, {Talents} talents)",
                        heroShortName, heroData.Abilities.Count, heroData.Talents.Count);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error processing {FileName}", fileInfo.Name);
                    errors.Add($"Error processing {fileInfo.Name}: {ex.Message}");
                }
            }

            // Save all changes in a single batch
            await dbContext.SaveChangesAsync(cancellationToken);

            result.HeroesUpdated = successCount;
            result.Success = successCount > 0;
            result.Message = $"Successfully synced {successCount} heroes from S2MA .stormmod files";
            result.Errors = errors;

            logger.LogInformation("S2MA hero sync completed: {Success}/{Total} heroes updated",
                successCount, stormModFiles.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Critical error during S2MA hero sync");
            errors.Add($"Critical error: {ex.Message}");
            result.Errors = errors;
        }

        return result;
    }

    /// <summary>
    /// Gets list of .stormmod files from GitHub API
    /// </summary>
    private async Task<List<GitHubFileInfo>> GetHeroModFileListAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.GetStringAsync(S2MARepoContentsUrl, cancellationToken);
            var files = JsonSerializer.Deserialize<List<GitHubFileInfo>>(response) ?? [];

            // Filter for .stormmod files only
            return files.Where(f => f.Name.EndsWith(".stormmod", StringComparison.OrdinalIgnoreCase)).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch .stormmod file list from GitHub");
            return [];
        }
    }

    /// <summary>
    /// Downloads a .stormmod file from GitHub
    /// </summary>
    private async Task<byte[]> DownloadStormModAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var url = S2MARawBaseUrl + filePath;
            return await httpClient.GetByteArrayAsync(url, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download .stormmod file: {FilePath}", filePath);
            return [];
        }
    }

    /// <summary>
    /// Parses hero data from MPQ archive
    /// </summary>
    private HeroS2MAData ParseHeroDataFromMpq(byte[] mpqData, string heroShortName)
    {
        try
        {
            using var archive = MpqArchive.Open(mpqData);

            // Look for hero data XML file: base.stormdata/gamedata/{heroShortName}data.xml
            var xmlPath = $"base.stormdata/gamedata/{heroShortName}data.xml";

            // Try to find the file (case-insensitive search as MPQ paths can vary)
            string? foundPath = null;
            foreach (var file in archive.KnownFiles)
            {
                if (file.Equals(xmlPath, StringComparison.OrdinalIgnoreCase))
                {
                    foundPath = file;
                    break;
                }
            }

            if (foundPath is null)
            {
                logger.LogWarning("Hero data XML not found in MPQ: {XmlPath}", xmlPath);
                return new HeroS2MAData();
            }

            var xmlBytes = archive.ReadFile(foundPath);
            var xmlContent = System.Text.Encoding.UTF8.GetString(xmlBytes);

            return ParseHeroXml(xmlContent);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse MPQ archive for hero: {HeroShortName}", heroShortName);
            return new HeroS2MAData();
        }
    }

    /// <summary>
    /// Parses hero XML to extract abilities and talents
    /// </summary>
    private HeroS2MAData ParseHeroXml(string xmlContent)
    {
        var data = new HeroS2MAData();

        try
        {
            var doc = XDocument.Parse(xmlContent);

            // Extract abilities - various ability types in S2MA
            var abilityElements = doc.Descendants()
                .Where(e => e.Name.LocalName.StartsWith("CAbil", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var abilElement in abilityElements)
            {
                var abilityId = abilElement.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(abilityId))
                    continue;

                var ability = new S2MAAbility
                {
                    AbilityId = abilityId,
                    Cooldown = ParseDouble(abilElement.Element("Cooldown")?.Attribute("TimeUse")?.Value),
                    Range = ParseDouble(abilElement.Element("Range")?.Attribute("value")?.Value),
                    ManaCost = ParseDouble(abilElement.Descendants("Vital")
                        .FirstOrDefault(v => v.Attribute("index")?.Value == "Energy")?
                        .Attribute("value")?.Value)
                };

                data.Abilities.Add(ability);
            }

            // Extract talents
            var talentElements = doc.Descendants()
                .Where(e => e.Name.LocalName == "CTalent")
                .ToList();

            foreach (var talentElement in talentElements)
            {
                var talentId = talentElement.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(talentId))
                    continue;

                var tierValue = talentElement.Element("Tier")?.Attribute("value")?.Value;
                var level = ParseTier(tierValue);
                if (level == 0)
                    continue; // Invalid tier

                var talent = new S2MATalent
                {
                    TalentId = talentId,
                    Level = level,
                    AbilityLink = talentElement.Element("AbilityLink")?.Attribute("value")?.Value,
                    Cooldown = ParseDouble(talentElement.Element("Cooldown")?.Attribute("TimeUse")?.Value)
                };

                data.Talents.Add(talent);
            }

            logger.LogDebug("Parsed XML: {Abilities} abilities, {Talents} talents",
                data.Abilities.Count, data.Talents.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse hero XML");
        }

        return data;
    }

    /// <summary>
    /// Enriches existing hero in database with S2MA data
    /// </summary>
    private async Task EnrichHeroWithS2MADataAsync(
        string heroShortName,
        HeroS2MAData s2maData,
        CancellationToken cancellationToken)
    {
        // Load hero with abilities and talents
        var hero = await dbContext.Heroes
            .Include(h => h.Abilities)
            .Include(h => h.Talents)
            .FirstOrDefaultAsync(h => h.ShortName == heroShortName, cancellationToken);

        if (hero is null)
        {
            logger.LogWarning("Hero not found in database: {HeroShortName} - will be created by heroes-talents fallback", heroShortName);
            return;
        }

        // Enrich abilities (update mechanical fields only, preserve presentation data)
        foreach (var s2maAbility in s2maData.Abilities)
        {
            var dbAbility = hero.Abilities
                .FirstOrDefault(a => a.AbilityId == s2maAbility.AbilityId || a.Uid == s2maAbility.AbilityId);

            if (dbAbility is not null)
            {
                // Update mechanical fields only
                if (s2maAbility.Cooldown.HasValue)
                    dbAbility.Cooldown = s2maAbility.Cooldown.Value;

                if (s2maAbility.ManaCost.HasValue)
                    dbAbility.ManaCost = s2maAbility.ManaCost.Value.ToString("0.##");

                if (s2maAbility.Range.HasValue)
                    dbAbility.Range = s2maAbility.Range.Value.ToString("0.##");

                logger.LogDebug("Updated ability {AbilityId} for {HeroShortName}", s2maAbility.AbilityId, heroShortName);
            }
        }

        // Enrich talents (update mechanical fields only, preserve presentation data)
        foreach (var s2maTalent in s2maData.Talents)
        {
            // Try to match by TalentTreeId first, then by Level (for talents without matching IDs)
            var dbTalent = hero.Talents
                .FirstOrDefault(t => t.TalentTreeId == s2maTalent.TalentId)
                ?? hero.Talents.FirstOrDefault(t => t.Level == s2maTalent.Level);

            if (dbTalent is not null)
            {
                // Update mechanical fields only
                if (s2maTalent.Cooldown.HasValue)
                    dbTalent.Cooldown = s2maTalent.Cooldown.Value;

                if (!string.IsNullOrEmpty(s2maTalent.AbilityLink))
                    dbTalent.AbilityId = s2maTalent.AbilityLink;

                logger.LogDebug("Updated talent {TalentId} (Level {Level}) for {HeroShortName}",
                    s2maTalent.TalentId, s2maTalent.Level, heroShortName);
            }
        }

        // Update last synced timestamp
        hero.LastSyncedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Parses a double value safely
    /// </summary>
    private static double? ParseDouble(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        return double.TryParse(value, out var result) ? result : null;
    }

    /// <summary>
    /// Converts S2MA tier (0-6) to HotS talent level (1,4,7,10,13,16,20)
    /// </summary>
    private static int ParseTier(string? tierValue)
    {
        if (string.IsNullOrEmpty(tierValue))
            return 0;

        if (!int.TryParse(tierValue, out var tier))
            return 0;

        if (tier < 0 || tier >= TierLevels.Length)
            return 0;

        return TierLevels[tier];
    }

    /// <summary>
    /// GitHub API file info model
    /// </summary>
    private sealed class GitHubFileInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }

    /// <summary>
    /// Container for parsed S2MA hero data
    /// </summary>
    private sealed class HeroS2MAData
    {
        public List<S2MAAbility> Abilities { get; set; } = [];
        public List<S2MATalent> Talents { get; set; } = [];
    }

    /// <summary>
    /// Parsed S2MA ability data
    /// </summary>
    private sealed class S2MAAbility
    {
        public string AbilityId { get; set; } = string.Empty;
        public double? Cooldown { get; set; }
        public double? ManaCost { get; set; }
        public double? Range { get; set; }
    }

    /// <summary>
    /// Parsed S2MA talent data
    /// </summary>
    private sealed class S2MATalent
    {
        public string TalentId { get; set; } = string.Empty;
        public int Level { get; set; }
        public string? AbilityLink { get; set; }
        public double? Cooldown { get; set; }
    }
}
