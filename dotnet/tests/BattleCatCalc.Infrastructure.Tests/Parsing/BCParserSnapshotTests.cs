using BattleCatCalc.Core.Enums;
using BattleCatCalc.Core.Models;
using BattleCatCalc.Infrastructure.Parsing;

namespace BattleCatCalc.Infrastructure.Tests.Parsing;

/// <summary>
/// 快照整合測試：使用真實 HTML fixture 驗證 BCParser 的完整解析結果。
/// 資料來源：seed=2127986414, event=2025-12-12_1019, lang=tw, count=100
/// </summary>
public class BCParserSnapshotTests
{
    private const string FixtureFile = "seed_2127986414_event_1019.html";

    private static readonly Lazy<(Track TrackA, Track TrackB)> Parsed = new(() =>
    {
        var html = FixtureHelper.LoadHtml(FixtureFile);
        return new BCParser().Parse(html);
    });

    private Track TrackA => Parsed.Value.TrackA;
    private Track TrackB => Parsed.Value.TrackB;

    [Fact]
    public void TrackLengths_BothAre100()
    {
        /// 動機：count=100 的 fixture 應各解析出 100 格。
        /// 方法：解析 fixture HTML。
        /// 驗證：Track A/B 各 100 個 slot。
        Assert.Equal(100, TrackA.Count);
        Assert.Equal(100, TrackB.Count);
    }

    [Fact]
    public void TrackA_FirstSlot_ParsedCorrectly()
    {
        /// 動機：驗證第一格的解析結果與 Python 版快照一致。
        /// 方法：取 Track A[0]。
        /// 驗證：id=1A, result=貓咪陰陽師, guaranteed=第10使徒, next_pos=11B。
        var slot = TrackA[0];

        Assert.Equal(new Position(1, "A"), slot.Id);
        Assert.Equal("貓咪陰陽師", slot.Result.AsCatName());
        Assert.Equal("第10使徒", slot.Guaranteed.AsCatName());
        Assert.Equal(new Position(11, "B"), slot.NextPos);
        Assert.False(slot.IsDuplicateRare);
        Assert.Null(slot.OriginalResult);
    }

    [Fact]
    public void TrackB_FirstSlot_ParsedCorrectly()
    {
        /// 動機：與 Python 版 test_track_b_first_slot 對齊。
        /// 方法：取 Track B[0]。
        /// 驗證：id=1B, result=貓咪陰陽師, guaranteed=白夜姬零, next_pos=12B。
        var slot = TrackB[0];

        Assert.Equal(new Position(1, "B"), slot.Id);
        Assert.Equal("貓咪陰陽師", slot.Result.AsCatName());
        Assert.Equal("白夜姬零", slot.Guaranteed.AsCatName());
        Assert.Equal(new Position(12, "B"), slot.NextPos);
    }

    [Fact]
    public void TrackA_SequentialIds_1To100()
    {
        /// 動機：ID 必須連續，否則模擬器會跳步。
        /// 方法：取出所有 ID。
        /// 驗證：為 1~100 連續整數。
        var ids = TrackA.Select(s => s.Id.Id).ToList();
        Assert.Equal(Enumerable.Range(1, 100).ToList(), ids);
    }

    [Fact]
    public void TrackB_SequentialIds_1To100()
    {
        var ids = TrackB.Select(s => s.Id.Id).ToList();
        Assert.Equal(Enumerable.Range(1, 100).ToList(), ids);
    }

    // 以下重複稀有的期望值取自 HTML 原文（<tr> 以 0 起算；欄位以 rowspan 還原後的網格為準）：
    //   第 19 列  B9 的貓                  「拳擊手貓」
    //   第 21 列  B10 替換前               「拳擊手貓」 必中「<- 21A 白夜姬零」
    //   第 22 列  B10 替換後               「<- 12A 侏羅貓」 必中「EVA初號機 -> 21B」
    //   第 21~23 列 A11（3 列只是配合 B10 的版面拉伸）結果欄只有第 23 列的「明日香貓」
    //   第 201~202 列 A100 原始「侏羅貓」、替換「<?> 薰貓」（A99 也是侏羅貓）

    [Fact]
    public void TrackB_Slot10_DuplicateRare()
    {
        /// 動機：B10 是同軌連續重複：B9、B10 的原始貓都是拳擊手貓，被替換成侏羅貓。
        /// 方法：取 TrackB.GetSlot(9) 與 GetSlot(10)。
        /// 驗證：B9 結果為拳擊手貓；B10 為重複稀有格，original=拳擊手貓、result=侏羅貓。
        Assert.Equal("拳擊手貓", TrackB.GetSlot(9)!.Result.AsCatName());

        var slot = TrackB.GetSlot(10);

        Assert.NotNull(slot);
        Assert.True(slot!.IsDuplicateRare);
        Assert.Equal("拳擊手貓", slot.OriginalResult);
        Assert.Equal("侏羅貓", slot.Result.AsCatName());
    }

    [Fact]
    public void TrackB_Slot10_SingleAndGuaranteedArrowsAreKeptApart()
    {
        /// 動機：B10 的單抽落點（結果欄 <- 12A）與必中落點（必中欄）是不同的箭頭，不可混用；
        ///      舊版後者覆蓋前者，單抽觸發重複時會跳到十格之外。
        /// 驗證：SingleNextPos=12A；NextPos=21B（替換後）；OriginalNextPos=21A（替換前）。
        var slot = TrackB.GetSlot(10)!;

        Assert.Equal(new Position(12, "A"), slot.SingleNextPos);
        Assert.Equal(new Position(21, "B"), slot.NextPos);
        Assert.Equal(new Position(21, "A"), slot.OriginalNextPos);
        Assert.Equal("白夜姬零", slot.OriginalGuaranteed);
        Assert.Equal("EVA初號機", slot.Guaranteed.AsCatName());
    }

    [Fact]
    public void TrackA_Slot11_IsNotDuplicateRare()
    {
        /// 動機：A11 佔 3 列只是版面拉伸，結果欄只有「明日香貓」，不是重複稀有格；
        ///      第 22 列的「侏羅貓」是 B10 的替換結果，舊版 parser 誤把它當成 A11 的原始貓。
        /// 驗證：非重複稀有、無 original、result=明日香貓、next=21B、無單抽落點。
        var slot = TrackA.GetSlot(11);

        Assert.NotNull(slot);
        Assert.False(slot!.IsDuplicateRare);
        Assert.Null(slot.OriginalResult);
        Assert.Equal("明日香貓", slot.Result.AsCatName());
        Assert.Equal("EVA初號機", slot.Guaranteed.AsCatName());
        Assert.Equal(new Position(21, "B"), slot.NextPos);
        Assert.Null(slot.SingleNextPos);
    }

    [Fact]
    public void TrackA_Slot100_EndOfTableDuplicate_HasNoLanding()
    {
        /// 動機：A99、A100 的原始貓都是侏羅貓，A100 替換成薰貓；HTML 標 <?>，落點超出表格範圍。
        /// 驗證：A100 為重複稀有格，且沒有單抽落點（不可猜）。
        Assert.Equal("侏羅貓", TrackA.GetSlot(99)!.Result.AsCatName());

        var slot = TrackA.GetSlot(100)!;

        Assert.True(slot.IsDuplicateRare);
        Assert.Equal("侏羅貓", slot.OriginalResult);
        Assert.Equal("薰貓", slot.Result.AsCatName());
        Assert.Null(slot.SingleNextPos);
    }

    [Fact]
    public void TrackB_Slot100_IsNotDuplicateRare()
    {
        /// 動機：B100 的結果欄只有「魔劍士貓」；「薰貓」是 A100 的替換結果，舊版 parser 誤讀進來。
        var slot = TrackB.GetSlot(100)!;

        Assert.False(slot.IsDuplicateRare);
        Assert.Null(slot.OriginalResult);
        Assert.Equal("魔劍士貓", slot.Result.AsCatName());
    }

    [Fact]
    public void DuplicateRareSlots_OnlyB10AndA100()
    {
        /// 動機：HTML 中佔 3 列的格子有 A11、B10、A100、B100 四個，其中 A11、B100 只是
        ///      配合相鄰重複稀有格的版面拉伸，不是重複稀有格。
        var flagged = TrackA.Concat(TrackB)
            .Where(s => s.IsDuplicateRare)
            .Select(s => s.Id)
            .ToHashSet();

        Assert.Equal(new HashSet<Position> { new(10, "B"), new(100, "A") }, flagged);
    }

    [Fact]
    public void TrackA_FirstSlot_RarityIsRare()
    {
        /// 動機：貓咪陰陽師是 rare 貓，驗證稀有度解析。
        /// 方法：取 Track A[0]。
        /// 驗證：Rarity=Rare。
        var slot = TrackA[0];
        Assert.Equal(CatRarity.Rare, slot.Rarity);
    }

    [Fact]
    public void TrackA_UberSlot_RarityIsUber()
    {
        /// 動機：驗證含 uber 稀有度的格子。
        /// 方法：找出第一個 Rarity=UberRare 的格子。
        /// 驗證：至少存在一個 Uber 格。
        var uberSlot = TrackA.FirstOrDefault(s => s.Rarity == CatRarity.UberRare);
        Assert.NotNull(uberSlot);
    }

    [Fact]
    public void TrackA_FirstSlot_GuaranteedRarityParsed()
    {
        /// 動機：保底貓（第10使徒）的稀有度應為 uber。
        /// 方法：取 Track A[0]。
        /// 驗證：find_select 的 Uber 分類應優先於保底格的 rare 配色。
        var slot = TrackA[0];
        Assert.Equal(CatRarity.UberRare, slot.GuaranteedRarity);
    }

    [Theory]
    [InlineData(CatRarity.Rare)]
    [InlineData(CatRarity.SuperRare)]
    [InlineData(CatRarity.UberRare)]
    public void TrackA_ContainsExpectedRarities(CatRarity rarity)
    {
        /// 動機：event 1019 fixture 應包含 rare、supa、uber。
        /// 方法：檢查所有格子的 Rarity。
        /// 驗證：至少有一格對應到該稀有度。
        Assert.Contains(TrackA, s => s.Rarity == rarity);
    }
}
