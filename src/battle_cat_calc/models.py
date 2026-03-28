from dataclasses import dataclass
from typing import Optional, Tuple, Union, List, Dict, Set
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
    id: Position  # Changed from Tuple[int, str] to Position
    result: Union[str, ParseStatus]
    guaranteed: Union[str, ParseStatus]
    next_pos: Optional[Union[Position, ParseStatus]] = None
    original_result: Optional[str] = None
    original_guaranteed: Optional[str] = None
    is_duplicate_rare: bool = False

class Track(UserList):
    """包含一系列 GachaSlots 的專業容器。
    
    繼承自 UserList 以提供完整的列表功能，同時允許自定義元數據和輔助方法。
    """
    def __init__(self, name: str, slots: List[GachaSlot] = None):
        super().__init__(slots or [])
        self.name = name
        # 建立 id → slot 的索引，將 get_slot 從 O(n) 降為 O(1)
        self._index: Dict[int, GachaSlot] = {s.id.id: s for s in self.data}

    def __repr__(self):
        return f"Track(name='{self.name}', slots={len(self.data)})"

    def get_slot(self, index: int) -> Optional[GachaSlot]:
        """傳回指定索引 (1-based ID) 的格子。O(1) 查找。"""
        return self._index.get(index)


@dataclass
class GachaPool:
    """代表一個轉蛋活動池。
    
    封裝單一池的 Track A/B 及必中設定，用於跨池路徑規劃。
    """
    pool_id: str              # 池子識別碼 (e.g., "ultra_souls")
    name: str                 # 顯示名稱 (e.g., "超靈魂祭")
    track_a: Track
    track_b: Track
    has_guaranteed: bool = True  # 是否有必中選項
    guaranteed_cost: int = 10    # 必中的抽數成本（十連 = 10 抽）


@dataclass
class GachaStep:
    """描述從一個位置移動到下一個位置的過程。"""
    source: Position
    target: Position
    unit: str
    pool_id: str = ""              # 在哪個池抽的
    is_guaranteed: bool = False
    is_track_switch: bool = False
    is_duplicate_rare: bool = False  # 是否因重複稀有跳軌
    context_data: Optional[dict] = None


@dataclass
class GachaResult:
    """模擬結果的結構化封裝。
    
    提供便利方法來分析和展示路徑規劃結果。
    """
    total_cost: int
    path: Tuple['GachaStep', ...]
    collected: Set[str]
    start_pos: Optional[Position] = None
    target_cats: Optional[Set[str]] = None
    
    @property
    def step_count(self) -> int:
        """路徑中的步數。"""
        return len(self.path)
    
    @property
    def guaranteed_count(self) -> int:
        """十連抽的次數。"""
        return sum(1 for step in self.path if step.is_guaranteed)
    
    @property
    def single_roll_count(self) -> int:
        """單抽的次數。"""
        return sum(1 for step in self.path if not step.is_guaranteed)
    
    @property
    def pools_used(self) -> Set[str]:
        """使用到的池子 ID 集合。"""
        return {step.pool_id for step in self.path if step.pool_id}
    
    @property
    def end_pos(self) -> Optional[Position]:
        """結束位置。"""
        return self.path[-1].target if self.path else self.start_pos
    
    def steps_by_pool(self) -> Dict[str, List['GachaStep']]:
        """依池子分組的步驟。"""
        result: Dict[str, List[GachaStep]] = {}
        for step in self.path:
            pool_id = step.pool_id or "default"
            if pool_id not in result:
                result[pool_id] = []
            result[pool_id].append(step)
        return result
    
    def to_summary(self) -> str:
        """生成易讀的摘要字串。"""
        lines = [
            f"=== 模擬結果 ===",
            f"總花費: {self.total_cost} 抽",
            f"路徑步數: {self.step_count} (單抽 {self.single_roll_count}, 十連 {self.guaranteed_count})",
            f"收集到: {', '.join(sorted(self.collected))}",
            f"使用池子: {', '.join(sorted(self.pools_used)) or 'default'}",
            f"起點: {self.start_pos} → 終點: {self.end_pos}",
            "",
            "=== 路徑詳情 ===",
        ]
        for i, step in enumerate(self.path, 1):
            step_type = "十連" if step.is_guaranteed else "單抽"
            switch = " (跳軌)" if step.is_track_switch else ""
            dup = " (重複稀有)" if step.is_duplicate_rare else ""
            lines.append(
                f"{i}. [{step.pool_id or 'default'}] {step.source.id}{step.source.track} → "
                f"{step.target.id}{step.target.track}: {step.unit} ({step_type}){switch}{dup}"
            )
        return "\n".join(lines)
    
    def to_dict(self) -> Dict:
        """轉換為可序列化的字典。"""
        return {
            "total_cost": self.total_cost,
            "step_count": self.step_count,
            "guaranteed_count": self.guaranteed_count,
            "single_roll_count": self.single_roll_count,
            "collected": list(sorted(self.collected)),
            "pools_used": list(sorted(self.pools_used)),
            "start_pos": {"id": self.start_pos.id, "track": self.start_pos.track} if self.start_pos else None,
            "end_pos": {"id": self.end_pos.id, "track": self.end_pos.track} if self.end_pos else None,
            "path": [
                {
                    "source": {"id": s.source.id, "track": s.source.track},
                    "target": {"id": s.target.id, "track": s.target.track},
                    "unit": s.unit,
                    "pool_id": s.pool_id,
                    "is_guaranteed": s.is_guaranteed,
                    "is_track_switch": s.is_track_switch,
                }
                for s in self.path
            ]
        }
    
    def __repr__(self) -> str:
        return f"GachaResult(cost={self.total_cost}, steps={self.step_count}, collected={len(self.collected)})"

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
