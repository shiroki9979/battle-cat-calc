using BattleCatCalc.Api.Contracts;
using BattleCatCalc.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BattleCatCalc.Api.Controllers;

/// <summary>
/// 軌道資料查詢端點。
/// </summary>
[ApiController]
[Route("api/v1")]
public sealed class TrackController(IFetcher fetcher, IParser parser) : ControllerBase
{
    /// <summary>
    /// GET /api/v1/tracks — 取得指定種子與活動的軌道資料。
    /// </summary>
    [HttpGet("tracks")]
    public async Task<IActionResult> GetTracksAsync(
        [FromQuery] string seed,
        [FromQuery(Name = "event")] string @event,
        [FromQuery] string lang = "tw",
        [FromQuery] int count = 100,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(seed))
            return BadRequest(CreateProblem("seed is required."));

        if (string.IsNullOrWhiteSpace(@event))
            return BadRequest(CreateProblem("event is required."));

        if (count is < 1 or > 999)
            return BadRequest(CreateProblem("count must be between 1 and 999."));

        var html = await fetcher.FetchHtmlAsync(seed, @event, lang, count, ct);
        var (trackA, trackB) = parser.Parse(html);

        var response = new TrackResponse
        {
            Seed = seed,
            EventId = @event,
            TrackA = trackA.Select(SlotDto.FromModel).ToList(),
            TrackB = trackB.Select(SlotDto.FromModel).ToList()
        };

        return Ok(response);
    }

    private static object CreateProblem(string detail) => new
    {
        type = "https://tools.ietf.org/html/rfc9457",
        title = "Bad Request",
        status = 400,
        detail
    };
}
