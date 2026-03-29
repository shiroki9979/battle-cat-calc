using System.Text.Json.Serialization;
using BattleCatCalc.Core.Models;

namespace BattleCatCalc.Api.Contracts;

/// <summary>
/// DTO for cat search results.
/// </summary>
public sealed class CatSearchResult
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("track")]
    public required string Track { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("guaranteed")]
    public required string Guaranteed { get; init; }

    [JsonPropertyName("isDuplicateRare")]
    public bool IsDuplicateRare { get; init; }

    public static CatSearchResult FromSlot(GachaSlot slot) => new()
    {
        Id = slot.Id.Id,
        Track = slot.Id.Track,
        Name = slot.Result.ToString(),
        Guaranteed = slot.Guaranteed.ToString(),
        IsDuplicateRare = slot.IsDuplicateRare
    };
}
