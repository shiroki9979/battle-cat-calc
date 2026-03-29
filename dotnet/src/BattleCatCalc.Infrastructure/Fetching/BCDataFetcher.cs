using BattleCatCalc.Core.Exceptions;
using BattleCatCalc.Core.Interfaces;

namespace BattleCatCalc.Infrastructure.Fetching;

/// <summary>
/// Fetches gacha seed tracking HTML from bc.godfat.org using HttpClient.
/// </summary>
public sealed class BCDataFetcher : IFetcher
{
    private const string BaseUrl = "https://bc.godfat.org/";

    private readonly HttpClient _httpClient;

    public BCDataFetcher(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> FetchHtmlAsync(
        string seed,
        string eventId,
        string lang = "en",
        int count = 100,
        CancellationToken cancellationToken = default)
    {
        var query = $"?seed={Uri.EscapeDataString(seed)}" +
                    $"&event={Uri.EscapeDataString(eventId)}" +
                    $"&lang={Uri.EscapeDataString(lang)}" +
                    $"&count={count}";

        try
        {
            var response = await _httpClient.GetAsync(BaseUrl + query, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new FetchException(
                $"Failed to fetch data for seed={seed}, event={eventId}: {ex.Message}",
                ex,
                seed,
                eventId,
                ex.StatusCode);
        }
    }
}
