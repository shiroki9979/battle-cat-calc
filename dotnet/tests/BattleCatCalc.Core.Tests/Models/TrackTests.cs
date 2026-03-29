using BattleCatCalc.Core.Models;

namespace BattleCatCalc.Core.Tests.Models;

public class TrackTests
{
    private static GachaSlot MakeSlot(int id, string track, string catName) =>
        new(
            Id: new Position(id, track),
            Result: new SlotValue.CatName(catName),
            Guaranteed: new SlotValue.CatName($"G-{catName}"));

    [Fact]
    public void Constructor_StoresNameAndSlots()
    {
        var slots = new[] { MakeSlot(1, "A", "Jizo"), MakeSlot(2, "A", "Gao") };
        var track = new Track("A", slots);

        Assert.Equal("A", track.Name);
        Assert.Equal(2, track.Count);
    }

    [Fact]
    public void Indexer_ReturnsSlotByListIndex()
    {
        var slots = new[] { MakeSlot(1, "A", "Jizo"), MakeSlot(2, "A", "Gao") };
        var track = new Track("A", slots);

        Assert.Equal("Jizo", track[0].Result.AsCatName());
        Assert.Equal("Gao", track[1].Result.AsCatName());
    }

    [Fact]
    public void GetSlot_ExistingId_ReturnsSlot()
    {
        var slots = new[] { MakeSlot(1, "A", "Jizo"), MakeSlot(5, "A", "Gao") };
        var track = new Track("A", slots);

        var slot = track.GetSlot(5);

        Assert.NotNull(slot);
        Assert.Equal("Gao", slot.Result.AsCatName());
    }

    [Fact]
    public void GetSlot_NonExistingId_ReturnsNull()
    {
        var track = new Track("A", [MakeSlot(1, "A", "Jizo")]);

        Assert.Null(track.GetSlot(99));
    }

    [Fact]
    public void Enumeration_YieldsAllSlots()
    {
        var slots = new[] { MakeSlot(1, "A", "Cat1"), MakeSlot(2, "A", "Cat2"), MakeSlot(3, "A", "Cat3") };
        var track = new Track("A", slots);

        var names = track.Select(s => s.Result.AsCatName()).ToList();

        Assert.Equal(["Cat1", "Cat2", "Cat3"], names);
    }

    [Fact]
    public void ToString_ShowsNameAndCount()
    {
        var track = new Track("B", [MakeSlot(1, "B", "Cat1")]);

        Assert.Equal("Track(name='B', slots=1)", track.ToString());
    }
}
