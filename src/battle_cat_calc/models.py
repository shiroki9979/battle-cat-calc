from dataclasses import dataclass
from typing import Optional, Tuple, Union, List
from enum import Enum
from collections import UserList

class ParseStatus(Enum):
    """代表解析過程中的特殊狀態。"""
    MULTIPLE_DATA = "Multiple Data Found"
    INVALID_DATA = "Invalid Data"
    AMBIGUOUS = "Ambiguous Position"

    def __str__(self):
        return self.value

@dataclass
class Position:
    """代表一個轉蛋軌道的位置。"""
    id: int
    track: str

@dataclass
class GachaSlot:
    """代表轉蛋列表中的單一個格子 (A 或 B 軌)。"""
    id: Tuple[int, str]  # (number, track)
    result: Union[str, ParseStatus]
    guaranteed: Union[str, ParseStatus]
    next_pos: Optional[Union[Position, ParseStatus]] = None

class Track(UserList):
    """包含一系列 GachaSlots 的專業容器。
    
    繼承自 UserList 以提供完整的列表功能，同時允許自定義元數據和輔助方法。
    """
    def __init__(self, name: str, slots: List[GachaSlot] = None):
        super().__init__(slots or [])
        self.name = name

    def __repr__(self):
        return f"Track(name='{self.name}', slots={len(self.data)})"

    def get_slot(self, index: int) -> Optional[GachaSlot]:
        """傳回指定索引 (1-based ID) 的格子。"""
        for slot in self.data:
            if slot.id[0] == index:
                return slot
        return None

@dataclass
class GachaStep:
    """描述從一個位置移動到下一個位置的過程"""
    source: Position
    target: Position
    unit: str
    is_guaranteed: bool = False
    is_track_switch: bool = False

class GachaNode:
    """抽卡模擬樹的節點，以 Track 位置為核心"""
    def __init__(self, position: Position, slot_data: Optional[GachaSlot] = None):
        self.position = position  # 例如 Posiion(id=1, track='A')
        self.slot_data = slot_data # 該位置的詳細資訊（貓咪名稱等）
        self.children: List['GachaNode'] = []
        self.parent: Optional['GachaNode'] = None
        self.step_from_parent: Optional[GachaStep] = None

    def add_child(self, child_node: 'GachaNode', step: GachaStep):
        child_node.parent = self
        child_node.step_from_parent = step
        self.children.append(child_node)

    def __repr__(self):
        return f"GachaNode({self.position.id}{self.position.track}, unit={self.slot_data.result if self.slot_data else 'Unknown'})"
