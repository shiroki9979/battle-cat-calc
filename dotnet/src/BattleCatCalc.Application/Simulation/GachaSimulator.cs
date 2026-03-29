using System.Collections.Frozen;
using BattleCatCalc.Core.Interfaces;
using BattleCatCalc.Core.Models;

namespace BattleCatCalc.Application.Simulation;

/// <summary>
/// A*/Dijkstra path finder that searches for the minimum-cost roll sequence
/// to collect all target cats. Supports single-pool and multi-pool modes.
/// </summary>
public sealed class GachaSimulator : ISimulator
{
    private readonly Dictionary<string, GachaPool> _pools;

    // 以下欄位在 FindBestPath 呼叫時初始化，僅供搜尋期間使用
    private Dictionary<string, int[]> _targetSortedPositions = [];
    private float _minCostPerAdvance;

    /// <summary>Single-pool constructor (backward-compatible).</summary>
    public GachaSimulator(Track trackA, Track trackB, bool hasGuaranteed = true, int guaranteedCost = 10)
    {
        var pool = new GachaPool("default", "Default Pool", trackA, trackB, hasGuaranteed, guaranteedCost);
        _pools = new() { ["default"] = pool };
    }

    /// <summary>Multi-pool constructor.</summary>
    public GachaSimulator(IEnumerable<GachaPool> pools)
    {
        _pools = pools.ToDictionary(p => p.PoolId);
    }

    /// <inheritdoc />
    public GachaResult? FindBestPath(Position start, FrozenSet<string> targets, int maxRolls = 1000)
        => FindBestPath(start, targets, maxRolls, beamWidth: null, preferGuaranteed: false);

    /// <summary>
    /// Extended search with beam width and guaranteed preference options.
    /// </summary>
    /// <param name="start">Starting position on the track.</param>
    /// <param name="targets">Set of target cat names to collect.</param>
    /// <param name="maxRolls">Maximum number of rolls to search.</param>
    /// <param name="beamWidth">Beam search width limit. Null for full A*. Recommended: 10000-50000.</param>
    /// <param name="preferGuaranteed">When true, single rolls get a 1.11x cost penalty to prefer guaranteed rolls.</param>
    /// <param name="progressCallback">Called per state: (cost, statesProcessed, queueSize).</param>
    public GachaResult? FindBestPath(
        Position start,
        FrozenSet<string> targets,
        int maxRolls,
        int? beamWidth,
        bool preferGuaranteed,
        Action<float, int, int>? progressCallback = null)
    {
        int targetCount = targets.Count;

        // 空目標特殊處理
        if (targetCount == 0)
            return new GachaResult(0, [], FrozenSet<string>.Empty, start, targets);

        int idCounter = 0;

        // 預計算：目標貓咪位置資料
        var (catMaxPos, targetSortedPositions) = PrecomputeTargetData(targets);
        _targetSortedPositions = targetSortedPositions;

        // 驗證所有目標貓咪是否存在
        var missing = catMaxPos.Where(kv => kv.Value == 0).Select(kv => kv.Key).Order().ToList();
        if (missing.Count > 0)
            throw new ArgumentException(
                $"Target cats not found in any pool: {string.Join(", ", missing)}");

        _minCostPerAdvance = Math.Min(
            1.0f,
            _pools.Values
                .Where(p => p.HasGuaranteed)
                .Select(p => (float)p.GuaranteedCost / 10)
                .DefaultIfEmpty(1.0f)
                .Min());

        // 初始狀態
        var initialCollected = FrozenSet<string>.Empty;
        float h = Heuristic(initialCollected, targets, targetCount, start.Id);
        int startId = ++idCounter;
        var startState = new SearchState(
            Priority: h,
            Cost: 0,
            PosId: start.Id,
            PosTrack: start.Track,
            Collected: initialCollected,
            StateId: startId,
            ParentId: null,
            LastStep: null,
            LastRolledRare: null);

        var pq = new PriorityQueue<SearchState, float>();
        pq.Enqueue(startState, startState.Priority);

        // 支配性剪枝: (posId, posTrack, lastRolledRare) -> [(cost, collected)]
        var visited = new Dictionary<(int, string, string?), List<(float Cost, FrozenSet<string> Collected)>>();
        var stateMap = new Dictionary<int, SearchState> { [startId] = startState };
        int statesProcessed = 0;

        while (pq.Count > 0)
        {
            var state = pq.Dequeue();
            statesProcessed++;

            progressCallback?.Invoke(state.Cost, statesProcessed, pq.Count);

            // 目標檢查：所有目標都已收集即為最佳解
            if (targets.IsSubsetOf(state.Collected))
            {
                var path = ReconstructPath(state, stateMap);
                int totalCost = ComputeActualCost(path);
                return new GachaResult(totalCost, path, state.Collected, start, targets);
            }

            if (state.Cost >= maxRolls)
                continue;

            // 支配性剪枝
            var posKey = (state.PosId, state.PosTrack, state.LastRolledRare);
            if (visited.TryGetValue(posKey, out var entries))
            {
                bool dominated = false;
                foreach (var (vCost, vCollected) in entries)
                {
                    if (vCost <= state.Cost && vCollected.IsSupersetOf(state.Collected))
                    {
                        dominated = true;
                        break;
                    }
                }
                if (dominated) continue;

                // 移除被當前狀態支配的舊項目
                entries.RemoveAll(e => state.Cost <= e.Cost && state.Collected.IsSupersetOf(e.Collected));
                entries.Add((state.Cost, state.Collected));
            }
            else
            {
                visited[posKey] = [(state.Cost, state.Collected)];
            }

            // 可達性剪枝：位置只會單調遞增，若剩餘目標的最大位置 < 當前位置則不可達
            bool unreachable = false;
            foreach (var cat in targets)
            {
                if (state.Collected.Contains(cat)) continue;
                if (catMaxPos.GetValueOrDefault(cat) < state.PosId)
                {
                    unreachable = true;
                    break;
                }
            }
            if (unreachable) continue;

            // 展開分支：對每個池嘗試單抽與保底
            float singleRollCost = preferGuaranteed ? 1.11f : 1;
            foreach (var poolId in _pools.Keys)
            {
                ProcessSingleRoll(state, targets, maxRolls, pq, stateMap, poolId, singleRollCost, ref idCounter, targetCount);
                ProcessGuaranteedRoll(state, targets, maxRolls, pq, stateMap, poolId, ref idCounter, targetCount);
            }

            // Beam Search：限制佇列大小以控制記憶體使用
            if (beamWidth.HasValue && pq.Count > beamWidth.Value * 2)
            {
                var best = pq.UnorderedItems
                    .OrderBy(x => x.Priority)
                    .Take(beamWidth.Value)
                    .ToList();

                pq = new PriorityQueue<SearchState, float>(best.Count);
                foreach (var item in best)
                    pq.Enqueue(item.Element, item.Priority);

                // 清理 stateMap：只保留 PQ 中所有狀態的祖先鏈
                var neededIds = CollectAncestorIds(best.Select(x => x.Element), stateMap);
                var keysToRemove = stateMap.Keys.Where(k => !neededIds.Contains(k)).ToList();
                foreach (var key in keysToRemove)
                    stateMap.Remove(key);
            }
        }

        return null;
    }

    #region Private Helpers

    private GachaSlot? GetSlot(int posId, string posTrack, string poolId)
    {
        if (!_pools.TryGetValue(poolId, out var pool)) return null;
        var track = posTrack == "A" ? pool.TrackA : pool.TrackB;
        return track.GetSlot(posId);
    }

    /// <summary>
    /// 位置感知的 admissible 啟發式估計。
    /// 取未收集數量與「最遠最近可得位置距離 × 每位置最低成本」的最大值。
    /// 位置只會單調遞增，保證估計為合法下界（A* 最優性）。
    /// </summary>
    private float Heuristic(FrozenSet<string> collected, FrozenSet<string> targets, int targetCount, int currentPos)
    {
        int uncollected = 0;
        foreach (var cat in targets)
            if (!collected.Contains(cat)) uncollected++;
        if (uncollected == 0) return 0;

        int maxMinDist = 0;
        foreach (var cat in targets)
        {
            if (collected.Contains(cat)) continue;
            if (!_targetSortedPositions.TryGetValue(cat, out var positions)) continue;

            int idx = Array.BinarySearch(positions, currentPos);
            if (idx < 0) idx = ~idx;
            if (idx < positions.Length)
            {
                int dist = positions[idx] - currentPos;
                if (dist > maxMinDist) maxMinDist = dist;
            }
        }

        return Math.Max(uncollected, maxMinDist * _minCostPerAdvance);
    }

    /// <summary>
    /// 預計算目標貓咪的位置資料（單次掃描所有池）。
    /// cat_max_pos 供可達性剪枝使用；sorted_positions 供位置感知啟發式使用。
    /// </summary>
    private (Dictionary<string, int> CatMaxPos, Dictionary<string, int[]> SortedPositions) PrecomputeTargetData(
        FrozenSet<string> targets)
    {
        var catMaxPos = new Dictionary<string, int>(targets.Count);
        var catPosSets = new Dictionary<string, HashSet<int>>(targets.Count);

        foreach (var cat in targets)
        {
            catMaxPos[cat] = 0;
            catPosSets[cat] = [];
        }

        foreach (var pool in _pools.Values)
        {
            foreach (var track in new[] { pool.TrackA, pool.TrackB })
            {
                foreach (var slot in track)
                {
                    int posId = slot.Id.Id;

                    var resultName = slot.Result.AsCatName();
                    if (resultName is not null && catMaxPos.ContainsKey(resultName))
                    {
                        if (posId > catMaxPos[resultName]) catMaxPos[resultName] = posId;
                        catPosSets[resultName].Add(posId);
                    }

                    var guaName = slot.Guaranteed.AsCatName();
                    if (guaName is not null && catMaxPos.ContainsKey(guaName))
                    {
                        if (posId > catMaxPos[guaName]) catMaxPos[guaName] = posId;
                        catPosSets[guaName].Add(posId);
                    }
                }
            }
        }

        var sortedPositions = new Dictionary<string, int[]>(catPosSets.Count);
        foreach (var (cat, positions) in catPosSets)
        {
            var arr = positions.ToArray();
            Array.Sort(arr);
            sortedPositions[cat] = arr;
        }

        return (catMaxPos, sortedPositions);
    }

    /// <summary>處理單次抽卡的分支邏輯。</summary>
    private void ProcessSingleRoll(
        SearchState state,
        FrozenSet<string> targets,
        int maxRolls,
        PriorityQueue<SearchState, float> pq,
        Dictionary<int, SearchState> stateMap,
        string poolId,
        float singleRollCost,
        ref int idCounter,
        int targetCount)
    {
        var slot = GetSlot(state.PosId, state.PosTrack, poolId);
        if (slot is null || !slot.Result.IsValid) return;

        float newCost = state.Cost + singleRollCost;
        if (newCost > maxRolls) return;

        string actualUnit = slot.Result.AsCatName()!;

        // 重複稀有判定
        bool isDupRare = slot.IsDuplicateRare && state.LastRolledRare == slot.OriginalResult;

        Position nextPos;
        if (isDupRare && slot.NextPos is Position np)
        {
            nextPos = np;
        }
        else
        {
            nextPos = new Position(state.PosId + 1, state.PosTrack);
            isDupRare = false;
        }

        // 更新稀有貓追蹤
        string? newLastRare = (slot.IsDuplicateRare || slot.OriginalResult is not null)
            ? actualUnit
            : state.LastRolledRare;

        var step = new GachaStep(
            Source: new Position(state.PosId, state.PosTrack),
            Target: nextPos,
            Unit: actualUnit,
            PoolId: poolId,
            IsGuaranteed: false,
            IsTrackSwitch: nextPos.Track != state.PosTrack,
            IsDuplicateRare: isDupRare);

        var newCollected = targets.Contains(actualUnit) && !state.Collected.Contains(actualUnit)
            ? new HashSet<string>(state.Collected) { actualUnit }.ToFrozenSet()
            : state.Collected;

        int newId = ++idCounter;
        float h = Heuristic(newCollected, targets, targetCount, nextPos.Id);

        var newState = new SearchState(
            Priority: newCost + h,
            Cost: newCost,
            PosId: nextPos.Id,
            PosTrack: nextPos.Track,
            Collected: newCollected,
            StateId: newId,
            ParentId: state.StateId,
            LastStep: step,
            LastRolledRare: newLastRare);

        stateMap[newId] = newState;
        pq.Enqueue(newState, newState.Priority);
    }

    /// <summary>處理十連抽（保底）的分支邏輯。</summary>
    private void ProcessGuaranteedRoll(
        SearchState state,
        FrozenSet<string> targets,
        int maxRolls,
        PriorityQueue<SearchState, float> pq,
        Dictionary<int, SearchState> stateMap,
        string poolId,
        ref int idCounter,
        int targetCount)
    {
        if (!_pools.TryGetValue(poolId, out var pool) || !pool.HasGuaranteed) return;

        float newCost = state.Cost + pool.GuaranteedCost;
        if (newCost > maxRolls) return;

        var slot = GetSlot(state.PosId, state.PosTrack, poolId);
        if (slot is null || !slot.Guaranteed.IsValid || slot.NextPos is not Position nextPos) return;

        var collectedInRoll = new HashSet<string>();
        string? lastRareInRoll = state.LastRolledRare;

        var track = state.PosTrack == "A" ? pool.TrackA : pool.TrackB;

        // 檢查接下來 10 格（不含保底）
        for (int i = 0; i < 10; i++)
        {
            var s = track.GetSlot(state.PosId + i);
            if (s is null || !s.Result.IsValid) return;

            string unit = s.Result.AsCatName()!;
            if (targets.Contains(unit)) collectedInRoll.Add(unit);
            if (s.IsDuplicateRare || s.OriginalResult is not null) lastRareInRoll = unit;
        }

        // 保底貓（保底是超激，不影響重複稀有判定）
        string guaUnit = slot.Guaranteed.AsCatName()!;
        if (targets.Contains(guaUnit)) collectedInRoll.Add(guaUnit);

        FrozenSet<string> newCollected;
        if (collectedInRoll.Count > 0)
        {
            var merged = new HashSet<string>(state.Collected);
            merged.UnionWith(collectedInRoll);
            newCollected = merged.Count > state.Collected.Count ? merged.ToFrozenSet() : state.Collected;
        }
        else
        {
            newCollected = state.Collected;
        }

        var step = new GachaStep(
            Source: new Position(state.PosId, state.PosTrack),
            Target: nextPos,
            Unit: guaUnit,
            PoolId: poolId,
            IsGuaranteed: true,
            IsTrackSwitch: true,
            IsDuplicateRare: false);

        int newId = ++idCounter;
        float h = Heuristic(newCollected, targets, targetCount, nextPos.Id);

        var newState = new SearchState(
            Priority: newCost + h,
            Cost: newCost,
            PosId: nextPos.Id,
            PosTrack: nextPos.Track,
            Collected: newCollected,
            StateId: newId,
            ParentId: state.StateId,
            LastStep: step,
            LastRolledRare: lastRareInRoll);

        stateMap[newId] = newState;
        pq.Enqueue(newState, newState.Priority);
    }

    /// <summary>從終點狀態回溯重建完整路徑。</summary>
    private static IReadOnlyList<GachaStep> ReconstructPath(SearchState state, Dictionary<int, SearchState> stateMap)
    {
        var path = new List<GachaStep>();
        var current = state;
        while (current.LastStep is not null)
        {
            path.Add(current.LastStep);
            if (current.ParentId is not int parentId) break;
            if (!stateMap.TryGetValue(parentId, out current)) break;
        }
        path.Reverse();
        return path;
    }

    /// <summary>從路徑計算實際抽卡成本（整數）。</summary>
    private int ComputeActualCost(IReadOnlyList<GachaStep> path)
    {
        int cost = 0;
        foreach (var step in path)
        {
            if (step.IsGuaranteed)
            {
                var poolId = step.PoolId.Length > 0 ? step.PoolId : "default";
                cost += _pools.TryGetValue(poolId, out var pool) ? pool.GuaranteedCost : 10;
            }
            else
            {
                cost += 1;
            }
        }
        return cost;
    }

    /// <summary>收集 PQ 中所有狀態的祖先 ID（Beam Search 清理 stateMap 用）。</summary>
    private static HashSet<int> CollectAncestorIds(
        IEnumerable<SearchState> states,
        Dictionary<int, SearchState> stateMap)
    {
        var neededIds = new HashSet<int>();
        foreach (var state in states)
        {
            int? currentId = state.StateId;
            while (currentId.HasValue && neededIds.Add(currentId.Value))
            {
                currentId = stateMap.TryGetValue(currentId.Value, out var parent) ? parent.ParentId : null;
            }
        }
        return neededIds;
    }

    #endregion
}
