using System.Globalization;
using HtmlAgilityPack;

namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Scrapes hero data from the Heroes of the Storm Fandom wiki.
/// </summary>
public sealed class HeroScraper(HttpClient httpClient, ILogger<HeroScraper> logger) : IHeroScraper
{
    private const string FandomBaseUrl = "https://heroesofthestorm.fandom.com";

    public string GetWikiUrl(string heroName)
    {
        var wikiName = heroName.Replace(" ", "_");
        return $"{FandomBaseUrl}/wiki/{Uri.EscapeDataString(wikiName)}";
    }

    public async Task<HeroWikiDetailInfo> GetHeroDetailsAsync(string wikiUrl, CancellationToken cancellationToken = default)
    {
        var details = new HeroWikiDetailInfo();

        try
        {
            logger.LogInformation("Fetching hero details from {Url}", wikiUrl);
            var html = await httpClient.GetStringAsync(wikiUrl, cancellationToken);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            ExtractInfoboxData(doc, details);
            ExtractDescription(doc, details);
            ExtractAbilityData(doc, details);
            ExtractTalentData(doc, details);

            logger.LogDebug("Extracted wiki details for hero at {Url}", wikiUrl);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Failed to fetch hero wiki page: {Url}", wikiUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse hero wiki page: {Url}", wikiUrl);
        }

        return details;
    }

    private void ExtractInfoboxData(HtmlDocument doc, HeroWikiDetailInfo details)
    {
        // Extract title from infobox
        var titleNode = doc.DocumentNode.SelectSingleNode(
            "//div[@data-source='title']//div[@class='pi-data-value'] | " +
            "//div[contains(@class, 'pi-title')]");
        if (titleNode is not null)
        {
            // The pi-title often contains the hero name, not the title/epithet
            // The title/epithet is usually in a data-source='title' element
            var titleDataNode = doc.DocumentNode.SelectSingleNode("//div[@data-source='title']//div[@class='pi-data-value']");
            if (titleDataNode is not null)
            {
                details.Title = CleanWikiText(titleDataNode.InnerText);
            }
        }

        // Extract role
        var roleNode = doc.DocumentNode.SelectSingleNode(
            "//div[@data-source='role']//div[@class='pi-data-value']");
        if (roleNode is not null)
        {
            details.Role = CleanWikiText(roleNode.InnerText);
        }

        // Extract difficulty
        var difficultyNode = doc.DocumentNode.SelectSingleNode(
            "//div[@data-source='difficulty']//div[@class='pi-data-value']");
        if (difficultyNode is not null)
        {
            details.Difficulty = CleanWikiText(difficultyNode.InnerText);
        }

        // Extract universe/franchise
        var universeNode = doc.DocumentNode.SelectSingleNode(
            "//div[@data-source='franchise']//div[@class='pi-data-value'] | " +
            "//div[@data-source='universe']//div[@class='pi-data-value']");
        if (universeNode is not null)
        {
            details.Universe = CleanWikiText(universeNode.InnerText);
        }

        // Extract stats
        ExtractStatValue(doc, "hp", value =>
        {
            if (int.TryParse(value.Replace(",", ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out var hp))
                details.BaseHealth = hp;
        });

        ExtractStatValue(doc, "hp_regen", value =>
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var regen))
                details.HealthRegen = regen;
        });

        ExtractStatValue(doc, "mp", value =>
        {
            if (int.TryParse(value.Replace(",", ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out var mana))
                details.BaseMana = mana;
        });

        ExtractStatValue(doc, "mp_regen", value =>
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var regen))
                details.ManaRegen = regen;
        });

        ExtractStatValue(doc, "damage", value =>
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dmg))
                details.BaseAttackDamage = dmg;
        });

        ExtractStatValue(doc, "attack_speed", value =>
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var speed))
                details.AttackSpeed = speed;
        });

        ExtractStatValue(doc, "attack_range", value =>
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var range))
                details.AttackRange = range;
        });

        // Extract high-res image from infobox
        var imageLink = doc.DocumentNode.SelectSingleNode(
            "//figure[contains(@class, 'pi-item')]//a | " +
            "//div[@class='infobox-image']//a");
        if (imageLink is not null)
        {
            var imageUrl = imageLink.GetAttributeValue("href", null);
            if (!string.IsNullOrEmpty(imageUrl))
            {
                details.SplashArtUrl = imageUrl.StartsWith("http") ? imageUrl : "https:" + imageUrl;
            }
        }
        else
        {
            var imageNode = doc.DocumentNode.SelectSingleNode(
                "//figure[contains(@class, 'pi-item')]//img | " +
                "//div[@class='infobox-image']//img");
            if (imageNode is not null)
            {
                var imageSrc = imageNode.GetAttributeValue("src", null);
                if (!string.IsNullOrEmpty(imageSrc) && !imageSrc.StartsWith("data:"))
                {
                    details.SplashArtUrl = imageSrc.StartsWith("http") ? imageSrc : "https:" + imageSrc;
                }
            }
        }
    }

    private static void ExtractStatValue(HtmlDocument doc, string dataSource, Action<string> setter)
    {
        var node = doc.DocumentNode.SelectSingleNode(
            $"//div[@data-source='{dataSource}']//div[@class='pi-data-value']");
        if (node is null) return;

        var text = CleanWikiText(node.InnerText);
        // Extract just the numeric part (remove " (+4% per level)" type suffixes)
        var numericPart = text.Split('(')[0].Split('+')[0].Trim();
        if (!string.IsNullOrEmpty(numericPart))
        {
            setter(numericPart);
        }
    }

    private static void ExtractDescription(HtmlDocument doc, HeroWikiDetailInfo details)
    {
        var descNode = doc.DocumentNode.SelectSingleNode("//div[@class='mw-parser-output']/p[not(@class)]");
        if (descNode is not null)
        {
            details.Description = CleanWikiText(descNode.InnerText);
        }

        // Try to find lore section
        var loreSection = doc.DocumentNode.SelectSingleNode(
            "//span[@id='Background' or @id='Lore']/parent::*/following-sibling::p[1]");
        if (loreSection is not null)
        {
            details.Lore = CleanWikiText(loreSection.InnerText);
        }
    }

    private void ExtractAbilityData(HtmlDocument doc, HeroWikiDetailInfo details)
    {
        // Look for ability tables on the wiki page
        var abilityHeaders = doc.DocumentNode.SelectNodes(
            "//span[@id='Abilities']/parent::*/following-sibling::table//tr | " +
            "//h2[contains(.,'Abilities')]/following-sibling::table//tr");

        if (abilityHeaders is null) return;

        foreach (var row in abilityHeaders)
        {
            var cells = row.SelectNodes("td");
            if (cells is null || cells.Count < 2) continue;

            var nameNode = cells[0].SelectSingleNode(".//b | .//strong | .//a");
            if (nameNode is null) continue;

            var abilityInfo = new WikiAbilityInfo
            {
                Name = CleanWikiText(nameNode.InnerText)
            };

            // Try to extract additional data from subsequent cells or data fields
            foreach (var cell in cells)
            {
                var cellText = CleanWikiText(cell.InnerText);
                if (cellText.Contains("Scaling", StringComparison.OrdinalIgnoreCase))
                    abilityInfo.Scaling = ExtractValueAfterLabel(cellText, "Scaling");
                if (cellText.Contains("Cast time", StringComparison.OrdinalIgnoreCase))
                    abilityInfo.CastTime = ExtractValueAfterLabel(cellText, "Cast time");
                if (cellText.Contains("Range", StringComparison.OrdinalIgnoreCase))
                    abilityInfo.Range = ExtractValueAfterLabel(cellText, "Range");
                if (cellText.Contains("Area", StringComparison.OrdinalIgnoreCase))
                    abilityInfo.AreaOfEffect = ExtractValueAfterLabel(cellText, "Area");
            }

            if (!string.IsNullOrEmpty(abilityInfo.Name))
            {
                details.Abilities.Add(abilityInfo);
            }
        }
    }

    private void ExtractTalentData(HtmlDocument doc, HeroWikiDetailInfo details)
    {
        // Look for talent tables
        var talentSection = doc.DocumentNode.SelectSingleNode(
            "//span[@id='Talents']/parent::*");

        if (talentSection is null) return;

        // Find tables after the Talents heading
        var talentTables = talentSection.SelectNodes(
            "following-sibling::table | following-sibling::div//table");

        if (talentTables is null) return;

        var currentLevel = 0;
        foreach (var table in talentTables)
        {
            // Check if this is a talent tier header
            var headerRow = table.SelectSingleNode(".//tr[1]//th");
            if (headerRow is not null)
            {
                var headerText = CleanWikiText(headerRow.InnerText);
                if (int.TryParse(new string(headerText.Where(char.IsDigit).ToArray()), out var level))
                {
                    currentLevel = level;
                }
            }

            var rows = table.SelectNodes(".//tr");
            if (rows is null) continue;

            foreach (var row in rows)
            {
                var cells = row.SelectNodes("td");
                if (cells is null || cells.Count < 2) continue;

                var nameNode = cells[0].SelectSingleNode(".//b | .//strong | .//a");
                if (nameNode is null && cells.Count > 1)
                    nameNode = cells[1].SelectSingleNode(".//b | .//strong | .//a");

                if (nameNode is null) continue;

                var talentInfo = new WikiTalentInfo
                {
                    Name = CleanWikiText(nameNode.InnerText),
                    Level = currentLevel
                };

                // Look for linked ability info
                var typeCell = cells.Count > 2 ? cells[2] : null;
                if (typeCell is not null)
                {
                    var linkedAbility = typeCell.SelectSingleNode(".//a");
                    if (linkedAbility is not null)
                    {
                        talentInfo.LinkedAbilityName = CleanWikiText(linkedAbility.InnerText);
                    }
                }

                if (!string.IsNullOrEmpty(talentInfo.Name))
                {
                    details.Talents.Add(talentInfo);
                }
            }
        }
    }

    private static string? ExtractValueAfterLabel(string text, string label)
    {
        var idx = text.IndexOf(label, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        var afterLabel = text[(idx + label.Length)..].TrimStart(':', ' ');
        var endIdx = afterLabel.IndexOfAny(['\n', '\r', ';']);
        return endIdx > 0 ? afterLabel[..endIdx].Trim() : afterLabel.Trim();
    }

    private static string CleanWikiText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        return HtmlEntity.DeEntitize(text)
            .Replace("[edit]", "")
            .Replace("[citation needed]", "")
            .Trim();
    }
}
