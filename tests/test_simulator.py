import pytest
from dataclasses import replace
from battle_cat_calc.simulator import GachaSimulator
from battle_cat_calc.models import Track, GachaSlot, Position, ParseStatus, GachaPool

def test_simulator_simple_single():
    """測試基本的單抽模擬。
    
    動機：驗證 Dijkstra 演算法能在最簡單的情況下（無跳軌、無保底）找到目標。
    方法：建立一條簡單的 Track A，目標設為第 2 格的貓咪，驗證模擬器能找到 Cost=2 的路徑。
    """
    # Track A: 1A: Cat1, 2A: Cat2
    track_a = Track("A", [
        GachaSlot(id=Position(1, 'A'), result="Cat1", guaranteed="Gua1", next_pos=Position(11, 'B')),
        GachaSlot(id=Position(2, 'A'), result="Cat2", guaranteed="Gua2", next_pos=Position(12, 'B')),
    ])
    track_b = Track("B", [])
    sim = GachaSimulator(track_a, track_b)
    
    # Target: Cat2, Max Rolls: 10
    result = sim.find_best_path(Position(1, 'A'), {"Cat2"}, 10)
    
    assert result is not None
    assert result.total_cost == 2 # 1A -> 2A
    assert len(result.path) == 2

def test_simulator_guaranteed_efficiency():
    """測試保底與單抽的效率比較。
    
    動機：Dijkstra 應該總是選擇成本最低的路徑。如果單抽 10 次比一次保底更便宜（或反之），它應該做出正確選擇。
    方法：設定目標在第 10 格。單抽需 10 抽 (Cost 10)，保底需 1500 貓罐頭（模擬為 Cost 11 或其他權重，此處假設 Cost 11）。驗證模擬器選擇單抽路徑。
    """
    # Scenario: Target is Cat10. 
    # Option 1: 10 single rolls (Cost 10)
    # Option 2: 1 guaranteed roll (Cost 11) -> Dijkstra should pick Single.
    
    slots = []
    for i in range(1, 12):
        slots.append(GachaSlot(id=Position(i, 'A'), result=f"Cat{i}", guaranteed="Other", next_pos=Position(i+10, 'B')))
    
    track_a = Track("A", slots)
    track_b = Track("B", [])
    sim = GachaSimulator(track_a, track_b)
    
    result = sim.find_best_path(Position(1, 'A'), {"Cat10"}, 20)
    assert result.total_cost == 10

def test_simulator_guaranteed_jump():
    """測試利用保底進行跳軌。
    
    動機：某些貓咪只在 Track B，必須透過 Track A 的保底跳到 Track B 才能獲得。
    方法：設定目標在 Track B。建立 Track A 讓它透過保底跳到 Track B，驗證模擬器能找到這條跨軌路徑。
    """
    # Scenario: Target is Cat15 which is only on Track B.
    # Must use guaranteed at 1A to jump to 11B.
    
    # Track A
    track_a = Track("A", [
        GachaSlot(id=Position(i, 'A'), result=f"A{i}", guaranteed="GuaA", next_pos=Position(i+10, 'B') if i==1 else None) for i in range(1, 12)
    ])
    # Track B
    track_b = Track("B", [
        GachaSlot(id=Position(i, 'B'), result=f"B{i}", guaranteed="GuaB", next_pos=None) for i in range(11, 20)
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
    assert result.total_cost == 12

def test_simulator_parse_status_termination():
    """測試遇到解析狀態 (ParseStatus) 時的中止行為。
    
    動機：若資料解析失敗或不明確，模擬器不應崩潰，而是視為無法通行的路徑。
    方法：在 Track 中故意插入 ParseStatus.MULTIPLE_DATA，驗證模擬器在到達該格時不會繼續搜尋或報錯。
    """
    # 1A: Cat1, 2A: ParseStatus.MULTIPLE_DATA
    track_a = Track("A", [
        GachaSlot(id=Position(1, 'A'), result="Cat1", guaranteed="Gua1", next_pos=Position(11, 'B')),
        GachaSlot(id=Position(2, 'A'), result=ParseStatus.MULTIPLE_DATA, guaranteed="Gua2", next_pos=Position(12, 'B')),
    ])
    track_b = Track("B", [])
    sim = GachaSimulator(track_a, track_b)
    
    # Try to find Cat1. Should work.
    res1 = sim.find_best_path(Position(1, 'A'), {"Cat1"}, 5)
    assert res1 is not None
    
    # Try to find Cat2 (which doesn't exist). Should raise ValueError.
    with pytest.raises(ValueError, match="NotExists"):
        sim.find_best_path(Position(1, 'A'), {"NotExists"}, 5)

def test_simulator_duplicate_rare_switch():
    """測試重複稀有導致的跳軌。
    
    動機：當連續兩格為同一隻稀有貓時，第二格會被替換並觸發跳軌。
    方法：建立一個 Track A，其中 2A 和 3A 原本都是同一隻貓，3A 發生替換並跳到 B 軌。
          驗證模擬器能正確處理這種跳軌情況，找到 B 軌上的目標。
    """
    # 模擬情境：2A 抽到 Archer（普通格），3A 的原始貓也是 Archer → 觸發重複，抽完跳到 4B
    sim = GachaSimulator(*_duplicate_rare_tracks())

    # 4B 的目標只有透過重複稀有的換軌才到得了（沿 A 軌走會到 4A）
    result = sim.find_best_path(Position(1, 'A'), {"TargetCatB4"}, 5)
    assert result is not None
    assert result.total_cost == 4
    assert [step.unit for step in result.path] == ["Cat1", "Archer", "ArcherReplaced", "TargetCatB4"]

    dup_step = result.path[2]
    assert dup_step.is_duplicate_rare is True
    assert dup_step.is_track_switch is True
    assert dup_step.target == Position(4, 'B')


def _duplicate_rare_tracks():
    """1A Cat1、2A Archer、3A 重複稀有（原始 Archer → ArcherReplaced，單抽落在 4B）。"""
    track_a = Track("A", [
        GachaSlot(id=Position(1, 'A'), result="Cat1", guaranteed="Gua1", next_pos=Position(11, 'B')),
        GachaSlot(id=Position(2, 'A'), result="Archer", guaranteed="Gua2", next_pos=Position(12, 'B')),
        GachaSlot(id=Position(3, 'A'), result="ArcherReplaced", guaranteed="Gua3", next_pos=Position(13, 'B'),
                  original_result="Archer", is_duplicate_rare=True,
                  single_next_pos=Position(4, 'B')),
        GachaSlot(id=Position(4, 'A'), result="Cat4", guaranteed="Gua4", next_pos=Position(14, 'B')),
    ])
    track_b = Track("B", [
        GachaSlot(id=Position(4, 'B'), result="TargetCatB4", guaranteed="GuaB4", next_pos=None),
        GachaSlot(id=Position(5, 'B'), result="CatB5", guaranteed="GuaB5", next_pos=None),
    ])
    return track_a, track_b


def test_simulator_duplicate_rare_not_triggered():
    """上一隻不是同一隻貓時不觸發重複：拿到替換前的貓，同軌前進一格。

    動機：表格的重複稀有格是假設「上一隻就是同一隻」算出來的；
          從別的路徑走到這格時，不能直接拿替換後的結果。
    """
    sim = GachaSimulator(*_duplicate_rare_tracks())

    result = sim.find_best_path(Position(3, 'A'), {"Archer"}, 1)
    assert result is not None
    step, = result.path
    assert (step.unit, step.target, step.is_duplicate_rare) == ("Archer", Position(4, 'A'), False)

    assert sim.find_best_path(Position(3, 'A'), {"ArcherReplaced"}, 1) is None


def test_simulator_duplicate_rare_without_landing_is_not_guessed():
    """觸發重複卻沒有單抽落點（例如 HTML 標 <?>）時，不猜落點，該路徑視為不可走。"""
    track_a, track_b = _duplicate_rare_tracks()
    track_a.get_slot(3).single_next_pos = None
    sim = GachaSimulator(track_a, track_b)

    assert sim.find_best_path(Position(1, 'A'), {"TargetCatB4"}, 5) is None


class TestGuaranteedWithDuplicateRare:
    """必中十連的前 10 抽照單抽規則走，重複稀有會換軌，不是固定同軌的 pos+0..pos+9。"""

    @staticmethod
    def _plain(track, prefix, other_track):
        return [
            GachaSlot(id=Position(i, track), result=f"{prefix}{i}", guaranteed=f"Gua{prefix}{i}",
                      next_pos=Position(i + 10, other_track))
            for i in range(1, 15)
        ]

    def _sim(self, track_a, track_b, guaranteed_cost=3):
        # 必中成本設得比單抽便宜，讓搜尋偏好必中，才測得到必中分支
        pool = GachaPool("p", "p", Track("A", track_a), Track("B", track_b),
                         has_guaranteed=True, guaranteed_cost=guaranteed_cost)
        return GachaSimulator([pool])

    def test_window_follows_track_switch(self):
        """1A 起：1A、2A(Archer)、3A 重複 → 5B，之後走 B 軌，第 8 隻是 8B 而不是 8A。"""
        a, b = self._plain('A', 'A', 'B'), self._plain('B', 'B', 'A')
        a[1] = replace(a[1], result="Archer")
        a[2] = replace(a[2], result="ArcherReplaced", original_result="Archer",
                       is_duplicate_rare=True, single_next_pos=Position(5, 'B'))
        b[7] = replace(b[7], result="OnPath")    # 8B：實際走到
        a[7] = replace(a[7], result="OffPath")   # 8A：舊版固定取 pos+i 會誤收
        sim = self._sim(a, b)

        result = sim.find_best_path(Position(1, 'A'), {"OnPath"}, 10)
        assert result is not None
        assert result.total_cost == 3
        step, = result.path
        assert step.is_guaranteed is True
        assert step.context_data['collected_units'] == ["OnPath"]

        # 走過 3A 的重複之後就回不到 A 軌的 8A，必中也收不到它
        assert sim.find_best_path(Position(1, 'A'), {"OffPath"}, 20) is None

    def test_start_slot_duplicate_not_triggered_uses_original_variant(self):
        """起始格是重複稀有格但沒被觸發：必中用替換前的超激與落點。"""
        sim = self._sim(*self._duplicate_variant_tracks())

        result = sim.find_best_path(Position(2, 'A'), {"GuaOrig"}, 5)
        assert result is not None
        step, = result.path
        assert (step.unit, step.target) == ("GuaOrig", Position(12, 'A'))
        assert step.is_track_switch is False

    def test_start_slot_duplicate_triggered_uses_replaced_variant(self):
        """先單抽 1A（Orig）再在 2A 做必中：2A 觸發重複，用替換後的超激與落點。"""
        sim = self._sim(*self._duplicate_variant_tracks())

        result = sim.find_best_path(Position(1, 'A'), {"GuaRepl"}, 5)
        assert result is not None
        assert result.total_cost == 4
        single, guaranteed = result.path
        assert single.unit == "Orig"
        assert (guaranteed.unit, guaranteed.target) == ("GuaRepl", Position(12, 'B'))
        assert guaranteed.is_track_switch is True

    def _duplicate_variant_tracks(self):
        a, b = self._plain('A', 'A', 'B'), self._plain('B', 'B', 'A')
        a[0] = replace(a[0], result="Orig")
        a[1] = replace(a[1], result="Repl", guaranteed="GuaRepl", next_pos=Position(12, 'B'),
                       original_result="Orig", original_guaranteed="GuaOrig",
                       original_next_pos=Position(12, 'A'), is_duplicate_rare=True,
                       single_next_pos=Position(4, 'B'))
        return a, b

    def test_heuristic_rate_uses_largest_duplicate_advance(self):
        """單抽最多前進 3 格時，每格最低成本不可高於 1/3，啟發式才維持下界。"""
        a, b = self._plain('A', 'A', 'B'), self._plain('B', 'B', 'A')
        a[1] = replace(a[1], result="Archer")
        a[2] = replace(a[2], original_result="Archer", is_duplicate_rare=True,
                       single_next_pos=Position(6, 'B'))
        sim = self._sim(a, b, guaranteed_cost=10)

        sim.find_best_path(Position(1, 'A'), {"A1"}, 1)
        assert sim._min_cost_per_advance <= 1 / 3

def test_simulator_no_path_found():
    """測試在限制內找不到路徑的情況。
    
    動機：當 max_rolls 不足以到達目標時，模擬器應回傳 None。
    方法：設定一個無法在限制抽數內到達的目標，驗證回傳值。
    """
    track_a = Track("A", [
        GachaSlot(id=Position(i, 'A'), result=f"Cat{i}", guaranteed=f"Gua{i}", next_pos=Position(i+10, 'B'))
        for i in range(1, 6)
    ])
    track_b = Track("B", [])
    
    sim = GachaSimulator(track_a, track_b)
    
    # 目標在第 5 格，但只允許 3 抽
    result = sim.find_best_path(Position(1, 'A'), {"Cat5"}, 3)
    assert result is None

def test_simulator_multiple_targets():
    """測試收集多個目標貓咪。
    
    動機：玩家通常想同時收集多隻目標貓咪，模擬器應找到最佳路徑。
    方法：設定兩個目標貓咪在不同位置，驗證模擬器能找到收集兩者的最短路徑。
    """
    track_a = Track("A", [
        GachaSlot(id=Position(1, 'A'), result="Target1", guaranteed="Gua1", next_pos=Position(11, 'B')),
        GachaSlot(id=Position(2, 'A'), result="Filler", guaranteed="Gua2", next_pos=Position(12, 'B')),
        GachaSlot(id=Position(3, 'A'), result="Target2", guaranteed="Gua3", next_pos=Position(13, 'B')),
    ])
    track_b = Track("B", [])
    
    sim = GachaSimulator(track_a, track_b)
    
    # 收集 Target1 和 Target2
    result = sim.find_best_path(Position(1, 'A'), {"Target1", "Target2"}, 10)
    
    assert result is not None
    assert result.total_cost == 3  # 1A -> 2A -> 3A
    assert "Target1" in result.collected
    assert "Target2" in result.collected

def test_simulator_empty_target():
    """測試空目標集合。
    
    動機：若不需要收集任何貓咪，模擬器應立刻回傳成功（Cost=0）。
    方法：傳入空的 target_cats 集合，驗證回傳 Cost 為 0。
    """
    track_a = Track("A", [
        GachaSlot(id=Position(1, 'A'), result="Cat1", guaranteed="Gua1", next_pos=Position(11, 'B')),
    ])
    track_b = Track("B", [])
    
    sim = GachaSimulator(track_a, track_b)
    
    result = sim.find_best_path(Position(1, 'A'), set(), 10)
    
    assert result is not None
    assert result.total_cost == 0

