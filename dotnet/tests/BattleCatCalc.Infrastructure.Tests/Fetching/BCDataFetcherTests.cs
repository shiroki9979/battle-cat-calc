using System.Net;
using BattleCatCalc.Core.Exceptions;
using BattleCatCalc.Infrastructure.Fetching;

namespace BattleCatCalc.Infrastructure.Tests.Fetching;

public class BCDataFetcherTests
{
    [Fact]
    public async Task FetchHtmlAsync_Success_ReturnsHtml()
    {
        /// 動機：驗證正常情況下 fetcher 回傳 HTML 內容。
        /// 方法：使用 HttpMessageHandler mock 模擬 200 回應。
        /// 驗證：回傳值等於預期 HTML。
        var expectedHtml = "<html><body><table></table></body></html>";
        var handler = new MockHttpHandler(HttpStatusCode.OK, expectedHtml);
        var httpClient = new HttpClient(handler);
        var fetcher = new BCDataFetcher(httpClient);

        var result = await fetcher.FetchHtmlAsync("123456", "test_event", "tw", 10);

        Assert.Equal(expectedHtml, result);
    }

    [Fact]
    public async Task FetchHtmlAsync_Success_SendsCorrectQueryParams()
    {
        /// 動機：確保所有參數正確編碼到 query string。
        /// 方法：擷取 MockHttpHandler 收到的 URI。
        /// 驗證：URI 包含所有預期參數。
        Uri? capturedUri = null;
        var handler = new MockHttpHandler(HttpStatusCode.OK, "ok", req => capturedUri = req.RequestUri);
        var httpClient = new HttpClient(handler);
        var fetcher = new BCDataFetcher(httpClient);

        await fetcher.FetchHtmlAsync("999", "special_event", "jp", 200);

        Assert.NotNull(capturedUri);
        var query = capturedUri!.Query;
        Assert.Contains("seed=999", query);
        Assert.Contains("event=special_event", query);
        Assert.Contains("lang=jp", query);
        Assert.Contains("count=200", query);
    }

    [Fact]
    public async Task FetchHtmlAsync_ServerError_ThrowsFetchException()
    {
        /// 動機：HTTP 5xx 應轉換為 FetchException 並攜帶 context。
        /// 方法：模擬 500 回應。
        /// 驗證：拋出 FetchException，含 seed 與 eventId。
        var handler = new MockHttpHandler(HttpStatusCode.InternalServerError, "");
        var httpClient = new HttpClient(handler);
        var fetcher = new BCDataFetcher(httpClient);

        var ex = await Assert.ThrowsAsync<FetchException>(
            () => fetcher.FetchHtmlAsync("123", "test"));

        Assert.Equal("123", ex.Seed);
        Assert.Equal("test", ex.EventId);
    }

    [Fact]
    public async Task FetchHtmlAsync_NotFound_ThrowsFetchException()
    {
        /// 動機：HTTP 404 應同樣轉為 FetchException。
        /// 方法：模擬 404 回應。
        /// 驗證：FetchException.StatusCode 為 404。
        var handler = new MockHttpHandler(HttpStatusCode.NotFound, "");
        var httpClient = new HttpClient(handler);
        var fetcher = new BCDataFetcher(httpClient);

        var ex = await Assert.ThrowsAsync<FetchException>(
            () => fetcher.FetchHtmlAsync("123", "test"));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task FetchHtmlAsync_EmptyResponse_ReturnsEmptyString()
    {
        /// 動機：空回應不應崩潰。
        /// 方法：模擬空 body 的 200 回應。
        /// 驗證：回傳空字串。
        var handler = new MockHttpHandler(HttpStatusCode.OK, "");
        var httpClient = new HttpClient(handler);
        var fetcher = new BCDataFetcher(httpClient);

        var result = await fetcher.FetchHtmlAsync("123", "test");

        Assert.Equal("", result);
    }

    [Fact]
    public async Task FetchHtmlAsync_Cancelled_ThrowsOperationCanceledException()
    {
        /// 動機：取消 token 應能中斷請求。
        /// 方法：傳入已取消的 CancellationToken。
        /// 驗證：拋出 OperationCanceledException 或 TaskCanceledException。
        var handler = new MockHttpHandler(HttpStatusCode.OK, "ok", delay: TimeSpan.FromSeconds(10));
        var httpClient = new HttpClient(handler);
        var fetcher = new BCDataFetcher(httpClient);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fetcher.FetchHtmlAsync("123", "test", cancellationToken: cts.Token));
    }

    /// <summary>
    /// Minimal HttpMessageHandler stub for unit testing without network.
    /// </summary>
    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;
        private readonly Action<HttpRequestMessage>? _onRequest;
        private readonly TimeSpan? _delay;

        public MockHttpHandler(HttpStatusCode statusCode, string content, Action<HttpRequestMessage>? onRequest = null, TimeSpan? delay = null)
        {
            _statusCode = statusCode;
            _content = content;
            _onRequest = onRequest;
            _delay = delay;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _onRequest?.Invoke(request);

            if (_delay is not null)
                await Task.Delay(_delay.Value, cancellationToken);

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content)
            };
        }
    }
}
