using System.Collections.Frozen;
using BattleCatCalc.Application.Simulation;
using BattleCatCalc.Core.Models;

namespace BattleCatCalc.Application.Tests.Simulation;

public class GachaSimulatorSalvageTests
{
    [Fact]
    public void EqualCost_PrefersMoreSalvageBeforeMoreGuaranteedRolls()
    {
        var guaranteed = Pool("guaranteed", 10, true, 10,
            i => $"Rare{i}", "Target");
        var rich = Pool("rich", 10, false, 10,
            i => i == 10 ? "Target" : $"Uber{i}", "Target");
        var values = Enumerable.Range(1, 10).ToDictionary(i => $"Rare{i}", _ => 5);
        foreach (var i in Enumerable.Range(1, 9)) values[$"Uber{i}"] = 50;
        values["Target"] = 50;

        var result = new GachaSimulator([guaranteed, rich]).FindBestPath(
            new Position(1, "A"), Targets(), 10, null, true, salvageValues: values);

        Assert.NotNull(result);
        Assert.Equal(10, result.TotalCost);
        Assert.Equal(500, result.TotalSalvageValue);
        Assert.Equal(0, result.GuaranteedCount);
    }

    [Fact]
    public void LowerCost_WinsOverHigherSalvage()
    {
        var pool = Pool("p", 10, true, 11,
            i => i == 10 ? "Target" : $"Rare{i}", "Target");
        var values = Enumerable.Range(1, 9).ToDictionary(i => $"Rare{i}", _ => 5);
        values["Target"] = 50;

        var result = new GachaSimulator([pool]).FindBestPath(
            new Position(1, "A"), Targets(), 11, null, true, salvageValues: values);

        Assert.NotNull(result);
        Assert.Equal(10, result.TotalCost);
        Assert.Equal(95, result.TotalSalvageValue);
        Assert.Equal(0, result.GuaranteedCount);
    }

    [Fact]
    public void ConvergingPrefixes_RetainTheHigherSalvageRoute()
    {
        var low = Pool("low", 3, false, 10,
            i => i == 3 ? "Target" : "Rare", "Target");
        var rich = Pool("rich", 3, false, 10,
            i => i == 1 ? "Uber" : i == 3 ? "Target" : "Rare", "Target");
        var values = new Dictionary<string, int> { ["Rare"] = 5, ["Uber"] = 50, ["Target"] = 50 };

        var result = new GachaSimulator([low, rich]).FindBestPath(
            new Position(1, "A"), Targets(), 3, null, false, salvageValues: values);

        Assert.NotNull(result);
        Assert.Equal(3, result.TotalCost);
        Assert.Equal(105, result.TotalSalvageValue);
        Assert.Equal("rich", result.Path[0].PoolId);
    }

    [Fact]
    public void GuaranteedRoll_ScoresAllTenNormalCatsAndTheBonus()
    {
        var pool = Pool("p", 10, true, 1, i => $"Rare{i}", "Target");
        var values = Enumerable.Range(1, 10).ToDictionary(i => $"Rare{i}", _ => 5);
        values["Target"] = 50;

        var result = new GachaSimulator([pool]).FindBestPath(
            new Position(1, "A"), Targets(), 1, null, true, salvageValues: values);

        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCost);
        Assert.Equal(100, result.TotalSalvageValue);
        Assert.Equal(1, result.GuaranteedCount);
    }

    [Fact]
    public void AStarWithSalvage_MatchesDijkstraCostValueAndGuaranteedCount()
    {
        foreach (var guaranteedCost in new[] { 3, 10 })
        {
            var low = Pool("low", 30, true, guaranteedCost,
                i => i == 20 ? "Target" : $"Low{i}", "Bonus");
            var rich = Pool("rich", 30, true, guaranteedCost + 1,
                i => i == 20 ? "Target" : $"Rich{i}", "Bonus");
            var values = Enumerable.Range(1, 30).ToDictionary(i => $"Low{i}", _ => 5);
            foreach (var i in Enumerable.Range(1, 30)) values[$"Rich{i}"] = 15;
            values["Target"] = values["Bonus"] = 50;
            var targets = new[] { "Target", "Bonus" }.ToFrozenSet();
            var astar = new GachaSimulator([low, rich]);
            var dijkstra = new GachaSimulator([low, rich]) { HeuristicEnabled = false };

            var expected = dijkstra.FindBestPath(new Position(1, "A"), targets, 30,
                null, true, salvageValues: values);
            var actual = astar.FindBestPath(new Position(1, "A"), targets, 30,
                null, true, salvageValues: values);

            Assert.NotNull(expected);
            Assert.NotNull(actual);
            Assert.Equal(expected.TotalCost, actual.TotalCost);
            Assert.Equal(expected.TotalSalvageValue, actual.TotalSalvageValue);
            Assert.Equal(expected.GuaranteedCount, actual.GuaranteedCount);
        }
    }

    private static FrozenSet<string> Targets() => new[] { "Target" }.ToFrozenSet();

    private static GachaPool Pool(string id, int count, bool hasGuaranteed, int guaranteedCost,
        Func<int, string> normalName, string bonus)
    {
        Track Build(string track) => new(track, Enumerable.Range(1, count).Select(i => new GachaSlot(
            new Position(i, track), new SlotValue.CatName(normalName(i)), new SlotValue.CatName(bonus),
            new Position(i + (track == "A" ? 10 : 11), track == "A" ? "B" : "A"))));
        return new GachaPool(id, id, Build("A"), Build("B"), hasGuaranteed, guaranteedCost);
    }
}
