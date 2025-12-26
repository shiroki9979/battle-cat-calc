from dataclasses import dataclass
from typing import Optional, Tuple, Union, List
from enum import Enum
from collections import UserList

class ParseStatus(Enum):
    """Represents special states during parsing."""
    MULTIPLE_DATA = "Multiple Data Found"
    INVALID_DATA = "Invalid Data"
    AMBIGUOUS = "Ambiguous Position"

    def __str__(self):
        return self.value

@dataclass
class Position:
    """Represents a gacha track position."""
    id: int
    track: str

@dataclass
class GachaSlot:
    """Represents a single slot in the gacha roll (A or B track)."""
    id: Tuple[int, str]  # (number, track)
    result: Union[str, ParseStatus]
    guaranteed: Union[str, ParseStatus]
    next_pos: Optional[Union[Position, ParseStatus]] = None

class Track(UserList):
    """A professional container for a sequence of GachaSlots.
    
    Inherits from UserList to provide full list functionality while allowing
    custom metadata and helper methods.
    """
    def __init__(self, name: str, slots: List[GachaSlot] = None):
        super().__init__(slots or [])
        self.name = name

    def __repr__(self):
        return f"Track(name='{self.name}', slots={len(self.data)})"

    def get_slot(self, index: int) -> Optional[GachaSlot]:
        """Returns the slot at the given 1-based index (ID)."""
        for slot in self.data:
            if slot.id[0] == index:
                return slot
        return None
