using BattleCatCalc.Core.Models;

namespace BattleCatCalc.Core.Interfaces;

/// <summary>
/// Parses raw HTML into structured gacha track data.
/// </summary>
public interface IParser
{
    /// <summary>
    /// Parses HTML into Track A and Track B.
    /// </summary>
    /// <param name="html">Raw HTML string from the seed tracking service.</param>
    /// <returns>A tuple of (TrackA, TrackB).</returns>
    (Track TrackA, Track TrackB) Parse(string html);
}
