import bisect
import heapq
import itertools
from typing import List, NamedTuple, Optional, Set, Tuple, Dict, Union, Callable
from dataclasses import dataclass, field
from .models import CatRarity, Track, GachaSlot, Position, ParseStatus, GachaStep, GachaPool, GachaResult


class _RollOutcome(NamedTuple):
    """單抽一個格子的結果。"""
    unit: str
    next_pos: Position
    is_duplicate_rare: bool


@dataclass(frozen=True, slots=True)
class SearchState:
    """A* 搜尋的狀態節點（延遲路徑建構版）。

    優先佇列的排序鍵放在佇列元素裡（見 find_best_path），不在這個類別上：
    f 值相同時優先展開 g 值大的（離目標較近），再以 state_id 保證順序穩定、結果可重現。
    路徑透過 parent_id + last_step 延遲建構，減少記憶體使用。
    slots=True 減少約 30-40% 記憶體佔用並加速屬性存取。
    """
    # Priority: A* 的 f 值
    priority: float
    # Cost: 目前累積的抽卡成本 (g 值)；prefer_guaranteed 時含單抽加權，只用來排序
    cost: float
    # Position: 目前所在的格子 ID 與 軌道 (A/B)
    pos_id: int
    pos_track: str
    # Collected: 目前已收集到的目標貓咪 (使用 frozenset 以便進行 hash)
    collected: frozenset
    # 延遲路徑建構：記錄父狀態 ID 和這一步的資訊
    state_id: int = 0
    parent_id: Optional[int] = None
    last_step: Optional[GachaStep] = None
    # 上一抽「實際拿到」的貓，用於跨池重複稀有判定。只記住可能觸發重複的貓
    # （目錄標為稀有的貓，見 GachaSimulator._scan_rules），其餘存 None，避免狀態空間無謂膨脹。
    last_rolled_rare: Optional[str] = None

class GachaSimulator:
    """基於 Track 資料的抽卡路徑模擬器。
    
    支援單池或多池模式：
    - 單池：傳入 (track_a, track_b)
    - 多池：傳入 List[GachaPool]
    """
    
    def __init__(
        self, 
        track_a_or_pools: Union[Track, List[GachaPool]], 
        track_b: Optional[Track] = None
    ):
        """初始化模擬器。
        
        Args:
            track_a_or_pools: Track A 物件，或 GachaPool 列表
            track_b: Track B 物件 (僅在單池模式使用)
        """
        if isinstance(track_a_or_pools, list):
            # 多池模式
            self.pools: Dict[str, GachaPool] = {
                pool.pool_id: pool for pool in track_a_or_pools
            }
            self.is_multi_pool = True
        else:
            # 單池模式 (向後相容)
            default_pool = GachaPool(
                pool_id="default",
                name="Default Pool",
                track_a=track_a_or_pools,
                track_b=track_b,
                has_guaranteed=True
            )
            self.pools = {"default": default_pool}
            self.is_multi_pool = False

        self._scan_rules()

    def _scan_rules(self) -> None:
        """掃描所有池，整理重複稀有判定與啟發式需要的常數。

        - _remembered_cats：狀態要記住的貓。遊戲只對「稀有」做連續重複判定，所以記目錄標為
          稀有的貓；沒有目錄的資料（例如手工測試資料）退而記「曾在重複稀有格被替換掉的貓」。
        - _min_cost_per_advance / _min_cost_per_cat：啟發式的兩個係數，都是從資料算出的下界，
          不能寫死為 1：單抽觸發重複稀有可一次前進多格（10B → 12A 是 2 格）、
          必中從 B 軌出發前進 11 格、必中以 guaranteed_cost 拿到 11 隻。
        """
        remembered: Set[str] = set()
        max_single_advance = 1
        max_guaranteed_advance = 1
        for pool in self.pools.values():
            for track in (pool.track_a, pool.track_b):
                if not track:
                    continue
                for slot in track:
                    if slot.rarity is CatRarity.RARE and isinstance(slot.result, str):
                        remembered.add(slot.result)
                    if slot.is_duplicate_rare and isinstance(slot.original_result, str):
                        remembered.add(slot.original_result)
                        if isinstance(slot.single_next_pos, Position):
                            max_single_advance = max(max_single_advance, slot.single_next_pos.id - slot.id.id)
                    if pool.has_guaranteed:
                        for landing in (slot.next_pos, slot.original_next_pos):
                            if isinstance(landing, Position):
                                max_guaranteed_advance = max(max_guaranteed_advance, landing.id - slot.id.id)
        self._remembered_cats: frozenset = frozenset(remembered)
        self._max_single_advance: int = max_single_advance

        guaranteed_pools = [pool for pool in self.pools.values() if pool.has_guaranteed]
        self._min_cost_per_advance: float = min(
            1.0 / max_single_advance,
            min((pool.guaranteed_cost / max_guaranteed_advance for pool in guaranteed_pools), default=1.0),
        )
        # 一次必中 = 10 隻一般結果 + 1 隻必中超激
        self._min_cost_per_cat: float = min(
            1.0,
            min((pool.guaranteed_cost / 11 for pool in guaranteed_pools), default=1.0),
        )

    def _remember(self, unit: str) -> Optional[str]:
        """決定要不要把剛拿到的貓記進狀態（見 SearchState.last_rolled_rare）。"""
        return unit if unit in self._remembered_cats else None

    def _resolve_single_roll(self, slot: GachaSlot, last_cat: Optional[str]) -> Optional[_RollOutcome]:
        """計算在 slot 單抽一次會拿到什麼、落在哪。

        重複稀有格的表格資料是假設「上一隻就是同一隻貓」算出來的：
        - 上一隻確實是 original_result：觸發重複，拿替換後的貓，落點用網站標示的單抽落點。
        - 否則（例如從別的軌道、別的池或必中十連後走到這格）不會觸發，拿的是替換前的貓，
          同軌前進一格。

        Returns:
            None 代表這格無法可靠模擬：資料無效、觸發重複卻沒有標示落點，
            或表格沒標這格會重複、上一隻卻就是這格的貓（見下）。
        """
        if self._is_invalid(slot.result):
            return None

        plain_next = Position(slot.id.id + 1, slot.id.track)
        if not (slot.is_duplicate_rare and isinstance(slot.original_result, str)):
            # 表格只標「同軌連續走來」會重複的格子。從別的路徑（必中落點、重複稀有跳格、
            # 換池）走到這格而上一隻就是這格的貓時，遊戲一樣會重抽，但表格沒有替換結果，
            # 無法預測；寧可不走這條路，也不要給出一條會在這裡分岔的計畫。
            if last_cat is not None and last_cat == slot.result:
                return None
            return _RollOutcome(slot.result, plain_next, False)

        if last_cat == slot.original_result:
            if not isinstance(slot.single_next_pos, Position):
                return None
            return _RollOutcome(slot.result, slot.single_next_pos, True)
        return _RollOutcome(slot.original_result, plain_next, False)

    def _get_slot(self, pos_id: int, pos_track: str, pool_id: str = "default") -> Optional[GachaSlot]:
        """從指定池取得格子資料。"""
        pool = self.pools.get(pool_id)
        if not pool:
            return None
        track = pool.track_a if pos_track == 'A' else pool.track_b
        if not track:
            return None
        return track.get_slot(pos_id)

    def _is_invalid(self, value) -> bool:
        return isinstance(value, ParseStatus)

    def _heuristic(self, collected: frozenset, target_cats: frozenset, target_count: int, current_pos: int) -> float:
        """位置感知的 admissible 啟發式估計。

        取以下兩者的最大值，兩者都是剩餘成本的下界：
        1. 未收集目標數量 × 每隻貓的最低成本（必中以 guaranteed_cost 拿 11 隻，所以不是 1）
        2. 到最遠「最近可得位置」的距離 × 每格的最低成本（重複稀有單抽可前進多格、
           必中從 B 軌出發前進 11 格，所以也不是 1）

        位置只會單調遞增，因此距離估計為合法下界。兩個係數都由 _scan_rules 從資料與池設定
        算出；寫死為 1 會高估，A* 就不再保證最優。
        相較單純計數，此估計在目標稀疏分布時能大幅收緊，減少展開狀態數。
        """
        uncollected = target_count - len(target_cats & collected)
        if uncollected == 0:
            return 0
        
        # 找出未收集目標中，離當前位置最遠的「最近出現位置」
        max_min_dist = 0
        for cat in target_cats:
            if cat in collected:
                continue
            positions = self._target_sorted_positions.get(cat)
            if not positions:
                continue
            idx = bisect.bisect_left(positions, current_pos)
            if idx < len(positions):
                dist = positions[idx] - current_pos
                if dist > max_min_dist:
                    max_min_dist = dist
        
        return max(uncollected * self._min_cost_per_cat, max_min_dist * self._min_cost_per_advance)

    def _precompute_target_data(self, target_cats: frozenset) -> Tuple[Dict[str, int], Dict[str, List[int]]]:
        """預計算目標貓咪的位置資料（單次掃描所有池的格子）。
        
        位置只會單調遞增（單抽 +1、保底跳至更後方），此特性供兩項剪枝使用：
        - cat_max_pos（可達性剪枝）：目標貓的最大位置 < 當前位置 → 不可達
        - target_sorted_positions（位置感知啟發式）：bisect 找出最近可得位置
        
        Returns:
            (cat_max_pos, target_sorted_positions)
        """
        cat_max_pos: Dict[str, int] = {cat: 0 for cat in target_cats}
        cat_pos_sets: Dict[str, set] = {cat: set() for cat in target_cats}
        
        for pool in self.pools.values():
            for track in [pool.track_a, pool.track_b]:
                if not track:
                    continue
                for slot in track:
                    pos_id = slot.id.id
                    # 重複稀有格沒被觸發時拿的是替換前的貓，所以 original_* 也算可得。
                    for value in (slot.result, slot.guaranteed, slot.original_result, slot.original_guaranteed):
                        if isinstance(value, str) and value in cat_max_pos:
                            if pos_id > cat_max_pos[value]:
                                cat_max_pos[value] = pos_id
                            cat_pos_sets[value].add(pos_id)
        
        target_sorted_positions = {cat: sorted(positions) for cat, positions in cat_pos_sets.items()}
        return cat_max_pos, target_sorted_positions

    def _reconstruct_path(self, state: SearchState, state_map: Dict[int, SearchState]) -> Tuple[GachaStep, ...]:
        """從終點狀態回溯重建完整路徑。
        
        Args:
            state: 終點狀態
            state_map: state_id -> SearchState 的映射
            
        Returns:
            從起點到終點的 GachaStep 序列
        """
        path = []
        current = state
        while current.last_step is not None:
            path.append(current.last_step)
            if current.parent_id is None:
                break
            current = state_map.get(current.parent_id)
            if current is None:
                break
        return tuple(reversed(path))

    def _actual_cost(self, path: Tuple[GachaStep, ...]) -> int:
        """從路徑算出實際抽數：單抽 1、必中依該池的 guaranteed_cost。

        搜尋用的 cost 在 prefer_guaranteed 時含單抽加權（1.11），只用來排序，不能當結果回報。
        """
        cost = 0
        for step in path:
            if step.is_guaranteed:
                pool = self.pools.get(step.pool_id or "default")
                cost += pool.guaranteed_cost if pool else 10
            else:
                cost += 1
        return cost

    def find_best_path(
        self, 
        start_pos: Position, 
        target_cats: Set[str], 
        max_rolls: int,
        progress_callback: Optional[Callable] = None,
        beam_width: Optional[int] = None,
        prefer_guaranteed: bool = False
    ) -> Optional[GachaResult]:
        """使用 A* 搜尋花費最小的抽卡路徑來收集所有目標貓咪。
        
        Args:
            start_pos: 起始位置
            target_cats: 目標貓咪集合
            max_rolls: 最大搜尋抽數
            progress_callback: 進度回調函數，每處理一個狀態時呼叫，
                              傳入參數為 (current_cost, states_processed, queue_size)
            beam_width: Beam Search 寬度限制。若設定，會限制佇列大小以控制記憶體使用。
                       設為 None 表示不限制（完整 A* 搜尋）。建議值：10000-50000。
                       注意：使用 Beam Search 可能無法找到最佳解。
            prefer_guaranteed: 是否偏好十連抽。若為 True，單抽會加上小懲罰 (1.11)，
                              讓演算法在成本相近時優先選擇十連抽。
        
        Returns:
            GachaResult 物件或 None（若找不到路徑）
        """
        target_cats_set = frozenset(target_cats)
        target_count = len(target_cats_set)
        
        # 處理空目標的特殊情況
        if not target_cats_set:
            return GachaResult(
                total_cost=0, 
                path=(), 
                collected=set(),
                start_pos=start_pos,
                target_cats=set()
            )
        
        # 區域計數器，避免全域狀態污染
        id_counter = itertools.count(1)
        
        # 預計算：目標貓咪位置資料（可達性剪枝 + 位置感知啟發式，單次掃描）
        cat_max_pos, self._target_sorted_positions = self._precompute_target_data(target_cats_set)
        
        # 驗證所有目標貓咪是否存在於搜索資料中（位置 1-based，max_pos=0 代表未出現）
        missing = {cat for cat, max_pos in cat_max_pos.items() if max_pos == 0}
        if missing:
            raise ValueError(
                f"以下目標貓咪不存在於任何池的搜索資料中：{', '.join(sorted(missing))}"
            )
        
        # 初始化: 將起點放入優先佇列 (Priority Queue)
        initial_collected = frozenset()
        h = self._heuristic(initial_collected, target_cats_set, target_count, start_pos.id)
        start_id = next(id_counter)
        start_state = SearchState(
            priority=0 + h,  # A*: f = g + h
            cost=0, 
            pos_id=start_pos.id, 
            pos_track=start_pos.track, 
            collected=initial_collected,
            state_id=start_id,
            parent_id=None,
            last_step=None
        )
        
        # 佇列元素：(f, -g, state_id, state)。f 相同時先展開 g 大的（離目標較近），
        # 再以 state_id 保證順序穩定，結果可重現。
        pq = [(start_state.priority, -start_state.cost, start_id, start_state)]
        # 支配性剪枝：(pos_id, pos_track, last_rolled_rare) -> [(cost, collected), ...]
        visited: Dict[tuple, list] = {}
        state_map: Dict[int, SearchState] = {start_id: start_state}  # 延遲路徑建構用
        states_processed = 0

        while pq:
            # A*: 取出 f 值最小的狀態
            _, _, _, state = heapq.heappop(pq)
            states_processed += 1
            
            # 進度回調
            if progress_callback:
                progress_callback(state.cost, states_processed, len(pq))

            # 目標檢查: 如果已收集的貓咪包含所有目標，這就是最佳解
            if target_cats_set <= state.collected:
                path = self._reconstruct_path(state, state_map)
                return GachaResult(
                    total_cost=self._actual_cost(path),
                    path=path,
                    collected=set(state.collected),
                    start_pos=start_pos,
                    target_cats=target_cats_set
                )

            # 超過最大抽卡次數，則略過
            if state.cost >= max_rolls:
                continue

            # 支配性剪枝 (Dominance Pruning):
            # 同位置若已存在「成本更低且收集更多（超集）」的已訪問狀態，
            # 當前狀態必定不優，可安全略過。
            # 相較精確匹配，支配性剪枝能合併 collected 為子集關係的狀態，
            # 大幅降低狀態空間（尤其目標貓咪數量多時）。
            pos_key = (state.pos_id, state.pos_track, state.last_rolled_rare)
            dominated = False
            if pos_key in visited:
                for v_cost, v_collected in visited[pos_key]:
                    if v_cost <= state.cost and v_collected >= state.collected:
                        dominated = True
                        break
            if dominated:
                continue
            # 加入已訪問，同時原地移除被當前狀態支配的舊項目
            if pos_key not in visited:
                visited[pos_key] = []
            else:
                entries = visited[pos_key]
                entries[:] = [
                    entry for entry in entries
                    if not (state.cost <= entry[0] and state.collected >= entry[1])
                ]
            visited[pos_key].append((state.cost, state.collected))

            # 可達性剪枝：位置只會單調遞增，若剩餘目標貓的最大位置 < 當前位置，不可達
            remaining = target_cats_set - state.collected
            if any(cat_max_pos.get(cat, 0) < state.pos_id for cat in remaining):
                continue

            # 展開下一步 (Branching): 對每個池嘗試單抽與十連抽
            single_roll_cost = 1.11 if prefer_guaranteed else 1
            for pool_id in self.pools:
                self._process_single_roll(state, target_cats_set, max_rolls, pq, state_map, pool_id, single_roll_cost, id_counter, target_count)
                self._process_guaranteed_roll(state, target_cats_set, max_rolls, pq, state_map, pool_id, id_counter, target_count)

            # Beam Search: 限制佇列大小以控制記憶體使用
            if beam_width is not None and len(pq) > beam_width * 2:
                # 只保留最好的 beam_width 個狀態
                pq = heapq.nsmallest(beam_width, pq)
                heapq.heapify(pq)
                
                # 清理 state_map: 只保留 pq 中狀態的祖先鏈
                needed_ids = self._collect_ancestor_ids([entry[3] for entry in pq], state_map)
                state_map = {k: v for k, v in state_map.items() if k in needed_ids}

        return None

    def _collect_ancestor_ids(self, states: List[SearchState], state_map: Dict[int, SearchState]) -> Set[int]:
        """收集 states 中所有狀態的祖先 ID。
        
        用於 Beam Search 時清理 state_map，只保留可能被 _reconstruct_path 使用的狀態。
        """
        needed_ids: Set[int] = set()
        for state in states:
            current_id = state.state_id
            while current_id is not None and current_id not in needed_ids:
                needed_ids.add(current_id)
                parent_state = state_map.get(current_id)
                current_id = parent_state.parent_id if parent_state else None
        return needed_ids


    def _process_single_roll(
        self, 
        state: SearchState, 
        target_cats: frozenset, 
        max_rolls: int, 
        pq: List,
        state_map: Dict[int, SearchState],
        pool_id: str = "default",
        single_roll_cost: float = 1,
        id_counter: Optional[itertools.count] = None,
        target_count: int = 0,
    ):
        """處理單次抽卡的分支邏輯。
        
        Args:
            state: 當前搜尋狀態
            target_cats: 目標貓咪集合（frozenset）
            max_rolls: 最大抽數限制
            pq: 優先佇列
            state_map: state_id -> SearchState 映射（延遲路徑建構用）
            pool_id: 要在哪個池抽
            single_roll_cost: 單抽的成本（預設 1，偏好十連抽時設為 1.11）
            id_counter: 區域狀態 ID 計數器
            target_count: 目標貓咪總數（避免重複計算 len）
        """
        slot = self._get_slot(state.pos_id, state.pos_track, pool_id)
        if not slot:
            return

        new_cost = state.cost + single_roll_cost
        if new_cost > max_rolls:
            return

        outcome = self._resolve_single_roll(slot, state.last_rolled_rare)
        if outcome is None:
            return
        actual_unit = outcome.unit
        next_pos = outcome.next_pos
        new_last_rare = self._remember(actual_unit)

        step = GachaStep(
            source=Position(state.pos_id, state.pos_track),
            target=next_pos,
            unit=actual_unit,
            pool_id=pool_id,
            is_guaranteed=False,
            is_track_switch=(next_pos.track != state.pos_track),
            is_duplicate_rare=outcome.is_duplicate_rare,
            context_data={'last_rolled_rare': state.last_rolled_rare}
        )

        new_collected = state.collected | {actual_unit} if actual_unit in target_cats else state.collected
        new_id = next(id_counter)
        h = self._heuristic(new_collected, target_cats, target_count, next_pos.id)
        
        new_state = SearchState(
            priority=new_cost + h,  # A*: f = g + h
            cost=new_cost,
            pos_id=next_pos.id,
            pos_track=next_pos.track,
            collected=new_collected,
            state_id=new_id,
            parent_id=state.state_id,
            last_step=step,
            last_rolled_rare=new_last_rare
        )
        
        state_map[new_id] = new_state
        heapq.heappush(pq, (new_state.priority, -new_state.cost, new_id, new_state))


    def _process_guaranteed_roll(
        self, 
        state: SearchState, 
        target_cats: frozenset, 
        max_rolls: int, 
        pq: List,
        state_map: Dict[int, SearchState],
        pool_id: str = "default",
        id_counter: Optional[itertools.count] = None,
        target_count: int = 0,
    ):
        """處理十連抽 (保底) 的分支邏輯。
        
        Args:
            state: 當前搜尋狀態
            target_cats: 目標貓咪集合（frozenset）
            max_rolls: 最大抽數限制
            pq: 優先佇列
            state_map: state_id -> SearchState 映射（延遲路徑建構用）
            pool_id: 要在哪個池抽
            id_counter: 區域狀態 ID 計數器
            target_count: 目標貓咪總數
        """
        pool = self.pools.get(pool_id)
        if not pool or not pool.has_guaranteed:
            return  # 該池沒有必中選項
            
        new_cost = state.cost + pool.guaranteed_cost
        if new_cost > max_rolls:
            return

        slot = self._get_slot(state.pos_id, state.pos_track, pool_id)
        if not slot:
            return

        collected_in_roll = set()
        last_cat = state.last_rolled_rare
        pos = Position(state.pos_id, state.pos_track)
        start_triggered_dup = False

        # 前 10 抽照單抽規則走：遇到重複稀有會換軌並多前進，不能假設一直在同一軌的 pos+i。
        for i in range(10):
            s = self._get_slot(pos.id, pos.track, pool_id)
            if not s:
                return  # 資料不完整，無法執行保底模擬
            outcome = self._resolve_single_roll(s, last_cat)
            if outcome is None:
                return
            if i == 0:
                start_triggered_dup = outcome.is_duplicate_rare
            if outcome.unit in target_cats:
                collected_in_roll.add(outcome.unit)
            last_cat = outcome.unit
            pos = outcome.next_pos

        # 表格的必中欄是以起始格自己的重複狀態算出來的：
        # 起始格是重複稀有格但沒被觸發時，要用替換前的版本。
        if (slot.is_duplicate_rare and not start_triggered_dup
                and slot.original_guaranteed is not None
                and isinstance(slot.original_next_pos, Position)):
            gua_unit, landing = slot.original_guaranteed, slot.original_next_pos
        else:
            gua_unit, landing = slot.guaranteed, slot.next_pos
        if self._is_invalid(gua_unit) or not isinstance(landing, Position):
            return

        # 第 11 隻是必中超激，它成為「上一隻」，因此重複判定在這之後重置。
        # 此為依規則推得的假設，尚無資料可獨立驗證。
        if gua_unit in target_cats:
            collected_in_roll.add(gua_unit)

        new_collected = state.collected | frozenset(collected_in_roll)

        step = GachaStep(
            source=Position(state.pos_id, state.pos_track),
            target=landing,
            unit=gua_unit,
            pool_id=pool_id,
            is_guaranteed=True,
            # 前 10 抽內發生奇數次重複稀有時，必中後會留在原軌道（例如 1B → 12B）。
            is_track_switch=(landing.track != state.pos_track),
            is_duplicate_rare=False,  # 保底本身不是重複稀有
            context_data={'collected_units': list(collected_in_roll)}
        )

        new_id = next(id_counter)
        h = self._heuristic(new_collected, target_cats, target_count, landing.id)

        new_state = SearchState(
            priority=new_cost + h,  # A*: f = g + h
            cost=new_cost,
            pos_id=landing.id,
            pos_track=landing.track,
            collected=new_collected,
            state_id=new_id,
            parent_id=state.state_id,
            last_step=step,
            last_rolled_rare=self._remember(gua_unit)
        )
        
        state_map[new_id] = new_state
        heapq.heappush(pq, (new_state.priority, -new_state.cost, new_id, new_state))
