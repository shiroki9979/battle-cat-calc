using System.Net;
using System.Text.Json;
using BattleCatCalc.Core.Exceptions;
using BattleCatCalc.Core.Interfaces;
using BattleCatCalc.Core.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace BattleCatCalc.Api.Tests;

/// <summary>
/// ExceptionHandlingMiddleware 單元測試
/// — 透過替換 Stub 使其在請求處理過程中拋出特定例外，驗證 HTTP 回應碼與 Problem Details 格式。
/// </summary>
public class ExceptionHandlingMiddlewareTests
{
    #region 輔助：拋出指定例外的 Fetcher Stub

    /// <summary>每次呼叫都拋出指定例外的 fetcher。</summary>
    private sealed class ThrowingFetcher(Exception exception) : IFetcher
    {
        public Task<string> FetchHtmlAsync(
            string seed, string eventId, string lang = "en",
            int count = 100, CancellationToken cancellationToken = default)
            => throw exception;

        public Task<string> FetchHomePageHtmlAsync(
            string lang = "en", CancellationToken cancellationToken = default)
            => throw exception;
    }

    /// <summary>建立注入 ThrowingFetcher 的測試用 HttpClient。</summary>
    private static HttpClient CreateClientWithThrowingFetcher(Exception exception)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // 移除既有 Fetcher 註冊
                var existing = services.Where(d => d.ServiceType == typeof(IFetcher)).ToList();
                foreach (var d in existing) services.Remove(d);
                var httpClientDescs = services.Where(d =>
                    d.ServiceType.FullName?.Contains("HttpClient") == true &&
                    d.ImplementationType?.FullName?.Contains("BCDataFetcher") == true).ToList();
                foreach (var d in httpClientDescs) services.Remove(d);

                services.AddSingleton<IFetcher>(new ThrowingFetcher(exception));
            });
        });
        return factory.CreateClient();
    }

    #endregion

    #region ParseException → 422

    [Fact]
    public async Task ParseException_Returns422_WithProblemDetails()
    {
        /// 動機：ParseException 應被 middleware 映射為 422 Parse Error。
        var ex = new ParseException("Bad HTML", html: "<div>bad</div>", selector: "table#result");
        using var client = CreateClientWithThrowingFetcher(ex);

        var response = await client.GetAsync("/api/v1/tracks?seed=123&event=1019");

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Parse Error", doc.RootElement.GetProperty("title").GetString());
        Assert.Contains("Bad HTML", doc.RootElement.GetProperty("detail").GetString());
    }

    #endregion

    #region FetchException → 502

    [Fact]
    public async Task FetchException_Returns502_WithProblemDetails()
    {
        /// 動機：FetchException 應被映射為 502 Upstream Error。
        var ex = new FetchException("Upstream down", "123", "1019", HttpStatusCode.ServiceUnavailable);
        using var client = CreateClientWithThrowingFetcher(ex);

        var response = await client.GetAsync("/api/v1/tracks?seed=123&event=1019");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Upstream Error", doc.RootElement.GetProperty("title").GetString());
        // 502 >= 500，detail 應被遮蔽以避免洩漏上游細節
        Assert.Equal("An unexpected error occurred.", doc.RootElement.GetProperty("detail").GetString());
    }

    #endregion

    #region SimulationException → 400

    [Fact]
    public async Task SimulationException_Returns400_WithProblemDetails()
    {
        /// 動機：SimulationException 應被映射為 400 Simulation Error。
        var ex = new SimulationException("Too complex", new Position(1, "A"), 100);
        using var client = CreateClientWithThrowingFetcher(ex);

        var response = await client.GetAsync("/api/v1/tracks?seed=123&event=1019");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Simulation Error", doc.RootElement.GetProperty("title").GetString());
    }

    #endregion

    #region ArgumentException → 400

    [Fact]
    public async Task ArgumentException_Returns400_WithProblemDetails()
    {
        /// 動機：一般 ArgumentException 應被映射為 400 Bad Request。
        var ex = new ArgumentException("Invalid param");
        using var client = CreateClientWithThrowingFetcher(ex);

        var response = await client.GetAsync("/api/v1/tracks?seed=123&event=1019");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Bad Request", doc.RootElement.GetProperty("title").GetString());
    }

    #endregion

    #region 未知例外 → 500（不洩漏內部細節）

    [Fact]
    public async Task UnhandledException_Returns500_WithoutInternalDetails()
    {
        /// 動機：未知例外應回傳 500，且 detail 不得包含原始錯誤訊息（避免資訊洩漏）。
        var ex = new InvalidOperationException("Internal secret info");
        using var client = CreateClientWithThrowingFetcher(ex);

        var response = await client.GetAsync("/api/v1/tracks?seed=123&event=1019");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Internal Server Error", doc.RootElement.GetProperty("title").GetString());
        // 不應洩漏內部錯誤細節
        Assert.DoesNotContain("Internal secret info", doc.RootElement.GetProperty("detail").GetString());
        Assert.Equal("An unexpected error occurred.", doc.RootElement.GetProperty("detail").GetString());
    }

    #endregion

    #region 回應格式

    [Fact]
    public async Task AllExceptions_ReturnProblemJsonContentType()
    {
        /// 動機：所有例外回應的 Content-Type 應為 application/problem+json。
        var ex = new FetchException("Test", "s", "e");
        using var client = CreateClientWithThrowingFetcher(ex);

        var response = await client.GetAsync("/api/v1/tracks?seed=123&event=1019");

        Assert.Contains("application/problem+json",
            response.Content.Headers.ContentType?.ToString() ?? "");
    }

    [Fact]
    public async Task ProblemDetails_ContainsInstancePath()
    {
        /// 動機：Problem Details 的 instance 應包含請求路徑。
        var ex = new ParseException("test");
        using var client = CreateClientWithThrowingFetcher(ex);

        var response = await client.GetAsync("/api/v1/tracks?seed=123&event=1019");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Contains("/api/v1/tracks", doc.RootElement.GetProperty("instance").GetString());
    }

    #endregion
}
