using System.Collections.Frozen;
using BattleCatCalc.Core.Models;

namespace BattleCatCalc.Core.Tests.Models;

public class GachaResultTests
{
    private static GachaStep MakeStep(int from, int to, string unit, string pool = "", bool guaranteed = false) =>
        new(
            Source: new Position(from, "A"),
            Target: new Position(to, "A"),
            Unit: unit,
            PoolId: pool,
            IsGuaranteed: guaranteed);

    [Fact]
    public void Properties_ComputeCorrectly()
    {
        var steps = new[]
        {
            MakeStep(1, 2, "CatA"),
            MakeStep(2, 3, "CatB"),
            MakeStep(3, 13, "CatC", "pool1", guaranteed: true)
        };
        var result = new GachaResult(
            totalCost: 12,
            path: steps,
            collected: new[] { "CatA", "CatB", "CatC" }.ToFrozenSet(),
            startPos: new Position(1, "A"));

        Assert.Equal(12, result.TotalCost);
        Assert.Equal(3, result.StepCount);
        Assert.Equal(2, result.SingleRollCount);
        Assert.Equal(1, result.GuaranteedCount);
        Assert.Equal(new Position(13, "A"), result.EndPos);
        Assert.Contains("pool1", (IEnumerable<string>)result.PoolsUsed);
    }

    [Fact]
    public void StepsByPool_GroupsCorrectly()
    {
        var steps = new[]
        {
            MakeStep(1, 2, "CatA", "pool1"),
            MakeStep(2, 3, "CatB", "pool2"),
            MakeStep(3, 4, "CatC", "pool1")
        };
        var result = new GachaResult(3, steps, new[] { "CatA", "CatB", "CatC" }.ToFrozenSet());

        var grouped = result.StepsByPool();

        Assert.Equal(2, grouped["pool1"].Count);
        Assert.Single(grouped["pool2"]);
    }

    [Fact]
    public void EndPos_EmptyPath_ReturnsStartPos()
    {
        var result = new GachaResult(
            totalCost: 0,
            path: [],
            collected: FrozenSet<string>.Empty,
            startPos: new Position(1, "A"));

        Assert.Equal(new Position(1, "A"), result.EndPos);
    }

    [Fact]
    public void ToString_ShowsSummary()
    {
        var result = new GachaResult(
            totalCost: 5,
            path: [MakeStep(1, 2, "CatA")],
            collected: new[] { "CatA" }.ToFrozenSet());

        Assert.Contains("cost=5", result.ToString());
        Assert.Contains("steps=1", result.ToString());
        Assert.Contains("collected=1", result.ToString());
    }
}
