using BattleCatCalc.Core.Interfaces;
using BattleCatCalc.Core.Models;

namespace BattleCatCalc.Application.Analysis;

/// <summary>
/// Provides query utilities over parsed gacha track data.
/// </summary>
public sealed class SeedAnalyzer : IAnalyzer
{
    private readonly Track _slots;

    public SeedAnalyzer(Track slots) => _slots = slots;

    /// <inheritdoc />
    public IEnumerable<GachaSlot> FindCat(string name)
    {
        foreach (var slot in _slots)
        {
            var catName = slot.Result.AsCatName();
            if (catName is not null && catName.Contains(name, StringComparison.Ordinal))
                yield return slot;
        }
    }

    /// <inheritdoc />
    public IEnumerable<Position> FindGuaranteedPositions(string name)
    {
        foreach (var slot in _slots)
        {
            var catName = slot.Guaranteed.AsCatName();
            if (catName is not null && catName.Contains(name, StringComparison.Ordinal))
                yield return slot.Id;
        }
    }
}
