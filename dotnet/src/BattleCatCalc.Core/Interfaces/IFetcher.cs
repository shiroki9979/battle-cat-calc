namespace BattleCatCalc.Core.Interfaces;

/// <summary>
/// Fetches raw HTML gacha data from the remote seed tracking service.
/// </summary>
public interface IFetcher
{
    /// <summary>
    /// Fetches the HTML page for a given seed and event.
    /// </summary>
    /// <param name="seed">The gacha seed value.</param>
    /// <param name="eventId">The event identifier.</param>
    /// <param name="lang">Language code (default: "en").</param>
    /// <param name="count">Number of rows to fetch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Raw HTML string.</returns>
    Task<string> FetchHtmlAsync(
        string seed,
        string eventId,
        string lang = "en",
        int count = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the home page HTML (used to extract the event list).
    /// </summary>
    /// <param name="lang">Language code (default: "en").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Raw HTML string of the home page.</returns>
    Task<string> FetchHomePageHtmlAsync(
        string lang = "en",
        CancellationToken cancellationToken = default);
}
