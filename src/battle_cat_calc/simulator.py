import heapq
from typing import List, Optional, Set, Tuple, Dict, Union, Callable
from dataclasses import dataclass, field
from .models import Track, GachaSlot, Position, ParseStatus, GachaStep, GachaPool, GachaResult

# 全域計數器用於生成唯一狀態 ID
_state_counter = 0

def _next_state_id() -> int:
    global _state_counter
    _state_counter += 1
    return _state_counter

@dataclass(frozen=True, order=True)
class SearchState:
    """A* 搜尋的狀態節點（延遲路徑建構版）。
    
    使用 priority (f = g + h) 作為排序依據。
    路徑透過 parent_id + last_step 延遲建構，減少記憶體使用。
    """
    # Priority: A* 的 f 值 (優先佇列的排序依據)
    priority: int
    # Cost: 目前累積的抽卡次數 (g 值)
    cost: int = field(compare=False)
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

    def _heuristic(self, collected: frozenset, target_cats: Set[str]) -> int:
        """估計從當前狀態到達目標的最小成本。
        
        使用樂觀估計：還缺幾隻貓就至少需要幾抽。
        這保證了 A* 的最優性（admissible heuristic）。
        """
        remaining = len(target_cats - collected)
        return remaining

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
        progress_callback: Optional[Callable] = None
    ) -> Optional[GachaResult]:
        """使用 A* 搜尋花費最小的抽卡路徑來收集所有目標貓咪。
        
        Args:
            start_pos: 起始位置
            target_cats: 目標貓咪集合
            max_rolls: 最大搜尋抽數
            progress_callback: 進度回調函數，每處理一個狀態時呼叫，
                              傳入參數為 (current_cost, states_processed, queue_size)
        
        Returns:
            GachaResult 物件或 None（若找不到路徑）
        """
        target_cats_set = set(target_cats)
        
        # 處理空目標的特殊情況
        if not target_cats_set:
            return GachaResult(
                total_cost=0, 
                path=(), 
                collected=set(),
                start_pos=start_pos,
                target_cats=set()
            )
        
        # 初始化: 將起點放入優先佇列 (Priority Queue)
        # A* 使用 f = g + h 作為優先級
        initial_collected = frozenset()
        initial_h = self._heuristic(initial_collected, target_cats_set)
        start_id = _next_state_id()
        start_state = SearchState(
            priority=0 + initial_h,  # f = g + h
            cost=0, 
            pos_id=start_pos.id, 
            pos_track=start_pos.track, 
            collected=initial_collected,
            state_id=start_id,
            parent_id=None,
            last_step=None
        )
        
        pq = [start_state]
        visited = {}  # (pos_id, pos_track, collected, last_rolled_rare) -> min_cost
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

            # 剪枝 (Pruning): 
            # 如果曾經用「更少或相同」的花費到達過「同位置且收集到同貓咪」的狀態，
            # 則當前這條路徑是多餘的，直接略過。
            # 注意：多池模式下，last_rolled_rare 也影響狀態唯一性
            state_key = (state.pos_id, state.pos_track, state.collected, state.last_rolled_rare)
            if state_key in visited and visited[state_key] <= state.cost:
                continue
            visited[state_key] = state.cost

            # 展開下一步 (Branching): 對每個池嘗試單抽與十連抽
            for pool_id in self.pools:
                self._process_single_roll(state, target_cats, max_rolls, pq, state_map, pool_id)
                self._process_guaranteed_roll(state, target_cats, max_rolls, pq, state_map, pool_id)

        return None

    def _process_single_roll(
        self, 
        state: SearchState, 
        target_cats: Set[str], 
        max_rolls: int, 
        pq: List,
        state_map: Dict[int, SearchState],
        pool_id: str = "default"
    ):
        """處理單次抽卡的分支邏輯。
        
        Args:
            state: 當前搜尋狀態
            target_cats: 目標貓咪集合
            max_rolls: 最大抽數限制
            pq: 優先佇列
            state_map: state_id -> SearchState 映射（延遲路徑建構用）
            pool_id: 要在哪個池抽
        """
        slot = self._get_slot(state.pos_id, state.pos_track, pool_id)
        if not slot or self._is_invalid(slot.result):
            return

        new_cost = state.cost + 1
        if new_cost > max_rolls:
            return

        unit = str(slot.result)
        
        # 檢查是否觸發重複稀有（目前假設 is_duplicate_rare 由 parser 標記）
        is_dup_rare = slot.is_duplicate_rare and (state.last_rolled_rare == slot.original_result)
        
        # 決定實際獲得的貓咪與下一個位置
        if is_dup_rare and slot.next_pos and isinstance(slot.next_pos, Position):
            # 重複稀有觸發：使用替換結果，跳軌
            next_pos = slot.next_pos
            actual_unit = unit  # slot.result 已經是替換後的結果
        else:
            # 正常單抽：前進到同軌道下一格
            next_pos = Position(state.pos_id + 1, state.pos_track)
            actual_unit = unit
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
        new_h = self._heuristic(new_collected, target_cats)
        new_id = _next_state_id()
        
        new_state = SearchState(
            priority=new_cost + new_h,  # f = g + h
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
        target_cats: Set[str], 
        max_rolls: int, 
        pq: List,
        state_map: Dict[int, SearchState],
        pool_id: str = "default"
    ):
        """處理十連抽 (保底) 的分支邏輯。
        
        Args:
            state: 當前搜尋狀態
            target_cats: 目標貓咪集合
            max_rolls: 最大抽數限制
            pq: 優先佇列
            state_map: state_id -> SearchState 映射（延遲路徑建構用）
            pool_id: 要在哪個池抽
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
        
        # 檢查接下來 10 格的貓咪（不含保底）
        for i in range(10):
            s = self._get_slot(state.pos_id + i, state.pos_track, pool_id)
            if not s or self._is_invalid(s.result):
                return # 資料不完整，無法執行保底模擬
            unit = str(s.result)
            if unit in target_cats:
                collected_in_roll.add(unit)
            # 更新最後的稀有貓記錄（簡化判斷）
            if s.is_duplicate_rare or s.original_result:
                last_rare_in_roll = unit
        
        # 加入保底貓咪（保底是超激，不影響重複稀有判定）
        gua_unit = str(slot.guaranteed)
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

        new_h = self._heuristic(new_collected, target_cats)
        new_id = _next_state_id()
        
        # 保底後的 last_rolled_rare：使用最後一格（第 10 格）的結果
        # 待驗證：是否保底超激會重置此記錄
        new_state = SearchState(
            priority=new_cost + new_h,  # f = g + h
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
