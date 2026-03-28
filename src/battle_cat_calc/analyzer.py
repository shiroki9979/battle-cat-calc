from typing import List, Optional
from .models import GachaSlot, Track, ParseStatus

class SeedAnalyzer:
    """分析已解析的轉蛋資料。"""
    
    def __init__(self, slots: Track):
        self.slots = slots

    def find_cat_by_name(self, name: str) -> List[GachaSlot]:
        """依名稱搜尋所有符合的格子，跳過 ParseStatus 類型的結果。"""
        results = []
        for slot in self.slots:
            if isinstance(slot.result, ParseStatus):
                continue
            if name in slot.result:
                results.append(slot)
        return results

    def get_next_guaranteed(self) -> Optional[GachaSlot]:
        """取得下一個可用的保底格子。"""
        for slot in self.slots:
            if slot.guaranteed and not isinstance(slot.guaranteed, ParseStatus):
                return slot
        return None
