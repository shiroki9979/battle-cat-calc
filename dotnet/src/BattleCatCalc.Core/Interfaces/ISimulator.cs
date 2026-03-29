using System.Collections.Frozen;
using BattleCatCalc.Core.Models;

namespace BattleCatCalc.Core.Interfaces;

/// <summary>
/// Finds the optimal roll path to collect target cats using Dijkstra/A*/Beam Search.
/// </summary>
public interface ISimulator
{
    /// <summary>
    /// Finds the minimum-cost path to collect all target cats.
    /// </summary>
    /// <param name="start">Starting position on the track.</param>
    /// <param name="targets">Set of target cat names to collect.</param>
    /// <param name="maxRolls">Maximum number of rolls to search (safety limit).</param>
    /// <returns>The optimal result, or <c>null</c> if no path exists within the limit.</returns>
    GachaResult? FindBestPath(Position start, FrozenSet<string> targets, int maxRolls = 1000);
}
