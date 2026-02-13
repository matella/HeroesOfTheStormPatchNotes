using System.Globalization;
using HtmlAgilityPack;

namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Helper class for scraping battleground data from Heroes of the Storm Fandom Wiki.
/// </summary>
public class BattlegroundScraper(HttpClient httpClient, ILogger<BattlegroundScraper> logger)
{
    private const string FandomBaseUrl = "https://heroesofthestorm.fandom.com";
    private const string BattlegroundListUrl = "https://heroesofthestorm.fandom.com/wiki/Battleground";

    /// <summary>
    /// Gets the list of all battlegrounds from the main Battleground wiki page.
    /// </summary>
    public async Task<List<BattlegroundBasicInfo>> GetBattlegroundListAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Fetching battleground list from Fandom wiki");
            var html = await httpClient.GetStringAsync(BattlegroundListUrl, cancellationToken);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var battlegrounds = new List<BattlegroundBasicInfo>();

            // Find the main battleground table
            var tableRows = doc.DocumentNode.SelectNodes("//table[contains(@class, 'wikitable')]//tr");
            if (tableRows == null)
            {
                logger.LogWarning("Could not find battleground table on wiki page");
                return battlegrounds;
            }

            // Skip header row
            foreach (var row in tableRows.Skip(1))
            {
                try
                {
                    var cells = row.SelectNodes("td");
                    if (cells == null || cells.Count < 7) continue;

                    // Extract data from table cells
                    var nameCell = cells[0];
                    var nameLink = nameCell.SelectSingleNode(".//a");
                    if (nameLink == null) continue;

                    var name = HtmlEntity.DeEntitize(nameLink.InnerText.Trim());
                    var wikiPath = nameLink.GetAttributeValue("href", "");
                    var wikiUrl = wikiPath.StartsWith("http") ? wikiPath : FandomBaseUrl + wikiPath;

                    var objective = HtmlEntity.DeEntitize(cells[1].InnerText.Trim());
                    var lanesText = HtmlEntity.DeEntitize(cells[2].InnerText.Trim());
                    var realm = HtmlEntity.DeEntitize(cells[5].InnerText.Trim());
                    var releaseDateText = HtmlEntity.DeEntitize(cells[6].InnerText.Trim());

                    // Extract image if present
                    var imageNode = nameCell.SelectSingleNode(".//img");
                    var imageUrl = imageNode?.GetAttributeValue("src", null);
                    if (!string.IsNullOrEmpty(imageUrl) && !imageUrl.StartsWith("http"))
                    {
                        imageUrl = "https:" + imageUrl;
                    }

                    battlegrounds.Add(new BattlegroundBasicInfo
                    {
                        Name = name,
                        ShortName = GenerateShortName(name),
                        WikiUrl = wikiUrl,
                        ObjectiveSummary = objective,
                        Lanes = ParseLanes(lanesText),
                        Realm = realm,
                        Universe = MapRealmToUniverse(realm),
                        ReleaseDate = ParseReleaseDate(releaseDateText),
                        ThumbnailUrl = imageUrl
                    });

                    logger.LogDebug("Parsed battleground: {Name}", name);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse battleground row");
                }
            }

            logger.LogInformation("Found {Count} battlegrounds", battlegrounds.Count);
            return battlegrounds;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch battleground list from Fandom");
            throw;
        }
    }

    /// <summary>
    /// Gets detailed information about a specific battleground from its wiki page.
    /// </summary>
    public async Task<BattlegroundDetailInfo> GetBattlegroundDetailsAsync(string wikiUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Fetching battleground details from {Url}", wikiUrl);
            var html = await httpClient.GetStringAsync(wikiUrl, cancellationToken);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var details = new BattlegroundDetailInfo();

            // Extract description (usually first paragraph after the intro)
            var descriptionNode = doc.DocumentNode.SelectSingleNode("//div[@class='mw-parser-output']/p[not(@class)]");
            if (descriptionNode != null)
            {
                details.Description = CleanWikiText(descriptionNode.InnerText);
            }

            // Extract objective details
            var objectiveSection = doc.DocumentNode.SelectSingleNode("//span[@id='Primary_Objectives' or @id='Objectives']/parent::*/following-sibling::*[1]");
            if (objectiveSection != null)
            {
                details.ObjectiveDetails = CleanWikiText(objectiveSection.InnerText);
            }

            // Extract objective timing from infobox
            var timingNode = doc.DocumentNode.SelectSingleNode("//div[@data-source='objective_time']//div[@class='pi-data-value']");
            if (timingNode != null)
            {
                details.ObjectiveTiming = CleanWikiText(timingNode.InnerText);
            }

            // Extract mercenary camps info
            var mercSection = doc.DocumentNode.SelectSingleNode("//span[@id='Mercenary_Camps']/parent::*/following-sibling::*");
            if (mercSection != null)
            {
                var mercContent = new System.Text.StringBuilder();
                var currentNode = mercSection;
                
                // Collect content until next heading
                while (currentNode != null && !currentNode.Name.StartsWith("h", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentNode.Name == "p" || currentNode.Name == "ul")
                    {
                        mercContent.AppendLine(CleanWikiText(currentNode.InnerText));
                    }
                    currentNode = currentNode.NextSibling;
                }
                
                details.MercCamps = mercContent.ToString().Trim();
            }

            // Extract boss info (if exists)
            var bossText = details.MercCamps ?? "";
            if (bossText.Contains("Boss", StringComparison.OrdinalIgnoreCase))
            {
                // Boss info is usually part of merc camps section
                var bossLines = bossText.Split('\n')
                    .Where(l => l.Contains("Boss", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (bossLines.Any())
                {
                    details.BossInfo = string.Join("\n", bossLines);
                }
            }

            // Extract tips/strategy
            var tipsSection = doc.DocumentNode.SelectSingleNode("//span[@id='Tips' or @id='Strategy']/parent::*/following-sibling::ul[1]");
            if (tipsSection != null)
            {
                details.Tips = CleanWikiText(tipsSection.InnerText);
            }

            // Extract high-res image
            var imageNode = doc.DocumentNode.SelectSingleNode("//figure[contains(@class, 'pi-item')]//img | //div[@class='infobox-image']//img");
            if (imageNode != null)
            {
                var imageSrc = imageNode.GetAttributeValue("src", null);
                if (!string.IsNullOrEmpty(imageSrc))
                {
                    details.FullImageUrl = imageSrc.StartsWith("http") ? imageSrc : "https:" + imageSrc;
                    // Remove scale-to-width parameters to get full resolution
                    details.FullImageUrl = details.FullImageUrl.Split('?')[0] + "/revision/latest";
                }
            }

            logger.LogDebug("Extracted details for battleground");
            return details;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch battleground details from {Url}", wikiUrl);
            throw;
        }
    }

    /// <summary>
    /// Generates a URL-friendly short name from the battleground name.
    /// </summary>
    public static string GenerateShortName(string name)
    {
        return name
            .ToLowerInvariant()
            .Replace("'", "")
            .Replace(" ", "-")
            .Replace(":", "")
            .Trim('-');
    }

    /// <summary>
    /// Maps wiki realm names to our Universe field.
    /// </summary>
    public static string MapRealmToUniverse(string realm)
    {
        return realm.ToLowerInvariant() switch
        {
            var r when r.Contains("azeroth") => "Warcraft",
            var r when r.Contains("sanctuary") => "Diablo",
            var r when r.Contains("koprulu") => "StarCraft",
            var r when r.Contains("overwatch") => "Overwatch",
            _ => "Nexus"
        };
    }

    /// <summary>
    /// Parses lane count from text (e.g., "3" or "2-Lane").
    /// </summary>
    private static string ParseLanes(string lanesText)
    {
        if (string.IsNullOrWhiteSpace(lanesText)) return "3";
        
        var digits = new string(lanesText.Where(char.IsDigit).ToArray());
        return string.IsNullOrEmpty(digits) ? "3" : digits;
    }

    /// <summary>
    /// Parses release date from wiki text.
    /// </summary>
    private static DateTime? ParseReleaseDate(string dateText)
    {
        if (string.IsNullOrWhiteSpace(dateText)) return null;

        // Try parsing various date formats
        var formats = new[]
        {
            "yyyy-MM-dd",
            "MMMM d, yyyy",
            "MMM d, yyyy",
            "d MMMM yyyy"
        };

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(dateText, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }
        }

        return null;
    }

    /// <summary>
    /// Cleans wiki text by removing markup artifacts.
    /// </summary>
    private static string CleanWikiText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        return HtmlEntity.DeEntitize(text)
            .Replace("[edit]", "")
            .Replace("[citation needed]", "")
            .Trim();
    }
}

/// <summary>
/// Basic battleground info from the main wiki table.
/// </summary>
public class BattlegroundBasicInfo
{
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string WikiUrl { get; set; } = string.Empty;
    public string ObjectiveSummary { get; set; } = string.Empty;
    public string Lanes { get; set; } = "3";
    public string Realm { get; set; } = string.Empty;
    public string Universe { get; set; } = string.Empty;
    public DateTime? ReleaseDate { get; set; }
    public string? ThumbnailUrl { get; set; }
}

/// <summary>
/// Detailed battleground info from individual wiki pages.
/// </summary>
public class BattlegroundDetailInfo
{
    public string? Description { get; set; }
    public string? ObjectiveDetails { get; set; }
    public string? ObjectiveTiming { get; set; }
    public string? MercCamps { get; set; }
    public string? BossInfo { get; set; }
    public string? Tips { get; set; }
    public string? FullImageUrl { get; set; }
}
