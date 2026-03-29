using BattleCatCalc.Core.Models;

namespace BattleCatCalc.Core.Interfaces;

/// <summary>
/// Provides analysis utilities for querying gacha track data.
/// </summary>
public interface IAnalyzer
{
    /// <summary>
    /// Finds all slots containing the specified cat name.
    /// </summary>
    IEnumerable<GachaSlot> FindCat(string name);

    /// <summary>
    /// Finds positions where the specified cat appears as a guaranteed result.
    /// </summary>
    IEnumerable<Position> FindGuaranteedPositions(string name);
}
