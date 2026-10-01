using System.Net;
using System.Net.Http.Json;
using BattleCatCalc.Api.Contracts;

namespace BattleCatCalc.Api.Tests;

public class EventTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    [Fact]
    public async Task GetEvents_ReturnsGroupedEvents()
    {
        /// 動機：確認 /events 端點回傳完整的分組活動清單。
        /// 方法：GET /api/v1/events。
        /// 驗證：回傳 3 筆資料，群組正確。
        var events = await _client.GetFromJsonAsync<List<EventDto>>("/api/v1/events");

        Assert.NotNull(events);
        Assert.Equal(3, events.Count);
        Assert.Equal(2, events.Count(e => e.Group == "Upcoming"));
        Assert.Single(events, e => e.Group == "Past");
    }

    [Fact]
    public async Task GetEvents_FirstEvent_HasCorrectFields()
    {
        /// 動機：驗證單一 EventDto 的欄位對應正確。
        /// 方法：GET /api/v1/events，取第一筆。
        /// 驗證：value、label、group 皆符合 stub 資料。
        var events = await _client.GetFromJsonAsync<List<EventDto>>("/api/v1/events");

        Assert.NotNull(events);
        var first = events[0];
        Assert.Equal("2026-03-30_943", first.Value);
        Assert.Contains("Test Event A", first.Label);
        Assert.Equal("Upcoming", first.Group);
    }

    [Fact]
    public async Task GetEvents_WithLangParam_Returns200()
    {
        /// 動機：lang 查詢參數應被接受但不影響 stub 回傳結果。
        /// 方法：GET /api/v1/events?lang=ja。
        /// 驗證：狀態碼 200，結果不變。
        var response = await _client.GetAsync("/api/v1/events?lang=ja");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var events = await response.Content.ReadFromJsonAsync<List<EventDto>>();
        Assert.NotNull(events);
        Assert.Equal(3, events.Count);
    }

    [Fact]
    public async Task RefreshEvents_ReturnsUpdatedList()
    {
        /// 動機：POST /events/refresh 應強制重新取得並回傳活動清單。
        /// 方法：POST /api/v1/events/refresh。
        /// 驗證：回傳與 GET 相同的資料結構。
        var response = await _client.PostAsync("/api/v1/events/refresh", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var events = await response.Content.ReadFromJsonAsync<List<EventDto>>();
        Assert.NotNull(events);
        Assert.Equal(3, events.Count);
    }
}
