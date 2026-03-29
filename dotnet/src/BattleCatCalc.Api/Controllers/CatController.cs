using BattleCatCalc.Api.Contracts;
using BattleCatCalc.Application.Analysis;
using BattleCatCalc.Core.Interfaces;
using BattleCatCalc.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace BattleCatCalc.Api.Controllers;

/// <summary>
/// 貓咪查詢端點。
/// </summary>
[ApiController]
[Route("api/v1/cats")]
public sealed class CatController(IFetcher fetcher, IParser parser) : ControllerBase
{
    /// <summary>
    /// GET /api/v1/cats/search — 搜尋貓咪名稱。
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> SearchCatsAsync(
        [FromQuery] string seed,
        [FromQuery(Name = "event")] string @event,
        [FromQuery] string name,
        [FromQuery] string lang = "tw",
        [FromQuery] int count = 100,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(CreateProblem("name is required."));

        var (trackA, trackB) = await FetchAndParse(seed, @event, lang, count, ct);

        var analyzerA = new SeedAnalyzer(trackA);
        var analyzerB = new SeedAnalyzer(trackB);
        var results = analyzerA.FindCat(name)
            .Concat(analyzerB.FindCat(name))
            .Select(CatSearchResult.FromSlot)
            .ToList();

        return Ok(results);
    }

    /// <summary>
    /// GET /api/v1/cats/guaranteed — 搜尋保底位置。
    /// </summary>
    [HttpGet("guaranteed")]
    public async Task<IActionResult> SearchGuaranteedAsync(
        [FromQuery] string seed,
        [FromQuery(Name = "event")] string @event,
        [FromQuery] string name,
        [FromQuery] string lang = "tw",
        [FromQuery] int count = 100,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(CreateProblem("name is required."));

        var (trackA, trackB) = await FetchAndParse(seed, @event, lang, count, ct);

        var analyzerA = new SeedAnalyzer(trackA);
        var analyzerB = new SeedAnalyzer(trackB);
        var results = analyzerA.FindGuaranteedPositions(name)
            .Concat(analyzerB.FindGuaranteedPositions(name))
            .Select(PositionDto.FromModel)
            .ToList();

        return Ok(results);
    }

    private async Task<(Track, Track)> FetchAndParse(
        string seed, string @event, string lang, int count, CancellationToken ct)
    {
        var html = await fetcher.FetchHtmlAsync(seed, @event, lang, count, ct);
        return parser.Parse(html);
    }

    private static object CreateProblem(string detail) => new
    {
        type = "https://tools.ietf.org/html/rfc9457",
        title = "Bad Request",
        status = 400,
        detail
    };
}
