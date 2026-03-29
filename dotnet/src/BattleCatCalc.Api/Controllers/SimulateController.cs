using System.Collections.Frozen;
using BattleCatCalc.Api.Contracts;
using BattleCatCalc.Application.Simulation;
using BattleCatCalc.Core.Interfaces;
using BattleCatCalc.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace BattleCatCalc.Api.Controllers;

/// <summary>
/// 模擬搜尋端點。
/// </summary>
[ApiController]
[Route("api/v1")]
public sealed class SimulateController(IFetcher fetcher, IParser parser) : ControllerBase
{
    /// <summary>
    /// POST /api/v1/simulate — 執行最佳路徑模擬。
    /// </summary>
    [HttpPost("simulate")]
    public async Task<IActionResult> SimulateAsync(
        [FromBody] SimulateRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Seed))
            return BadRequest(CreateProblem("seed is required."));

        if (request.Pools is not { Count: > 0 })
            return BadRequest(CreateProblem("At least one pool is required."));

        if (request.TargetCats is not { Count: > 0 })
            return BadRequest(CreateProblem("At least one target cat is required."));

        if (request.MaxRolls is < 1 or > 10000)
            return BadRequest(CreateProblem("maxRolls must be between 1 and 10000."));

        // 依序取得各池資料並建構 GachaPool
        var pools = new List<GachaPool>(request.Pools.Count);
        foreach (var poolSpec in request.Pools)
        {
            var html = await fetcher.FetchHtmlAsync(
                request.Seed, poolSpec.EventId, poolSpec.Lang, poolSpec.Count, ct);
            var (trackA, trackB) = parser.Parse(html);

            pools.Add(new GachaPool(
                PoolId: poolSpec.EventId,
                Name: poolSpec.EventId,
                TrackA: trackA,
                TrackB: trackB));
        }

        var simulator = new GachaSimulator(pools);
        var targets = request.TargetCats.ToFrozenSet();
        var start = request.StartPos.ToModel();

        var result = simulator.FindBestPath(
            start, targets, request.MaxRolls,
            request.BeamWidth, preferGuaranteed: false);

        if (result is null)
            return NotFound(new
            {
                type = "https://tools.ietf.org/html/rfc9457",
                title = "No Path Found",
                status = 404,
                detail = $"No path found within {request.MaxRolls} rolls."
            });

        return Ok(SimulateResponse.FromResult(result));
    }

    private static object CreateProblem(string detail) => new
    {
        type = "https://tools.ietf.org/html/rfc9457",
        title = "Bad Request",
        status = 400,
        detail
    };
}
