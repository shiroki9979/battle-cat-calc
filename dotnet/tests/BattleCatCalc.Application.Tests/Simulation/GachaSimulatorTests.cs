using System.Collections.Frozen;
using BattleCatCalc.Application.Simulation;
using BattleCatCalc.Core.Models;

namespace BattleCatCalc.Application.Tests.Simulation;

public class GachaSimulatorTests
{
    #region Empty / trivial targets

    [Fact]
    public void FindBestPath_EmptyTargets_ReturnsCostZero()
    {
        var pool = TrackBuilder.BuildPool();
        var sim = new GachaSimulator([pool]);
        var start = new Position(1, "A");

        var result = sim.FindBestPath(start, FrozenSet<string>.Empty);

        Assert.NotNull(result);
        Assert.Equal(0, result.TotalCost);
        Assert.Empty(result.Path);
    }

    #endregion

    #region Single target

    [Fact]
    public void FindBestPath_SingleTarget_FindsWithSingleRoll()
    {
        // Cat1 在 1A，從 1A 開始只需一抽
        var pool = TrackBuilder.BuildPool();
        var sim = new GachaSimulator([pool]);
        var start = new Position(1, "A");
        var targets = new[] { "Cat1" }.ToFrozenSet();

        var result = sim.FindBestPath(start, targets);

        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCost);
        Assert.Single(result.Path);
        Assert.Equal("Cat1", result.Path[0].Unit);
        Assert.False(result.Path[0].IsGuaranteed);
    }

    [Fact]
    public void FindBestPath_SingleTargetFarAway_CostEqualsDistance()
    {
        // Cat5 在 5A，從 1A 開始需要 5 抽（1A→2A→3A→4A→5A）
        var pool = TrackBuilder.BuildPool();
        var sim = new GachaSimulator([pool]);
        var start = new Position(1, "A");
        var targets = new[] { "Cat5" }.ToFrozenSet();

        var result = sim.FindBestPath(start, targets);

        Assert.NotNull(result);
        Assert.Equal(5, result.TotalCost);
        Assert.Contains("Cat5", (IEnumerable<string>)result.Collected);
    }

    #endregion

    #region Target not found

    [Fact]
    public void FindBestPath_TargetNotInAnyPool_ThrowsArgumentException()
    {
        var pool = TrackBuilder.BuildPool();
        var sim = new GachaSimulator([pool]);
        var start = new Position(1, "A");
        var targets = new[] { "NonExistentCat" }.ToFrozenSet();

        var ex = Assert.Throws<ArgumentException>(() => sim.FindBestPath(start, targets));
        Assert.Contains("NonExistentCat", ex.Message);
    }

    #endregion

    #region Guaranteed roll

    [Fact]
    public void FindBestPath_TargetOnlyInGuaranteed_UsesGuaranteedPath()
    {
        // Gua1 只出現在保底結果中，單抽拿不到
        var pool = TrackBuilder.BuildPool();
        var sim = new GachaSimulator([pool]);
        var start = new Position(1, "A");
        var targets = new[] { "Gua1" }.ToFrozenSet();

        var result = sim.FindBestPath(start, targets);

        Assert.NotNull(result);
        Assert.Contains(result.Path, s => s.IsGuaranteed);
        Assert.Contains("Gua1", (IEnumerable<string>)result.Collected);
    }

    #endregion

    #region Multiple targets

    [Fact]
    public void FindBestPath_MultipleTargets_CollectsAll()
    {
        var pool = TrackBuilder.BuildPool();
        var sim = new GachaSimulator([pool]);
        var start = new Position(1, "A");
        var targets = new[] { "Cat1", "Cat3" }.ToFrozenSet();

        var result = sim.FindBestPath(start, targets);

        Assert.NotNull(result);
        foreach (var target in targets)
            Assert.Contains(target, (IEnumerable<string>)result.Collected);
        // Cat3 在 3A，從 1A 開始至少需要 3 抽
        Assert.InRange(result.TotalCost, 3, int.MaxValue);
    }

    #endregion

    #region Multi-pool

    [Fact]
    public void FindBestPath_MultiPool_SearchesAllPools()
    {
        // Pool B 有 "SpecialCat" 在 1A，Pool A 沒有
        var poolA = TrackBuilder.BuildPool(poolId: "poolA");
        var poolB = TrackBuilder.BuildPool(poolId: "poolB", slotFactoryA: (i, t) =>
            new GachaSlot(
                new Position(i, t),
                new SlotValue.CatName(i == 1 ? "SpecialCat" : $"Cat{i}"),
                new SlotValue.CatName($"Gua{i}"),
                new Position(i + 10, "B")));

        var sim = new GachaSimulator([poolA, poolB]);
        var start = new Position(1, "A");
        var targets = new[] { "SpecialCat" }.ToFrozenSet();

        var result = sim.FindBestPath(start, targets);

        Assert.NotNull(result);
        Assert.Contains("SpecialCat", (IEnumerable<string>)result.Collected);
        Assert.Equal("poolB", result.Path[0].PoolId);
    }

    #endregion

    #region Max rolls exceeded

    [Fact]
    public void FindBestPath_MaxRollsExceeded_ReturnsNull()
    {
        var pool = TrackBuilder.BuildPool();
        var sim = new GachaSimulator([pool]);
        var start = new Position(1, "A");
        // Cat10 在 10A，但 maxRolls=5 不夠到達
        var targets = new[] { "Cat10" }.ToFrozenSet();

        var result = sim.FindBestPath(start, targets, maxRolls: 5);

        Assert.Null(result);
    }

    #endregion

    #region Duplicate rare / track switch

    [Fact]
    public void FindBestPath_DuplicateRare_HandlesTrackSwitch()
    {
        // 設定第 2 格為重複稀有：上一隻是 "RareCat" 時觸發，單抽落在 5B
        var trackA = new Track("A",
        [
            new GachaSlot(
                new Position(1, "A"),
                new SlotValue.CatName("RareCat"),
                new SlotValue.CatName("Gua1"),
                new Position(11, "B")),
            new GachaSlot(
                new Position(2, "A"),
                new SlotValue.CatName("ReplacementCat"),
                new SlotValue.CatName("Gua2"),
                new Position(12, "B"),
                OriginalResult: "RareCat",
                IsDuplicateRare: true,
                SingleNextPos: new Position(5, "B")),
            .. Enumerable.Range(3, 18).Select(i => new GachaSlot(
                new Position(i, "A"),
                new SlotValue.CatName($"Cat{i}"),
                new SlotValue.CatName($"Gua{i}"),
                new Position(i + 10, "B")))
        ]);
        var trackB = TrackBuilder.Build("B", 20);

        var pool = new GachaPool("default", "Default", trackA, trackB);
        var sim = new GachaSimulator([pool]);
        var start = new Position(1, "A");
        // 目標是 Cat5（在 5B）；沿 A 軌走到不了，只有透過 2A 的重複稀有換軌才到得了：1A → 2A → 5B
        var targets = new[] { "Cat5" }.ToFrozenSet();

        var result = sim.FindBestPath(start, targets);

        Assert.NotNull(result);
        Assert.Contains("Cat5", (IEnumerable<string>)result.Collected);
        Assert.Equal(3, result.TotalCost);
        Assert.Equal(["RareCat", "ReplacementCat", "Cat5"], result.Path.Select(s => s.Unit));
        Assert.True(result.Path[1].IsDuplicateRare);
        Assert.True(result.Path[1].IsTrackSwitch);
    }

    #endregion

    #region Invariant: position monotonically increases

    [Fact]
    public void FindBestPath_PositionMonotonicallyIncreases()
    {
        var pool = TrackBuilder.BuildPool(trackSize: 30);
        var sim = new GachaSimulator([pool]);
        var start = new Position(1, "A");
        var targets = new[] { "Cat1", "Cat5", "Cat10" }.ToFrozenSet();

        var result = sim.FindBestPath(start, targets, maxRolls: 100);

        Assert.NotNull(result);
        int lastPosId = 0;
        foreach (var step in result.Path)
        {
            Assert.True(step.Source.Id >= lastPosId,
                $"Position decreased: {step.Source.Id} < {lastPosId}");
            lastPosId = step.Source.Id;
        }
    }

    #endregion

    #region Beam search

    [Fact]
    public void FindBestPath_BeamSearch_ReturnsResult()
    {
        var pool = TrackBuilder.BuildPool(trackSize: 30);
        var sim = new GachaSimulator([pool]);
        var start = new Position(1, "A");
        var targets = new[] { "Cat3", "Cat7" }.ToFrozenSet();

        var result = sim.FindBestPath(start, targets, maxRolls: 100,
            beamWidth: 10, preferGuaranteed: false);

        Assert.NotNull(result);
        Assert.True(targets.IsSubsetOf(result.Collected));
    }

    #endregion

    #region Single-pool constructor

    [Fact]
    public void FindBestPath_SinglePoolConstructor_Works()
    {
        var trackA = TrackBuilder.Build("A", 10);
        var trackB = TrackBuilder.Build("B", 10);
        var sim = new GachaSimulator(trackA, trackB);
        var start = new Position(1, "A");
        var targets = new[] { "Cat1" }.ToFrozenSet();

        var result = sim.FindBestPath(start, targets);

        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCost);
    }

    #endregion

    #region Progress callback

    [Fact]
    public void FindBestPath_ProgressCallback_IsCalled()
    {
        var pool = TrackBuilder.BuildPool();
        var sim = new GachaSimulator([pool]);
        var start = new Position(1, "A");
        var targets = new[] { "Cat3" }.ToFrozenSet();
        int callCount = 0;

        sim.FindBestPath(start, targets, maxRolls: 100,
            beamWidth: null, preferGuaranteed: false,
            progressCallback: (_, _, _) => callCount++);

        Assert.True(callCount > 0);
    }

    #endregion

    #region No guaranteed pool

    [Fact]
    public void FindBestPath_NoGuaranteedPool_OnlyUsesSingleRolls()
    {
        var pool = TrackBuilder.BuildPool(hasGuaranteed: false);
        var sim = new GachaSimulator([pool]);
        var start = new Position(1, "A");
        var targets = new[] { "Cat3" }.ToFrozenSet();

        var result = sim.FindBestPath(start, targets);

        Assert.NotNull(result);
        Assert.All(result.Path, s => Assert.False(s.IsGuaranteed));
    }

    #endregion
}
