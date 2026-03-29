using System.Text.Json.Serialization;
using BattleCatCalc.Core.Models;

namespace BattleCatCalc.Api.Contracts;

/// <summary>
/// Response DTO for simulation result.
/// </summary>
public sealed class SimulateResponse
{
    [JsonPropertyName("totalCost")]
    public int TotalCost { get; init; }

    [JsonPropertyName("stepCount")]
    public int StepCount { get; init; }

    [JsonPropertyName("guaranteedCount")]
    public int GuaranteedCount { get; init; }

    [JsonPropertyName("singleRollCount")]
    public int SingleRollCount { get; init; }

    [JsonPropertyName("collected")]
    public required IReadOnlyList<string> Collected { get; init; }

    [JsonPropertyName("startPos")]
    public required PositionDto StartPos { get; init; }

    [JsonPropertyName("endPos")]
    public PositionDto? EndPos { get; init; }

    [JsonPropertyName("path")]
    public required IReadOnlyList<StepDto> Path { get; init; }

    public static SimulateResponse FromResult(GachaResult result) => new()
    {
        TotalCost = result.TotalCost,
        StepCount = result.StepCount,
        GuaranteedCount = result.GuaranteedCount,
        SingleRollCount = result.SingleRollCount,
        Collected = [.. result.Collected],
        StartPos = result.StartPos is { } sp
            ? PositionDto.FromModel(sp)
            : new PositionDto { Id = 0, Track = "A" },
        EndPos = result.EndPos is { } ep ? PositionDto.FromModel(ep) : null,
        Path = result.Path.Select(StepDto.FromModel).ToList()
    };
}

/// <summary>
/// DTO for a single simulation step.
/// </summary>
public sealed class StepDto
{
    [JsonPropertyName("source")]
    public required PositionDto Source { get; init; }

    [JsonPropertyName("target")]
    public required PositionDto Target { get; init; }

    [JsonPropertyName("unit")]
    public required string Unit { get; init; }

    [JsonPropertyName("poolId")]
    public required string PoolId { get; init; }

    [JsonPropertyName("isGuaranteed")]
    public bool IsGuaranteed { get; init; }

    public static StepDto FromModel(GachaStep step) => new()
    {
        Source = PositionDto.FromModel(step.Source),
        Target = PositionDto.FromModel(step.Target),
        Unit = step.Unit,
        PoolId = step.PoolId,
        IsGuaranteed = step.IsGuaranteed
    };
}
