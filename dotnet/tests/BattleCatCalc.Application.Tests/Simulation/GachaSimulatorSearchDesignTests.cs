using System.Collections.Frozen;
using BattleCatCalc.Application.Simulation;
using BattleCatCalc.Core.Enums;
using BattleCatCalc.Core.Models;

namespace BattleCatCalc.Application.Tests.Simulation;

/// <summary>
/// 設計審查後的修正：啟發式必須是下界、未標記的連續稀有要拒走、排序穩定、回報整數成本。
/// </summary>
public class GachaSimulatorSearchDesignTests
{
    private static FrozenSet<string> Targets(params string[] names) => names.ToFrozenSet();

    /// <summary>無重複稀有的雙軌：必中 A_n → B_(n+10)、B_n → A_(n+11)（與 fixture 相同）。</summary>
    private static (Track A, Track B) LandingTracks(int count = 40)
    {
        Track Build(string track)
        {
            var other = track == "A" ? "B" : "A";
            var step = track == "A" ? 10 : 11;
            return new Track(track, Enumerable.Range(1, count).Select(i => new GachaSlot(
                new Position(i, track),
                new SlotValue.CatName($"c{i}{track}"),
                new SlotValue.CatName($"g{i}{track}"),
                new Position(i + step, other))));
        }
        return (Build("A"), Build("B"));
    }

    #region 啟發式下界

    [Fact]
    public void PerAdvanceBound_UsesLargestGuaranteedAdvance()
    {
        /// 動機：必中從 B 軌出發前進 11 格、成本 10，每格最低成本必須 ≤ 10/11；寫死 1.0 會高估。
        var (a, b) = LandingTracks();
        Assert.Equal(10.0 / 11, new GachaSimulator(a, b).MinCostPerAdvance, 5);
    }

    [Fact]
    public void PerCatBound_CountsElevenCatsPerGuaranteed()
    {
        /// 動機：一次必中以 GuaranteedCost 拿 11 隻，每隻最低成本是 GuaranteedCost / 11。
        var (a, b) = LandingTracks();
        Assert.Equal(10.0 / 11, new GachaSimulator(a, b).MinCostPerCat, 5);

        var cheap = new GachaSimulator([new GachaPool("p", "p", a, b, true, GuaranteedCost: 3)]);
        Assert.Equal(3.0 / 11, cheap.MinCostPerCat, 5);
    }

    [Fact]
    public void AStar_MatchesExactSearch_OnElevenStepLandings()
    {
        /// 動機：A* 與關閉啟發式的精確搜尋必須得到相同成本（可容許性的回歸測試）。
        ///      從 B 軌起點、目標落在連續必中的落點上，是舊係數高估最多的情境。
        var (a, b) = LandingTracks(70);
        var astar = new GachaSimulator(a, b);
        var exact = new GachaSimulator(a, b) { HeuristicEnabled = false };

        foreach (var start in new[] { 1, 2, 5 })
        foreach (var pos in new[] { 12, 23, 33, 44, 54 })
        foreach (var track in new[] { "A", "B" })
        {
            var targets = Targets($"c{pos}{track}");
            var expected = exact.FindBestPath(new Position(start, "B"), targets, 80);
            var actual = astar.FindBestPath(new Position(start, "B"), targets, 80);
            Assert.Equal(expected?.TotalCost, actual?.TotalCost);
            Assert.Equal(expected?.GuaranteedCount, actual?.GuaranteedCount);
        }
    }

    #endregion

    #region 未標記的連續稀有

    /// <summary>1A Archer、2A Archer（或已標記的替換格）、3A T；3B 也有 T。</summary>
    private static GachaSimulator RepeatAt2A(CatRarity? firstRarity, CatRarity? secondRarity, bool secondMarked = false)
    {
        var second = secondMarked
            ? new GachaSlot(new Position(2, "A"), new SlotValue.CatName("Replaced"), new SlotValue.CatName("G2"),
                new Position(12, "B"), OriginalResult: "Archer", IsDuplicateRare: true,
                SingleNextPos: new Position(3, "B"), ResultRarity: secondRarity)
            : new GachaSlot(new Position(2, "A"), new SlotValue.CatName("Archer"), new SlotValue.CatName("G2"),
                new Position(12, "B"), ResultRarity: secondRarity);

        var trackA = new Track("A",
        [
            new GachaSlot(new Position(1, "A"), new SlotValue.CatName("Archer"), new SlotValue.CatName("G1"),
                new Position(11, "B"), ResultRarity: firstRarity),
            second,
            new GachaSlot(new Position(3, "A"), new SlotValue.CatName("T"), new SlotValue.CatName("G3"), new Position(13, "B")),
        ]);
        var trackB = new Track("B",
        [
            new GachaSlot(new Position(3, "B"), new SlotValue.CatName("T"), new SlotValue.CatName("G3B")),
        ]);
        return new GachaSimulator(trackA, trackB);
    }

    [Fact]
    public void UnmarkedRepeatOfRare_IsRefused()
    {
        /// 動機：表格只標「同軌連續走來」的重複稀有；沒標的格子上一隻就是同一隻稀有貓時，
        ///      遊戲會重抽但表格沒有替換結果，模擬器必須拒走而不是假裝沒事。唯一的路被拒 → 找不到 T。
        var sim = RepeatAt2A(CatRarity.Rare, CatRarity.Rare);

        Assert.Null(sim.FindBestPath(new Position(1, "A"), Targets("T"), 5));
    }

    [Fact]
    public void RepeatOfNonRare_IsAllowed()
    {
        /// 動機：只有稀有才有連續重複判定；超激連續相同照常前進。
        var trackA = new Track("A",
        [
            new GachaSlot(new Position(1, "A"), new SlotValue.CatName("Uber"), new SlotValue.CatName("G"), ResultRarity: CatRarity.UberRare),
            new GachaSlot(new Position(2, "A"), new SlotValue.CatName("Uber"), new SlotValue.CatName("G"), ResultRarity: CatRarity.UberRare),
            new GachaSlot(new Position(3, "A"), new SlotValue.CatName("T"), new SlotValue.CatName("G")),
        ]);
        var sim = new GachaSimulator(trackA, new Track("B", []));

        var result = sim.FindBestPath(new Position(1, "A"), Targets("T"), 5);

        Assert.NotNull(result);
        Assert.Equal(3, result.TotalCost);
    }

    [Fact]
    public void MarkedDuplicate_StillTriggersAndJumps()
    {
        /// 動機：同樣的連續 Archer，表格有標替換與落點時照規則走：拿 Replaced 並落到 3B。
        var sim = RepeatAt2A(CatRarity.Rare, CatRarity.Rare, secondMarked: true);

        var result = sim.FindBestPath(new Position(1, "A"), Targets("T"), 5);

        Assert.NotNull(result);
        Assert.Equal(["Archer", "Replaced", "T"], result.Path.Select(s => s.Unit));
        Assert.True(result.Path[1].IsDuplicateRare);
        Assert.Equal(new Position(3, "B"), result.Path[1].Target);
    }

    [Fact]
    public void WithoutCatalog_ReplacedCatIsStillRemembered()
    {
        /// 動機：沒有稀有度目錄的資料（例如手工測試）退而記住曾被替換掉的貓，重複稀有格仍會觸發。
        var sim = RepeatAt2A(firstRarity: null, secondRarity: null, secondMarked: true);

        var result = sim.FindBestPath(new Position(1, "A"), Targets("T"), 5);

        Assert.NotNull(result);
        Assert.True(result.Path[1].IsDuplicateRare);
    }

    #endregion

    #region 排序穩定與回報成本

    [Fact]
    public void DefaultSearch_PrefersGuaranteedAtEqualMinimumCost()
    {
        var pool = TrackBuilder.BuildPool(guaranteedCost: 1);

        var result = new GachaSimulator([pool]).FindBestPath(new Position(1, "A"), Targets("Cat1"), 1);

        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCost);
        Assert.Equal(1, result.GuaranteedCount);
    }

    [Fact]
    public void PreferGuaranteed_PreservesMoreGuaranteedRollsWhenPathsConverge()
    {
        // Both prefixes reach 21B at cost 2; the cheap pool takes two guaranteed rolls instead of one.
        var expensive = TrackBuilder.BuildPool(poolId: "expensive", trackSize: 40, guaranteedCost: 2,
            slotFactoryA: (id, track) => new GachaSlot(new Position(id, track),
                new SlotValue.CatName($"Cat{id}"), new SlotValue.CatName($"Gua{id}"),
                new Position(id == 1 ? 21 : id + 10, "B")),
            slotFactoryB: (id, track) => new GachaSlot(new Position(id, track),
                new SlotValue.CatName(id == 21 ? "Target" : $"Cat{id}"),
                new SlotValue.CatName($"Gua{id}"), new Position(id + 11, "A")));
        var cheap = TrackBuilder.BuildPool(poolId: "cheap", trackSize: 40, guaranteedCost: 1,
            slotFactoryB: (id, track) => new GachaSlot(new Position(id, track),
                new SlotValue.CatName(id == 21 ? "Target" : $"Cat{id}"),
                new SlotValue.CatName($"Gua{id}"),
                id == 11 ? new Position(21, "B") : new Position(id + 11, "A")));

        var result = new GachaSimulator([expensive, cheap]).FindBestPath(new Position(1, "A"),
            Targets("Target"), 3, beamWidth: null, preferGuaranteed: true);

        Assert.NotNull(result);
        Assert.Equal(3, result.TotalCost);
        Assert.Equal(3, result.GuaranteedCount);
        Assert.Equal(0, result.SingleRollCount);
        Assert.All(result.Path, step => Assert.Equal("cheap", step.PoolId));
    }

    [Fact]
    public void PreferGuaranteed_ChoosesMoreGuaranteedRollsAtEqualCost()
    {
        var pool = TrackBuilder.BuildPool(guaranteedCost: 1);

        var result = new GachaSimulator([pool]).FindBestPath(new Position(1, "A"),
            Targets("Cat1"), 1, beamWidth: null, preferGuaranteed: true);

        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCost);
        Assert.Equal(1, result.GuaranteedCount);
        Assert.Equal(0, result.SingleRollCount);
    }

    [Fact]
    public void PreferGuaranteed_DoesNotIncreaseMinimumCost()
    {
        var pool = TrackBuilder.BuildPool(guaranteedCost: 11, slotFactoryA: (id, track) =>
            new GachaSlot(new Position(id, track),
                new SlotValue.CatName(id == 10 ? "Target" : $"Cat{id}"),
                new SlotValue.CatName(id == 1 ? "Target" : $"Gua{id}"),
                new Position(id + 10, "B")));

        var result = new GachaSimulator([pool]).FindBestPath(new Position(1, "A"),
            Targets("Target"), 20, beamWidth: null, preferGuaranteed: true);

        Assert.NotNull(result);
        Assert.Equal(10, result.TotalCost);
        Assert.Equal(0, result.GuaranteedCount);
        Assert.Equal(10, result.SingleRollCount);
    }

    [Fact]
    public void SameQuery_YieldsIdenticalPath()
    {
        /// 動機：f 相同時以 (-g, StateId) 決定順序，結果必須可重現。
        var (a, b) = LandingTracks(30);
        var targets = Targets("c3A", "c7A", "g12A");

        var runs = Enumerable.Range(0, 3)
            .Select(_ => new GachaSimulator(a, b).FindBestPath(new Position(1, "A"), targets, 60))
            .ToList();

        Assert.All(runs, r => Assert.NotNull(r));
        var first = runs[0]!.Path.Select(s => (s.Source, s.Target, s.Unit, s.PoolId)).ToList();
        foreach (var r in runs.Skip(1))
            Assert.Equal(first, r!.Path.Select(s => (s.Source, s.Target, s.Unit, s.PoolId)).ToList());
    }

    [Fact]
    public void TotalCost_IsActualRollCount_EvenWithPreferGuaranteed()
    {
        // Guaranteed preference preserves actual cost: one single plus one guaranteed costs 11.
        var (a, b) = LandingTracks();
        var slots = a.ToList();
        slots[1] = slots[1] with { Guaranteed = new SlotValue.CatName("Target") };
        var sim = new GachaSimulator(new Track("A", slots), b);

        var result = sim.FindBestPath(new Position(1, "A"), Targets("Target"), 20, beamWidth: null, preferGuaranteed: true);

        Assert.NotNull(result);
        Assert.Equal(11, result.TotalCost);
    }

    #endregion
}
