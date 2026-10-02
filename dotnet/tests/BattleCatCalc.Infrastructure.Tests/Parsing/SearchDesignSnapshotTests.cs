using System.Collections.Frozen;
using BattleCatCalc.Application.Simulation;
using BattleCatCalc.Core.Enums;
using BattleCatCalc.Core.Models;
using BattleCatCalc.Infrastructure.Parsing;

namespace BattleCatCalc.Infrastructure.Tests.Parsing;

/// <summary>
/// 以真實 fixture 驗證：A* 與精確搜尋一致；稀有度取自目錄而非儲存格 class。
/// </summary>
public class SearchDesignSnapshotTests
{
    private static readonly Lazy<(Track A, Track B)> Parsed1019 = new(() =>
        new BCParser().Parse(FixtureHelper.LoadHtml("seed_2127986414_event_1019.html")));

    private static readonly Lazy<(Track A, Track B)> Parsed1020 = new(() =>
        new BCParser().Parse(FixtureHelper.LoadHtml("seed_2127986414_event_1020.html")));

    private static IEnumerable<string> AllCats(params (Track A, Track B)[] parsed) =>
        parsed.SelectMany(p => p.A.Concat(p.B))
            .SelectMany(s => new[] { s.Result.AsCatName(), s.Guaranteed.AsCatName() })
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .Distinct()
            .Order();

    private static void CrossCheck(Func<GachaSimulator> make, IReadOnlyList<string> cats, int trials, int seed)
    {
        /// 啟發式一旦高估，A* 就可能回傳較差的解；用關閉啟發式的 Dijkstra 當基準做隨機抽查。
        var astar = make();
        var exact = make();
        exact.HeuristicEnabled = false;
        var rng = new Random(seed);

        for (var i = 0; i < trials; i++)
        {
            var targets = Enumerable.Range(0, rng.Next(1, 4)).Select(_ => cats[rng.Next(cats.Count)]).ToFrozenSet();
            var start = new Position(rng.Next(1, 61), rng.Next(2) == 0 ? "A" : "B");
            var expected = exact.FindBestPath(start, targets, 80)?.TotalCost;
            var actual = astar.FindBestPath(start, targets, 80)?.TotalCost;
            Assert.True(expected == actual, $"start={start} targets=[{string.Join(",", targets)}]: A*={actual} exact={expected}");
        }
    }

    [Fact]
    public void AStar_MatchesExact_SinglePool()
    {
        var (a, b) = Parsed1019.Value;
        CrossCheck(() => new GachaSimulator(a, b), AllCats(Parsed1019.Value).ToList(), trials: 40, seed: 11);
    }

    [Fact]
    public void AStar_MatchesExact_MultiPool()
    {
        var p1 = Parsed1019.Value;
        var p2 = Parsed1020.Value;
        CrossCheck(
            () => new GachaSimulator([new GachaPool("1019", "1019", p1.A, p1.B), new GachaPool("1020", "1020", p2.A, p2.B)]),
            AllCats(p1, p2).ToList(), trials: 30, seed: 12);
    }

    [Fact]
    public void ResultRarity_FollowsCatalogNotCssClass()
    {
        /// 動機：儲存格 class 是 godfat 依慶典費率上色的「稀有度帶」（拳擊手貓 supa_fest、貓咪番長 uber_fest），
        ///      目錄（#find_select）才是貓本身的稀有度（Rare、Super）。遊戲的連續重複判定要看目錄。
        var (a, b) = Parsed1019.Value;

        var boxer = b.GetSlot(9)!;
        Assert.Equal("拳擊手貓", boxer.Result.AsCatName());
        Assert.Equal(CatRarity.Rare, boxer.ResultRarity);
        Assert.Equal(CatRarity.SuperRareFest, boxer.Rarity);

        var boss = a.GetSlot(13)!;
        Assert.Equal("貓咪番長", boss.Result.AsCatName());
        Assert.Equal(CatRarity.SuperRare, boss.ResultRarity);
        Assert.Equal(CatRarity.UberRareFest, boss.Rarity);

        Assert.Equal(CatRarity.Rare, a.GetSlot(1)!.ResultRarity);       // 貓咪陰陽師
        Assert.Equal(CatRarity.UberRare, a.GetSlot(18)!.ResultRarity);  // EVA零號機
    }

    [Fact]
    public void DuplicateRareSlots_AreRareInCatalog()
    {
        var (a, b) = Parsed1019.Value;
        foreach (var slot in new[] { b.GetSlot(10)!, a.GetSlot(100)! })
        {
            Assert.True(slot.IsDuplicateRare);
            Assert.Equal(CatRarity.Rare, slot.ResultRarity);
        }
    }
}
