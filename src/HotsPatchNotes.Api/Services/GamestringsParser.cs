using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using HotsPatchNotes.Shared.Models;

namespace HotsPatchNotes.Api.Services;

public interface IGamestringsParser
{
    Task<Dictionary<string, GamestringEntry>?> FetchAndParseGamestringsAsync(
        string buildNumber,
        string locale = "enus",
        CancellationToken cancellationToken = default);

    int ApplyGamestringsToTalents(
        IEnumerable<Talent> talents,
        Dictionary<string, GamestringEntry> gamestrings);
}

public sealed partial class GamestringsParser(
    HttpClient httpClient,
    IHtmlContentService htmlContentService,
    ILogger<GamestringsParser> logger) : IGamestringsParser
{
    private const string GamestringsUrlPattern = "https://raw.githubusercontent.com/HeroesToolChest/heroes-data/master/heroesdata/{0}/gamestrings/gamestrings_{1}_{2}.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<Dictionary<string, GamestringEntry>?> FetchAndParseGamestringsAsync(
        string buildNumber,
        string locale = "enus",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = string.Format(GamestringsUrlPattern, buildNumber, buildNumber, locale);
            logger.LogDebug("Fetching gamestrings from {Url}", url);

            var jsonContent = await httpClient.GetStringAsync(url, cancellationToken);
            var gamestringFile = JsonSerializer.Deserialize<GamestringFile>(jsonContent, JsonOptions);

            if (gamestringFile?.Gamestrings?.AbilTalent is null)
            {
                logger.LogWarning("No abiltalent gamestrings found in build {Build}", buildNumber);
                return null;
            }

            var result = new Dictionary<string, GamestringEntry>(StringComparer.OrdinalIgnoreCase);

            // Parse talent names and descriptions from nested structure
            var names = gamestringFile.Gamestrings.AbilTalent.Name ?? new Dictionary<string, string>();
            var fullDescriptions = gamestringFile.Gamestrings.AbilTalent.Full ?? new Dictionary<string, string>();
            var shortDescriptions = gamestringFile.Gamestrings.AbilTalent.Short ?? new Dictionary<string, string>();

            // Combine all keys (some may have name but no description, or vice versa)
            var allKeys = names.Keys
                .Union(fullDescriptions.Keys)
                .Union(shortDescriptions.Keys)
                .Distinct();

            foreach (var key in allKeys)
            {
                var parsedKey = ParseGamestringKey(key);
                if (parsedKey is null)
                {
                    logger.LogTrace("Skipping malformed gamestring key: {Key}", key);
                    continue;
                }

                var name = names.GetValueOrDefault(key, string.Empty);
                var fullDescription = fullDescriptions.GetValueOrDefault(key, string.Empty);
                var shortDescription = shortDescriptions.GetValueOrDefault(key, string.Empty);

                // Sanitize HTML from gamestrings
                var sanitizedName = string.IsNullOrWhiteSpace(name)
                    ? string.Empty
                    : htmlContentService.SanitizeGamestring(name);

                var sanitizedFull = string.IsNullOrWhiteSpace(fullDescription)
                    ? string.Empty
                    : htmlContentService.SanitizeGamestring(fullDescription);

                var sanitizedShort = string.IsNullOrWhiteSpace(shortDescription)
                    ? null
                    : htmlContentService.SanitizeGamestring(shortDescription);

                if (!string.IsNullOrWhiteSpace(sanitizedName) || !string.IsNullOrWhiteSpace(sanitizedFull))
                {
                    var entry = new GamestringEntry(sanitizedName, sanitizedFull, sanitizedShort);

                    // Use NameId as primary key (e.g., "AbathurCombatStyleBombardStrain")
                    // This matches with TalentTreeId or TooltipId
                    result[parsedKey.NameId] = entry;

                    // Also store by ButtonId if different (e.g., "AbathurLocustStrainBombardStrainTalent")
                    if (!string.Equals(parsedKey.NameId, parsedKey.ButtonId, StringComparison.OrdinalIgnoreCase))
                    {
                        result[parsedKey.ButtonId] = entry;
                    }
                }
            }

            logger.LogInformation("Parsed {Count} gamestring entries from build {Build}", result.Count, buildNumber);
            return result;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Failed to fetch gamestrings for build {Build} (HTTP error)", buildNumber);
            return null;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse gamestrings JSON for build {Build}", buildNumber);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error fetching gamestrings for build {Build}", buildNumber);
            return null;
        }
    }

    public int ApplyGamestringsToTalents(
        IEnumerable<Talent> talents,
        Dictionary<string, GamestringEntry> gamestrings)
    {
        var matchCount = 0;

        foreach (var talent in talents)
        {
            GamestringEntry? entry = null;

            // Try matching by TalentTreeId (primary)
            if (!string.IsNullOrWhiteSpace(talent.TalentTreeId) &&
                gamestrings.TryGetValue(talent.TalentTreeId, out var treeMatch))
            {
                entry = treeMatch;
                logger.LogTrace("Matched talent '{Name}' by TalentTreeId: {Id}", talent.Name, talent.TalentTreeId);
            }
            // Try matching by TooltipId (secondary)
            else if (!string.IsNullOrWhiteSpace(talent.TooltipId) &&
                     gamestrings.TryGetValue(talent.TooltipId, out var tooltipMatch))
            {
                entry = tooltipMatch;
                logger.LogTrace("Matched talent '{Name}' by TooltipId: {Id}", talent.Name, talent.TooltipId);
            }
            // Try fuzzy name match (fallback)
            else
            {
                var normalizedTalentName = NormalizeName(talent.Name);
                entry = gamestrings.Values.FirstOrDefault(gs =>
                    NormalizeName(gs.Name).Equals(normalizedTalentName, StringComparison.OrdinalIgnoreCase));

                if (entry is not null)
                {
                    logger.LogTrace("Matched talent '{Name}' by fuzzy name match", talent.Name);
                }
            }

            if (entry is not null)
            {
                // Update talent fields with gamestring data
                if (!string.IsNullOrWhiteSpace(entry.Name))
                {
                    talent.Name = entry.Name;
                }

                if (!string.IsNullOrWhiteSpace(entry.Description))
                {
                    talent.Description = entry.Description;
                }

                matchCount++;
            }
            else
            {
                logger.LogDebug("No gamestring match for talent: {Name} (TreeId: {TreeId}, TooltipId: {TooltipId})",
                    talent.Name, talent.TalentTreeId ?? "null", talent.TooltipId ?? "null");
            }
        }

        return matchCount;
    }

    /// <summary>
    /// Parses a gamestring key in format: "NameId|ButtonId|AbilityType|IsPassive"
    /// Example: "AbathurCombatStyleBombardStrain|AbathurLocustStrainBombardStrainTalent|Trait|False"
    /// </summary>
    private static GamestringKey? ParseGamestringKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var parts = key.Split('|');
        if (parts.Length != 4)
            return null;

        return new GamestringKey(
            NameId: parts[0],
            ButtonId: parts[1],
            AbilityType: parts[2],
            IsPassive: bool.TryParse(parts[3], out var isPassive) && isPassive
        );
    }

    /// <summary>
    /// Normalizes a name for fuzzy matching by removing special characters and whitespace.
    /// </summary>
    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        return RemoveNonAlphanumeric().Replace(name, string.Empty).ToLowerInvariant();
    }

    [GeneratedRegex(@"[^a-zA-Z0-9]")]
    private static partial Regex RemoveNonAlphanumeric();

    #region Data Models for Gamestrings JSON

    private class GamestringFile
    {
        public GamestringMeta? Meta { get; set; }
        public GamestringRoot? Gamestrings { get; set; }
    }

    private class GamestringMeta
    {
        public string? Version { get; set; }
        public string? Locale { get; set; }
    }

    private class GamestringRoot
    {
        public GamestringAbilTalent? AbilTalent { get; set; }
    }

    private class GamestringAbilTalent
    {
        public Dictionary<string, string>? Name { get; set; }
        public Dictionary<string, string>? Full { get; set; }
        public Dictionary<string, string>? Short { get; set; }
    }

    #endregion
}

/// <summary>
/// Represents a parsed gamestring entry with name and descriptions.
/// </summary>
public record GamestringEntry(string Name, string Description, string? ShortDescription);

/// <summary>
/// Represents the parsed components of a gamestring key.
/// </summary>
public record GamestringKey(string NameId, string ButtonId, string AbilityType, bool IsPassive);
