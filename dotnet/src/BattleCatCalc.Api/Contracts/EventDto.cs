using System.Text.Json.Serialization;

namespace BattleCatCalc.Api.Contracts;

/// <summary>
/// Response DTO for an individual gacha event.
/// </summary>
public sealed class EventDto
{
    [JsonPropertyName("value")]
    public required string Value { get; init; }

    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("group")]
    public required string Group { get; init; }
}
