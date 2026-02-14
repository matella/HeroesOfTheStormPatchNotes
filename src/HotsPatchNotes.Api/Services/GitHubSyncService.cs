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

/// <summary>
/// Service interface for syncing Heroes of the Storm data from various sources.
/// </summary>
public interface IGitHubSyncService
{
    /// <summary>
    /// Syncs all data (heroes, patches, battlegrounds) from all available sources.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Sync result with counts and any errors.</returns>
    Task<SyncResultDto> SyncAllAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Syncs hero data from the heroespatchnotes/heroes-talents GitHub repository.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Sync result with hero count and any errors.</returns>
    Task<SyncResultDto> SyncHeroesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Syncs patch data from the heroespatchnotes/heroes-patch-data GitHub archive.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Sync result with patch count and any errors.</returns>
    Task<SyncResultDto> SyncPatchesFromGitHubAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Syncs patch data by scraping the BlueTracker website.
    /// </summary>
    /// <param name="isInitialSync">If true, scans all pages; if false, only scans the first page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Sync result with patch count and any errors.</returns>
    Task<SyncResultDto> SyncPatchesFromBlueTrackerAsync(bool isInitialSync = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Syncs battleground data from the Heroes of the Storm Fandom wiki.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Sync result with battleground count and any errors.</returns>
    Task<SyncResultDto> SyncBattlegroundsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for syncing Heroes of the Storm data from GitHub repositories and web sources.
/// </summary>
public sealed partial class GitHubSyncService(
    HotsDbContext dbContext,
    HttpClient httpClient,
    ILogger<GitHubSyncService> logger,
    IHtmlContentService htmlContentService,
    IBattlegroundScraper battlegroundScraper,
    IImageDownloadService imageDownloadService,
    IHeroScraper heroScraper) : IGitHubSyncService
{
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

    public async Task<SyncResultDto> SyncAllAsync(CancellationToken cancellationToken = default)
    {
        var isInitialSync = !await dbContext.Patches.AnyAsync(cancellationToken);
        logger.LogInformation("Starting {SyncType} sync", isInitialSync ? "initial" : "subsequent");

        if (isInitialSync)
        {
            // Initial sync: GitHub (archive) → BlueTracker (all pages) → Battlegrounds
            var heroResult = await SyncHeroesAsync(cancellationToken);
            var patchResult = await SyncPatchesFromGitHubAsync(cancellationToken);
            var webPatchResult = await SyncPatchesFromBlueTrackerAsync(isInitialSync: true, cancellationToken);
            var battlegroundResult = await SyncBattlegroundsAsync(cancellationToken);

            return new SyncResultDto
            {
                Success = heroResult.Success && patchResult.Success && webPatchResult.Success && battlegroundResult.Success,
                Message = $"Initial sync complete: {heroResult.HeroesUpdated} heroes, {patchResult.PatchesUpdated + webPatchResult.PatchesUpdated} patches, {battlegroundResult.HeroesUpdated} battlegrounds",
                HeroesUpdated = heroResult.HeroesUpdated,
                PatchesUpdated = patchResult.PatchesUpdated + webPatchResult.PatchesUpdated,
                SyncedAt = DateTime.UtcNow,
                Errors = heroResult.Errors.Concat(patchResult.Errors).Concat(webPatchResult.Errors).Concat(battlegroundResult.Errors).ToList()
            };
        }
        else
        {
            // Subsequent sync: BlueTracker (page 1 only) - no GitHub needed
            var heroResult = await SyncHeroesAsync(cancellationToken);
            var webPatchResult = await SyncPatchesFromBlueTrackerAsync(isInitialSync: false, cancellationToken);

            return new SyncResultDto
            {
                Success = heroResult.Success && webPatchResult.Success,
                Message = $"Sync complete: {heroResult.HeroesUpdated} heroes, {webPatchResult.PatchesUpdated} patches updated",
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
            logger.LogInformation("Found {Count} hero files to sync", heroFiles.Count);

            foreach (var heroFile in heroFiles)
            {
                try
                {
                    await SyncHeroAsync(heroFile, cancellationToken);
                    result.HeroesUpdated++;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to sync hero: {HeroFile}", heroFile);
                    result.Errors.Add($"Failed to sync {heroFile}: {ex.Message}");
                }
            }

            result.Success = result.Errors.Count == 0;
            result.Message = $"Synced {result.HeroesUpdated} heroes";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync heroes");
            result.Errors.Add($"Failed to sync heroes: {ex.Message}");
        }

        return result;
    }

    public async Task<SyncResultDto> SyncPatchesFromGitHubAsync(CancellationToken cancellationToken = default)
    {
        var result = new SyncResultDto { SyncedAt = DateTime.UtcNow };

        try
        {
            var response = await httpClient.GetStringAsync(PatchesUrl, cancellationToken);
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
                var existingPatch = await dbContext.Patches
                    .FirstOrDefaultAsync(p => p.InternalId == patch.InternalId, cancellationToken);

                if (existingPatch == null)
                {
                    // Only create new patches from GitHub (tertiary source)
                    existingPatch = new Patch { InternalId = patch.InternalId, Source = "github" };
                    dbContext.Patches.Add(existingPatch);
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

            await dbContext.SaveChangesAsync(cancellationToken);
            result.Success = true;
            result.Message = $"Synced {result.PatchesUpdated} patches from GitHub";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync patches from GitHub");
            result.Errors.Add($"Failed to sync patches: {ex.Message}");
        }

        return result;
    }

    public async Task<SyncResultDto> SyncPatchesFromBlueTrackerAsync(bool isInitialSync = false, CancellationToken cancellationToken = default)
    {
        var result = new SyncResultDto { SyncedAt = DateTime.UtcNow };

        // Sync from BlueTracker (follows "View Full Article" links for Blizzard content)
        try
        {
            var blueTrackerCount = await SyncBlueTrackerPatchesAsync(isInitialSync, cancellationToken);
            result.PatchesUpdated += blueTrackerCount;
            logger.LogInformation("Synced {Count} patches from BlueTracker (isInitialSync: {IsInitial})", blueTrackerCount, isInitialSync);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync patches from BlueTracker");
            result.Errors.Add($"BlueTracker sync failed: {ex.Message}");
        }

        result.Success = result.Errors.Count == 0;
        result.Message = $"Synced {result.PatchesUpdated} patches from web sources";
        return result;
    }

    #region BlueTracker Scraping

    /// <summary>
    /// Syncs patches from BlueTracker by scraping their patch notes listings.
    /// </summary>
    /// <param name="scanAllPages">If true, scans all available pages; if false, only scans the first page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of patches synced.</returns>
    private async Task<int> SyncBlueTrackerPatchesAsync(bool scanAllPages, CancellationToken cancellationToken)
    {
        var count = 0;
        var patchLinks = await ScrapeBlueTrackerPatchListingsAsync(scanAllPages, cancellationToken);

        foreach (var patchInfo in patchLinks)
        {
            try
            {
                // Check if patch already exists
                var existingPatch = await dbContext.Patches
                    .FirstOrDefaultAsync(p => p.InternalId == patchInfo.InternalId, cancellationToken);

                if (existingPatch == null)
                {
                    existingPatch = new Patch { InternalId = patchInfo.InternalId, Source = "bluetracker" };
                    dbContext.Patches.Add(existingPatch);
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
                    await FetchAndParseBlueTrackerArticleAsync(patchInfo, existingPatch, cancellationToken);

                    // Parse content into hero/map sections if we got content
                    if (!string.IsNullOrEmpty(existingPatch.Content))
                    {
                        // Save the patch first to get its ID (needed for section FK)
                        await dbContext.SaveChangesAsync(cancellationToken);
                        await ExtractAndSaveHeroSectionsFromPatchAsync(existingPatch, cancellationToken);
                    }
                }

                existingPatch.PatchName = patchInfo.Title;
                existingPatch.LiveDate = patchInfo.Date;
                existingPatch.PatchType = InferPatchTypeFromTitle(patchInfo.Title);
                existingPatch.AlternateLink = patchInfo.Url;
                existingPatch.LastSyncedAt = DateTime.UtcNow;

                if (string.IsNullOrEmpty(existingPatch.Source))
                    existingPatch.Source = "bluetracker";

                count++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to sync BlueTracker patch: {Title}", patchInfo.Title);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return count;
    }

    /// <summary>
    /// Scrapes the BlueTracker website for patch note listings.
    /// </summary>
    /// <param name="scanAllPages">If true, paginates through all pages; if false, only scans page 1.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of patch information scraped from BlueTracker.</returns>
    private async Task<List<BlueTrackerPatchInfo>> ScrapeBlueTrackerPatchListingsAsync(bool scanAllPages, CancellationToken cancellationToken)
    {
        var allTopics = new List<BlueTrackerTopicInfo>();
        var page = 1;
        var hasMorePages = true;

        while (hasMorePages)
        {
            var pageUrl = page == 1 ? BlueTrackerUrl : $"{BlueTrackerUrl}?page={page}";
            logger.LogInformation("Scanning BlueTracker page {Page}: {Url}", page, pageUrl);

            try
            {
                var html = await httpClient.GetStringAsync(pageUrl, cancellationToken);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var topicsFound = ExtractTopicsFromTableRows(doc);

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
                        logger.LogWarning("Reached page limit of 50, stopping pagination");
                        hasMorePages = false;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch BlueTracker page {Page}", page);
                hasMorePages = false;
            }
        }

        // Apply EU preference and forum type preference
        var selectedPatches = DeduplicateTopicsByRegionPreference(allTopics);

        return selectedPatches;
    }

    /// <summary>
    /// Extracts topic information from BlueTracker HTML table rows.
    /// </summary>
    /// <param name="doc">The HTML document to parse.</param>
    /// <returns>List of topic information extracted from the table.</returns>
    private List<BlueTrackerTopicInfo> ExtractTopicsFromTableRows(HtmlDocument doc)
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
                Date = date ?? ParseDateFromPatchTitle(title)
            });
        }

        return topics;
    }

    /// <summary>
    /// Deduplicates topics that appear in multiple regions/forums and selects the preferred version.
    /// Preference: EU General Discussion > EU Blogs > US General Discussion > US Blogs.
    /// </summary>
    /// <param name="topics">List of topics to deduplicate.</param>
    /// <returns>Deduplicated list with preferred topic versions.</returns>
    private List<BlueTrackerPatchInfo> DeduplicateTopicsByRegionPreference(List<BlueTrackerTopicInfo> topics)
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

            var internalId = ConvertTitleToUrlSlug(selected.Title);

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

    /// <summary>
    /// Fetches and parses patch content from a BlueTracker article.
    /// May follow "View Full Article" links to get content from official Blizzard sources.
    /// </summary>
    /// <param name="patchInfo">Information about the patch to fetch.</param>
    /// <param name="patch">The patch entity to populate with content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task FetchAndParseBlueTrackerArticleAsync(BlueTrackerPatchInfo patchInfo, Patch patch, CancellationToken cancellationToken)
    {
        try
        {
            var html = await httpClient.GetStringAsync(patchInfo.Url, cancellationToken);
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
                            logger.LogInformation("Found 'View Full Article' link for {Title}: {Url}", patchInfo.Title, fullArticleUrl);

                            // Store the official link
                            patch.OfficialLink = fullArticleUrl;

                            // Fetch the full content from the linked page (Blizzard News)
                            await ExtractContentFromBlizzardForumPostAsync(fullArticleUrl, patch, cancellationToken);

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
                        logger.LogInformation("Falling back to Blogs version for {Title}", patchInfo.Title);
                        await ExtractContentFromBlogsPostAsync(patchInfo.BlogsUrl, patch, cancellationToken);
                        if (!string.IsNullOrEmpty(patch.Content))
                        {
                            return;
                        }
                    }
                }

                // For Blogs topics or as final fallback: extract content directly from post-content
                patch.ContentHtml = contentNode.InnerHtml;
                patch.Content = htmlContentService.SanitizeHtml(contentNode.InnerHtml);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch content for {Title}", patchInfo.Title);
        }
    }

    /// <summary>
    /// Extracts patch content from a BlueTracker Blogs post.
    /// </summary>
    /// <param name="blogsUrl">URL of the Blogs post.</param>
    /// <param name="patch">The patch entity to populate with content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task ExtractContentFromBlogsPostAsync(string blogsUrl, Patch patch, CancellationToken cancellationToken)
    {
        try
        {
            var html = await httpClient.GetStringAsync(blogsUrl, cancellationToken);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Blogs content is in div.post-content
            var contentNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'post-content')]");
            if (contentNode != null)
            {
                patch.ContentHtml = contentNode.InnerHtml;
                patch.Content = htmlContentService.SanitizeHtml(contentNode.InnerHtml);
                patch.Source = "bluetracker";
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch Blogs content from {Url}", blogsUrl);
        }
    }

    #endregion

    #region Blizzard Content Fetching

    /// <summary>
    /// Extracts patch content from an official Blizzard forum post or news article.
    /// </summary>
    /// <param name="url">URL of the Blizzard content.</param>
    /// <param name="patch">The patch entity to populate with content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task ExtractContentFromBlizzardForumPostAsync(string url, Patch patch, CancellationToken cancellationToken)
    {
        try
        {
            var html = await httpClient.GetStringAsync(url, cancellationToken);
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
                    patch.Content = htmlContentService.SanitizeHtml(contentNode.InnerHtml);
                }
                else
                {
                    logger.LogWarning("Content from {Url} doesn't appear to be valid patch notes", url);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch Blizzard content for {Url}", url);
        }
    }

    #endregion

    #region Patch Section Parsing

    /// <summary>
    /// Extracts hero-specific sections from patch content and saves them to the database.
    /// Uses a two-phase save to properly handle parent-child section relationships.
    /// </summary>
    /// <param name="patch">The patch to extract sections from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task ExtractAndSaveHeroSectionsFromPatchAsync(Patch patch, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogDebug("Extracting sections from patch {PatchId}: {PatchName}", patch.Id, patch.PatchName);

            // Get all hero names for matching
            var heroes = await dbContext.Heroes
                .Select(h => new { h.Id, h.Name, h.ShortName })
                .ToListAsync(cancellationToken);

            var heroNameMap = BuildHeroNameLookup(heroes);

            // Clear existing sections for this patch
            var existingSections = await dbContext.PatchSections
                .Where(s => s.PatchId == patch.Id)
                .ToListAsync(cancellationToken);
            
            if (existingSections.Any())
            {
                logger.LogDebug("Removing {Count} existing sections for patch {PatchName}", 
                    existingSections.Count, patch.PatchName);
                dbContext.PatchSections.RemoveRange(existingSections);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            // Parse HTML content (now stored as sanitized HTML)
            var htmlContent = patch.Content;
            if (string.IsNullOrEmpty(htmlContent))
            {
                logger.LogDebug("No content for patch {PatchName}", patch.PatchName);
                return;
            }

            // Parse sections from HTML
            var sections = ExtractSectionsFromHtmlDocument(htmlContent, heroNameMap, patch.Id, patch.PatchName ?? "Unknown");

            if (!sections.Any())
            {
                logger.LogDebug("No sections extracted from patch {PatchName}", patch.PatchName);
                return;
            }

            // Save sections in two phases to handle parent-child relationships
            // Phase 1: Add all sections without ParentSectionId
            var sectionsWithoutParents = sections.Select(s => new PatchSection
            {
                PatchId = s.PatchId,
                Order = s.Order,
                HeadingLevel = s.HeadingLevel,
                ParentSectionId = null, // Will be set in phase 2
                SectionType = s.SectionType,
                EntityName = s.EntityName,
                HeroId = s.HeroId,
                Content = s.Content
            }).ToList();

            foreach (var section in sectionsWithoutParents)
            {
                dbContext.PatchSections.Add(section);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            // Phase 2: Update ParentSectionId with real database IDs
            for (int i = 0; i < sections.Count; i++)
            {
                var originalSection = sections[i];
                var savedSection = sectionsWithoutParents[i];

                // Find parent based on heading hierarchy
                if (originalSection.HeadingLevel > 1)
                {
                    // Look backwards for a section with lower heading level
                    for (int j = i - 1; j >= 0; j--)
                    {
                        if (sections[j].HeadingLevel < originalSection.HeadingLevel)
                        {
                            savedSection.ParentSectionId = sectionsWithoutParents[j].Id;
                            break;
                        }
                    }
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Saved {Count} sections for patch {PatchName}",
                sections.Count,
                patch.PatchName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to extract and save sections for patch {PatchId}: {PatchName}", 
                patch.Id, patch.PatchName);
            throw;
        }
    }

    /// <summary>
    /// Builds a comprehensive hero name lookup dictionary for matching hero names in patch notes.
    /// Includes exact names, short names, and normalized versions (without special characters).
    /// </summary>
    /// <typeparam name="T">The type of hero data objects.</typeparam>
    /// <param name="heroes">List of hero data with Id, Name, and ShortName properties.</param>
    /// <returns>Dictionary mapping hero names (and variations) to hero IDs.</returns>
    private Dictionary<string, int> BuildHeroNameLookup<T>(List<T> heroes) where T : class
    {
        var heroNameMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (dynamic hero in heroes)
        {
            int heroId = hero.Id;
            string heroName = hero.Name;
            string shortName = hero.ShortName;

            // Add exact name
            if (!string.IsNullOrEmpty(heroName))
                heroNameMap.TryAdd(heroName.ToLowerInvariant(), heroId);

            // Add short name
            if (!string.IsNullOrEmpty(shortName))
                heroNameMap.TryAdd(shortName.ToLowerInvariant(), heroId);

            // Add normalized versions (remove special characters)
            var normalizedName = NormalizeHeroName(heroName);
            if (!string.IsNullOrEmpty(normalizedName))
                heroNameMap.TryAdd(normalizedName, heroId);

            var normalizedShort = NormalizeHeroName(shortName);
            if (!string.IsNullOrEmpty(normalizedShort))
                heroNameMap.TryAdd(normalizedShort, heroId);
        }

        return heroNameMap;
    }

    /// <summary>
    /// Normalizes a hero name by removing special characters (apostrophes, periods, spaces, dashes).
    /// This helps match hero names like "Zul'jin" with "Zuljin" in patch notes.
    /// </summary>
    /// <param name="name">The hero name to normalize.</param>
    /// <returns>Normalized hero name in lowercase, or empty string if input is null/empty.</returns>
    private static string NormalizeHeroName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        // Remove apostrophes, periods, spaces, dashes, and convert to lowercase
        return SpecialCharactersRegex().Replace(name, "").ToLowerInvariant();
    }

    /// <summary>
    /// Extracts structured sections from HTML patch content.
    /// Identifies headings, content, section types, and attempts to match hero names.
    /// </summary>
    /// <param name="htmlContent">The HTML content to parse.</param>
    /// <param name="heroNameMap">Dictionary mapping hero names to IDs.</param>
    /// <param name="patchId">The ID of the patch these sections belong to.</param>
    /// <param name="patchName">The name of the patch (for logging).</param>
    /// <returns>List of patch sections with hero associations where found.</returns>
    private List<PatchSection> ExtractSectionsFromHtmlDocument(
        string htmlContent,
        Dictionary<string, int> heroNameMap,
        int patchId,
        string patchName)
    {
        var sections = new List<PatchSection>();
        var doc = new HtmlDocument();
        doc.LoadHtml(htmlContent);

        var allNodes = doc.DocumentNode.ChildNodes;
        int order = 0;
        int unmatchedHeroCount = 0;

        for (int i = 0; i < allNodes.Count; i++)
        {
            var node = allNodes[i];

            // Check if this is a heading
            if (!node.Name.StartsWith("h", StringComparison.OrdinalIgnoreCase) ||
                node.Name.Length != 2 ||
                !char.IsDigit(node.Name[1]))
            {
                continue;
            }

            var headingLevel = int.Parse(node.Name[1].ToString());
            var headingText = HtmlEntity.DeEntitize(node.InnerText).Trim();

            // Skip if heading level is too deep (> 4)
            if (headingLevel > 4)
                continue;

            // Collect content until next heading
            var contentNodes = new List<HtmlNode>();
            for (int j = i + 1; j < allNodes.Count; j++)
            {
                var nextNode = allNodes[j];
                
                // Stop at next heading of same or higher level
                if (nextNode.Name.StartsWith("h", StringComparison.OrdinalIgnoreCase) &&
                    nextNode.Name.Length == 2 &&
                    char.IsDigit(nextNode.Name[1]))
                {
                    var nextHeadingLevel = int.Parse(nextNode.Name[1].ToString());
                    if (nextHeadingLevel <= headingLevel)
                        break;
                }

                contentNodes.Add(nextNode);
            }

            // Build HTML content from collected nodes
            var sectionContent = string.Join("", contentNodes.Select(n => n.OuterHtml));
            sectionContent = htmlContentService.SanitizeHtml(sectionContent);

            // Determine section type
            var sectionType = ClassifySectionTypeFromHeading(headingText, headingLevel);

            // Check if this is a hero section - try multiple matching strategies
            int? heroId = TryMatchHeroName(headingText, heroNameMap, out bool matched);
            
            if (matched)
            {
                sectionType = "Hero";
            }
            else if (sectionType == "Hero")
            {
                // Section seems like it should be a hero but we couldn't match it
                logger.LogWarning(
                    "Could not match hero name '{HeroName}' in patch '{PatchName}' to any hero in database",
                    headingText, patchName);
                unmatchedHeroCount++;
                heroId = null; // Explicitly set to null (schema allows this)
            }

            // Create the section (without ParentSectionId - will be set after save)
            var section = new PatchSection
            {
                PatchId = patchId,
                Order = order++,
                HeadingLevel = headingLevel,
                ParentSectionId = null, // Will be updated after sections are saved
                SectionType = sectionType,
                EntityName = headingText,
                HeroId = heroId,
                Content = sectionContent
            };

            sections.Add(section);
        }

        if (unmatchedHeroCount > 0)
        {
            logger.LogWarning("Patch '{PatchName}' had {Count} unmatched hero names", 
                patchName, unmatchedHeroCount);
        }

        return sections;
    }

    /// <summary>
    /// Attempts to match a heading text to a hero name using multiple strategies.
    /// First tries exact case-insensitive match, then normalized match.
    /// </summary>
    /// <param name="headingText">The heading text to match.</param>
    /// <param name="heroNameMap">Dictionary of hero names to IDs.</param>
    /// <param name="matched">Output parameter indicating if a match was found.</param>
    /// <returns>The hero ID if matched, otherwise null.</returns>
    private int? TryMatchHeroName(string headingText, Dictionary<string, int> heroNameMap, out bool matched)
    {
        matched = false;

        if (string.IsNullOrWhiteSpace(headingText))
            return null;

        // Strategy 1: Exact case-insensitive match
        if (heroNameMap.TryGetValue(headingText.ToLowerInvariant(), out var heroId))
        {
            matched = true;
            return heroId;
        }

        // Strategy 2: Normalized match (remove special characters)
        var normalized = NormalizeHeroName(headingText);
        if (!string.IsNullOrEmpty(normalized) && heroNameMap.TryGetValue(normalized, out heroId))
        {
            matched = true;
            return heroId;
        }

        // No match found
        return null;
    }



    /// <summary>
    /// Classifies a section type based on its heading text and level.
    /// </summary>
    /// <param name="headingText">The text of the heading.</param>
    /// <param name="headingLevel">The level of the heading (1-4).</param>
    /// <returns>The classified section type.</returns>
    private static string ClassifySectionTypeFromHeading(string headingText, int headingLevel)
    {
        var lower = headingText.ToLowerInvariant();

        // Check for known section types
        if (lower.Contains("general"))
            return "General";
        if (lower.Contains("map") || lower.Contains("battleground"))
            return "Map";
        if (lower.Contains("balance"))
            return "Balance";
        if (lower.Contains("bug") || lower.Contains("fix"))
            return "BugFix";
        if (lower.Contains("hero"))
            return "HeroList";

        // Default based on level
        return headingLevel switch
        {
            1 => "Title",
            2 => "Section",
            3 => "Subsection",
            4 => "Entity",
            _ => "Content"
        };
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Converts a patch title to a URL-friendly slug.
    /// </summary>
    /// <param name="title">The patch title to convert.</param>
    /// <returns>URL-friendly slug.</returns>
    private static string ConvertTitleToUrlSlug(string title)
    {
        // Convert title to URL-friendly internal ID
        var normalized = title.ToLowerInvariant()
            .Replace("heroes of the storm", "")
            .Trim();

        // Remove special characters and replace spaces with hyphens
        normalized = SlugNonAlphanumericRegex().Replace(normalized, "");
        normalized = SlugWhitespaceRegex().Replace(normalized, "-");
        normalized = SlugMultipleDashRegex().Replace(normalized, "-");
        normalized = normalized.Trim('-');

        return normalized;
    }

    /// <summary>
    /// Infers the patch type from the patch title.
    /// </summary>
    /// <param name="title">The patch title.</param>
    /// <returns>The inferred patch type.</returns>
    private static string InferPatchTypeFromTitle(string title)
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

    /// <summary>
    /// Attempts to parse a date from a patch title string.
    /// </summary>
    /// <param name="title">The patch title containing a date.</param>
    /// <returns>Parsed date if found, otherwise null.</returns>
    private static DateTime? ParseDateFromPatchTitle(string title)
    {
        // Try to extract date patterns like "January 14, 2026" or "December 12, 2025"
        Regex[] dateRegexes = [MonthNameDateRegex(), NumericDateSlashRegex(), IsoDateRegex()];

        foreach (var regex in dateRegexes)
        {
            var match = regex.Match(title);
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



    #endregion

    #region Existing Methods

    private async Task<List<string>> GetHeroFileListAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetStringAsync(HeroListUrl, cancellationToken);
        var files = JsonSerializer.Deserialize<List<GitHubFileInfo>>(response, JsonOptions);

        return files?
            .Where(f => f.Name.EndsWith(".json"))
            .Select(f => f.Name.Replace(".json", ""))
            .ToList() ?? new List<string>();
    }

    private async Task SyncHeroAsync(string heroShortName, CancellationToken cancellationToken)
    {
        var url = $"{HeroesBaseUrl}{heroShortName}.json";
        var response = await httpClient.GetStringAsync(url, cancellationToken);
        var heroData = JsonSerializer.Deserialize<GitHubHeroData>(response, JsonOptions);

        if (heroData == null)
        {
            throw new InvalidOperationException($"Failed to parse hero data for {heroShortName}");
        }

        var existingHero = await dbContext.Heroes
            .Include(h => h.Abilities)
            .Include(h => h.Talents)
            .FirstOrDefaultAsync(h => h.ShortName == heroShortName, cancellationToken);

        if (existingHero == null)
        {
            existingHero = new Hero { ShortName = heroShortName };
            dbContext.Heroes.Add(existingHero);
        }
        else
        {
            // Clear existing abilities and talents for update
            dbContext.Abilities.RemoveRange(existingHero.Abilities);
            dbContext.Talents.RemoveRange(existingHero.Talents);
        }

        await MapHeroDataAsync(heroData, existingHero, cancellationToken);

        // Enrich with wiki data
        try
        {
            var wikiUrl = existingHero.WikiUrl ?? heroScraper.GetWikiUrl(existingHero.Name);
            var wikiDetails = await heroScraper.GetHeroDetailsAsync(wikiUrl, cancellationToken);
            EnrichHeroWithWikiData(existingHero, wikiDetails);

            // Rate limiting between wiki requests
            await Task.Delay(2000, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to enrich hero {HeroName} with wiki data", existingHero.Name);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogDebug("Synced hero: {HeroName}", existingHero.Name);
    }

    private static void EnrichHeroWithWikiData(Hero hero, HeroWikiDetailInfo wikiDetails)
    {
        // Only update fields that are empty or where wiki data provides richer info
        hero.WikiUrl ??= $"https://heroesofthestorm.fandom.com/wiki/{Uri.EscapeDataString(hero.Name.Replace(" ", "_"))}";

        if (!string.IsNullOrEmpty(wikiDetails.Title))
            hero.Title ??= wikiDetails.Title;

        if (!string.IsNullOrEmpty(wikiDetails.Universe))
            hero.Universe ??= wikiDetails.Universe;

        if (!string.IsNullOrEmpty(wikiDetails.Difficulty))
            hero.Difficulty ??= wikiDetails.Difficulty;

        if (!string.IsNullOrEmpty(wikiDetails.Description))
            hero.Description ??= wikiDetails.Description;

        if (!string.IsNullOrEmpty(wikiDetails.Lore))
            hero.Lore ??= wikiDetails.Lore;

        if (!string.IsNullOrEmpty(wikiDetails.SplashArtUrl))
            hero.SplashArtUrl ??= wikiDetails.SplashArtUrl;

        // Stats - only set if not already populated
        hero.BaseHealth ??= wikiDetails.BaseHealth;
        hero.HealthRegen ??= wikiDetails.HealthRegen;
        hero.BaseMana ??= wikiDetails.BaseMana;
        hero.ManaRegen ??= wikiDetails.ManaRegen;
        hero.BaseAttackDamage ??= wikiDetails.BaseAttackDamage;
        hero.AttackSpeed ??= wikiDetails.AttackSpeed;
        hero.AttackRange ??= wikiDetails.AttackRange;

        // Enrich abilities with wiki data
        foreach (var wikiAbility in wikiDetails.Abilities)
        {
            var matchingAbility = hero.Abilities.FirstOrDefault(a =>
                a.Name.Equals(wikiAbility.Name, StringComparison.OrdinalIgnoreCase));

            if (matchingAbility is null) continue;

            if (!string.IsNullOrEmpty(wikiAbility.Scaling))
                matchingAbility.Scaling ??= wikiAbility.Scaling;
            if (!string.IsNullOrEmpty(wikiAbility.CastTime))
                matchingAbility.CastTime ??= wikiAbility.CastTime;
            if (!string.IsNullOrEmpty(wikiAbility.Range))
                matchingAbility.Range ??= wikiAbility.Range;
            if (!string.IsNullOrEmpty(wikiAbility.AreaOfEffect))
                matchingAbility.AreaOfEffect ??= wikiAbility.AreaOfEffect;
        }

        // Enrich talents with wiki data
        foreach (var wikiTalent in wikiDetails.Talents)
        {
            var matchingTalent = hero.Talents.FirstOrDefault(t =>
                t.Name.Equals(wikiTalent.Name, StringComparison.OrdinalIgnoreCase) &&
                (wikiTalent.Level == 0 || t.Level == wikiTalent.Level));

            if (matchingTalent is null) continue;

            if (!string.IsNullOrEmpty(wikiTalent.LinkedAbilityName))
                matchingTalent.LinkedAbilityName ??= wikiTalent.LinkedAbilityName;
            if (!string.IsNullOrEmpty(wikiTalent.Properties))
                matchingTalent.Properties ??= wikiTalent.Properties;
        }
    }

    private async Task MapHeroDataAsync(GitHubHeroData data, Hero hero, CancellationToken cancellationToken)
    {
        hero.Name = data.Name ?? hero.ShortName;
        hero.HyperlinkId = data.HyperlinkId;
        hero.AttributeId = data.AttributeId;
        hero.Role = data.Role;
        hero.ExpandedRole = data.ExpandedRole;
        hero.Type = data.Type;
        hero.ReleasePatch = data.ReleasePatch;
        hero.LastSyncedAt = DateTime.UtcNow;

        // Download hero icon if available
        if (!string.IsNullOrEmpty(data.Icon))
        {
            var localIconPath = await imageDownloadService.DownloadImageAsync(
                data.Icon,
                "heroes",
                hero.ShortName,
                cancellationToken);

            hero.Icon = localIconPath ?? data.Icon; // Use local path if download succeeded, otherwise keep original URL
        }

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

    #region Battleground Sync

    public async Task<SyncResultDto> SyncBattlegroundsAsync(CancellationToken cancellationToken = default)
    {
        var result = new SyncResultDto { SyncedAt = DateTime.UtcNow };

        try
        {
            logger.LogInformation("Starting battleground sync from Fandom wiki");

            // Get list of battlegrounds
            var battlegroundList = await battlegroundScraper.GetBattlegroundListAsync(cancellationToken);
            logger.LogInformation("Found {Count} battlegrounds to sync", battlegroundList.Count);

            var syncedCount = 0;

            foreach (var bgInfo in battlegroundList)
            {
                try
                {
                    logger.LogInformation("Processing battleground: {Name} (ShortName: {ShortName})", bgInfo.Name, bgInfo.ShortName);

                    // Check if battleground already exists
                    var existing = await dbContext.Battlegrounds
                        .FirstOrDefaultAsync(b => b.ShortName == bgInfo.ShortName, cancellationToken);

                    if (existing != null)
                    {
                        logger.LogInformation("Found existing battleground with ShortName '{ShortName}': {Name}", bgInfo.ShortName, existing.Name);
                    }

                    // Get detailed info
                    var details = await battlegroundScraper.GetBattlegroundDetailsAsync(bgInfo.WikiUrl, cancellationToken);

                    // Download battleground image if available
                    string? localImagePath = null;
                    var sourceImageUrl = details.FullImageUrl ?? bgInfo.ThumbnailUrl;
                    if (!string.IsNullOrEmpty(sourceImageUrl))
                    {
                        localImagePath = await imageDownloadService.DownloadImageAsync(
                            sourceImageUrl,
                            "battlegrounds",
                            bgInfo.ShortName,
                            cancellationToken);
                    }

                    var imageUrl = localImagePath ?? sourceImageUrl;

                    if (existing == null)
                    {
                        // Create new battleground
                        var battleground = new Battleground
                        {
                            ShortName = bgInfo.ShortName,
                            Name = bgInfo.Name,
                            MapType = bgInfo.Lanes + "-Lane",
                            Description = htmlContentService.SanitizeHtml(details.Description ?? bgInfo.ObjectiveSummary),
                            Objective = htmlContentService.SanitizeHtml(details.ObjectiveDetails ?? bgInfo.ObjectiveSummary),
                            ObjectiveTiming = details.ObjectiveTiming,
                            MercCamps = htmlContentService.SanitizeHtml(details.MercCamps ?? ""),
                            BossInfo = htmlContentService.SanitizeHtml(details.BossInfo ?? ""),
                            Tips = htmlContentService.SanitizeHtml(details.Tips ?? ""),
                            ImageUrl = imageUrl,
                            Universe = bgInfo.Universe,
                            ReleaseDate = bgInfo.ReleaseDate,
                            IsInRotation = true
                        };

                        dbContext.Battlegrounds.Add(battleground);
                        logger.LogInformation("Added new battleground: {Name}", bgInfo.Name);
                    }
                    else
                    {
                        // Update existing battleground
                        existing.Name = bgInfo.Name;
                        existing.MapType = bgInfo.Lanes + "-Lane";
                        existing.Description = htmlContentService.SanitizeHtml(details.Description ?? bgInfo.ObjectiveSummary);
                        existing.Objective = htmlContentService.SanitizeHtml(details.ObjectiveDetails ?? bgInfo.ObjectiveSummary);
                        existing.ObjectiveTiming = details.ObjectiveTiming;
                        existing.MercCamps = htmlContentService.SanitizeHtml(details.MercCamps ?? "");
                        existing.BossInfo = htmlContentService.SanitizeHtml(details.BossInfo ?? "");
                        existing.Tips = htmlContentService.SanitizeHtml(details.Tips ?? "");
                        existing.ImageUrl = imageUrl ?? existing.ImageUrl;
                        existing.Universe = bgInfo.Universe;
                        existing.ReleaseDate = bgInfo.ReleaseDate ?? existing.ReleaseDate;

                        logger.LogInformation("Updated battleground: {Name}", bgInfo.Name);
                    }

                    syncedCount++;

                    // Save changes after each battleground to avoid UNIQUE constraint violations
                    await dbContext.SaveChangesAsync(cancellationToken);

                    // Rate limiting - wait 2 seconds between requests
                    await Task.Delay(2000, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to sync battleground: {Name}", bgInfo.Name);
                    result.Errors.Add($"Failed to sync {bgInfo.Name}: {ex.Message}");
                }
            }

            result.Success = true;
            result.HeroesUpdated = syncedCount; // Reuse counter
            result.Message = $"Synced {syncedCount} of {battlegroundList.Count} battlegrounds";
            logger.LogInformation("Battleground sync complete: {Count} synced", syncedCount);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"Battleground sync failed: {ex.Message}");
            logger.LogError(ex, "Battleground sync failed");
        }

        return result;
    }

    #endregion

    #region Generated Regex

    [GeneratedRegex(@"['.\-\s]")]
    private static partial Regex SpecialCharactersRegex();

    [GeneratedRegex(@"[^a-z0-9\s-]")]
    private static partial Regex SlugNonAlphanumericRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SlugWhitespaceRegex();

    [GeneratedRegex(@"-+")]
    private static partial Regex SlugMultipleDashRegex();

    [GeneratedRegex(@"(January|February|March|April|May|June|July|August|September|October|November|December)\s+(\d{1,2}),?\s+(\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex MonthNameDateRegex();

    [GeneratedRegex(@"(\d{1,2})[/-](\d{1,2})[/-](\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex NumericDateSlashRegex();

    [GeneratedRegex(@"(\d{4})[/-](\d{1,2})[/-](\d{1,2})", RegexOptions.IgnoreCase)]
    private static partial Regex IsoDateRegex();

    #endregion
}
