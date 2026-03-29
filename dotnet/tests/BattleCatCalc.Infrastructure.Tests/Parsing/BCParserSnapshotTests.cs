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

    [Fact]
    public void TrackA_Slot11_DuplicateRare()
    {
        /// 動機：A11 在 Python 版為重複稀有格，驗證跨語言一致。
        /// 方法：取 TrackA.GetSlot(11)。
        /// 驗證：is_duplicate_rare=true, original=侏羅貓, result=明日香貓, next=21B。
        var slot = TrackA.GetSlot(11);

        Assert.NotNull(slot);
        Assert.True(slot!.IsDuplicateRare);
        Assert.Equal("侏羅貓", slot.OriginalResult);
        Assert.Equal("明日香貓", slot.Result.AsCatName());
        Assert.Equal(new Position(21, "B"), slot.NextPos);
    }

    [Fact]
    public void TrackB_Slot10_DuplicateRare()
    {
        /// 動機：B10 在 Python 版亦為重複稀有格。
        /// 方法：取 TrackB.GetSlot(10)。
        /// 驗證：is_duplicate_rare=true。
        var slot = TrackB.GetSlot(10);

        Assert.NotNull(slot);
        Assert.True(slot!.IsDuplicateRare);
    }
}
