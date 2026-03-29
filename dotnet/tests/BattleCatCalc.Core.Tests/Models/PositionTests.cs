using BattleCatCalc.Core.Models;

namespace BattleCatCalc.Core.Tests.Models;

public class PositionTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var pos = new Position(1, "A");

        Assert.Equal(1, pos.Id);
        Assert.Equal("A", pos.Track);
    }

    [Fact]
    public void ToString_ReturnsIdAndTrack()
    {
        var pos = new Position(5, "B");

        Assert.Equal("5B", pos.ToString());
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new Position(3, "A");
        var b = new Position(3, "A");

        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = new Position(1, "A");
        var b = new Position(1, "B");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void GetHashCode_SameValues_SameHash()
    {
        var a = new Position(7, "A");
        var b = new Position(7, "A");

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void CanBeUsedAsDictionaryKey()
    {
        var dict = new Dictionary<Position, string>
        {
            [new Position(1, "A")] = "cat1"
        };

        Assert.Equal("cat1", dict[new Position(1, "A")]);
    }
}
