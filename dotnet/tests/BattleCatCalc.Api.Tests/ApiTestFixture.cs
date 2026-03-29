using BattleCatCalc.Core.Interfaces;
using BattleCatCalc.Core.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace BattleCatCalc.Api.Tests;

/// <summary>
/// Stub fetcher that returns pre-built HTML for testing.
/// </summary>
internal sealed class StubFetcher : IFetcher
{
    public Task<string> FetchHtmlAsync(
        string seed, string eventId, string lang = "en",
        int count = 100, CancellationToken cancellationToken = default)
    {
        // 最小化 HTML 模擬 bc.godfat.org 回應結構
        return Task.FromResult(StubHtml);
    }

    /// <summary>
    /// 產生一個 2-row 的最小 HTML，足以通過 BCParser 解析。
    /// 包含 Track A/B 各兩個 slot，第二個 slot 有保底欄位。
    /// </summary>
    public const string StubHtml = """
        <html><body>
        <table id="result">
        <tr>
          <td rowspan="1"><a href="#1A">1A</a></td>
          <td>CatA1</td>
          <td><a href="#2A">2A</a></td>
          <td>GuaCatA1</td>
          <td><a href="#1B">next</a></td>
          <td rowspan="1"><a href="#1B">1B</a></td>
          <td>CatB1</td>
          <td><a href="#2B">2B</a></td>
          <td>GuaCatB1</td>
          <td><a href="#1A">next</a></td>
        </tr>
        <tr>
          <td rowspan="1"><a href="#2A">2A</a></td>
          <td>CatA2</td>
          <td><a href="#3A">3A</a></td>
          <td>GuaCatA2</td>
          <td><a href="#2B">next</a></td>
          <td rowspan="1"><a href="#2B">2B</a></td>
          <td>CatB2</td>
          <td><a href="#3B">3B</a></td>
          <td>GuaCatB2</td>
          <td><a href="#2A">next</a></td>
        </tr>
        </table>
        </body></html>
        """;
}

/// <summary>
/// Stub parser that returns predictable Track A/B data without HTML dependency.
/// </summary>
internal sealed class StubParser : IParser
{
    public (Track TrackA, Track TrackB) Parse(string html)
    {
        var trackA = new Track("A", [
            new GachaSlot(
                new Position(1, "A"),
                new SlotValue.CatName("CatA1"),
                new SlotValue.CatName("GuaCatA1"),
                new Position(2, "A")),
            new GachaSlot(
                new Position(2, "A"),
                new SlotValue.CatName("CatA2"),
                new SlotValue.CatName("GuaCatA2"),
                new Position(3, "A")),
        ]);

        var trackB = new Track("B", [
            new GachaSlot(
                new Position(1, "B"),
                new SlotValue.CatName("CatB1"),
                new SlotValue.CatName("GuaCatB1"),
                new Position(2, "B")),
            new GachaSlot(
                new Position(2, "B"),
                new SlotValue.CatName("CatB2"),
                new SlotValue.CatName("GuaCatB2"),
                new Position(3, "B")),
        ]);

        return (trackA, trackB);
    }
}

/// <summary>
/// WebApplicationFactory with stubbed dependencies for integration testing.
/// </summary>
public class ApiTestFixture : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // 移除真實的 Fetcher 與 Parser，替換為 Stub
            var fetcherDesc = services.SingleOrDefault(d => d.ServiceType == typeof(IFetcher));
            if (fetcherDesc is not null) services.Remove(fetcherDesc);

            var parserDesc = services.SingleOrDefault(d => d.ServiceType == typeof(IParser));
            if (parserDesc is not null) services.Remove(parserDesc);

            // HttpClientFactory 會註冊多個服務，需額外清理
            var httpClientDescs = services.Where(d =>
                d.ServiceType.FullName?.Contains("HttpClient") == true &&
                d.ImplementationType?.FullName?.Contains("BCDataFetcher") == true).ToList();
            foreach (var desc in httpClientDescs) services.Remove(desc);

            services.AddSingleton<IFetcher, StubFetcher>();
            services.AddSingleton<IParser, StubParser>();
        });
    }
}
