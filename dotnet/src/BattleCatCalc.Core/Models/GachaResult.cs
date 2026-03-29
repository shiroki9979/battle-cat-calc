using System.Collections.Frozen;
using System.Text;

namespace BattleCatCalc.Core.Models;

/// <summary>
/// Structured result of a gacha simulation path search.
/// </summary>
public sealed class GachaResult
{
    public GachaResult(
        int totalCost,
        IReadOnlyList<GachaStep> path,
        FrozenSet<string> collected,
        Position? startPos = null,
        FrozenSet<string>? targetCats = null)
    {
        TotalCost = totalCost;
        Path = path;
        Collected = collected;
        StartPos = startPos;
        TargetCats = targetCats;
    }

    public int TotalCost { get; }
    public IReadOnlyList<GachaStep> Path { get; }
    public FrozenSet<string> Collected { get; }
    public Position? StartPos { get; }
    public FrozenSet<string>? TargetCats { get; }

    public int StepCount => Path.Count;
    public int GuaranteedCount => Path.Count(s => s.IsGuaranteed);
    public int SingleRollCount => Path.Count(s => !s.IsGuaranteed);
    public Position? EndPos => Path.Count > 0 ? Path[^1].Target : StartPos;

    /// <summary>Returns the set of distinct pool IDs used in the path.</summary>
    public FrozenSet<string> PoolsUsed =>
        Path.Where(s => !string.IsNullOrEmpty(s.PoolId))
            .Select(s => s.PoolId)
            .ToFrozenSet();

    /// <summary>Groups steps by their pool ID.</summary>
    public Dictionary<string, List<GachaStep>> StepsByPool()
    {
        var result = new Dictionary<string, List<GachaStep>>();
        foreach (var step in Path)
        {
            var key = string.IsNullOrEmpty(step.PoolId) ? "default" : step.PoolId;
            if (!result.TryGetValue(key, out var list))
            {
                list = [];
                result[key] = list;
            }
            list.Add(step);
        }
        return result;
    }

    public override string ToString() =>
        $"GachaResult(cost={TotalCost}, steps={StepCount}, collected={Collected.Count})";
}
