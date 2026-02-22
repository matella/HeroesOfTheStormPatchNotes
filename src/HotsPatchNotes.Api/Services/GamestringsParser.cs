using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using HotsPatchNotes.Shared.Models;

namespace HotsPatchNotes.Api.Services;

public interface IGamestringsParser
{
    Task<GamestringsResult?> FetchAndParseGamestringsAsync(
        string dirName,
        string locale = "enus",
        CancellationToken cancellationToken = default);

    int ApplyGamestringsToAbilities(
        IEnumerable<Ability> abilities,
        Dictionary<string, GamestringEntry> gamestrings);

    int ApplyGamestringsToTalents(
        IEnumerable<Talent> talents,
        Dictionary<string, GamestringEntry> gamestrings);
}

public sealed partial class GamestringsParser(
    HttpClient httpClient,
    IHtmlContentService htmlContentService,
    ILogger<GamestringsParser> logger) : IGamestringsParser
{
    // dirName is the full version string e.g. "2.55.15.96477"
    // buildNum is the last numeric segment e.g. "96477"
    private const string GamestringsUrlPattern = "https://raw.githubusercontent.com/HeroesToolChest/heroes-data/master/heroesdata/{0}/gamestrings/gamestrings_{1}_{2}.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<GamestringsResult?> FetchAndParseGamestringsAsync(
        string dirName,
        string locale = "enus",
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Extract numeric build number from full version string (e.g. "2.55.15.96477" → "96477")
            var buildNum = dirName.Split('.').Last();
            var url = string.Format(GamestringsUrlPattern, dirName, buildNum, locale);
            logger.LogDebug("Fetching gamestrings from {Url}", url);

            var jsonContent = await httpClient.GetStringAsync(url, cancellationToken);
            var gamestringFile = JsonSerializer.Deserialize<GamestringFile>(jsonContent, JsonOptions);

            if (gamestringFile?.Gamestrings is null)
            {
                logger.LogWarning("No gamestrings found in build dir {DirName}", dirName);
                return null;
            }

            var abilTalentEntries = ParseAbilTalentSection(gamestringFile.Gamestrings.AbilTalent, dirName);
            var unitEntries = ParseUnitSection(gamestringFile.Gamestrings.Unit);

            logger.LogInformation(
                "Parsed {AbilCount} abiltalent and {UnitCount} unit gamestring entries from build dir {DirName}",
                abilTalentEntries.Count, unitEntries.Count, dirName);

            return new GamestringsResult(abilTalentEntries, unitEntries);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Failed to fetch gamestrings for build dir {DirName} (HTTP error)", dirName);
            return null;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse gamestrings JSON for build dir {DirName}", dirName);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error fetching gamestrings for build dir {DirName}", dirName);
            return null;
        }
    }

    public int ApplyGamestringsToAbilities(
        IEnumerable<Ability> abilities,
        Dictionary<string, GamestringEntry> gamestrings)
    {
        var matchCount = 0;

        foreach (var ability in abilities)
        {
            GamestringEntry? entry = null;

            // Try matching by AbilityId (= NameId in gamestrings)
            if (!string.IsNullOrWhiteSpace(ability.AbilityId) &&
                gamestrings.TryGetValue(ability.AbilityId, out var idMatch))
            {
                entry = idMatch;
                logger.LogTrace("Matched ability '{Name}' by AbilityId: {Id}", ability.Name, ability.AbilityId);
            }
            // Try matching by Uid (= ButtonId in gamestrings)
            else if (!string.IsNullOrWhiteSpace(ability.Uid) &&
                     gamestrings.TryGetValue(ability.Uid, out var uidMatch))
            {
                entry = uidMatch;
                logger.LogTrace("Matched ability '{Name}' by Uid: {Id}", ability.Name, ability.Uid);
            }
            // Fuzzy name match
            else
            {
                var normalizedName = NormalizeName(ability.Name);
                if (!string.IsNullOrWhiteSpace(normalizedName))
                {
                    entry = gamestrings.Values.FirstOrDefault(gs =>
                        NormalizeName(gs.Name).Equals(normalizedName, StringComparison.OrdinalIgnoreCase));

                    if (entry is not null)
                        logger.LogTrace("Matched ability '{Name}' by fuzzy name match", ability.Name);
                }
            }

            if (entry is not null)
            {
                if (!string.IsNullOrWhiteSpace(entry.Name))
                    ability.Name = entry.Name;

                if (!string.IsNullOrWhiteSpace(entry.Description))
                    ability.Description = entry.Description;

                matchCount++;
            }
            else
            {
                logger.LogDebug("No gamestring match for ability: {Name} (AbilityId: {AbilityId}, Uid: {Uid})",
                    ability.Name, ability.AbilityId ?? "null", ability.Uid ?? "null");
            }
        }

        return matchCount;
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
            // Fuzzy name match (fallback)
            else
            {
                var normalizedTalentName = NormalizeName(talent.Name);
                entry = gamestrings.Values.FirstOrDefault(gs =>
                    NormalizeName(gs.Name).Equals(normalizedTalentName, StringComparison.OrdinalIgnoreCase));

                if (entry is not null)
                    logger.LogTrace("Matched talent '{Name}' by fuzzy name match", talent.Name);
            }

            if (entry is not null)
            {
                if (!string.IsNullOrWhiteSpace(entry.Name))
                    talent.Name = entry.Name;

                if (!string.IsNullOrWhiteSpace(entry.Description))
                    talent.Description = entry.Description;

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
    /// Parses the abiltalent section into a lookup dictionary keyed by NameId and ButtonId.
    /// </summary>
    private Dictionary<string, GamestringEntry> ParseAbilTalentSection(
        GamestringAbilTalent? abilTalent,
        string dirName)
    {
        var result = new Dictionary<string, GamestringEntry>(StringComparer.OrdinalIgnoreCase);

        if (abilTalent is null)
        {
            logger.LogWarning("No abiltalent gamestrings found in build dir {DirName}", dirName);
            return result;
        }

        var names = abilTalent.Name ?? [];
        var fullDescriptions = abilTalent.Full ?? [];
        var shortDescriptions = abilTalent.Short ?? [];

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

                result[parsedKey.NameId] = entry;

                if (!string.Equals(parsedKey.NameId, parsedKey.ButtonId, StringComparison.OrdinalIgnoreCase))
                    result[parsedKey.ButtonId] = entry;
            }
        }

        return result;
    }

    /// <summary>
    /// Parses the unit section into a lookup dictionary keyed by HyperlinkId (hero identifier).
    /// Each entry contains hero-level metadata: role, expandedRole, type, difficulty, title, description.
    /// </summary>
    private Dictionary<string, HeroGamestringEntry> ParseUnitSection(GamestringUnit? unit)
    {
        var result = new Dictionary<string, HeroGamestringEntry>(StringComparer.OrdinalIgnoreCase);

        if (unit is null)
            return result;

        // Collect all hero keys from any available sub-dict
        var allKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dict in new[] { unit.Role, unit.ExpandedRole, unit.Type, unit.Difficulty, unit.Title, unit.Description }
                     .Where(d => d is not null))
        {
            foreach (var k in dict!.Keys)
                allKeys.Add(k);
        }

        foreach (var heroKey in allKeys)
        {
            var entry = new HeroGamestringEntry(
                Role: unit.Role?.GetValueOrDefault(heroKey),
                ExpandedRole: unit.ExpandedRole?.GetValueOrDefault(heroKey),
                Type: unit.Type?.GetValueOrDefault(heroKey),
                Difficulty: unit.Difficulty?.GetValueOrDefault(heroKey),
                Title: unit.Title?.GetValueOrDefault(heroKey),
                Description: string.IsNullOrWhiteSpace(unit.Description?.GetValueOrDefault(heroKey))
                    ? null
                    : htmlContentService.SanitizeGamestring(unit.Description.GetValueOrDefault(heroKey)!));

            result[heroKey] = entry;
        }

        return result;
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
        public GamestringUnit? Unit { get; set; }
    }

    private class GamestringAbilTalent
    {
        public Dictionary<string, string>? Name { get; set; }
        public Dictionary<string, string>? Full { get; set; }
        public Dictionary<string, string>? Short { get; set; }
    }

    private class GamestringUnit
    {
        public Dictionary<string, string>? Role { get; set; }

        [JsonPropertyName("expandedrole")]
        public Dictionary<string, string>? ExpandedRole { get; set; }

        public Dictionary<string, string>? Type { get; set; }
        public Dictionary<string, string>? Difficulty { get; set; }
        public Dictionary<string, string>? Title { get; set; }
        public Dictionary<string, string>? Description { get; set; }
    }

    #endregion
}

/// <summary>
/// Combined result from parsing a gamestrings JSON file.
/// Contains ability/talent entries (keyed by NameId/ButtonId) and hero-level unit entries (keyed by HyperlinkId).
/// </summary>
public record GamestringsResult(
    Dictionary<string, GamestringEntry> AbilTalent,
    Dictionary<string, HeroGamestringEntry> Unit);

/// <summary>
/// Represents a parsed gamestring entry with name and descriptions for abilities/talents.
/// </summary>
public record GamestringEntry(string Name, string Description, string? ShortDescription);

/// <summary>
/// Represents hero-level metadata extracted from the gamestrings unit section.
/// </summary>
public record HeroGamestringEntry(
    string? Role,
    string? ExpandedRole,
    string? Type,
    string? Difficulty,
    string? Title,
    string? Description);

/// <summary>
/// Represents the parsed components of a gamestring key.
/// </summary>
public record GamestringKey(string NameId, string ButtonId, string AbilityType, bool IsPassive);
