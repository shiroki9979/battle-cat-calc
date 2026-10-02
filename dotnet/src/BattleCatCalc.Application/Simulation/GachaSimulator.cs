using System.Collections.Frozen;
using BattleCatCalc.Core.Enums;
using BattleCatCalc.Core.Interfaces;
using BattleCatCalc.Core.Models;

// 佇列排序鍵：f 相同時先展開 g 大的（離目標較近），再以 StateId 保證順序穩定、結果可重現。
using SearchPriority = (float F, float NegCost, int Seq);

namespace BattleCatCalc.Application.Simulation;

/// <summary>
/// A*/Dijkstra path finder that searches for the minimum-cost roll sequence
/// to collect all target cats. Supports single-pool and multi-pool modes.
/// </summary>
public sealed class GachaSimulator : ISimulator
{
    private readonly Dictionary<string, GachaPool> _pools;

    // 狀態要記住的貓：目錄標為稀有的貓（遊戲只對稀有做連續重複判定），
    // 沒有目錄的資料退而記「曾在重複稀有格被替換掉的貓」。見 SearchState.LastRolledRare。
    private readonly FrozenSet<string> _rememberedCats;

    // 啟發式的兩個係數，都是從資料與池設定算出的下界（見 ScanRules）。
    private readonly float _minCostPerAdvance;
    private readonly float _minCostPerCat;

    // 以下欄位在 FindBestPath 呼叫時初始化，僅供搜尋期間使用
    private Dictionary<string, int[]> _targetSortedPositions = [];

    /// <summary>每格的最低成本（啟發式係數，供測試驗證下界）。</summary>
    internal float MinCostPerAdvance => _minCostPerAdvance;

    /// <summary>每隻貓的最低成本（啟發式係數，供測試驗證下界）。</summary>
    internal float MinCostPerCat => _minCostPerCat;

    /// <summary>測試接縫：關閉啟發式即退化為 Dijkstra，作為 A* 最優性的比對基準。</summary>
    internal bool HeuristicEnabled { get; set; } = true;

    /// <summary>單抽一個格子的結果。</summary>
    private readonly record struct RollOutcome(string Unit, Position NextPos, bool IsDuplicateRare);

    /// <summary>Single-pool constructor (backward-compatible).</summary>
    public GachaSimulator(Track trackA, Track trackB, bool hasGuaranteed = true, int guaranteedCost = 10)
    {
        var pool = new GachaPool("default", "Default Pool", trackA, trackB, hasGuaranteed, guaranteedCost);
        _pools = new() { ["default"] = pool };
        (_rememberedCats, _minCostPerAdvance, _minCostPerCat) = ScanRules(_pools.Values);
    }

    /// <summary>Multi-pool constructor.</summary>
    public GachaSimulator(IEnumerable<GachaPool> pools)
    {
        _pools = pools.ToDictionary(p => p.PoolId);
        (_rememberedCats, _minCostPerAdvance, _minCostPerCat) = ScanRules(_pools.Values);
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

        var pq = new PriorityQueue<SearchState, SearchPriority>();
        pq.Enqueue(startState, PriorityOf(startState));

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

                pq = new PriorityQueue<SearchState, SearchPriority>(best.Count);
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

    private static SearchPriority PriorityOf(SearchState state) => (state.Priority, -state.Cost, state.StateId);

    private GachaSlot? GetSlot(int posId, string posTrack, string poolId)
    {
        if (!_pools.TryGetValue(poolId, out var pool)) return null;
        var track = posTrack == "A" ? pool.TrackA : pool.TrackB;
        return track.GetSlot(posId);
    }

    /// <summary>
    /// 掃描所有池，整理重複稀有判定與啟發式需要的常數。
    /// 啟發式的兩個係數不能寫死為 1：單抽觸發重複稀有可一次前進多格（10B → 12A 是 2 格）、
    /// 必中從 B 軌出發前進 11 格、必中以 GuaranteedCost 拿到 11 隻；寫死會高估而失去 A* 的最優性。
    /// </summary>
    private static (FrozenSet<string> Remembered, float MinCostPerAdvance, float MinCostPerCat) ScanRules(
        IEnumerable<GachaPool> pools)
    {
        var remembered = new HashSet<string>();
        var maxSingleAdvance = 1;
        var maxGuaranteedAdvance = 1;
        var guaranteedCosts = new List<int>();

        foreach (var pool in pools)
        {
            if (pool.HasGuaranteed) guaranteedCosts.Add(pool.GuaranteedCost);

            foreach (var track in new[] { pool.TrackA, pool.TrackB })
            {
                foreach (var slot in track)
                {
                    if (slot.ResultRarity == CatRarity.Rare && slot.Result.AsCatName() is string rare)
                        remembered.Add(rare);

                    if (slot.IsDuplicateRare && slot.OriginalResult is string original)
                    {
                        remembered.Add(original);
                        if (slot.SingleNextPos is Position jump)
                            maxSingleAdvance = Math.Max(maxSingleAdvance, jump.Id - slot.Id.Id);
                    }

                    if (!pool.HasGuaranteed) continue;
                    foreach (var landing in new[] { slot.NextPos, slot.OriginalNextPos })
                        if (landing is Position l)
                            maxGuaranteedAdvance = Math.Max(maxGuaranteedAdvance, l.Id - slot.Id.Id);
                }
            }
        }

        float minCostPerAdvance = Math.Min(
            1.0f / maxSingleAdvance,
            guaranteedCosts.Select(c => (float)c / maxGuaranteedAdvance).DefaultIfEmpty(1.0f).Min());
        // 一次必中 = 10 隻一般結果 + 1 隻必中超激
        float minCostPerCat = Math.Min(
            1.0f,
            guaranteedCosts.Select(c => (float)c / 11).DefaultIfEmpty(1.0f).Min());

        return (remembered.ToFrozenSet(), minCostPerAdvance, minCostPerCat);
    }

    /// <summary>決定要不要把剛拿到的貓記進狀態（見 SearchState.LastRolledRare）。</summary>
    private string? Remember(string unit) => _rememberedCats.Contains(unit) ? unit : null;

    /// <summary>
    /// 計算在 slot 單抽一次會拿到什麼、落在哪。
    /// 重複稀有格的表格資料是假設「上一隻就是同一隻貓」算出來的：
    /// 上一隻確實是 OriginalResult 時觸發（拿替換後的貓，落點用網站標示的單抽落點）；
    /// 否則不觸發（拿替換前的貓，同軌前進一格）。
    /// </summary>
    /// <returns>
    /// null 代表這格無法可靠模擬：資料無效、觸發重複卻沒有標示落點，
    /// 或表格沒標這格會重複、上一隻卻就是這格的貓（見下）。
    /// </returns>
    private static RollOutcome? ResolveSingleRoll(GachaSlot slot, string? lastCat)
    {
        if (!slot.Result.IsValid) return null;

        var result = slot.Result.AsCatName()!;
        var plainNext = new Position(slot.Id.Id + 1, slot.Id.Track);

        if (!slot.IsDuplicateRare || slot.OriginalResult is not string original)
        {
            // 表格只標「同軌連續走來」會重複的格子。從別的路徑（必中落點、重複稀有跳格、換池）
            // 走到這格而上一隻就是這格的貓時，遊戲一樣會重抽，但表格沒有替換結果，無法預測；
            // 寧可不走這條路，也不要給出一條會在這裡分岔的計畫。
            if (lastCat is not null && lastCat == result) return null;
            return new RollOutcome(result, plainNext, false);
        }

        if (lastCat == original)
        {
            return slot.SingleNextPos is Position landing
                ? new RollOutcome(result, landing, true)
                : null;
        }

        return new RollOutcome(original, plainNext, false);
    }

    /// <summary>
    /// 位置感知的 admissible 啟發式估計，取以下兩者的最大值，兩者都是剩餘成本的下界：
    /// 未收集數量 × 每隻貓的最低成本（必中以 GuaranteedCost 拿 11 隻，所以不是 1）、
    /// 最遠「最近可得位置」的距離 × 每格的最低成本（重複稀有單抽可前進多格、必中從 B 軌前進 11 格）。
    /// 位置只會單調遞增，因此距離估計為合法下界；兩個係數由 ScanRules 從資料算出，寫死為 1 會高估。
    /// </summary>
    private float Heuristic(FrozenSet<string> collected, FrozenSet<string> targets, int targetCount, int currentPos)
    {
        if (!HeuristicEnabled) return 0;

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

        return Math.Max(uncollected * _minCostPerCat, maxMinDist * _minCostPerAdvance);
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

                    // 重複稀有格沒被觸發時拿的是替換前的貓，所以 Original* 也算可得。
                    foreach (var name in new[]
                    {
                        slot.Result.AsCatName(), slot.Guaranteed.AsCatName(),
                        slot.OriginalResult, slot.OriginalGuaranteed
                    })
                    {
                        if (name is null || !catMaxPos.ContainsKey(name)) continue;

                        if (posId > catMaxPos[name]) catMaxPos[name] = posId;
                        catPosSets[name].Add(posId);
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
        PriorityQueue<SearchState, SearchPriority> pq,
        Dictionary<int, SearchState> stateMap,
        string poolId,
        float singleRollCost,
        ref int idCounter,
        int targetCount)
    {
        var slot = GetSlot(state.PosId, state.PosTrack, poolId);
        if (slot is null) return;

        float newCost = state.Cost + singleRollCost;
        if (newCost > maxRolls) return;

        if (ResolveSingleRoll(slot, state.LastRolledRare) is not { } outcome) return;

        string actualUnit = outcome.Unit;
        Position nextPos = outcome.NextPos;
        string? newLastRare = Remember(actualUnit);

        var step = new GachaStep(
            Source: new Position(state.PosId, state.PosTrack),
            Target: nextPos,
            Unit: actualUnit,
            PoolId: poolId,
            IsGuaranteed: false,
            IsTrackSwitch: nextPos.Track != state.PosTrack,
            IsDuplicateRare: outcome.IsDuplicateRare);

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
        pq.Enqueue(newState, PriorityOf(newState));
    }

    /// <summary>處理十連抽（保底）的分支邏輯。</summary>
    private void ProcessGuaranteedRoll(
        SearchState state,
        FrozenSet<string> targets,
        int maxRolls,
        PriorityQueue<SearchState, SearchPriority> pq,
        Dictionary<int, SearchState> stateMap,
        string poolId,
        ref int idCounter,
        int targetCount)
    {
        if (!_pools.TryGetValue(poolId, out var pool) || !pool.HasGuaranteed) return;

        float newCost = state.Cost + pool.GuaranteedCost;
        if (newCost > maxRolls) return;

        var slot = GetSlot(state.PosId, state.PosTrack, poolId);
        if (slot is null) return;

        var collectedInRoll = new HashSet<string>();
        string? lastCat = state.LastRolledRare;
        var pos = new Position(state.PosId, state.PosTrack);
        bool startTriggeredDup = false;

        // 前 10 抽照單抽規則走：遇到重複稀有會換軌並多前進，不能假設一直在同軌的 pos+i。
        for (int i = 0; i < 10; i++)
        {
            var s = GetSlot(pos.Id, pos.Track, poolId);
            if (s is null) return; // 資料不完整，無法執行保底模擬
            if (ResolveSingleRoll(s, lastCat) is not { } outcome) return;

            if (i == 0) startTriggeredDup = outcome.IsDuplicateRare;
            if (targets.Contains(outcome.Unit)) collectedInRoll.Add(outcome.Unit);
            lastCat = outcome.Unit;
            pos = outcome.NextPos;
        }

        // 表格的必中欄是以起始格自己的重複狀態算出來的：
        // 起始格是重複稀有格但沒被觸發時，要用替換前的版本。
        string? guaUnit;
        Position? landing;
        if (slot.IsDuplicateRare && !startTriggeredDup
            && slot.OriginalGuaranteed is not null && slot.OriginalNextPos is not null)
        {
            guaUnit = slot.OriginalGuaranteed;
            landing = slot.OriginalNextPos;
        }
        else
        {
            guaUnit = slot.Guaranteed.AsCatName();
            landing = slot.NextPos;
        }
        if (guaUnit is null || landing is not Position nextPos) return;

        // 第 11 隻是必中超激，它成為「上一隻」，因此重複判定在這之後重置。
        // 此為依規則推得的假設，尚無資料可獨立驗證。
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
            // 前 10 抽內發生奇數次重複稀有時，必中後會留在原軌道（例如 1B → 12B）。
            IsTrackSwitch: nextPos.Track != state.PosTrack,
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
            LastRolledRare: Remember(guaUnit));

        stateMap[newId] = newState;
        pq.Enqueue(newState, PriorityOf(newState));
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
