namespace BattleCatCalc.Core.Models;

/// <summary>
/// Represents a gacha event pool with its two tracks and guaranteed settings.
/// </summary>
/// <param name="PoolId">Pool identifier (e.g. "ultra_souls").</param>
/// <param name="Name">Display name (e.g. "Ultra Souls").</param>
/// <param name="TrackA">Track A slot data.</param>
/// <param name="TrackB">Track B slot data.</param>
/// <param name="HasGuaranteed">Whether this pool offers a guaranteed (十連) option.</param>
/// <param name="GuaranteedCost">Number of rolls for a guaranteed pull (typically 10).</param>
public record class GachaPool(
    string PoolId,
    string Name,
    Track TrackA,
    Track TrackB,
    bool HasGuaranteed = true,
    int GuaranteedCost = 10);
