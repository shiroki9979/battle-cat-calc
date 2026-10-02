using System.Collections.Frozen;
using BattleCatCalc.Application.Simulation;
using BattleCatCalc.Core.Models;

namespace BattleCatCalc.Application.Tests.Simulation;

/// <summary>
/// 重複稀有的規則：觸發取決於「上一隻貓」，落點用單抽落點，必中十連的前 10 抽跟著換軌。
/// </summary>
public class GachaSimulatorDuplicateRareTests
{
    private static FrozenSet<string> Targets(params string[] names) => names.ToFrozenSet();

    private static GachaSlot Plain(int i, string track, string prefix) => new(
        new Position(i, track),
        new SlotValue.CatName($"{prefix}{i}"),
        new SlotValue.CatName($"Gua{prefix}{i}"),
        new Position(i + 10, track == "A" ? "B" : "A"));

    private static List<GachaSlot> PlainTrack(string track, int count = 14) =>
        Enumerable.Range(1, count).Select(i => Plain(i, track, track)).ToList();

    // 必中成本設得比單抽便宜，讓搜尋偏好必中，才測得到必中分支
    private static GachaSimulator Sim(List<GachaSlot> a, List<GachaSlot> b, int guaranteedCost = 3) =>
        new([new GachaPool("p", "p", new Track("A", a), new Track("B", b), true, guaranteedCost)]);

    /// <summary>1A Cat1、2A Archer、3A 重複稀有（原始 Archer → ArcherReplaced，單抽落在 4B）。</summary>
    private static GachaSimulator SimWithDuplicateAt3A(Position? landing = null)
    {
        var trackA = new Track("A",
        [
            new GachaSlot(new Position(1, "A"), new SlotValue.CatName("Cat1"), new SlotValue.CatName("Gua1"), new Position(11, "B")),
            new GachaSlot(new Position(2, "A"), new SlotValue.CatName("Archer"), new SlotValue.CatName("Gua2"), new Position(12, "B")),
            new GachaSlot(new Position(3, "A"), new SlotValue.CatName("ArcherReplaced"), new SlotValue.CatName("Gua3"),
                new Position(13, "B"), OriginalResult: "Archer", IsDuplicateRare: true, SingleNextPos: landing),
            new GachaSlot(new Position(4, "A"), new SlotValue.CatName("Cat4"), new SlotValue.CatName("Gua4"), new Position(14, "B")),
        ]);
        var trackB = new Track("B",
        [
            new GachaSlot(new Position(4, "B"), new SlotValue.CatName("TargetCatB4"), new SlotValue.CatName("GuaB4")),
            new GachaSlot(new Position(5, "B"), new SlotValue.CatName("CatB5"), new SlotValue.CatName("GuaB5")),
        ]);
        return new GachaSimulator(trackA, trackB);
    }

    #region 單抽

    [Fact]
    public void SingleRoll_PreviousCatMatches_TriggersDuplicateAndFollowsLanding()
    {
        /// 動機：2A 抽到 Archer（普通格，不是重複稀有格），3A 的原始貓也是 Archer → 觸發重複，抽完跳到 4B。
        ///      舊版只在被標記的重複稀有格更新「上一隻」，連續重複時永遠不會觸發。
        var sim = SimWithDuplicateAt3A(new Position(4, "B"));

        var result = sim.FindBestPath(new Position(1, "A"), Targets("TargetCatB4"), 5);

        Assert.NotNull(result);
        Assert.Equal(4, result.TotalCost);
        Assert.Equal(["Cat1", "Archer", "ArcherReplaced", "TargetCatB4"], result.Path.Select(s => s.Unit));

        var dup = result.Path[2];
        Assert.True(dup.IsDuplicateRare);
        Assert.True(dup.IsTrackSwitch);
        Assert.Equal(new Position(4, "B"), dup.Target);
    }

    [Fact]
    public void SingleRoll_PreviousCatDiffers_GivesOriginalCat()
    {
        /// 動機：上一隻不是同一隻貓時不觸發重複：拿到替換前的貓，同軌前進一格。
        var sim = SimWithDuplicateAt3A(new Position(4, "B"));

        var result = sim.FindBestPath(new Position(3, "A"), Targets("Archer"), 1);

        Assert.NotNull(result);
        var step = Assert.Single(result.Path);
        Assert.Equal(("Archer", new Position(4, "A"), false), (step.Unit, step.Target, step.IsDuplicateRare));

        Assert.Null(sim.FindBestPath(new Position(3, "A"), Targets("ArcherReplaced"), 1));
    }

    [Fact]
    public void SingleRoll_TriggeredWithoutLanding_IsNotGuessed()
    {
        /// 動機：觸發重複卻沒有單抽落點（例如 HTML 標 <?>）時，不猜落點，該路徑視為不可走。
        var sim = SimWithDuplicateAt3A(landing: null);

        Assert.Null(sim.FindBestPath(new Position(1, "A"), Targets("TargetCatB4"), 5));
    }

    #endregion

    #region 必中十連

    [Fact]
    public void Guaranteed_WindowFollowsTrackSwitch()
    {
        /// 動機：1A 起：1A、2A(Archer)、3A 重複 → 5B，之後走 B 軌，第 8 隻是 8B 而不是 8A。
        ///      舊版固定取同軌的 pos+0..pos+9，會誤收 8A。
        var a = PlainTrack("A");
        var b = PlainTrack("B");
        a[1] = a[1] with { Result = new SlotValue.CatName("Archer") };
        a[2] = a[2] with
        {
            Result = new SlotValue.CatName("ArcherReplaced"),
            OriginalResult = "Archer",
            IsDuplicateRare = true,
            SingleNextPos = new Position(5, "B")
        };
        b[7] = b[7] with { Result = new SlotValue.CatName("OnPath") };   // 8B：實際走到
        a[7] = a[7] with { Result = new SlotValue.CatName("OffPath") };  // 8A：固定取 pos+i 會誤收
        var sim = Sim(a, b);

        var result = sim.FindBestPath(new Position(1, "A"), Targets("OnPath"), 10);

        Assert.NotNull(result);
        Assert.Equal(3, result.TotalCost);
        Assert.True(Assert.Single(result.Path).IsGuaranteed);

        // 走過 3A 的重複之後就回不到 A 軌的 8A，必中也收不到它
        Assert.Null(sim.FindBestPath(new Position(1, "A"), Targets("OffPath"), 20));
    }

    private static (List<GachaSlot> A, List<GachaSlot> B) DuplicateVariantTracks()
    {
        var a = PlainTrack("A");
        var b = PlainTrack("B");
        a[0] = a[0] with { Result = new SlotValue.CatName("Orig") };
        a[1] = a[1] with
        {
            Result = new SlotValue.CatName("Repl"),
            Guaranteed = new SlotValue.CatName("GuaRepl"),
            NextPos = new Position(12, "B"),
            OriginalResult = "Orig",
            OriginalGuaranteed = "GuaOrig",
            OriginalNextPos = new Position(12, "A"),
            IsDuplicateRare = true,
            SingleNextPos = new Position(4, "B")
        };
        return (a, b);
    }

    [Fact]
    public void Guaranteed_StartSlotDuplicateNotTriggered_UsesOriginalVariant()
    {
        /// 動機：起始格是重複稀有格但沒被觸發：必中用替換前的超激與落點。
        var (a, b) = DuplicateVariantTracks();

        var result = Sim(a, b).FindBestPath(new Position(2, "A"), Targets("GuaOrig"), 5);

        Assert.NotNull(result);
        var step = Assert.Single(result.Path);
        Assert.Equal(("GuaOrig", new Position(12, "A"), false), (step.Unit, step.Target, step.IsTrackSwitch));
    }

    [Fact]
    public void Guaranteed_StartSlotDuplicateTriggered_UsesReplacedVariant()
    {
        /// 動機：先單抽 1A（Orig）再在 2A 做必中：2A 觸發重複，用替換後的超激與落點。
        var (a, b) = DuplicateVariantTracks();

        var result = Sim(a, b).FindBestPath(new Position(1, "A"), Targets("GuaRepl"), 5);

        Assert.NotNull(result);
        Assert.Equal(4, result.TotalCost);
        Assert.Equal("Orig", result.Path[0].Unit);
        var guaranteed = result.Path[1];
        Assert.Equal(("GuaRepl", new Position(12, "B"), true), (guaranteed.Unit, guaranteed.Target, guaranteed.IsTrackSwitch));
    }

    #endregion

    #region 啟發式

    [Fact]
    public void Heuristic_AccountsForDuplicateAdvance_FindsOptimalCost()
    {
        /// 動機：重複稀有讓單抽一次前進多格，啟發式的每格最低成本必須納入，否則會高估剩餘成本，
        ///      A* 先展開別條路而回傳非最佳解。
        /// 設計：1A Archer、2A 重複稀有（落在 11A，前進 9 格）、12A 是目標 T。
        ///      最佳路徑 1A → 2A → 11A → 12A，共 4 抽；另有 1A 必中直接得到 T，成本 5。
        var a = PlainTrack("A", 20);
        var b = PlainTrack("B", 20);
        a[0] = a[0] with { Result = new SlotValue.CatName("Archer"), Guaranteed = new SlotValue.CatName("T") };
        a[1] = a[1] with
        {
            Result = new SlotValue.CatName("Replaced"),
            OriginalResult = "Archer",
            IsDuplicateRare = true,
            SingleNextPos = new Position(11, "A")
        };
        a[11] = a[11] with { Result = new SlotValue.CatName("T") };
        var sim = Sim(a, b, guaranteedCost: 5);

        var result = sim.FindBestPath(new Position(1, "A"), Targets("T"), 20);

        Assert.NotNull(result);
        Assert.Equal(4, result.TotalCost);
    }

    #endregion
}
