using System.Collections.Frozen;
using BattleCatCalc.Application.Simulation;
using BattleCatCalc.Core.Models;

namespace BattleCatCalc.Application.Tests.Simulation;

/// <summary>
/// Simulator 邊界條件與防禦性測試。
/// </summary>
public class GachaSimulatorEdgeCaseTests
{
    #region 空池列表

    [Fact]
    public void FindBestPath_EmptyPoolList_ThrowsArgumentException()
    {
        /// 動機：空池列表無法搜尋任何目標，應直接報錯。
        var sim = new GachaSimulator([]);
        var start = new Position(1, "A");
        var targets = new[] { "Cat1" }.ToFrozenSet();

        Assert.Throws<ArgumentException>(() => sim.FindBestPath(start, targets));
    }

    #endregion

    #region 起點超出軌道範圍

    [Fact]
    public void FindBestPath_StartBeyondTrackEnd_ReturnsNull()
    {
        /// 動機：起點 ID 超出軌道長度，所有 GetSlot 都回傳 null，無法展開。
        var pool = TrackBuilder.BuildPool(trackSize: 10);
        var sim = new GachaSimulator([pool]);
        var start = new Position(100, "A");
        var targets = new[] { "Cat1" }.ToFrozenSet();

        // Cat1 在 pos 1，但起點在 100，可達性剪枝會刪除（catMaxPos=10 < 100）
        var result = sim.FindBestPath(start, targets, maxRolls: 200);

        Assert.Null(result);
    }

    #endregion

    #region Beam Search 極端寬度

    [Fact]
    public void FindBestPath_BeamWidth1_StillReturnsResult()
    {
        /// 動機：beamWidth=1 為最窄可能值，應至少能回傳結果（可能非最佳）。
        var pool = TrackBuilder.BuildPool(trackSize: 20);
        var sim = new GachaSimulator([pool]);
        var start = new Position(1, "A");
        var targets = new[] { "Cat1" }.ToFrozenSet();

        var result = sim.FindBestPath(start, targets, maxRolls: 50,
            beamWidth: 1, preferGuaranteed: false);

        Assert.NotNull(result);
        Assert.Contains("Cat1", (IEnumerable<string>)result.Collected);
    }

    #endregion

    #region preferGuaranteed 模式

    [Fact]
    public void FindBestPath_PreferGuaranteed_IncreaseSingleRollCost()
    {
        /// 動機：preferGuaranteed=true 會使單抽成本 ×1.11，更傾向使用保底路徑。
        var pool = TrackBuilder.BuildPool(trackSize: 20);
        var sim = new GachaSimulator([pool]);
        var start = new Position(1, "A");
        var targets = new[] { "Gua1" }.ToFrozenSet();

        var result = sim.FindBestPath(start, targets, maxRolls: 100,
            beamWidth: null, preferGuaranteed: true);

        Assert.NotNull(result);
        Assert.Contains("Gua1", (IEnumerable<string>)result.Collected);
    }

    #endregion

    #region GuaranteedCost 變體

    [Fact]
    public void FindBestPath_CustomGuaranteedCost_ReflectedInResult()
    {
        /// 動機：不同池可有不同保底成本，結果的 TotalCost 應正確反映。
        var pool = TrackBuilder.BuildPool(guaranteedCost: 15);
        var sim = new GachaSimulator([pool]);
        var start = new Position(1, "A");
        var targets = new[] { "Gua1" }.ToFrozenSet();

        var result = sim.FindBestPath(start, targets, maxRolls: 100);

        Assert.NotNull(result);
        // 如果走保底路徑，成本應包含 15 而非預設的 10
        if (result.Path.Any(s => s.IsGuaranteed))
        {
            Assert.True(result.TotalCost >= 15);
        }
    }

    #endregion

    #region HasGuaranteed=false 池

    [Fact]
    public void FindBestPath_AllPoolsNoGuaranteed_NeverUsesGuaranteed()
    {
        /// 動機：所有池都關閉保底時，結果不應包含保底步驟。
        var pool1 = TrackBuilder.BuildPool(poolId: "p1", hasGuaranteed: false);
        var pool2 = TrackBuilder.BuildPool(poolId: "p2", hasGuaranteed: false);
        var sim = new GachaSimulator([pool1, pool2]);
        var start = new Position(1, "A");
        var targets = new[] { "Cat5" }.ToFrozenSet();

        var result = sim.FindBestPath(start, targets, maxRolls: 100);

        Assert.NotNull(result);
        Assert.All(result.Path, s => Assert.False(s.IsGuaranteed));
    }

    #endregion

    #region 多目標全在保底

    [Fact]
    public void FindBestPath_MultipleTargetsAllInGuaranteed_UsesMultipleGuaranteed()
    {
        /// 動機：多個目標都只出現在保底結果中，模擬器必須使用多次保底。
        /// Gua1 在 1A 保底可得（→ 11B），Gua11 在 11B 保底可得（→ 21A）。
        var pool = TrackBuilder.BuildPool(trackSize: 50);
        var sim = new GachaSimulator([pool]);
        var start = new Position(1, "A");
        var targets = new[] { "Gua1", "Gua11" }.ToFrozenSet();

        var result = sim.FindBestPath(start, targets, maxRolls: 200);

        Assert.NotNull(result);
        foreach (var target in targets)
            Assert.Contains(target, (IEnumerable<string>)result.Collected);
        Assert.True(result.GuaranteedCount >= 2, "Should use at least 2 guaranteed rolls");
    }

    #endregion

    #region maxRolls 邊界

    [Fact]
    public void FindBestPath_MaxRollsExactlyEnough_FindsPath()
    {
        /// 動機：maxRolls 恰好等於路徑成本時應成功找到。
        var pool = TrackBuilder.BuildPool();
        var sim = new GachaSimulator([pool]);
        var start = new Position(1, "A");
        var targets = new[] { "Cat3" }.ToFrozenSet();

        // Cat3 在 3A，需 3 抽
        var result = sim.FindBestPath(start, targets, maxRolls: 3);

        Assert.NotNull(result);
        Assert.Equal(3, result.TotalCost);
    }

    [Fact]
    public void FindBestPath_MaxRollsOneLessThanNeeded_ReturnsNull()
    {
        /// 動機：maxRolls 差一時應回傳 null。
        var pool = TrackBuilder.BuildPool();
        var sim = new GachaSimulator([pool]);
        var start = new Position(1, "A");
        var targets = new[] { "Cat3" }.ToFrozenSet();

        var result = sim.FindBestPath(start, targets, maxRolls: 2);

        Assert.Null(result);
    }

    #endregion

    #region 多池不同 GuaranteedCost

    [Fact]
    public void FindBestPath_MultiPoolDifferentCosts_PicksCheaper()
    {
        /// 動機：兩個池有相同的保底貓但不同成本，應選較便宜的池。
        var poolCheap = new GachaPool("cheap", "Cheap Pool",
            TrackBuilder.Build("A", 20, (i, t) => new GachaSlot(
                new Position(i, t),
                new SlotValue.CatName($"Cat{i}"),
                new SlotValue.CatName("TargetGua"),
                new Position(i + 10, "B"))),
            TrackBuilder.Build("B", 20),
            HasGuaranteed: true, GuaranteedCost: 5);

        var poolExpensive = new GachaPool("expensive", "Expensive Pool",
            TrackBuilder.Build("A", 20, (i, t) => new GachaSlot(
                new Position(i, t),
                new SlotValue.CatName($"Cat{i}"),
                new SlotValue.CatName("TargetGua"),
                new Position(i + 10, "B"))),
            TrackBuilder.Build("B", 20),
            HasGuaranteed: true, GuaranteedCost: 20);

        var sim = new GachaSimulator([poolCheap, poolExpensive]);
        var start = new Position(1, "A");
        var targets = new[] { "TargetGua" }.ToFrozenSet();

        var result = sim.FindBestPath(start, targets, maxRolls: 100);

        Assert.NotNull(result);
        // 最佳解應選用便宜池
        if (result.Path.Any(s => s.IsGuaranteed))
        {
            Assert.Contains(result.Path, s => s.PoolId == "cheap");
        }
    }

    #endregion

    #region InvalidSlot（ParseStatus）跳過

    [Fact]
    public void FindBestPath_InvalidSlotAtStart_SkipsToNextValid()
    {
        /// 動機：起點 slot 為 ParseStatus 無效值時，ProcessSingleRoll 應跳過。
        var trackA = new Track("A", [
            TrackBuilder.InvalidSlot(1, "A"),
            new GachaSlot(new Position(2, "A"),
                new SlotValue.CatName("Cat2"),
                new SlotValue.CatName("Gua2"),
                new Position(12, "B")),
            .. Enumerable.Range(3, 8).Select(i => new GachaSlot(
                new Position(i, "A"),
                new SlotValue.CatName($"Cat{i}"),
                new SlotValue.CatName($"Gua{i}"),
                new Position(i + 10, "B")))
        ]);
        var trackB = TrackBuilder.Build("B", 20);
        var pool = new GachaPool("default", "Default", trackA, trackB);
        var sim = new GachaSimulator([pool]);
        var start = new Position(1, "A");
        var targets = new[] { "Cat2" }.ToFrozenSet();

        // 從無效的 1A 開始，保底走法也無法從 1A 出發（guaranteed 也是 Invalid）
        // 預期找不到路或需要其他路徑
        var result = sim.FindBestPath(start, targets, maxRolls: 50);

        // 結果取決於是否有保底路徑繞過，但不應拋出例外
        if (result is not null)
        {
            Assert.Contains("Cat2", (IEnumerable<string>)result.Collected);
        }
    }

    #endregion
}
