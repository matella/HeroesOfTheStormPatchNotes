using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using HtmlAgilityPack;
using HotsPatchNotes.Api.Data;
using HotsPatchNotes.Shared.Models;
using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Api.Services;

public interface IGitHubSyncService
{
    Task<SyncResultDto> SyncAllAsync(CancellationToken cancellationToken = default);
    Task<SyncResultDto> SyncHeroesAsync(CancellationToken cancellationToken = default);
    Task<SyncResultDto> SyncPatchesAsync(CancellationToken cancellationToken = default);
    Task<SyncResultDto> SyncWebPatchesAsync(bool isInitialSync = false, CancellationToken cancellationToken = default);
}

public class GitHubSyncService : IGitHubSyncService
{
    private readonly HotsDbContext _dbContext;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubSyncService> _logger;

    private const string HeroesBaseUrl = "https://raw.githubusercontent.com/heroespatchnotes/heroes-talents/master/hero/";
    private const string HeroListUrl = "https://api.github.com/repos/heroespatchnotes/heroes-talents/contents/hero";
    private const string PatchesUrl = "https://raw.githubusercontent.com/heroespatchnotes/heroes-patch-data/master/patchversions.json";
    private const string BlueTrackerUrl = "https://www.bluetracker.gg/heroes/";
    private const string BlueTrackerBaseUrl = "https://www.bluetracker.gg";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public GitHubSyncService(HotsDbContext dbContext, HttpClient httpClient, ILogger<GitHubSyncService> logger)
    {
        _dbContext = dbContext;
        _httpClient = httpClient;
        _logger = logger;

        // Set User-Agent for API requests
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "HotsPatchNotes-API/1.0");
        }
    }

    public async Task<SyncResultDto> SyncAllAsync(CancellationToken cancellationToken = default)
    {
        var isInitialSync = !await _dbContext.Patches.AnyAsync(cancellationToken);
        _logger.LogInformation("Starting {SyncType} sync", isInitialSync ? "initial" : "subsequent");

        if (isInitialSync)
        {
            // Initial sync: GitHub (archive) → BlueTracker (all pages)
            var heroResult = await SyncHeroesAsync(cancellationToken);
            var patchResult = await SyncPatchesAsync(cancellationToken);
            var webPatchResult = await SyncWebPatchesAsync(isInitialSync: true, cancellationToken);

            return new SyncResultDto
            {
                Success = heroResult.Success && patchResult.Success,
                Message = "Initial sync completed",
                HeroesUpdated = heroResult.HeroesUpdated,
                PatchesUpdated = patchResult.PatchesUpdated + webPatchResult.PatchesUpdated,
                SyncedAt = DateTime.UtcNow,
                Errors = heroResult.Errors.Concat(patchResult.Errors).Concat(webPatchResult.Errors).ToList()
            };
        }
        else
        {
            // Subsequent sync: BlueTracker (page 1 only) - no GitHub needed
            var heroResult = await SyncHeroesAsync(cancellationToken);
            var webPatchResult = await SyncWebPatchesAsync(isInitialSync: false, cancellationToken);

            return new SyncResultDto
            {
                Success = heroResult.Success && webPatchResult.Success,
                Message = "Sync completed",
                HeroesUpdated = heroResult.HeroesUpdated,
                PatchesUpdated = webPatchResult.PatchesUpdated,
                SyncedAt = DateTime.UtcNow,
                Errors = heroResult.Errors.Concat(webPatchResult.Errors).ToList()
            };
        }
    }

    public async Task<SyncResultDto> SyncHeroesAsync(CancellationToken cancellationToken = default)
    {
        var result = new SyncResultDto { SyncedAt = DateTime.UtcNow };

        try
        {
            // Get list of hero files from GitHub API
            var heroFiles = await GetHeroFileListAsync(cancellationToken);
            _logger.LogInformation("Found {Count} hero files to sync", heroFiles.Count);

            foreach (var heroFile in heroFiles)
            {
                try
                {
                    await SyncHeroAsync(heroFile, cancellationToken);
                    result.HeroesUpdated++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sync hero: {HeroFile}", heroFile);
                    result.Errors.Add($"Failed to sync {heroFile}: {ex.Message}");
                }
            }

            result.Success = result.Errors.Count == 0;
            result.Message = $"Synced {result.HeroesUpdated} heroes";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync heroes");
            result.Errors.Add($"Failed to sync heroes: {ex.Message}");
        }

        return result;
    }

    public async Task<SyncResultDto> SyncPatchesAsync(CancellationToken cancellationToken = default)
    {
        var result = new SyncResultDto { SyncedAt = DateTime.UtcNow };

        try
        {
            var response = await _httpClient.GetStringAsync(PatchesUrl, cancellationToken);
            var patchData = JsonSerializer.Deserialize<List<GitHubPatchData>>(response, JsonOptions);

            if (patchData == null)
            {
                result.Errors.Add("Failed to parse patch data");
                return result;
            }

            // Filter out patches with empty InternalId and group by InternalId to handle duplicates
            var uniquePatches = patchData
                .Where(p => !string.IsNullOrEmpty(p.InternalId))
                .GroupBy(p => p.InternalId)
                .Select(g => g.First())
                .ToList();

            foreach (var patch in uniquePatches)
            {
                var existingPatch = await _dbContext.Patches
                    .FirstOrDefaultAsync(p => p.InternalId == patch.InternalId, cancellationToken);

                if (existingPatch == null)
                {
                    // Only create new patches from GitHub (tertiary source)
                    existingPatch = new Patch { InternalId = patch.InternalId, Source = "github" };
                    _dbContext.Patches.Add(existingPatch);
                    MapPatchData(patch, existingPatch);
                    result.PatchesUpdated++;
                }
                else if (existingPatch.Source == "github")
                {
                    // Only update if the existing patch is from GitHub (don't overwrite Blizzard or BlueTracker data)
                    MapPatchData(patch, existingPatch);
                    result.PatchesUpdated++;
                }
                // Skip patches from Blizzard or BlueTracker - they have better/more recent data
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            result.Success = true;
            result.Message = $"Synced {result.PatchesUpdated} patches from GitHub";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync patches from GitHub");
            result.Errors.Add($"Failed to sync patches: {ex.Message}");
        }

        return result;
    }

    public async Task<SyncResultDto> SyncWebPatchesAsync(bool isInitialSync = false, CancellationToken cancellationToken = default)
    {
        var result = new SyncResultDto { SyncedAt = DateTime.UtcNow };

        // Sync from BlueTracker (follows "View Full Article" links for Blizzard content)
        try
        {
            var blueTrackerCount = await SyncBlueTrackerPatchesAsync(isInitialSync, cancellationToken);
            result.PatchesUpdated += blueTrackerCount;
            _logger.LogInformation("Synced {Count} patches from BlueTracker (isInitialSync: {IsInitial})", blueTrackerCount, isInitialSync);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync patches from BlueTracker");
            result.Errors.Add($"BlueTracker sync failed: {ex.Message}");
        }

        result.Success = result.Errors.Count == 0;
        result.Message = $"Synced {result.PatchesUpdated} patches from web sources";
        return result;
    }

    #region BlueTracker Scraping

    private async Task<int> SyncBlueTrackerPatchesAsync(bool scanAllPages, CancellationToken cancellationToken)
    {
        var count = 0;
        var patchLinks = await GetBlueTrackerPatchLinksAsync(scanAllPages, cancellationToken);

        foreach (var patchInfo in patchLinks)
        {
            try
            {
                // Check if patch already exists
                var existingPatch = await _dbContext.Patches
                    .FirstOrDefaultAsync(p => p.InternalId == patchInfo.InternalId, cancellationToken);

                if (existingPatch == null)
                {
                    existingPatch = new Patch { InternalId = patchInfo.InternalId, Source = "bluetracker" };
                    _dbContext.Patches.Add(existingPatch);
                }

                // Don't overwrite Blizzard source data (Blizzard is authoritative)
                if (existingPatch.Source == "blizzard")
                {
                    // Only set alternate link if not already set
                    if (string.IsNullOrEmpty(existingPatch.AlternateLink))
                        existingPatch.AlternateLink = patchInfo.Url;
                    continue;
                }

                // Fetch content if we don't have it yet (may follow "View Full Article" link)
                if (string.IsNullOrEmpty(existingPatch.Content))
                {
                    await FetchBlueTrackerPatchContentAsync(patchInfo, existingPatch, cancellationToken);
                    
                    // Parse content into hero/map sections if we got content
                    if (!string.IsNullOrEmpty(existingPatch.Content))
                    {
                        await ParsePatchSectionsAsync(existingPatch, cancellationToken);
                    }
                }

                existingPatch.PatchName = patchInfo.Title;
                existingPatch.LiveDate = patchInfo.Date;
                existingPatch.PatchType = DeterminePatchType(patchInfo.Title);
                existingPatch.AlternateLink = patchInfo.Url;
                existingPatch.LastSyncedAt = DateTime.UtcNow;

                if (string.IsNullOrEmpty(existingPatch.Source))
                    existingPatch.Source = "bluetracker";

                count++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync BlueTracker patch: {Title}", patchInfo.Title);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return count;
    }

    private async Task<List<BlueTrackerPatchInfo>> GetBlueTrackerPatchLinksAsync(bool scanAllPages, CancellationToken cancellationToken)
    {
        var allTopics = new List<BlueTrackerTopicInfo>();
        var page = 1;
        var hasMorePages = true;

        while (hasMorePages)
        {
            var pageUrl = page == 1 ? BlueTrackerUrl : $"{BlueTrackerUrl}?page={page}";
            _logger.LogInformation("Scanning BlueTracker page {Page}: {Url}", page, pageUrl);

            try
            {
                var html = await _httpClient.GetStringAsync(pageUrl, cancellationToken);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var topicsFound = ParseBlueTrackerTopics(doc);

                if (topicsFound.Count == 0)
                {
                    hasMorePages = false;
                    break;
                }

                // Filter for patch notes topics only
                var patchTopics = topicsFound.Where(t =>
                    t.Title.Contains("Patch Notes", StringComparison.OrdinalIgnoreCase) ||
                    t.Title.Contains("Balance Patch", StringComparison.OrdinalIgnoreCase)).ToList();

                allTopics.AddRange(patchTopics);

                // If not initial sync, only scan page 1
                if (!scanAllPages)
                {
                    hasMorePages = false;
                }
                else
                {
                    // Check if we found any patch notes on this page
                    if (patchTopics.Count == 0)
                    {
                        // No patch notes on this page, but continue checking a few more pages 
                        if (page > 5 && allTopics.Count > 0)
                        {
                            hasMorePages = false;
                        }
                    }
                    page++;

                    // Safety limit
                    if (page > 50)
                    {
                        _logger.LogWarning("Reached page limit of 50, stopping pagination");
                        hasMorePages = false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch BlueTracker page {Page}", page);
                hasMorePages = false;
            }
        }

        // Apply EU preference and forum type preference
        var selectedPatches = SelectPreferredTopics(allTopics);

        return selectedPatches;
    }

    private List<BlueTrackerTopicInfo> ParseBlueTrackerTopics(HtmlDocument doc)
    {
        var topics = new List<BlueTrackerTopicInfo>();

        // Find all table rows in the topic listing
        var rows = doc.DocumentNode.SelectNodes("//table[contains(@class, 'topic-listing')]//tbody//tr");
        if (rows == null) return topics;

        foreach (var row in rows)
        {
            // Get the topic link
            var topicLink = row.SelectSingleNode(".//a[contains(@class, 'topic-link')]");
            if (topicLink == null) continue;

            var href = topicLink.GetAttributeValue("href", "");
            var title = topicLink.GetAttributeValue("title", "") ?? topicLink.InnerText.Trim();

            // Skip if not a valid topic link
            if (string.IsNullOrEmpty(href) || !href.Contains("/heroes/topic/"))
                continue;

            // Determine region from URL (eu-en or us-en)
            var isEu = href.Contains("/eu-en/");
            var isUs = href.Contains("/us-en/");

            // Get forum type from the Forum column
            var forumLink = row.SelectSingleNode(".//a[contains(@class, 'link-forum')]");
            var forumText = forumLink?.InnerText.Trim() ?? "";
            var isGeneralDiscussion = forumText.Contains("General Discussion", StringComparison.OrdinalIgnoreCase);
            var isBlogs = forumText.Contains("Blogs", StringComparison.OrdinalIgnoreCase);

            // Get date from datetime attribute
            var timeNode = row.SelectSingleNode(".//time[@datetime]");
            DateTime? date = null;
            if (timeNode != null)
            {
                var dateTimeStr = timeNode.GetAttributeValue("datetime", "");
                if (DateTime.TryParse(dateTimeStr, out var parsedDate))
                {
                    date = parsedDate;
                }
            }

            var fullUrl = href.StartsWith("http") ? href : $"{BlueTrackerBaseUrl}{href}";

            topics.Add(new BlueTrackerTopicInfo
            {
                Title = title,
                Url = fullUrl,
                IsEu = isEu,
                IsUs = isUs,
                IsGeneralDiscussion = isGeneralDiscussion,
                IsBlogs = isBlogs,
                Date = date ?? ExtractDateFromTitle(title)
            });
        }

        return topics;
    }

    private List<BlueTrackerPatchInfo> SelectPreferredTopics(List<BlueTrackerTopicInfo> topics)
    {
        var result = new List<BlueTrackerPatchInfo>();

        // Group by title (same patch may appear multiple times with different regions/forums)
        var groupedByTitle = topics
            .GroupBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var group in groupedByTitle)
        {
            // Preference order:
            // 1. EU General Discussion (has "View Full Article" link)
            // 2. EU Blogs (has full content directly)
            // 3. US General Discussion
            // 4. US Blogs

            var selected = group
                .OrderByDescending(t => t.IsEu)                    // EU first
                .ThenByDescending(t => t.IsGeneralDiscussion)      // General Discussion first
                .ThenByDescending(t => t.Date)                     // Most recent first
                .FirstOrDefault();

            if (selected == null) continue;

            // Also find the Blogs version for fallback content
            var blogsVersion = group
                .Where(t => t.IsBlogs && t.IsEu)
                .FirstOrDefault() ?? group.Where(t => t.IsBlogs).FirstOrDefault();

            var internalId = GenerateInternalId(selected.Title);

            result.Add(new BlueTrackerPatchInfo
            {
                Title = selected.Title,
                Url = selected.Url,
                InternalId = internalId,
                Date = selected.Date,
                IsGeneralDiscussion = selected.IsGeneralDiscussion,
                IsBlogs = selected.IsBlogs,
                BlogsUrl = blogsVersion?.Url
            });
        }

        return result.DistinctBy(p => p.InternalId).ToList();
    }

    private async Task FetchBlueTrackerPatchContentAsync(BlueTrackerPatchInfo patchInfo, Patch patch, CancellationToken cancellationToken)
    {
        try
        {
            var html = await _httpClient.GetStringAsync(patchInfo.Url, cancellationToken);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Try to find the main content area
            var contentNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'post-content')]")
                ?? doc.DocumentNode.SelectSingleNode("//article")
                ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'content')]");

            if (contentNode != null)
            {
                // For General Discussion topics: look for "View Full Article" link
                if (patchInfo.IsGeneralDiscussion)
                {
                    var viewFullArticleLink = contentNode.SelectSingleNode(".//a[contains(text(), 'View Full Article')]")
                        ?? contentNode.SelectSingleNode(".//a[contains(text(), 'View full article')]")
                        ?? contentNode.SelectSingleNode(".//a[contains(translate(text(), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), 'view full article')]");

                    if (viewFullArticleLink != null)
                    {
                        var fullArticleUrl = viewFullArticleLink.GetAttributeValue("href", "");
                        if (!string.IsNullOrEmpty(fullArticleUrl))
                        {
                            _logger.LogInformation("Found 'View Full Article' link for {Title}: {Url}", patchInfo.Title, fullArticleUrl);

                            // Store the official link
                            patch.OfficialLink = fullArticleUrl;

                            // Fetch the full content from the linked page (Blizzard News)
                            await FetchBlizzardPatchContentAsync(fullArticleUrl, patch, cancellationToken);

                            // If we successfully got content from Blizzard, update source
                            if (!string.IsNullOrEmpty(patch.Content))
                            {
                                patch.Source = "blizzard";
                                return;
                            }
                        }
                    }

                    // If "View Full Article" failed, try the Blogs version as fallback
                    if (!string.IsNullOrEmpty(patchInfo.BlogsUrl))
                    {
                        _logger.LogInformation("Falling back to Blogs version for {Title}", patchInfo.Title);
                        await FetchBlogsContentAsync(patchInfo.BlogsUrl, patch, cancellationToken);
                        if (!string.IsNullOrEmpty(patch.Content))
                        {
                            return;
                        }
                    }
                }

                // For Blogs topics or as final fallback: extract content directly from post-content
                patch.ContentHtml = contentNode.InnerHtml;
                patch.Content = ConvertHtmlToMarkdown(contentNode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch content for {Title}", patchInfo.Title);
        }
    }

    private async Task FetchBlogsContentAsync(string blogsUrl, Patch patch, CancellationToken cancellationToken)
    {
        try
        {
            var html = await _httpClient.GetStringAsync(blogsUrl, cancellationToken);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Blogs content is in div.post-content
            var contentNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'post-content')]");
            if (contentNode != null)
            {
                patch.ContentHtml = contentNode.InnerHtml;
                patch.Content = ConvertHtmlToMarkdown(contentNode);
                patch.Source = "bluetracker";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch Blogs content from {Url}", blogsUrl);
        }
    }

    #endregion

    #region Blizzard Content Fetching

    private async Task FetchBlizzardPatchContentAsync(string url, Patch patch, CancellationToken cancellationToken)
    {
        try
        {
            var html = await _httpClient.GetStringAsync(url, cancellationToken);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Blizzard article content is typically in section.blog or article
            var contentNode = doc.DocumentNode.SelectSingleNode("//section[contains(@class, 'blog')]")
                ?? doc.DocumentNode.SelectSingleNode("//article")
                ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'article-content')]")
                ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'blog-detail')]")
                ?? doc.DocumentNode.SelectSingleNode("//main");

            if (contentNode != null)
            {
                // Validate content - check if it looks like actual patch notes
                var text = contentNode.InnerText;
                if (text.Contains("Heroes of the Storm", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("Patch Notes", StringComparison.OrdinalIgnoreCase) ||
                    text.Length > 500)
                {
                    patch.ContentHtml = contentNode.InnerHtml;
                    patch.Content = ConvertHtmlToMarkdown(contentNode);
                }
                else
                {
                    _logger.LogWarning("Content from {Url} doesn't appear to be valid patch notes", url);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch Blizzard content for {Url}", url);
        }
    }

    #endregion

    #region Patch Section Parsing

    private async Task ParsePatchSectionsAsync(Patch patch, CancellationToken cancellationToken)
    {
        try
        {
            // Get all hero names for matching
            var heroes = await _dbContext.Heroes
                .Select(h => new { h.Id, h.Name, h.ShortName })
                .ToListAsync(cancellationToken);

            var heroNameMap = heroes.ToDictionary(h => h.Name.ToLowerInvariant(), h => h.Id);
            
            // Also add common variations
            foreach (var hero in heroes)
            {
                var shortLower = hero.ShortName.ToLowerInvariant();
                if (!heroNameMap.ContainsKey(shortLower))
                    heroNameMap[shortLower] = hero.Id;
            }

            // Clear existing sections for this patch
            var existingSections = await _dbContext.PatchSections
                .Where(s => s.PatchId == patch.Id)
                .ToListAsync(cancellationToken);
            _dbContext.PatchSections.RemoveRange(existingSections);

            // Parse the HTML content to find hero sections
            if (!string.IsNullOrEmpty(patch.ContentHtml))
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(patch.ContentHtml);

                // Find all headings that might be hero names
                var headings = doc.DocumentNode.SelectNodes("//h2 | //h3 | //h4 | //strong");
                if (headings != null)
                {
                    var currentSection = new PatchSection();
                    var sectionContent = new System.Text.StringBuilder();
                    string? currentHeroName = null;
                    int? currentHeroId = null;
                    bool inHeroSection = false;

                    foreach (var heading in headings)
                    {
                        var headingText = heading.InnerText.Trim();
                        var headingLower = headingText.ToLowerInvariant();

                        // Check if this heading matches a hero name
                        if (heroNameMap.TryGetValue(headingLower, out var heroId))
                        {
                            // Save previous section if we were in one
                            if (inHeroSection && !string.IsNullOrWhiteSpace(sectionContent.ToString()))
                            {
                                var section = new PatchSection
                                {
                                    PatchId = patch.Id,
                                    SectionType = "Hero",
                                    EntityName = currentHeroName!,
                                    HeroId = currentHeroId,
                                    Content = sectionContent.ToString().Trim(),
                                    ContentHtml = GetSectionHtml(doc, currentHeroName!)
                                };
                                _dbContext.PatchSections.Add(section);
                            }

                            // Start new hero section
                            currentHeroName = headingText;
                            currentHeroId = heroId;
                            inHeroSection = true;
                            sectionContent.Clear();

                            // Get the content following this heading
                            var nextSibling = heading.NextSibling;
                            while (nextSibling != null)
                            {
                                if (nextSibling.Name == "h2" || nextSibling.Name == "h3" || nextSibling.Name == "h4")
                                {
                                    var siblingText = nextSibling.InnerText.Trim().ToLowerInvariant();
                                    if (heroNameMap.ContainsKey(siblingText))
                                        break; // Next hero section
                                }
                                
                                if (nextSibling.NodeType == HtmlAgilityPack.HtmlNodeType.Element ||
                                    nextSibling.NodeType == HtmlAgilityPack.HtmlNodeType.Text)
                                {
                                    var text = nextSibling.InnerText.Trim();
                                    if (!string.IsNullOrEmpty(text))
                                        sectionContent.AppendLine(text);
                                }
                                
                                nextSibling = nextSibling.NextSibling;
                            }
                        }
                    }

                    // Save the last section
                    if (inHeroSection && !string.IsNullOrWhiteSpace(sectionContent.ToString()))
                    {
                        var section = new PatchSection
                        {
                            PatchId = patch.Id,
                            SectionType = "Hero",
                            EntityName = currentHeroName!,
                            HeroId = currentHeroId,
                            Content = sectionContent.ToString().Trim(),
                            ContentHtml = GetSectionHtml(doc, currentHeroName!)
                        };
                        _dbContext.PatchSections.Add(section);
                    }
                }
            }

            _logger.LogInformation("Parsed {Count} sections from patch {PatchName}", 
                _dbContext.ChangeTracker.Entries<PatchSection>().Count(e => e.State == EntityState.Added),
                patch.PatchName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse sections for patch {PatchName}", patch.PatchName);
        }
    }

    private static string? GetSectionHtml(HtmlDocument doc, string heroName)
    {
        // Try to find the section HTML for this hero
        var headings = doc.DocumentNode.SelectNodes($"//h2[contains(text(), '{heroName}')] | //h3[contains(text(), '{heroName}')] | //h4[contains(text(), '{heroName}')]");
        if (headings == null || headings.Count == 0) return null;

        var heading = headings.First();
        var sb = new System.Text.StringBuilder();
        sb.Append(heading.OuterHtml);

        var nextSibling = heading.NextSibling;
        while (nextSibling != null)
        {
            if (nextSibling.Name == "h2" || nextSibling.Name == "h3" || nextSibling.Name == "h4")
                break;
            sb.Append(nextSibling.OuterHtml);
            nextSibling = nextSibling.NextSibling;
        }

        return sb.ToString();
    }

    #endregion

    #region Helper Methods

    private static string GenerateInternalId(string title)
    {
        // Convert title to URL-friendly internal ID
        var normalized = title.ToLowerInvariant()
            .Replace("heroes of the storm", "")
            .Trim();

        // Remove special characters and replace spaces with hyphens
        normalized = Regex.Replace(normalized, @"[^a-z0-9\s-]", "");
        normalized = Regex.Replace(normalized, @"\s+", "-");
        normalized = Regex.Replace(normalized, @"-+", "-");
        normalized = normalized.Trim('-');

        return normalized;
    }

    private static string DeterminePatchType(string title)
    {
        var lowerTitle = title.ToLowerInvariant();

        if (lowerTitle.Contains("ptr"))
            return "PTR";
        if (lowerTitle.Contains("balance"))
            return "Balance Update";
        if (lowerTitle.Contains("hotfix"))
            return "Hotfix Patch";
        if (lowerTitle.Contains("live"))
            return "Major Patch";

        return "Patch Notes";
    }

    private static DateTime? ExtractDateFromTitle(string title)
    {
        // Try to extract date patterns like "January 14, 2026" or "December 12, 2025"
        var patterns = new[]
        {
            @"(January|February|March|April|May|June|July|August|September|October|November|December)\s+(\d{1,2}),?\s+(\d{4})",
            @"(\d{1,2})[/-](\d{1,2})[/-](\d{4})",
            @"(\d{4})[/-](\d{1,2})[/-](\d{1,2})"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(title, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (DateTime.TryParse(match.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    return date;
                }
            }
        }

        return null;
    }

    private static string ConvertHtmlToMarkdown(HtmlNode node)
    {
        var text = new System.Text.StringBuilder();
        ConvertNodeToMarkdown(node, text);
        return text.ToString().Trim();
    }

    private static void ConvertNodeToMarkdown(HtmlNode node, System.Text.StringBuilder sb)
    {
        foreach (var child in node.ChildNodes)
        {
            switch (child.Name.ToLower())
            {
                case "#text":
                    var textContent = HtmlEntity.DeEntitize(child.InnerText);
                    if (!string.IsNullOrWhiteSpace(textContent))
                        sb.Append(textContent);
                    break;

                case "h1":
                    sb.AppendLine();
                    sb.Append("# ");
                    ConvertNodeToMarkdown(child, sb);
                    sb.AppendLine();
                    break;

                case "h2":
                    sb.AppendLine();
                    sb.Append("## ");
                    ConvertNodeToMarkdown(child, sb);
                    sb.AppendLine();
                    break;

                case "h3":
                    sb.AppendLine();
                    sb.Append("### ");
                    ConvertNodeToMarkdown(child, sb);
                    sb.AppendLine();
                    break;

                case "h4":
                    sb.AppendLine();
                    sb.Append("#### ");
                    ConvertNodeToMarkdown(child, sb);
                    sb.AppendLine();
                    break;

                case "p":
                    sb.AppendLine();
                    ConvertNodeToMarkdown(child, sb);
                    sb.AppendLine();
                    break;

                case "br":
                    sb.AppendLine();
                    break;

                case "strong":
                case "b":
                    sb.Append("**");
                    ConvertNodeToMarkdown(child, sb);
                    sb.Append("**");
                    break;

                case "em":
                case "i":
                    sb.Append("*");
                    ConvertNodeToMarkdown(child, sb);
                    sb.Append("*");
                    break;

                case "ul":
                    sb.AppendLine();
                    foreach (var li in child.SelectNodes("li") ?? Enumerable.Empty<HtmlNode>())
                    {
                        sb.Append("- ");
                        ConvertNodeToMarkdown(li, sb);
                        sb.AppendLine();
                    }
                    break;

                case "ol":
                    sb.AppendLine();
                    var index = 1;
                    foreach (var li in child.SelectNodes("li") ?? Enumerable.Empty<HtmlNode>())
                    {
                        sb.Append($"{index}. ");
                        ConvertNodeToMarkdown(li, sb);
                        sb.AppendLine();
                        index++;
                    }
                    break;

                case "a":
                    var href = child.GetAttributeValue("href", "");
                    sb.Append('[');
                    ConvertNodeToMarkdown(child, sb);
                    sb.Append($"]({href})");
                    break;

                case "hr":
                    sb.AppendLine();
                    sb.AppendLine("---");
                    break;

                case "div":
                case "span":
                case "section":
                case "article":
                    ConvertNodeToMarkdown(child, sb);
                    break;

                default:
                    ConvertNodeToMarkdown(child, sb);
                    break;
            }
        }
    }

    #endregion

    #region Existing Methods

    private async Task<List<string>> GetHeroFileListAsync(CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetStringAsync(HeroListUrl, cancellationToken);
        var files = JsonSerializer.Deserialize<List<GitHubFileInfo>>(response, JsonOptions);

        return files?
            .Where(f => f.Name.EndsWith(".json"))
            .Select(f => f.Name.Replace(".json", ""))
            .ToList() ?? new List<string>();
    }

    private async Task SyncHeroAsync(string heroShortName, CancellationToken cancellationToken)
    {
        var url = $"{HeroesBaseUrl}{heroShortName}.json";
        var response = await _httpClient.GetStringAsync(url, cancellationToken);
        var heroData = JsonSerializer.Deserialize<GitHubHeroData>(response, JsonOptions);

        if (heroData == null)
        {
            throw new InvalidOperationException($"Failed to parse hero data for {heroShortName}");
        }

        var existingHero = await _dbContext.Heroes
            .Include(h => h.Abilities)
            .Include(h => h.Talents)
            .FirstOrDefaultAsync(h => h.ShortName == heroShortName, cancellationToken);

        if (existingHero == null)
        {
            existingHero = new Hero { ShortName = heroShortName };
            _dbContext.Heroes.Add(existingHero);
        }
        else
        {
            // Clear existing abilities and talents for update
            _dbContext.Abilities.RemoveRange(existingHero.Abilities);
            _dbContext.Talents.RemoveRange(existingHero.Talents);
        }

        MapHeroData(heroData, existingHero);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Synced hero: {HeroName}", existingHero.Name);
    }

    private void MapHeroData(GitHubHeroData data, Hero hero)
    {
        hero.Name = data.Name ?? hero.ShortName;
        hero.HyperlinkId = data.HyperlinkId;
        hero.AttributeId = data.AttributeId;
        hero.Icon = data.Icon;
        hero.Role = data.Role;
        hero.ExpandedRole = data.ExpandedRole;
        hero.Type = data.Type;
        hero.ReleasePatch = data.ReleasePatch;
        hero.LastSyncedAt = DateTime.UtcNow;

        if (DateTime.TryParse(data.ReleaseDate, out var releaseDate))
        {
            hero.ReleaseDate = releaseDate;
        }

        if (data.Tags != null)
        {
            hero.TagsJson = JsonSerializer.Serialize(data.Tags);
        }

        // Map abilities
        if (data.Abilities != null)
        {
            foreach (var (formName, abilities) in data.Abilities)
            {
                foreach (var abilityData in abilities)
                {
                    var ability = new Ability
                    {
                        Hero = hero,
                        FormName = formName,
                        Uid = abilityData.Uid,
                        Name = abilityData.Name ?? "Unknown",
                        Description = abilityData.Description,
                        Hotkey = abilityData.Hotkey,
                        AbilityId = abilityData.AbilityId,
                        Cooldown = abilityData.Cooldown,
                        ManaCost = abilityData.ManaCost?.ToString(),
                        Icon = abilityData.Icon,
                        Type = abilityData.Type,
                        IsTrait = abilityData.Trait ?? false
                    };
                    hero.Abilities.Add(ability);
                }
            }
        }

        // Map talents
        if (data.Talents != null)
        {
            foreach (var (levelStr, talents) in data.Talents)
            {
                if (!int.TryParse(levelStr, out var level)) continue;

                foreach (var talentData in talents)
                {
                    var talent = new Talent
                    {
                        Hero = hero,
                        Level = level,
                        TooltipId = talentData.TooltipId,
                        TalentTreeId = talentData.TalentTreeId,
                        Name = talentData.Name ?? "Unknown",
                        Description = talentData.Description,
                        Icon = talentData.Icon,
                        Type = talentData.Type,
                        Sort = talentData.Sort ?? 0,
                        Cooldown = talentData.Cooldown,
                        AbilityId = talentData.AbilityId
                    };

                    if (talentData.AbilityLinks != null)
                    {
                        talent.AbilityLinksJson = JsonSerializer.Serialize(talentData.AbilityLinks);
                    }

                    hero.Talents.Add(talent);
                }
            }
        }
    }

    private void MapPatchData(GitHubPatchData data, Patch patch)
    {
        patch.PatchName = data.PatchName;
        patch.PatchType = data.PatchType;
        patch.GameVersion = data.GameVersion;
        patch.FullVersion = data.FullVersion;
        patch.OfficialLink = data.OfficialLink;
        patch.AlternateLink = data.AlternateLink;
        patch.LiveBuild = data.LiveBuild;
        patch.PtrOfficialLink = data.PtrOfficialLink;
        patch.PtrBuild = data.PtrBuild;
        patch.LastSyncedAt = DateTime.UtcNow;
        patch.Source = "github";

        if (DateTime.TryParse(data.LiveDate, out var liveDate))
        {
            patch.LiveDate = liveDate;
        }

        if (DateTime.TryParse(data.PtrDate, out var ptrDate))
        {
            patch.PtrDate = ptrDate;
        }
    }

    #endregion

    #region Data Models

    private class BlueTrackerTopicInfo
    {
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public bool IsEu { get; set; }
        public bool IsUs { get; set; }
        public bool IsGeneralDiscussion { get; set; }
        public bool IsBlogs { get; set; }
        public DateTime? Date { get; set; }
    }

    private class BlueTrackerPatchInfo
    {
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string InternalId { get; set; } = string.Empty;
        public DateTime? Date { get; set; }
        public bool IsGeneralDiscussion { get; set; }
        public bool IsBlogs { get; set; }
        public string? BlogsUrl { get; set; }
    }

    private class GitHubFileInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    private class GitHubHeroData
    {
        public int? Id { get; set; }
        public string? ShortName { get; set; }
        public string? HyperlinkId { get; set; }
        public string? AttributeId { get; set; }
        public string? Name { get; set; }
        public string? Icon { get; set; }
        public string? Role { get; set; }
        public string? ExpandedRole { get; set; }
        public string? Type { get; set; }
        public string? ReleaseDate { get; set; }
        public string? ReleasePatch { get; set; }
        public List<string>? Tags { get; set; }
        public Dictionary<string, List<GitHubAbilityData>>? Abilities { get; set; }
        public Dictionary<string, List<GitHubTalentData>>? Talents { get; set; }
    }

    private class GitHubAbilityData
    {
        public string? Uid { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Hotkey { get; set; }
        public string? AbilityId { get; set; }
        public double? Cooldown { get; set; }
        public object? ManaCost { get; set; }
        public string? Icon { get; set; }
        public string? Type { get; set; }
        public bool? Trait { get; set; }
    }

    private class GitHubTalentData
    {
        public string? TooltipId { get; set; }
        public string? TalentTreeId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public string? Type { get; set; }
        public int? Sort { get; set; }
        public double? Cooldown { get; set; }
        public string? AbilityId { get; set; }
        public List<string>? AbilityLinks { get; set; }
    }

    private class GitHubPatchData
    {
        public string InternalId { get; set; } = string.Empty;
        public string? PatchName { get; set; }
        public string? OfficialLink { get; set; }
        public string? AlternateLink { get; set; }
        public string? PatchType { get; set; }
        public string? GameVersion { get; set; }
        public string? FullVersion { get; set; }
        public string? PtrOfficialLink { get; set; }
        public string? PtrDate { get; set; }
        public string? PtrBuild { get; set; }
        public string? LiveDate { get; set; }
        public string? LiveBuild { get; set; }
    }

    #endregion
}
