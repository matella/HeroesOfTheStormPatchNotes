namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Service interface for scraping battleground data from Heroes of the Storm Fandom Wiki.
/// </summary>
public interface IBattlegroundScraper
{
    /// <summary>
    /// Gets the list of all battlegrounds from the main Battleground wiki page.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of basic battleground information.</returns>
    Task<List<BattlegroundBasicInfo>> GetBattlegroundListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed information about a specific battleground from its wiki page.
    /// </summary>
    /// <param name="wikiUrl">The URL of the battleground's wiki page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detailed battleground information.</returns>
    Task<BattlegroundDetailInfo> GetBattlegroundDetailsAsync(string wikiUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Constructs the Fandom wiki URL for a battleground given its display name.
    /// </summary>
    /// <param name="battlegroundName">The display name of the battleground (e.g., "Cursed Hollow").</param>
    /// <returns>The full wiki URL.</returns>
    string GetWikiUrl(string battlegroundName);
}
