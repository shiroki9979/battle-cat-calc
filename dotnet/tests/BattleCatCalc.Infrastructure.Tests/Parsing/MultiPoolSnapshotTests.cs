using System.Collections.Frozen;
using BattleCatCalc.Application.Simulation;
using BattleCatCalc.Core.Models;
using BattleCatCalc.Infrastructure.Parsing;

namespace BattleCatCalc.Infrastructure.Tests.Parsing;

/// <summary>
/// 多池模擬器快照測試：使用真實 HTML fixture 建構雙池模擬器，
/// 驗證跨池搜尋結果與 Python 版 TestMultiPoolSnapshot 完全一致。
/// 資料來源：seed=2127986414, event 1019（EVA合作轉蛋）+ event 1020（EVA合作轉蛋2nd）
/// </summary>
public class MultiPoolSnapshotTests
{
    private static readonly Lazy<GachaSimulator> Simulator = new(() =>
    {
        var parser = new BCParser();

        var html1019 = FixtureHelper.LoadHtml("seed_2127986414_event_1019.html");
        var (trackA1019, trackB1019) = parser.Parse(html1019);

        var html1020 = FixtureHelper.LoadHtml("seed_2127986414_event_1020.html");
        var (trackA1020, trackB1020) = parser.Parse(html1020);

        var pool1019 = new GachaPool(
            PoolId: "1019", Name: "EVA合作轉蛋",
            TrackA: trackA1019, TrackB: trackB1019,
            HasGuaranteed: true, GuaranteedCost: 10);

        var pool1020 = new GachaPool(
            PoolId: "1020", Name: "EVA合作轉蛋2nd",
            TrackA: trackA1020, TrackB: trackB1020,
            HasGuaranteed: true, GuaranteedCost: 10);

        return new GachaSimulator([pool1019, pool1020]);
    });

    private GachaSimulator Sim => Simulator.Value;

    #region 跨池雙目標

    [Fact]
    public void CrossPool_TwoTargets_Cost25()
    {
        /// 動機：「第10使徒」(1019獨佔) + 「第9使徒」(1020獨佔) 需跨池保底，cost=25。
        /// 方法：以 1A 起始搜尋兩個跨池目標。
        /// 驗證：cost=25，兩目標皆收集，使用兩個池。
        var targets = new[] { "第10使徒", "第9使徒" }.ToFrozenSet();

        var result = Sim.FindBestPath(new Position(1, "A"), targets, 50);

        Assert.NotNull(result);
        Assert.Equal(25, result.TotalCost);
        Assert.True(targets.IsSubsetOf(result.Collected));
        Assert.Equal(new[] { "1019", "1020" }.ToFrozenSet(), result.PoolsUsed);
    }

    #endregion

    #region 跨池路徑細節

    [Fact]
    public void CrossPool_PathContainsBothPools()
    {
        /// 動機：跨池路徑應包含來自不同池的步驟。
        /// 方法：搜尋跨池目標，檢查路徑中的 PoolId。
        /// 驗證：路徑步驟涵蓋 1019 與 1020 兩個池。
        var targets = new[] { "第10使徒", "第9使徒" }.ToFrozenSet();

        var result = Sim.FindBestPath(new Position(1, "A"), targets, 50);

        Assert.NotNull(result);
        var poolIdsInPath = result.Path.Select(s => s.PoolId).ToHashSet();
        Assert.Contains("1019", poolIdsInPath);
        Assert.Contains("1020", poolIdsInPath);
    }

    #endregion

    #region 單池單目標

    [Fact]
    public void SingleTarget_1019Only_Cost10()
    {
        /// 動機：「第10使徒」為 1019 獨佔超激，一次十連即可取得，cost=10。
        /// 方法：多池模式下搜尋單一 1019 獨佔目標。
        /// 驗證：cost=10, guaranteed_count=1, 使用 1019 池。
        var targets = new[] { "第10使徒" }.ToFrozenSet();

        var result = Sim.FindBestPath(new Position(1, "A"), targets, 20);

        Assert.NotNull(result);
        Assert.Equal(10, result.TotalCost);
        Assert.Equal(1, result.GuaranteedCount);
        Assert.Contains("1019", (IEnumerable<string>)result.PoolsUsed);
    }

    [Fact]
    public void SingleTarget_1020Guaranteed_Cost11()
    {
        /// 動機：「獨眼少女明日香」在 1020 的 A2 保底可得，cost=11（1 單抽 + 1 十連）。
        /// 方法：搜尋 1020 獨佔保底目標。
        /// 驗證：cost=11。
        var targets = new[] { "獨眼少女明日香" }.ToFrozenSet();

        var result = Sim.FindBestPath(new Position(1, "A"), targets, 20);

        Assert.NotNull(result);
        Assert.Equal(11, result.TotalCost);
    }

    #endregion

    #region 四目標跨池

    [Fact]
    public void FourTargets_CrossPool_Cost46()
    {
        /// 動機：4 目標跨池（各 2 隻獨佔），cost=46。
        /// 方法：搜尋兩池各兩隻獨佔角色。
        /// 驗證：cost=46，所有目標皆收集，使用兩個池。
        var targets = new[] { "第10使徒", "EVA零號機", "第9使徒", "獨眼少女明日香" }.ToFrozenSet();

        var result = Sim.FindBestPath(new Position(1, "A"), targets, 60);

        Assert.NotNull(result);
        Assert.Equal(46, result.TotalCost);
        Assert.True(targets.IsSubsetOf(result.Collected));
        Assert.Equal(new[] { "1019", "1020" }.ToFrozenSet(), result.PoolsUsed);
    }

    #endregion

    #region B 軌起始

    [Fact]
    public void StartFromTrackB_Cost20()
    {
        /// 動機：從 B 軌起始搜尋「第9使徒」（1020獨佔），驗證起點軌道不影響搜尋正確性。
        /// 方法：以 1B 起始搜尋。
        /// 驗證：cost=20。最佳路徑：單抽 B1~B10（B9 抽到拳擊手貓，B10 觸發重複稀有，落在 12A，
        ///      1 抽前進 2 格），再於 1020 的 12A 做必中十連。1020 的 12A 必中欄為「第9使徒 -> 22B」。
        ///      舊版漏掉重複稀有的換軌，只能走兩次必中而得到 24。
        var targets = new[] { "第9使徒" }.ToFrozenSet();

        var result = Sim.FindBestPath(new Position(1, "B"), targets, 30);

        Assert.NotNull(result);
        Assert.Equal(20, result.TotalCost);

        var last = result.Path[^1];
        Assert.True(last.IsGuaranteed);
        Assert.Equal(("1020", new Position(12, "A"), new Position(22, "B")),
            (last.PoolId, last.Source, last.Target));
        Assert.Contains(result.Path.Take(result.Path.Count - 1),
            s => s.IsDuplicateRare && s.Target == new Position(12, "A"));
    }

    #endregion

    #region Beam Search 多池

    [Fact]
    public void BeamSearch_MultiPool_FindsResult()
    {
        /// 動機：Beam Search 在多池模式下應仍能找到合理結果（可能非最優）。
        /// 方法：以 beamWidth=100 搜尋跨池目標。
        /// 驗證：找到結果，cost <= 30（A* 最優為 25，Beam Search 容許略高）。
        var targets = new[] { "第10使徒", "第9使徒" }.ToFrozenSet();

        var result = Sim.FindBestPath(
            new Position(1, "A"), targets, 50,
            beamWidth: 100, preferGuaranteed: false);

        Assert.NotNull(result);
        Assert.True(targets.IsSubsetOf(result.Collected));
        Assert.True(result.TotalCost <= 30,
            $"Beam Search cost {result.TotalCost} exceeds tolerance 30 (A* optimal=25)");
    }

    #endregion
}
