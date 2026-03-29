using System.Net;
using System.Text.Json;

namespace BattleCatCalc.Api.Tests;

/// <summary>
/// Integration tests for health and tracks endpoints.
/// </summary>
public class HealthAndTrackTests : IClassFixture<ApiTestFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _client;

    public HealthAndTrackTests(ApiTestFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    #region Health

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("healthy", body);
    }

    #endregion

    #region Tracks

    [Fact]
    public async Task GetTracks_ValidParams_ReturnsTrackData()
    {
        var response = await _client.GetAsync("/api/v1/tracks?seed=123&event=1019");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("123", root.GetProperty("seed").GetString());
        Assert.Equal("1019", root.GetProperty("eventId").GetString());
        Assert.True(root.GetProperty("trackA").GetArrayLength() > 0);
        Assert.True(root.GetProperty("trackB").GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetTracks_InvalidCount_Returns400()
    {
        var response = await _client.GetAsync("/api/v1/tracks?seed=123&event=1019&count=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTracks_SlotContainsExpectedFields()
    {
        var response = await _client.GetAsync("/api/v1/tracks?seed=123&event=1019");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var firstSlot = doc.RootElement.GetProperty("trackA")[0];
        Assert.True(firstSlot.TryGetProperty("id", out _));
        Assert.True(firstSlot.TryGetProperty("track", out _));
        Assert.True(firstSlot.TryGetProperty("result", out _));
        Assert.True(firstSlot.TryGetProperty("guaranteed", out _));
        Assert.True(firstSlot.TryGetProperty("isDuplicateRare", out _));
    }

    #endregion
}
