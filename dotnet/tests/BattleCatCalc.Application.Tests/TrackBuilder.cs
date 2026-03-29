using BattleCatCalc.Core.Enums;
using BattleCatCalc.Core.Models;

namespace BattleCatCalc.Application.Tests;

/// <summary>
/// Helpers for building mock Track/GachaPool data in tests.
/// </summary>
internal static class TrackBuilder
{
    /// <summary>
    /// Builds a track with sequential slots. Each slot gets result "Cat{i}" and guaranteed "Gua{i}".
    /// NextPos defaults to (i+10, oppositeTrack) to simulate guaranteed track switch.
    /// </summary>
    internal static Track Build(string trackName, int count, Func<int, string, GachaSlot>? slotFactory = null)
    {
        var opposite = trackName == "A" ? "B" : "A";
        var slots = Enumerable.Range(1, count).Select(i =>
            slotFactory?.Invoke(i, trackName) ?? new GachaSlot(
                Id: new Position(i, trackName),
                Result: new SlotValue.CatName($"Cat{i}"),
                Guaranteed: new SlotValue.CatName($"Gua{i}"),
                NextPos: new Position(i + 10, opposite)));
        return new Track(trackName, slots);
    }

    /// <summary>
    /// Builds a default GachaPool with both tracks.
    /// </summary>
    internal static GachaPool BuildPool(
        string poolId = "default",
        int trackSize = 20,
        bool hasGuaranteed = true,
        int guaranteedCost = 10,
        Func<int, string, GachaSlot>? slotFactoryA = null,
        Func<int, string, GachaSlot>? slotFactoryB = null)
    {
        var trackA = Build("A", trackSize, slotFactoryA);
        var trackB = Build("B", trackSize, slotFactoryB);
        return new GachaPool(poolId, poolId, trackA, trackB, hasGuaranteed, guaranteedCost);
    }

    /// <summary>Creates a slot with a ParseStatus error result.</summary>
    internal static GachaSlot InvalidSlot(int id, string track) =>
        new(new Position(id, track),
            new SlotValue.Status(ParseStatus.InvalidData),
            new SlotValue.Status(ParseStatus.InvalidData));
}
