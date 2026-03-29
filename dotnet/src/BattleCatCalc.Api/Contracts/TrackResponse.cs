using System.Text.Json.Serialization;
using BattleCatCalc.Core.Models;

namespace BattleCatCalc.Api.Contracts;

/// <summary>
/// Response DTO for gacha track data.
/// </summary>
public sealed class TrackResponse
{
    [JsonPropertyName("seed")]
    public required string Seed { get; init; }

    [JsonPropertyName("eventId")]
    public required string EventId { get; init; }

    [JsonPropertyName("trackA")]
    public required IReadOnlyList<SlotDto> TrackA { get; init; }

    [JsonPropertyName("trackB")]
    public required IReadOnlyList<SlotDto> TrackB { get; init; }
}

/// <summary>
/// DTO for a single gacha slot.
/// </summary>
public sealed class SlotDto
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("track")]
    public required string Track { get; init; }

    [JsonPropertyName("result")]
    public required string Result { get; init; }

    [JsonPropertyName("guaranteed")]
    public required string Guaranteed { get; init; }

    [JsonPropertyName("nextPos")]
    public PositionDto? NextPos { get; init; }

    [JsonPropertyName("isDuplicateRare")]
    public bool IsDuplicateRare { get; init; }

    public static SlotDto FromModel(GachaSlot slot) => new()
    {
        Id = slot.Id.Id,
        Track = slot.Id.Track,
        Result = slot.Result.ToString(),
        Guaranteed = slot.Guaranteed.ToString(),
        NextPos = slot.NextPos is { } np ? PositionDto.FromModel(np) : null,
        IsDuplicateRare = slot.IsDuplicateRare
    };
}

/// <summary>
/// DTO for a position on the track.
/// </summary>
public sealed class PositionDto
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("track")]
    public required string Track { get; init; }

    public static PositionDto FromModel(Position pos) => new()
    {
        Id = pos.Id,
        Track = pos.Track
    };

    public Position ToModel() => new(Id, Track);
}
