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

    /// <summary>
    /// Parses the home page HTML to extract the event dropdown list.
    /// </summary>
    /// <param name="html">Raw HTML of the home page.</param>
    /// <returns>List of available events grouped by category.</returns>
    IReadOnlyList<EventInfo> ParseEventList(string html);
}
