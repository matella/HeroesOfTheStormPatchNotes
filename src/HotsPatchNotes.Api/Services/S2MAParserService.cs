using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using HotsPatchNotes.Api.Data;
using HotsPatchNotes.Shared.Models;
using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Service for parsing battleground data from S2MA map files in jamiephan/HeroesOfTheStorm_S2MA repository.
/// Provides authoritative source for battleground metadata with fallback to wiki scraping for rich descriptions.
/// </summary>
public sealed partial class S2MAParserService(
    HotsDbContext dbContext,
    HttpClient httpClient,
    ILogger<S2MAParserService> logger,
    IBattlegroundScraper battlegroundScraper,
    IHtmlContentService htmlContentService,
    IImageDownloadService imageDownloadService) : IS2MAParserService
{
    private const string S2MARepoUrl = "https://api.github.com/repos/jamiephan/HeroesOfTheStorm_S2MA/contents";
    private const string S2MARawBaseUrl = "https://raw.githubusercontent.com/jamiephan/HeroesOfTheStorm_S2MA/master";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // Map of S2MA file names to battleground display names
    private static readonly Dictionary<string, BattlegroundMetadata> S2MABattlegroundMap = new()
    {
        ["alteracpass.s2ma"] = new("Alterac Pass", "alterac-pass", "3-Lane", "Warcraft"),
        ["battlefieldofeternity.s2ma"] = new("Battlefield of Eternity", "battlefield-of-eternity", "2-Lane", "Diablo"),
        ["blackheartsbay.s2ma"] = new("Blackheart's Bay", "blackhearts-bay", "3-Lane", "Nexus"),
        ["braxisholdout.s2ma"] = new("Braxis Holdout", "braxis-holdout", "2-Lane", "StarCraft"),
        ["cursedhollow.s2ma"] = new("Cursed Hollow", "cursed-hollow", "3-Lane", "Raven Lord"),
        ["dragonshire.s2ma"] = new("Dragon Shire", "dragon-shire", "3-Lane", "Nexus"),
        ["gardensofterror.s2ma"] = new("Garden of Terror", "garden-of-terror", "3-Lane", "Nexus"),
        ["hanamura.s2ma"] = new("Hanamura Temple", "hanamura-temple", "2-Lane", "Overwatch"),
        ["hauntedmines.s2ma"] = new("Haunted Mines", "haunted-mines", "2-Lane", "Raven Lord"),
        ["infernalshrines.s2ma"] = new("Infernal Shrines", "infernal-shrines", "3-Lane", "Diablo"),
        ["skytemple.s2ma"] = new("Sky Temple", "sky-temple", "3-Lane", "Luxoria"),
        ["tombofthespiderqueen.s2ma"] = new("Tomb of the Spider Queen", "tomb-of-the-spider-queen", "3-Lane", "Luxoria"),
        ["towersofdoom.s2ma"] = new("Towers of Doom", "towers-of-doom", "3-Lane", "Raven Lord"),
        ["volskayafoundry.s2ma"] = new("Volskaya Foundry", "volskaya-foundry", "2-Lane", "Overwatch"),
        ["warheadjunction.s2ma"] = new("Warhead Junction", "warhead-junction", "3-Lane", "StarCraft"),
    };

    public async Task<SyncResultDto> SyncBattlegroundsFromS2MAAsync(CancellationToken cancellationToken = default)
    {
        var result = new SyncResultDto { SyncedAt = DateTime.UtcNow };

        try
        {
            logger.LogInformation("Starting battleground sync from S2MA repository");

            // Get list of S2MA files
            var s2maFiles = await GetS2MAFileListAsync(cancellationToken);
            logger.LogInformation("Found {Count} S2MA map files", s2maFiles.Count);

            var syncedCount = 0;

            foreach (var file in s2maFiles)
            {
                try
                {
                    var fileName = file.Name.ToLowerInvariant();

                    // Check if this is a known battleground
                    if (!S2MABattlegroundMap.TryGetValue(fileName, out var metadata))
                    {
                        logger.LogDebug("Skipping unknown S2MA file: {FileName}", file.Name);
                        continue;
                    }

                    logger.LogInformation("Processing battleground: {Name} from S2MA", metadata.Name);

                    // Check if battleground already exists
                    var existing = await dbContext.Battlegrounds
                        .FirstOrDefaultAsync(b => b.ShortName == metadata.ShortName, cancellationToken);

                    // Hybrid approach: Use S2MA for authoritative metadata, wiki for rich descriptions
                    BattlegroundDetailInfo? wikiDetails = null;
                    try
                    {
                        var wikiUrl = battlegroundScraper.GetWikiUrl(metadata.Name);
                        wikiDetails = await battlegroundScraper.GetBattlegroundDetailsAsync(wikiUrl, cancellationToken);

                        // Rate limiting between wiki requests
                        await Task.Delay(2000, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to fetch wiki data for {BattlegroundName}, using S2MA data only", metadata.Name);
                    }

                    // Download battleground image if available from wiki
                    string? localImagePath = null;
                    var sourceImageUrl = wikiDetails?.FullImageUrl;
                    if (!string.IsNullOrEmpty(sourceImageUrl))
                    {
                        localImagePath = await imageDownloadService.DownloadImageAsync(
                            sourceImageUrl,
                            "battlegrounds",
                            metadata.ShortName,
                            cancellationToken);
                    }

                    var imageUrl = localImagePath ?? sourceImageUrl;

                    if (existing is null)
                    {
                        // Create new battleground with S2MA metadata + wiki details
                        var battleground = new Battleground
                        {
                            ShortName = metadata.ShortName,
                            Name = metadata.Name,
                            MapType = metadata.MapType,
                            Universe = metadata.Universe,
                            Description = wikiDetails is not null ? htmlContentService.SanitizeHtml(wikiDetails.Description ?? "") : null,
                            Objective = wikiDetails is not null ? htmlContentService.SanitizeHtml(wikiDetails.ObjectiveDetails ?? "") : null,
                            ObjectiveTiming = wikiDetails?.ObjectiveTiming,
                            MercCamps = wikiDetails is not null ? htmlContentService.SanitizeHtml(wikiDetails.MercCamps ?? "") : null,
                            BossInfo = wikiDetails is not null ? htmlContentService.SanitizeHtml(wikiDetails.BossInfo ?? "") : null,
                            Tips = wikiDetails is not null ? htmlContentService.SanitizeHtml(wikiDetails.Tips ?? "") : null,
                            ImageUrl = imageUrl,
                            IsInRotation = true
                        };

                        dbContext.Battlegrounds.Add(battleground);
                        logger.LogInformation("Added new battleground: {Name} (source: S2MA + wiki)", metadata.Name);
                    }
                    else
                    {
                        // Update existing battleground - S2MA data takes precedence for core metadata
                        existing.Name = metadata.Name;
                        existing.MapType = metadata.MapType;
                        existing.Universe = metadata.Universe;

                        // Only update wiki fields if we successfully fetched wiki data
                        if (wikiDetails is not null)
                        {
                            existing.Description = htmlContentService.SanitizeHtml(wikiDetails.Description ?? existing.Description ?? "");
                            existing.Objective = htmlContentService.SanitizeHtml(wikiDetails.ObjectiveDetails ?? existing.Objective ?? "");
                            existing.ObjectiveTiming = wikiDetails.ObjectiveTiming ?? existing.ObjectiveTiming;
                            existing.MercCamps = htmlContentService.SanitizeHtml(wikiDetails.MercCamps ?? existing.MercCamps ?? "");
                            existing.BossInfo = htmlContentService.SanitizeHtml(wikiDetails.BossInfo ?? existing.BossInfo ?? "");
                            existing.Tips = htmlContentService.SanitizeHtml(wikiDetails.Tips ?? existing.Tips ?? "");
                        }

                        existing.ImageUrl = imageUrl ?? existing.ImageUrl;

                        logger.LogInformation("Updated battleground: {Name} (source: S2MA + wiki)", metadata.Name);
                    }

                    syncedCount++;

                    // Save changes after each battleground to avoid UNIQUE constraint violations
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to sync battleground from S2MA: {FileName}", file.Name);
                    result.Errors.Add($"Failed to sync {file.Name}: {ex.Message}");
                }
            }

            result.Success = true;
            result.HeroesUpdated = syncedCount; // Reuse counter for battlegrounds
            result.Message = $"Synced {syncedCount} battlegrounds from S2MA";
            logger.LogInformation("S2MA battleground sync complete: {Count} synced", syncedCount);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"S2MA battleground sync failed: {ex.Message}");
            logger.LogError(ex, "S2MA battleground sync failed");
        }

        return result;
    }

    /// <summary>
    /// Gets the list of S2MA files from the repository.
    /// </summary>
    private async Task<List<GitHubFileInfo>> GetS2MAFileListAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.GetStringAsync(S2MARepoUrl, cancellationToken);
            var files = JsonSerializer.Deserialize<List<GitHubFileInfo>>(response, JsonOptions);

            if (files is null) return [];

            // Filter for .s2ma files only
            return files
                .Where(f => f.Name.EndsWith(".s2ma", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list S2MA files");
            return [];
        }
    }

    #region Data Models

    private record BattlegroundMetadata(
        string Name,
        string ShortName,
        string MapType,
        string Universe);

    private class GitHubFileInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
    }

    #endregion
}
