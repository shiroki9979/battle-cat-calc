using BattleCatCalc.Core.Models;
using BattleCatCalc.Infrastructure.Parsing;

namespace BattleCatCalc.Infrastructure.Tests.Parsing;

/// <summary>
/// 第二池（event 1020）快照測試：驗證 BCParser 對不同池的解析結果與 Python 版一致。
/// 資料來源：seed=2127986414, event=2025-12-12_1020, lang=tw, count=100
/// </summary>
public class BCParserSnapshot1020Tests
{
    private const string FixtureFile = "seed_2127986414_event_1020.html";

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
        /// 動機：驗證第二池的軌道長度與第一池一致。
        /// 方法：解析 fixture HTML。
        /// 驗證：Track A/B 各 100 個 slot。
        Assert.Equal(100, TrackA.Count);
        Assert.Equal(100, TrackB.Count);
    }

    [Fact]
    public void TrackA_FirstSlot_ParsedCorrectly()
    {
        /// 動機：與 Python 版 test_track_a_first_slot 對齊。
        /// 方法：取 Track A[0]。
        /// 驗證：id=1A, result=貓咪陰陽師, guaranteed=第9使徒, next_pos=11B。
        var slot = TrackA[0];

        Assert.Equal(new Position(1, "A"), slot.Id);
        Assert.Equal("貓咪陰陽師", slot.Result.AsCatName());
        Assert.Equal("第9使徒", slot.Guaranteed.AsCatName());
        Assert.Equal(new Position(11, "B"), slot.NextPos);
    }

    [Fact]
    public void TrackB_FirstSlot_ParsedCorrectly()
    {
        /// 動機：與 Python 版 test_track_b_first_slot 對齊。
        /// 方法：取 Track B[0]。
        /// 驗證：id=1B, result=貓咪陰陽師, guaranteed=獨眼少女明日香。
        var slot = TrackB[0];

        Assert.Equal(new Position(1, "B"), slot.Id);
        Assert.Equal("貓咪陰陽師", slot.Result.AsCatName());
        Assert.Equal("獨眼少女明日香", slot.Guaranteed.AsCatName());
    }

    [Fact]
    public void TrackA_SequentialIds_1To100()
    {
        /// 動機：ID 必須連續。
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
    public void ExclusiveCats_Pool1020Contains()
    {
        /// 動機：與 Python 版 test_exclusive_cats 對齊，驗證 1020 池獨佔角色存在。
        var allCats = TrackA.Concat(TrackB)
            .Where(s => s.Result.IsValid)
            .Select(s => s.Result.AsCatName()!)
            .Concat(
                TrackA.Concat(TrackB)
                    .Where(s => s.Guaranteed.IsValid)
                    .Select(s => s.Guaranteed.AsCatName()!))
            .ToHashSet();

        // 1020 獨佔角色
        Assert.Contains("第9使徒", allCats);
        Assert.Contains("獨眼少女明日香", allCats);
        Assert.Contains("EVA2號機", allCats);
        Assert.Contains("EVA第13號機", allCats);
    }

    [Fact]
    public void ExclusiveCats_Pool1020DoesNotContain1019Exclusive()
    {
        /// 動機：1020 池不應包含 1019 獨佔角色。
        var allCats = TrackA.Concat(TrackB)
            .Where(s => s.Result.IsValid)
            .Select(s => s.Result.AsCatName()!)
            .Concat(
                TrackA.Concat(TrackB)
                    .Where(s => s.Guaranteed.IsValid)
                    .Select(s => s.Guaranteed.AsCatName()!))
            .ToHashSet();

        // 1019 獨佔角色不應出現
        Assert.DoesNotContain("第10使徒", allCats);
        Assert.DoesNotContain("EVA零號機", allCats);
        Assert.DoesNotContain("白夜姬零", allCats);
    }
}
