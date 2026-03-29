namespace BattleCatCalc.Api.Contracts;

/// <summary>
/// Query parameters for fetching gacha track data.
/// </summary>
public sealed record TrackRequest(
    string Seed,
    string Event,
    string Lang = "tw",
    int Count = 100);
