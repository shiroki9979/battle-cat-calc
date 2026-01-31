import heapq
from typing import List, Optional, Set, Tuple, Dict
from dataclasses import dataclass, field
from .models import Track, GachaSlot, Position, ParseStatus

@dataclass(frozen=True, order=True)
class SearchState:
    # Cost: 目前累積的抽卡次數 (優先佇列的排序依據)
    cost: int
    # Position: 目前所在的格子 ID 與 軌道 (A/B)
    pos_id: int = field(compare=False)
    pos_track: str = field(compare=False)
    # Collected: 目前已收集到的目標貓咪 (使用 tuple 以便進行 hash)
    collected: Tuple[str, ...] = field(compare=False)  # Using tuple for hashing
    path: Tuple[Tuple[str, Position, Set[str]], ...] = field(compare=False, default=())

class GachaSimulator:
    """基於 Track 資料的抽卡路徑模擬器。"""
    
    def __init__(self, track_a: Track, track_b: Track):
        self.tracks = {
            'A': track_a,
            'B': track_b
        }

    def _get_slot(self, pos_id: int, pos_track: str) -> Optional[GachaSlot]:
        track = self.tracks.get(pos_track)
        if not track:
            return None
        return track.get_slot(pos_id)

    def _is_invalid(self, value) -> bool:
        return isinstance(value, ParseStatus)

    def find_best_path(self, start_pos: Position, target_cats: Set[str], max_rolls: int) -> Optional[Dict]:
        """使用 Dijkstra 搜尋花費最小的抽卡路徑來收集所有目標貓咪。"""
        # 初始化: 將起點放入優先佇列 (Priority Queue)
        # Dijkstra 會自動保證我們總是先處理「目前花費最小」的路徑
        start_state = SearchState(
            cost=0, 
            pos_id=start_pos.id, 
            pos_track=start_pos.track, 
            collected=()
        )
        
        pq = [start_state]
        visited = {} # (pos_id, pos_track, collected_tuple) -> min_cost
        target_cats = set(target_cats)

        while pq:
            # 取出最優解: 從佇列中取出 Cost 最小的狀態
            state = heapq.heappop(pq)

            # 目標檢查: 如果已收集的貓咪包含所有目標，這就是最佳解
            if set(target_cats).issubset(set(state.collected)):
                return {
                    "total_cost": state.cost,
                    "path": state.path,
                    "collected": set(state.collected)
                }

            # 超過最大抽卡次數，則略過
            if state.cost >= max_rolls:
                continue

            # 剪枝 (Pruning): 
            # 如果我們曾經用「更少或相同」的花費到達過「同位置且收集到同貓咪」的狀態，
            # 則當前這條路徑是多餘的，直接略過。
            state_key = (state.pos_id, state.pos_track, state.collected)
            if state_key in visited and visited[state_key] <= state.cost:
                continue
            visited[state_key] = state.cost

            # 展開下一步 (Branching): 嘗試單抽與十連抽
            self._process_single_roll(state, target_cats, max_rolls, pq)
            self._process_guaranteed_roll(state, target_cats, max_rolls, pq)

        return None

    def _process_single_roll(self, state: SearchState, target_cats: Set[str], max_rolls: int, pq: List):
        """處理單次抽卡的分支邏輯。"""
        slot = self._get_slot(state.pos_id, state.pos_track)
        if not slot or self._is_invalid(slot.result):
            return

        new_cost = state.cost + 1
        if new_cost > max_rolls:
            return

        current_collected = set(state.collected)
        unit = str(slot.result)
        if unit in target_cats:
            current_collected.add(unit)

        # 單抽通常會前進到同軌道的下一格 (需確認是否有跳軌機制)
        # 假設一般情況是前進 1 格，維持同軌道
        next_pos = Position(state.pos_id + 1, state.pos_track)
        
        # 注意: GachaSlot.next_pos 通常來自保底欄位 (Guaranteed Column)，
        # 代表保底後的去向。單抽通常不使用此欄位，除非有特殊機制。
        # 在此實作中，暫時假定單抽不使用 next_pos 跳軌。


        new_path = state.path + (("Single", Position(state.pos_id, state.pos_track), {unit}),)
        
        heapq.heappush(pq, SearchState(
            cost=new_cost,
            pos_id=next_pos.id,
            pos_track=next_pos.track,
            collected=tuple(sorted(list(current_collected))),
            path=new_path
        ))

    def _process_guaranteed_roll(self, state: SearchState, target_cats: Set[str], max_rolls: int, pq: List):
        """處理十連抽 (保底) 的分支邏輯。"""
        new_cost = state.cost + 10
        if new_cost > max_rolls:
            return

        slot = self._get_slot(state.pos_id, state.pos_track)
        # 檢查起始格是否有效且包含必要的保底/跳軌資訊
        if not slot or self._is_invalid(slot.guaranteed) or not isinstance(slot.next_pos, Position):
            return

        collected_in_roll = set()
        
        # 檢查接下來 10 格的貓咪
        for i in range(10):
            s = self._get_slot(state.pos_id + i, state.pos_track)
            if not s or self._is_invalid(s.result):
                return # 資料不完整，無法執行保底模擬
            unit = str(s.result)
            if unit in target_cats:
                collected_in_roll.add(unit)
        
        # 加入保底貓咪
        gua_unit = str(slot.guaranteed)
        if gua_unit in target_cats:
            collected_in_roll.add(gua_unit)
            
        current_collected = set(state.collected).union(collected_in_roll)
        
        new_path = state.path + (("Guaranteed", Position(state.pos_id, state.pos_track), collected_in_roll),)
        
        heapq.heappush(pq, SearchState(
            cost=new_cost,
            pos_id=slot.next_pos.id,
            pos_track=slot.next_pos.track,
            collected=tuple(sorted(list(current_collected))),
            path=new_path
        ))
