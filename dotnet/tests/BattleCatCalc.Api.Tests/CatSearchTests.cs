using System.Net;
using System.Text.Json;

namespace BattleCatCalc.Api.Tests;

/// <summary>
/// Integration tests for cat search endpoints.
/// </summary>
public class CatSearchTests : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client;

    public CatSearchTests(ApiTestFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    #region /cats/search

    [Fact]
    public async Task SearchCats_MatchFound_ReturnsResults()
    {
        var response = await _client.GetAsync("/api/v1/cats/search?seed=123&event=1019&name=CatA1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement;

        Assert.True(arr.GetArrayLength() > 0);
        Assert.Equal("CatA1", arr[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task SearchCats_NoMatch_ReturnsEmptyArray()
    {
        var response = await _client.GetAsync("/api/v1/cats/search?seed=123&event=1019&name=ZZZ_NO_MATCH");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task SearchCats_MissingName_Returns400()
    {
        var response = await _client.GetAsync("/api/v1/cats/search?seed=123&event=1019&name=");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region /cats/guaranteed

    [Fact]
    public async Task SearchGuaranteed_MatchFound_ReturnsPositions()
    {
        var response = await _client.GetAsync("/api/v1/cats/guaranteed?seed=123&event=1019&name=GuaCatA1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement;

        Assert.True(arr.GetArrayLength() > 0);
        Assert.Equal("A", arr[0].GetProperty("track").GetString());
    }

    [Fact]
    public async Task SearchGuaranteed_NoMatch_ReturnsEmptyArray()
    {
        var response = await _client.GetAsync("/api/v1/cats/guaranteed?seed=123&event=1019&name=ZZZ_NO_MATCH");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetArrayLength());
    }

    #endregion
}
