using System.Collections.Frozen;
using BattleCatCalc.Application.Simulation;
using BattleCatCalc.Core.Models;
using BattleCatCalc.Infrastructure.Parsing;

namespace BattleCatCalc.Infrastructure.Tests.Parsing;

/// <summary>
/// 重複稀有的快照測試：以真實 HTML fixture 驗證模擬器的單抽轉換規則。
/// 資料來源：seed=2127986414, event 1019 / 1020。
/// B9、B10 的原始貓都是拳擊手貓；HTML 標示 B10 觸發重複後單抽落在 12A。
/// </summary>
public class DuplicateRareSnapshotTests
{
    private const string Fixture1019 = "seed_2127986414_event_1019.html";

    private static (Track TrackA, Track TrackB) Load(string fixture) =>
        new BCParser().Parse(FixtureHelper.LoadHtml(fixture));

    private static readonly Lazy<GachaSimulator> Sim1019 = new(() =>
    {
        var (trackA, trackB) = Load(Fixture1019);
        return new GachaSimulator(trackA, trackB);
    });

    private static GachaSimulator Sim => Sim1019.Value;

    #region 模擬器

    [Fact]
    public void SingleRoll_DuplicateTriggered_FollowsSiteArrow()
    {
        /// 動機：從 B9 連抽，B10 觸發重複稀有，拿到侏羅貓並照網站箭頭落在 12A（換軌且前進 2 格）。
        ///      舊版沒有記住 B9 的貓，不會觸發重複，走到 11B。
        var targets = new[] { "侏羅貓" }.ToFrozenSet();

        var result = Sim.FindBestPath(new Position(9, "B"), targets, 5);

        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCost);

        var first = result.Path[0];
        Assert.Equal((new Position(9, "B"), new Position(10, "B"), "拳擊手貓"),
            (first.Source, first.Target, first.Unit));
        Assert.False(first.IsDuplicateRare);

        var second = result.Path[1];
        Assert.Equal((new Position(10, "B"), new Position(12, "A"), "侏羅貓"),
            (second.Source, second.Target, second.Unit));
        Assert.True(second.IsDuplicateRare);
        Assert.True(second.IsTrackSwitch);
    }

    [Fact]
    public void SingleRoll_DuplicateNotTriggered_GivesOriginalCat()
    {
        /// 動機：直接站在 B10（上一隻不是拳擊手貓）不會觸發重複：拿到替換前的拳擊手貓，同軌前進 1 格。
        var result = Sim.FindBestPath(new Position(10, "B"), new[] { "拳擊手貓" }.ToFrozenSet(), 2);

        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCost);
        var step = Assert.Single(result.Path);
        Assert.Equal((new Position(11, "B"), "拳擊手貓", false),
            (step.Target, step.Unit, step.IsDuplicateRare));

        // 替換後的侏羅貓在沒觸發時拿不到
        Assert.Null(Sim.FindBestPath(new Position(10, "B"), new[] { "侏羅貓" }.ToFrozenSet(), 1));
    }

    [Fact]
    public void Guaranteed_WindowWithDuplicate_StaysOnTrack()
    {
        /// 動機：B1 的必中十連包含 B10 的重複稀有（奇數次），落在 12B 而不是換軌。
        /// HTML：1B 必中欄「白夜姬零 -> 12B」。
        var result = Sim.FindBestPath(new Position(1, "B"), new[] { "白夜姬零" }.ToFrozenSet(), 10);

        Assert.NotNull(result);
        var step = Assert.Single(result.Path);
        Assert.True(step.IsGuaranteed);
        Assert.Equal(new Position(12, "B"), step.Target);
        Assert.False(step.IsTrackSwitch);
    }

    #endregion

    #region 交叉驗證

    /// <summary>
    /// 兩種彼此獨立的 HTML 箭頭必須互相一致，用來檢驗重複稀有的單抽落點：
    /// - 照單抽規則（含結果欄的重複稀有落點）走 10 抽，再讓必中超激消耗 1 個亂數：
    ///   A_n 的亂數編號是奇數、B_n 是偶數，所以 A_n → B_n，B_n → A_(n+1)。
    /// - 與表格「必中欄」的箭頭比對（例如 1A → 11B、1B → 12B、12A → 22B）。
    /// 前者靠結果欄的箭頭（B10 → 12A），後者靠必中欄的箭頭，兩者互不引用。
    /// 若重複稀有的落點判斷錯誤，所有視窗包含 B10 的起點（B1~B10）都會對不上。
    /// </summary>
    [Theory]
    [InlineData("seed_2127986414_event_1019.html")]
    [InlineData("seed_2127986414_event_1020.html")]
    public void TenRollWalk_LandsWhereGuaranteedArrowSays(string fixture)
    {
        var (trackA, trackB) = Load(fixture);
        Track TrackOf(string track) => track == "A" ? trackA : trackB;

        var checkedCount = 0;
        var mismatches = new List<string>();

        foreach (var slot in trackA.Concat(trackB))
        {
            if (slot.NextPos is not Position tableLanding) continue; // 落點超出表格範圍

            // 表格的必中欄假設起始格自己的重複稀有被觸發
            var last = slot.IsDuplicateRare ? slot.OriginalResult : null;
            var pos = slot.Id;
            var complete = true;

            for (var i = 0; i < 10; i++)
            {
                var current = TrackOf(pos.Track).GetSlot(pos.Id);
                if (current is null || Walk(current, last) is not var (unit, next))
                {
                    complete = false;
                    break;
                }
                last = unit;
                pos = next;
            }
            if (!complete) continue;

            var expected = pos.Track == "A" ? new Position(pos.Id, "B") : new Position(pos.Id + 1, "A");
            checkedCount++;
            if (expected != tableLanding)
                mismatches.Add($"{slot.Id}: walk ends at {pos}, expected {expected}, table says {tableLanding}");
        }

        Assert.Empty(mismatches);
        Assert.True(checkedCount >= 150, $"Only {checkedCount} windows were checked");
    }

    /// <summary>單抽一格的規則（測試用的獨立寫法，不引用模擬器內部）。</summary>
    private static (string Unit, Position Next)? Walk(GachaSlot slot, string? last)
    {
        var result = slot.Result.AsCatName();
        if (result is null) return null;

        var plain = new Position(slot.Id.Id + 1, slot.Id.Track);
        if (!slot.IsDuplicateRare || slot.OriginalResult is not string original) return (result, plain);
        if (last != original) return (original, plain);
        return slot.SingleNextPos is Position landing ? (result, landing) : null;
    }

    #endregion
}
