using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BattleCatCalc.Api.Contracts;

namespace BattleCatCalc.Api.Tests;

/// <summary>
/// Integration tests for the simulate endpoint.
/// </summary>
public class SimulateTests : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client;

    public SimulateTests(ApiTestFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task Simulate_ValidRequest_ReturnsResult()
    {
        var request = new SimulateRequest
        {
            Seed = "123",
            Pools = [new PoolSpec { EventId = "1019" }],
            StartPos = new PositionDto { Id = 1, Track = "A" },
            TargetCats = ["CatA1"],
            MaxRolls = 50
        };

        var response = await _client.PostAsJsonAsync("/api/v1/simulate", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("totalCost").GetInt32() >= 0);
        Assert.True(root.GetProperty("stepCount").GetInt32() >= 0);
        Assert.True(root.GetProperty("path").GetArrayLength() >= 0);
    }

    [Fact]
    public async Task Simulate_NoPools_Returns400()
    {
        var request = new SimulateRequest
        {
            Seed = "123",
            Pools = [],
            StartPos = new PositionDto { Id = 1, Track = "A" },
            TargetCats = ["CatA1"]
        };

        var response = await _client.PostAsJsonAsync("/api/v1/simulate", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Simulate_NoTargets_Returns400()
    {
        var request = new SimulateRequest
        {
            Seed = "123",
            Pools = [new PoolSpec { EventId = "1019" }],
            StartPos = new PositionDto { Id = 1, Track = "A" },
            TargetCats = []
        };

        var response = await _client.PostAsJsonAsync("/api/v1/simulate", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Simulate_InvalidMaxRolls_Returns400()
    {
        var request = new SimulateRequest
        {
            Seed = "123",
            Pools = [new PoolSpec { EventId = "1019" }],
            StartPos = new PositionDto { Id = 1, Track = "A" },
            TargetCats = ["CatA1"],
            MaxRolls = 0
        };

        var response = await _client.PostAsJsonAsync("/api/v1/simulate", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Simulate_NonExistentTarget_Returns400_ViaExceptionHandler()
    {
        // 不存在的目標貓 → GachaSimulator 拋出 ArgumentException → middleware 轉 400
        var request = new SimulateRequest
        {
            Seed = "123",
            Pools = [new PoolSpec { EventId = "1019" }],
            StartPos = new PositionDto { Id = 1, Track = "A" },
            TargetCats = ["NonExistentCat"],
            MaxRolls = 50
        };

        var response = await _client.PostAsJsonAsync("/api/v1/simulate", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.ToString());
    }
}
