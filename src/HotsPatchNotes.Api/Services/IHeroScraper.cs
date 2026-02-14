namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Interface for scraping hero data from the Heroes of the Storm Fandom wiki.
/// </summary>
public interface IHeroScraper
{
    /// <summary>
    /// Gets detailed hero information from their Fandom wiki page.
    /// </summary>
    /// <param name="wikiUrl">URL of the hero's wiki page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Scraped hero detail information.</returns>
    Task<HeroWikiDetailInfo> GetHeroDetailsAsync(string wikiUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates the Fandom wiki URL for a hero based on their display name.
    /// </summary>
    /// <param name="heroName">The hero's display name.</param>
    /// <returns>The constructed wiki URL.</returns>
    string GetWikiUrl(string heroName);
}
