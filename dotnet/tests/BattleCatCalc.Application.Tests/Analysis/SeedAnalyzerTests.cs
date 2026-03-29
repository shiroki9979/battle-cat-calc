using BattleCatCalc.Application.Analysis;
using BattleCatCalc.Core.Enums;
using BattleCatCalc.Core.Models;

namespace BattleCatCalc.Application.Tests.Analysis;

public class SeedAnalyzerTests
{
    private static Track BuildTrack()
    {
        var slots = new GachaSlot[]
        {
            new(new Position(1, "A"), new SlotValue.CatName("火焰貓"), new SlotValue.CatName("超激火焰貓")),
            new(new Position(2, "A"), new SlotValue.CatName("冰凍貓"), new SlotValue.CatName("超激冰凍貓")),
            new(new Position(3, "A"), new SlotValue.Status(ParseStatus.InvalidData), new SlotValue.CatName("超激雷電貓")),
            new(new Position(4, "A"), new SlotValue.CatName("暗黑貓"), new SlotValue.Status(ParseStatus.Ambiguous)),
            new(new Position(5, "A"), new SlotValue.CatName("火焰貓"), new SlotValue.CatName("超激火焰貓")),
        };
        return new Track("A", slots);
    }

    #region FindCat

    [Fact]
    public void FindCat_ExactMatch_ReturnsSlots()
    {
        var analyzer = new SeedAnalyzer(BuildTrack());

        var results = analyzer.FindCat("冰凍貓").ToList();

        Assert.Single(results);
        Assert.Equal(new Position(2, "A"), results[0].Id);
    }

    [Fact]
    public void FindCat_PartialMatch_ReturnsAll()
    {
        var analyzer = new SeedAnalyzer(BuildTrack());

        // "火焰貓" 出現在 1A 和 5A
        var results = analyzer.FindCat("火焰貓").ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[0].Id.Id);
        Assert.Equal(5, results[1].Id.Id);
    }

    [Fact]
    public void FindCat_SubstringMatch_ReturnsSlots()
    {
        var analyzer = new SeedAnalyzer(BuildTrack());

        // "火焰" 也能匹配 "火焰貓"
        var results = analyzer.FindCat("火焰").ToList();

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void FindCat_NoMatch_ReturnsEmpty()
    {
        var analyzer = new SeedAnalyzer(BuildTrack());

        var results = analyzer.FindCat("不存在的貓").ToList();

        Assert.Empty(results);
    }

    [Fact]
    public void FindCat_SkipsParseStatus_ReturnsOnlyValid()
    {
        var analyzer = new SeedAnalyzer(BuildTrack());

        // 3A 的 result 是 ParseStatus.InvalidData，應被跳過
        var all = analyzer.FindCat("").ToList();

        // 只有 1A, 2A, 4A, 5A 有有效 result（共 4 個）
        Assert.Equal(4, all.Count);
        Assert.DoesNotContain(all, s => s.Id.Id == 3);
    }

    #endregion

    #region FindGuaranteedPositions

    [Fact]
    public void FindGuaranteedPositions_ReturnsMatchingPositions()
    {
        var analyzer = new SeedAnalyzer(BuildTrack());

        var results = analyzer.FindGuaranteedPositions("超激火焰貓").ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal(new Position(1, "A"), results[0]);
        Assert.Equal(new Position(5, "A"), results[1]);
    }

    [Fact]
    public void FindGuaranteedPositions_NoMatch_ReturnsEmpty()
    {
        var analyzer = new SeedAnalyzer(BuildTrack());

        var results = analyzer.FindGuaranteedPositions("不存在").ToList();

        Assert.Empty(results);
    }

    [Fact]
    public void FindGuaranteedPositions_SkipsParseStatus()
    {
        var analyzer = new SeedAnalyzer(BuildTrack());

        // 4A 的 guaranteed 是 ParseStatus.Ambiguous，不應出現
        var all = analyzer.FindGuaranteedPositions("").ToList();

        Assert.DoesNotContain(all, p => p.Id == 4);
    }

    #endregion
}
