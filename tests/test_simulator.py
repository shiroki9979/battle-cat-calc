import pytest
from battle_cat_calc.simulator import GachaSimulator
from battle_cat_calc.models import Track, GachaSlot, Position, ParseStatus

def test_simulator_simple_single():
    # Track A: 1A: Cat1, 2A: Cat2
    track_a = Track("A", [
        GachaSlot(id=(1, 'A'), result="Cat1", guaranteed="Gua1", next_pos=Position(11, 'B')),
        GachaSlot(id=(2, 'A'), result="Cat2", guaranteed="Gua2", next_pos=Position(12, 'B')),
    ])
    track_b = Track("B", [])
    sim = GachaSimulator(track_a, track_b)
    
    # Target: Cat2, Max Rolls: 10
    result = sim.find_best_path(Position(1, 'A'), {"Cat2"}, 10)
    
    assert result is not None
    assert result["total_cost"] == 2 # 1A -> 2A
    assert len(result["path"]) == 2

def test_simulator_guaranteed_efficiency():
    # Scenario: Target is Cat10. 
    # Option 1: 10 single rolls (Cost 10)
    # Option 2: 1 guaranteed roll (Cost 11) -> Dijkstra should pick Single.
    
    slots = []
    for i in range(1, 12):
        slots.append(GachaSlot(id=(i, 'A'), result=f"Cat{i}", guaranteed="Other", next_pos=Position(i+10, 'B')))
    
    track_a = Track("A", slots)
    track_b = Track("B", [])
    sim = GachaSimulator(track_a, track_b)
    
    result = sim.find_best_path(Position(1, 'A'), {"Cat10"}, 20)
    assert result["total_cost"] == 10

def test_simulator_guaranteed_jump():
    # Scenario: Target is Cat15 which is only on Track B.
    # Must use guaranteed at 1A to jump to 11B.
    
    # Track A
    track_a = Track("A", [
        GachaSlot(id=(i, 'A'), result=f"A{i}", guaranteed="GuaA", next_pos=Position(i+10, 'B') if i==1 else None) for i in range(1, 12)
    ])
    # Track B
    track_b = Track("B", [
        GachaSlot(id=(i, 'B'), result=f"B{i}", guaranteed="GuaB", next_pos=None) for i in range(11, 20)
    ])
    
    sim = GachaSimulator(track_a, track_b)
    
    # Target Cat: B12
    # 1A (Gua) -> 11B (Cost 10). B11 collected.
    # 11B (Single) -> 12B (Cost 10+1=11). B11 collected at 11B step? No, step collects result of that slot.
    # Wait, 1A (Gua) collects 1A..10A and GuaA. Lands on 11B.
    # Next step: Start at 11B.
    # Single roll at 11B: Collects result of 11B (="B11"). Moves to 12B. Cost +1. Total 11.
    # Next step: Start at 12B.
    # Single roll at 12B: Collects result of 12B (="B12"). Moves to 13B. Cost +1. Total 12.
    result = sim.find_best_path(Position(1, 'A'), {"B12"}, 20)
    
    assert result is not None
    assert result["total_cost"] == 12

def test_simulator_parse_status_termination():
    # 1A: Cat1, 2A: ParseStatus.MULTIPLE_DATA
    track_a = Track("A", [
        GachaSlot(id=(1, 'A'), result="Cat1", guaranteed="Gua1", next_pos=Position(11, 'B')),
        GachaSlot(id=(2, 'A'), result=ParseStatus.MULTIPLE_DATA, guaranteed="Gua2", next_pos=Position(12, 'B')),
    ])
    track_b = Track("B", [])
    sim = GachaSimulator(track_a, track_b)
    
    # Try to find Cat1. Should work.
    res1 = sim.find_best_path(Position(1, 'A'), {"Cat1"}, 5)
    assert res1 is not None
    
    # Try to find Cat2 (which doesn't exist). Should fail because it hits ParseStatus at 2A.
    res2 = sim.find_best_path(Position(1, 'A'), {"NotExists"}, 5)
    assert res2 is None
