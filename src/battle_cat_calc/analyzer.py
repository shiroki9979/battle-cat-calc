from typing import List, Optional, Union
from .models import GachaSlot, Track, ParseStatus

class SeedAnalyzer:
    """Analyzes parsed gacha data."""
    
    def __init__(self, slots: Track):
        self.slots = slots

    def find_cat_by_name(self, name: str) -> List[GachaSlot]:
        """Finds all occurrences of a cat by name in the slots."""
        results = []
        for slot in self.slots:
            # Ensure result is a string before searching
            res_str = str(slot.result) if isinstance(slot.result, ParseStatus) else slot.result
            if name in res_str:
                results.append(slot)
        return results

    def get_next_guaranteed(self) -> Optional[GachaSlot]:
        """Finds the next guaranteed slot if available."""
        for slot in self.slots:
            if slot.guaranteed and not isinstance(slot.guaranteed, ParseStatus):
                return slot
        return None
