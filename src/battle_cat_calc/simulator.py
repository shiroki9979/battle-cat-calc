import heapq
from typing import List, Optional, Set, Tuple, Dict
from dataclasses import dataclass, field
from .models import Track, GachaSlot, Position, ParseStatus

@dataclass(frozen=True, order=True)
class SearchState:
    cost: int
    pos_id: int = field(compare=False)
    pos_track: str = field(compare=False)
    collected: Tuple[str, ...] = field(compare=False)  # Using tuple for hashing
    path: Tuple[Tuple[str, Position, Set[str]], ...] = field(compare=False, default=())

class GachaSimulator:
    """基於 Track 資料的抽卡路徑模擬器"""
    
    def __init__(self, track_a: Track, track_b: Track):
        self.tracks = {
            'A': track_a,
            'B': track_b
        }

    def _get_slot(self, pos_id: int, pos_track: str) -> Optional[GachaSlot]:
        track = self.tracks.get(pos_track)
        if not track:
            return None
        # Track is a UserList of GachaSlots. slots IDs are 1-based usually.
        # We use Track.get_slot(index) which looks for slot.id[0] == index.
        return track.get_slot(pos_id)

    def _is_invalid(self, value) -> bool:
        return isinstance(value, ParseStatus)

    def find_best_path(self, start_pos: Position, target_cats: Set[str], max_rolls: int) -> Optional[Dict]:
        """使用 Dijkstra 搜尋花費最小的抽卡路徑來收集所有目標貓咪"""
        
        # Priority Queue: (cost, current_id, current_track, collected_tuple, path_tuple)
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
            state = heapq.heappop(pq)
            # 檢查是否已達成目標
            current_collected_set = set(state.collected)
            if target_cats.issubset(current_collected_set):
                return {
                    "total_cost": state.cost,
                    "path": state.path,
                    "collected": current_collected_set
                }

            if state.cost >= max_rolls:
                continue

            state_key = (state.pos_id, state.pos_track, tuple(sorted(list(current_collected_set))))
            if state_key in visited and visited[state_key] <= state.cost:
                continue
            visited[state_key] = state.cost

            # --- 分支 1: 單抽 ---
            slot = self._get_slot(state.pos_id, state.pos_track)
            # if slot and not self._is_invalid(slot.result):
            #     new_collected = current_collected_set.copy()
            #     unit = str(slot.result)
            #     if unit in target_cats:
            #         new_collected.add(unit)
                
            #     new_cost = state.cost + 1
            #     if new_cost <= max_rolls:
            #         new_path = state.path + (("Single", Position(state.pos_id, state.pos_track), {unit}),)
            #         heapq.heappush(pq, SearchState(
            #             cost=new_cost,
            #             pos_id=state.pos_id + 1,
            #             pos_track=state.pos_track,
            #             collected=tuple(sorted(list(new_collected))),
            #             path=new_path
            #         ))

            # --- 分支 2: 十連抽 (保底) ---
            # 取得接下來 10 個結果
            is_guaranteed_valid = True
            guaranteed_units = set()
            
            # Check current slot for guaranteed and next_pos
            if not slot or self._is_invalid(slot.guaranteed) or not isinstance(slot.next_pos, Position):
                is_guaranteed_valid = False
            else:
                for i in range(10):
                    s = self._get_slot(state.pos_id + i, state.pos_track)
                    if not s or self._is_invalid(s.result):
                        is_guaranteed_valid = False
                        break
                    unit = str(s.result)
                    if unit in target_cats:
                        guaranteed_units.add(unit)
                
                if is_guaranteed_valid:
                    # 加入保底貓咪
                    gua_unit = str(slot.guaranteed)
                    if gua_unit in target_cats:
                        guaranteed_units.add(gua_unit)
                    
                    new_collected = current_collected_set.union(guaranteed_units)
                    new_cost = state.cost + 10
                    if new_cost <= max_rolls:
                        new_path = state.path + (("Guaranteed", Position(state.pos_id, state.pos_track), guaranteed_units),)
                        heapq.heappush(pq, SearchState(
                            cost=new_cost,
                            pos_id=slot.next_pos.id,
                            pos_track=slot.next_pos.track,
                            collected=tuple(sorted(list(new_collected))),
                            path=new_path
                        ))

        return None
