using System.Text.Json.Serialization;

namespace BattleCatCalc.Api.Contracts;

/// <summary>
/// Request body for simulation endpoint.
/// </summary>
public sealed class SimulateRequest
{
    [JsonPropertyName("seed")]
    public required string Seed { get; init; }

    [JsonPropertyName("pools")]
    public required IReadOnlyList<PoolSpec> Pools { get; init; }

    [JsonPropertyName("startPos")]
    public required PositionDto StartPos { get; init; }

    [JsonPropertyName("targetCats")]
    public required IReadOnlyList<string> TargetCats { get; init; }

    [JsonPropertyName("maxRolls")]
    public int MaxRolls { get; init; } = 250;

    [JsonPropertyName("beamWidth")]
    public int? BeamWidth { get; init; }
}

/// <summary>
/// Specifies a pool to include in simulation.
/// </summary>
public sealed class PoolSpec
{
    [JsonPropertyName("eventId")]
    public required string EventId { get; init; }

    [JsonPropertyName("lang")]
    public string Lang { get; init; } = "tw";

    [JsonPropertyName("count")]
    public int Count { get; init; } = 200;
}
