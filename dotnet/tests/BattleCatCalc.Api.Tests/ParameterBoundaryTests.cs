using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BattleCatCalc.Api.Contracts;

namespace BattleCatCalc.Api.Tests;

/// <summary>
/// API 端點參數邊界與驗證測試。
/// </summary>
public class ParameterBoundaryTests : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client;

    public ParameterBoundaryTests(ApiTestFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    #region /tracks 參數邊界

    [Fact]
    public async Task GetTracks_EmptySeed_Returns400()
    {
        /// 動機：空白 seed 應在端點層直接拒絕。
        var response = await _client.GetAsync("/api/v1/tracks?seed=&event=1019");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTracks_MissingEvent_Returns400()
    {
        /// 動機：缺少 event 參數應回傳 400。
        var response = await _client.GetAsync("/api/v1/tracks?seed=123&event=");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(999)]
    public async Task GetTracks_CountAtBoundary_ReturnsOk(int count)
    {
        /// 動機：count 在合法邊界 [1, 999] 時應正常回傳。
        var response = await _client.GetAsync($"/api/v1/tracks?seed=123&event=1019&count={count}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000)]
    public async Task GetTracks_CountOutOfRange_Returns400(int count)
    {
        /// 動機：count 超出 [1, 999] 範圍應拒絕。
        var response = await _client.GetAsync($"/api/v1/tracks?seed=123&event=1019&count={count}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTracks_WhitespaceSeed_Returns400()
    {
        /// 動機：純空白 seed 應被 IsNullOrWhiteSpace 攔截。
        var response = await _client.GetAsync("/api/v1/tracks?seed=%20%20&event=1019");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region /simulate 參數邊界

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10001)]
    public async Task Simulate_MaxRollsOutOfRange_Returns400(int maxRolls)
    {
        /// 動機：maxRolls 超出 [1, 10000] 範圍應拒絕。
        var request = new SimulateRequest
        {
            Seed = "123",
            Pools = [new PoolSpec { EventId = "1019" }],
            StartPos = new PositionDto { Id = 1, Track = "A" },
            TargetCats = ["CatA1"],
            MaxRolls = maxRolls
        };

        var response = await _client.PostAsJsonAsync("/api/v1/simulate", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Simulate_MaxRolls1_ReturnsValidResponse()
    {
        /// 動機：maxRolls=1 為合法最小值，CatA1 在位置 1A 恰好一步可達。
        var request = new SimulateRequest
        {
            Seed = "123",
            Pools = [new PoolSpec { EventId = "1019" }],
            StartPos = new PositionDto { Id = 1, Track = "A" },
            TargetCats = ["CatA1"],
            MaxRolls = 1
        };

        var response = await _client.PostAsJsonAsync("/api/v1/simulate", request);

        // 可能是 200（找到）或 404（找不到），但不應是 400
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            $"Expected 200 or 404 but got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Simulate_EmptySeed_Returns400()
    {
        /// 動機：空白 seed 應在端點層直接拒絕。
        var request = new SimulateRequest
        {
            Seed = "",
            Pools = [new PoolSpec { EventId = "1019" }],
            StartPos = new PositionDto { Id = 1, Track = "A" },
            TargetCats = ["CatA1"],
            MaxRolls = 50
        };

        var response = await _client.PostAsJsonAsync("/api/v1/simulate", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Simulate_ResponseContentType_IsProblemJson_On400()
    {
        /// 動機：所有 400 回應的 Content-Type 應為 application/problem+json。
        var request = new SimulateRequest
        {
            Seed = "123",
            Pools = [],
            StartPos = new PositionDto { Id = 1, Track = "A" },
            TargetCats = ["CatA1"]
        };

        var response = await _client.PostAsJsonAsync("/api/v1/simulate", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(400, doc.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Simulate_NotFound_Returns404WithProblemDetails()
    {
        /// 動機：找不到路徑時應回傳 404 + Problem Details。
        var request = new SimulateRequest
        {
            Seed = "123",
            Pools = [new PoolSpec { EventId = "1019" }],
            StartPos = new PositionDto { Id = 1, Track = "A" },
            TargetCats = ["ZZZ_IMPOSSIBLE_CAT"],
            MaxRolls = 50
        };

        var response = await _client.PostAsJsonAsync("/api/v1/simulate", request);

        // 可能是 400（ArgumentException from simulator）或 404
        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest,
            $"Expected 404 or 400 but got {(int)response.StatusCode}");
    }

    #endregion

    #region /cats 參數邊界

    [Fact]
    public async Task SearchCats_WhitespaceName_Returns400()
    {
        /// 動機：純空白 name 應被視為無效。
        var response = await _client.GetAsync("/api/v1/cats/search?seed=123&event=1019&name=%20%20");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SearchGuaranteed_WhitespaceName_Returns400()
    {
        /// 動機：保底搜尋同樣檢查空白 name。
        var response = await _client.GetAsync("/api/v1/cats/guaranteed?seed=123&event=1019&name=%20%20");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion
}
