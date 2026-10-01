using BattleCatCalc.Api.Contracts;
using BattleCatCalc.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.ComponentModel.DataAnnotations;

namespace BattleCatCalc.Api.Controllers;

/// <summary>
/// 活動清單查詢端點。從 bc.godfat.org 首頁解析可用活動，並以 MemoryCache 快取。
/// </summary>
[ApiController]
[Route("api/v1")]
public sealed class EventController(IFetcher fetcher, IParser parser, IMemoryCache cache) : ControllerBase
{
    private const string CacheKey = "EventList";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
    private const string SupportedLanguages = "^(en|tw|jp|kr)$";

    /// <summary>
    /// GET /api/v1/events — 取得可用活動清單。
    /// </summary>
    [HttpGet("events")]
    public async Task<IActionResult> GetEventsAsync(
        [FromQuery, RegularExpression(SupportedLanguages)] string lang = "en",
        CancellationToken ct = default)
    {
        var cacheKeyWithLang = $"{CacheKey}_{lang}";

        if (!cache.TryGetValue(cacheKeyWithLang, out List<EventDto>? events))
        {
            var html = await fetcher.FetchHomePageHtmlAsync(lang, ct);
            var parsed = parser.ParseEventList(html);

            events = parsed.Select(e => new EventDto
            {
                Value = e.Value,
                Label = e.Label,
                Group = e.Group,
            }).ToList();

            cache.Set(cacheKeyWithLang, events, CacheDuration);
        }

        return Ok(events);
    }

    /// <summary>
    /// POST /api/v1/events/refresh — 強制重新整理活動清單快取。
    /// </summary>
    [HttpPost("events/refresh")]
    public async Task<IActionResult> RefreshEventsAsync(
        [FromQuery, RegularExpression(SupportedLanguages)] string lang = "en",
        CancellationToken ct = default)
    {
        var cacheKeyWithLang = $"{CacheKey}_{lang}";

        var html = await fetcher.FetchHomePageHtmlAsync(lang, ct);
        var parsed = parser.ParseEventList(html);

        var events = parsed.Select(e => new EventDto
        {
            Value = e.Value,
            Label = e.Label,
            Group = e.Group,
        }).ToList();

        cache.Set(cacheKeyWithLang, events, CacheDuration);

        return Ok(events);
    }
}
