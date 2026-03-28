import bisect
import heapq
import itertools
from typing import List, Optional, Set, Tuple, Dict, Union, Callable
from dataclasses import dataclass, field
from .models import Track, GachaSlot, Position, ParseStatus, GachaStep, GachaPool, GachaResult

@dataclass(frozen=True, order=True, slots=True)
class SearchState:
    """A* 搜尋的狀態節點（延遲路徑建構版）。
    
    使用 priority (f = g + h) 作為排序依據。
    路徑透過 parent_id + last_step 延遲建構，減少記憶體使用。
    slots=True 減少約 30-40% 記憶體佔用並加速屬性存取。
    """
    # Priority: A* 的 f 值 (優先佇列的排序依據)
    priority: float
    # Cost: 目前累積的抽卡次數 (g 值)
    cost: float = field(compare=False)
    # Position: 目前所在的格子 ID 與 軌道 (A/B)
    pos_id: int = field(compare=False)
    pos_track: str = field(compare=False)
    # Collected: 目前已收集到的目標貓咪 (使用 frozenset 以便進行 hash)
    collected: frozenset = field(compare=False)
    # 延遲路徑建構：記錄父狀態 ID 和這一步的資訊
    state_id: int = field(compare=False, default=0)
    parent_id: Optional[int] = field(compare=False, default=None)
    last_step: Optional[GachaStep] = field(compare=False, default=None)
    # 追蹤上一抽的稀有貓名稱，用於跨池重複稀有判定
    last_rolled_rare: Optional[str] = field(compare=False, default=None)

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
        
        取以下兩者的最大值：
        1. 未收集目標數量（每抽至多收集一隻）
        2. 到最遠「最近可得位置」的距離 × 每位置最低成本
        
        位置只會單調遞增，因此距離估計為合法下界，保證 A* 最優性。
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
        
        return max(uncollected, max_min_dist * self._min_cost_per_advance)

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
                    if isinstance(slot.result, str) and slot.result in cat_max_pos:
                        cat = slot.result
                        if pos_id > cat_max_pos[cat]:
                            cat_max_pos[cat] = pos_id
                        cat_pos_sets[cat].add(pos_id)
                    if isinstance(slot.guaranteed, str) and slot.guaranteed in cat_max_pos:
                        cat = slot.guaranteed
                        if pos_id > cat_max_pos[cat]:
                            cat_max_pos[cat] = pos_id
                        cat_pos_sets[cat].add(pos_id)
        
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
            prefer_guaranteed: 是否偏好十連抽。若為 True，單抽會加上小懲罰 (1.05)，
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
        
        self._min_cost_per_advance = min(
            1.0,
            min((pool.guaranteed_cost / 10 for pool in self.pools.values() if pool.has_guaranteed), default=1.0)
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
        
        pq = [start_state]
        # 支配性剪枝：(pos_id, pos_track, last_rolled_rare) -> [(cost, collected), ...]
        visited: Dict[tuple, list] = {}
        state_map: Dict[int, SearchState] = {start_id: start_state}  # 延遲路徑建構用
        states_processed = 0

        while pq:
            # A*: 取出 f 值最小的狀態
            state = heapq.heappop(pq)
            states_processed += 1
            
            # 進度回調
            if progress_callback:
                progress_callback(state.cost, states_processed, len(pq))

            # 目標檢查: 如果已收集的貓咪包含所有目標，這就是最佳解
            if target_cats_set <= state.collected:
                return GachaResult(
                    total_cost=state.cost,
                    path=self._reconstruct_path(state, state_map),
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
                needed_ids = self._collect_ancestor_ids(pq, state_map)
                state_map = {k: v for k, v in state_map.items() if k in needed_ids}

        return None

    def _collect_ancestor_ids(self, pq: List[SearchState], state_map: Dict[int, SearchState]) -> Set[int]:
        """收集 pq 中所有狀態的祖先 ID。
        
        用於 Beam Search 時清理 state_map，只保留可能被 _reconstruct_path 使用的狀態。
        """
        needed_ids: Set[int] = set()
        for state in pq:
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
            single_roll_cost: 單抽的成本（預設 1，偏好十連抽時設為 1.05）
            id_counter: 區域狀態 ID 計數器
            target_count: 目標貓咪總數（避免重複計算 len）
        """
        slot = self._get_slot(state.pos_id, state.pos_track, pool_id)
        if not slot or self._is_invalid(slot.result):
            return

        new_cost = state.cost + single_roll_cost
        if new_cost > max_rolls:
            return

        actual_unit = slot.result  # _is_invalid 已排除 ParseStatus，此處必為 str
        
        # 檢查是否觸發重複稀有（目前假設 is_duplicate_rare 由 parser 標記）
        is_dup_rare = slot.is_duplicate_rare and (state.last_rolled_rare == slot.original_result)
        
        # 決定實際獲得的貓咪與下一個位置
        if is_dup_rare and slot.next_pos and isinstance(slot.next_pos, Position):
            # 重複稀有觸發：使用替換結果，跳軌
            next_pos = slot.next_pos
        else:
            # 正常單抽：前進到同軌道下一格
            next_pos = Position(state.pos_id + 1, state.pos_track)
            is_dup_rare = False  # 確保未觸發時為 False

        # 更新 last_rolled_rare：只有稀有貓會更新此欄位
        # 簡化判斷：若 slot 有 original_result，代表這是稀有貓替換情境
        new_last_rare = actual_unit if slot.is_duplicate_rare or slot.original_result else state.last_rolled_rare

        step = GachaStep(
            source=Position(state.pos_id, state.pos_track),
            target=next_pos,
            unit=actual_unit,
            pool_id=pool_id,
            is_guaranteed=False,
            is_track_switch=(next_pos.track != state.pos_track),
            is_duplicate_rare=is_dup_rare,
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
        heapq.heappush(pq, new_state)


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
        # 檢查起始格是否有效且包含必要的保底/跳軌資訊
        if not slot or self._is_invalid(slot.guaranteed) or not isinstance(slot.next_pos, Position):
            return

        collected_in_roll = set()
        last_rare_in_roll = state.last_rolled_rare  # 追蹤十連中最後的稀有貓
        
        # 迴圈外取得 track 物件，避免每次迭代都重複做 pool/track 查找
        track = pool.track_a if state.pos_track == 'A' else pool.track_b
        
        # 檢查接下來 10 格的貓咪（不含保底）
        for i in range(10):
            s = track.get_slot(state.pos_id + i)
            if not s or self._is_invalid(s.result):
                return # 資料不完整，無法執行保底模擬
            unit = s.result  # _is_invalid 已排除 ParseStatus
            if unit in target_cats:
                collected_in_roll.add(unit)
            # 更新最後的稀有貓記錄（簡化判斷）
            if s.is_duplicate_rare or s.original_result:
                last_rare_in_roll = unit
        
        # 加入保底貓咪（保底是超激，不影響重複稀有判定）
        gua_unit = slot.guaranteed  # 已確認非 ParseStatus
        if gua_unit in target_cats:
            collected_in_roll.add(gua_unit)
            
        new_collected = state.collected | frozenset(collected_in_roll)

        step = GachaStep(
            source=Position(state.pos_id, state.pos_track),
            target=slot.next_pos,
            unit=gua_unit,
            pool_id=pool_id,
            is_guaranteed=True,
            is_track_switch=True,
            is_duplicate_rare=False,  # 保底本身不是重複稀有
            context_data={'collected_units': list(collected_in_roll)}
        )

        new_id = next(id_counter)
        h = self._heuristic(new_collected, target_cats, target_count, slot.next_pos.id)
        
        # 保底後的 last_rolled_rare：使用最後一格（第 10 格）的結果
        # 待驗證：是否保底超激會重置此記錄
        new_state = SearchState(
            priority=new_cost + h,  # A*: f = g + h
            cost=new_cost,
            pos_id=slot.next_pos.id,
            pos_track=slot.next_pos.track,
            collected=new_collected,
            state_id=new_id,
            parent_id=state.state_id,
            last_step=step,
            last_rolled_rare=last_rare_in_roll  # 保留最後稀有貓記錄
        )
        
        state_map[new_id] = new_state
        heapq.heappush(pq, new_state)
