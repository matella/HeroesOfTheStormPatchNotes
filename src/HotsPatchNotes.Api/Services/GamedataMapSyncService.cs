using Microsoft.EntityFrameworkCore;
using HotsPatchNotes.Api.Data;
using HotsPatchNotes.Shared.Models;
using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Service that syncs battleground metadata from gamestrings.txt files in the
/// jamiephan/HeroesOfTheStorm_Gamedata repository. Provides authoritative source
/// for battleground names, objectives, and descriptions parsed directly from game client data.
/// </summary>
public sealed class GamedataMapSyncService(
    HotsDbContext dbContext,
    HttpClient httpClient,
    ILogger<GamedataMapSyncService> logger) : IGamedataMapSyncService
{
    private const string MainGamestringsUrl =
        "https://raw.githubusercontent.com/jamiephan/HeroesOfTheStorm_Gamedata/master/" +
        "mods/heroesdata.stormmod/enus.stormdata/localizeddata/gamestrings.txt";

    private const string AlteracPassGamestringsUrl =
        "https://raw.githubusercontent.com/jamiephan/HeroesOfTheStorm_Gamedata/master/" +
        "mods/heroesmapmods/battlegroundmapmods/alteracpass.stormmod/enus.stormdata/localizeddata/gamestrings.txt";

    private const string LoadingScreenPrefix = "UI/MapLoadingScreen/";

    /// <summary>
    /// Maps gamestrings key → (ShortName, MapType, Universe) for all 15 competitive battlegrounds.
    /// Braxis Holdout uses key "HoldOut" and Alterac Pass uses key "AlteracValley" in their respective files.
    /// </summary>
    private static readonly Dictionary<string, MapStaticMetadata> KnownMaps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AlteracValley"]         = new("alterac-pass",            "3-Lane", "Warcraft"),
        ["BattlefieldOfEternity"] = new("battlefield-of-eternity", "2-Lane", "Diablo"),
        ["BlackheartsBay"]        = new("blackhearts-bay",         "3-Lane", "Nexus"),
        ["HoldOut"]               = new("braxis-holdout",          "2-Lane", "StarCraft"),
        ["CursedHollow"]          = new("cursed-hollow",           "3-Lane", "Raven Lord"),
        ["Dragonshire"]           = new("dragon-shire",            "3-Lane", "Nexus"),
        ["GardenOfTerror"]        = new("garden-of-terror",        "3-Lane", "Nexus"),
        ["Hanamura"]              = new("hanamura-temple",         "2-Lane", "Overwatch"),
        ["HauntedMines"]          = new("haunted-mines",           "2-Lane", "Raven Lord"),
        ["InfernalShrines"]       = new("infernal-shrines",        "3-Lane", "Diablo"),
        ["LostCavern"]            = new("lost-cavern",             "1-Lane", "Nexus"),
        ["SkyTemple"]             = new("sky-temple",              "3-Lane", "Luxoria"),
        ["TombOfTheSpiderQueen"]  = new("tomb-of-the-spider-queen","3-Lane", "Luxoria"),
        ["Volskaya"]              = new("volskaya-foundry",        "2-Lane", "Overwatch"),
        ["WarheadJunction"]       = new("warhead-junction",        "3-Lane", "StarCraft"),
    };

    /// <inheritdoc/>
    public async Task<SyncResultDto> SyncBattlegroundsFromGamedataAsync(CancellationToken cancellationToken = default)
    {
        var result = new SyncResultDto { SyncedAt = DateTime.UtcNow };

        try
        {
            logger.LogInformation("Starting battleground sync from Gamedata gamestrings");

            // Fetch both gamestrings files; failures return empty arrays gracefully
            var mainLines = await FetchGamestringsLinesAsync(MainGamestringsUrl, cancellationToken);
            var alteracLines = await FetchGamestringsLinesAsync(AlteracPassGamestringsUrl, cancellationToken);

            if (mainLines.Length == 0 && alteracLines.Length == 0)
            {
                result.Errors.Add("Both gamestrings URLs returned no data");
                result.Message = "No data fetched from Gamedata repository";
                return result;
            }

            logger.LogInformation("Fetched {Main} lines from main file, {Alterac} lines from Alterac Pass file",
                mainLines.Length, alteracLines.Length);

            // Merge all lines and parse map loading screen entries
            var allLines = mainLines.Concat(alteracLines).ToArray();
            var entries = ParseMapLoadingScreenEntries(allLines);

            logger.LogInformation("Parsed {Count} map loading screen entries", entries.Count);

            var syncedCount = 0;

            foreach (var (mapKey, entry) in entries)
            {
                if (!KnownMaps.TryGetValue(mapKey, out var metadata))
                {
                    logger.LogDebug("Skipping unknown map key: {MapKey}", mapKey);
                    continue;
                }

                await CreateOrUpdateBattlegroundAsync(mapKey, entry, metadata, cancellationToken);
                syncedCount++;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            result.HeroesUpdated = syncedCount;
            result.Success = syncedCount > 0 || entries.Count > 0;
            result.Message = $"Synced {syncedCount} battlegrounds from Gamedata gamestrings";

            logger.LogInformation("Gamedata battleground sync complete: {Count} synced", syncedCount);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"Gamedata battleground sync failed: {ex.Message}");
            logger.LogError(ex, "Gamedata battleground sync failed");
        }

        return result;
    }

    /// <summary>
    /// Fetches a gamestrings.txt file and returns its lines. Returns an empty array on failure.
    /// </summary>
    private async Task<string[]> FetchGamestringsLinesAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            var content = await httpClient.GetStringAsync(url, cancellationToken);
            return content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch gamestrings from {Url}", url);
            return [];
        }
    }

    /// <summary>
    /// Parses gamestrings lines for UI/MapLoadingScreen/ entries.
    /// Each line has the format: UI/MapLoadingScreen/{MapKey}/{SubKey}={Value}
    /// Returns a dictionary keyed by map key with all parsed fields populated.
    /// </summary>
    private static Dictionary<string, ParsedMapEntry> ParseMapLoadingScreenEntries(string[] lines)
    {
        var entries = new Dictionary<string, ParsedMapEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            if (!line.StartsWith(LoadingScreenPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var eqIndex = line.IndexOf('=');
            if (eqIndex < 0)
                continue;

            var keyPart = line[LoadingScreenPrefix.Length..eqIndex];
            var value = line[(eqIndex + 1)..].Trim();

            var slashIndex = keyPart.IndexOf('/');
            if (slashIndex < 0)
                continue;

            var mapKey = keyPart[..slashIndex];
            var subKey = keyPart[(slashIndex + 1)..];

            if (!entries.TryGetValue(mapKey, out var entry))
            {
                entry = new ParsedMapEntry();
                entries[mapKey] = entry;
            }

            switch (subKey)
            {
                case "Name":        entry.Name         = value; break;
                case "Title1":      entry.Title1       = value; break;
                case "Description1":entry.Description1 = value; break;
                case "Title2":      entry.Title2       = value; break;
                case "Description2":entry.Description2 = value; break;
                case "Title3":      entry.Title3       = value; break;
                case "Description3":entry.Description3 = value; break;
            }
        }

        return entries;
    }

    /// <summary>
    /// Creates or updates a Battleground entity from parsed gamestrings data.
    /// Authoritative fields (Name, MapType, Universe) are always overwritten.
    /// Wiki-enriched fields (ObjectiveTiming, MercCamps, BossInfo, Tips, ImageUrl)
    /// are never touched — the wiki scraper owns those.
    /// Description and Objective use null-coalescing to preserve wiki-enriched content.
    /// </summary>
    private async Task CreateOrUpdateBattlegroundAsync(
        string mapKey,
        ParsedMapEntry entry,
        MapStaticMetadata metadata,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.Battlegrounds
            .FirstOrDefaultAsync(b => b.ShortName == metadata.ShortName, cancellationToken);

        if (existing is null)
        {
            var battleground = new Battleground
            {
                ShortName = metadata.ShortName,
                Name = entry.Name ?? mapKey,
                MapType = metadata.MapType,
                Universe = metadata.Universe,
                Description = entry.Description1,
                Objective = entry.Title1,
                IsInRotation = true,
            };

            dbContext.Battlegrounds.Add(battleground);
            logger.LogInformation("Added new battleground from Gamedata: {Name} ({ShortName})",
                battleground.Name, metadata.ShortName);
        }
        else
        {
            // Always overwrite authoritative fields from the game client data
            existing.Name = entry.Name ?? mapKey;
            existing.MapType = metadata.MapType;
            existing.Universe = metadata.Universe;

            // Preserve wiki-enriched content; only fill in if not already set
            existing.Description ??= entry.Description1;
            existing.Objective ??= entry.Title1;

            logger.LogInformation("Updated battleground from Gamedata: {Name} ({ShortName})",
                existing.Name, metadata.ShortName);
        }
    }

    #region Data Models

    private record MapStaticMetadata(string ShortName, string MapType, string Universe);

    private sealed class ParsedMapEntry
    {
        public string? Name { get; set; }
        public string? Title1 { get; set; }
        public string? Description1 { get; set; }
        public string? Title2 { get; set; }
        public string? Description2 { get; set; }
        public string? Title3 { get; set; }
        public string? Description3 { get; set; }
    }

    #endregion
}
