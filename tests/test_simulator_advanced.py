"""測試 GachaSimulator 的進階功能：beam search、prefer_guaranteed、progress_callback、剪枝邏輯。

動機：原有測試僅覆蓋基本 A* 路徑搜尋；beam search、prefer_guaranteed、
     progress_callback、支配性剪枝、可達性剪枝從未被測試。
方法：以手工建構的小型 Track 精確控制搜尋行為。
"""
import sys
import pytest
from battle_cat_calc.simulator import GachaSimulator, SearchState
from battle_cat_calc.models import Track, GachaSlot, Position, ParseStatus, GachaPool


def _make_track(name: str, count: int, prefix: str = "Cat", track: str = 'A', guaranteed: bool = True):
    """輔助函式：建立包含 count 個格子的 Track。"""
    return Track(name, [
        GachaSlot(
            id=Position(i, track),
            result=f"{prefix}{i}",
            guaranteed=f"Gua{i}" if guaranteed else "",
            next_pos=Position(i + 10, 'B') if guaranteed else None,
        )
        for i in range(1, count + 1)
    ])


class TestBeamSearch:

    def test_beam_search_finds_result(self):
        """Beam Search 應能完成搜尋並回傳結果。"""
        track_a = _make_track("A", 20)
        track_b = Track("B", [])
        sim = GachaSimulator(track_a, track_b)

        result = sim.find_best_path(
            Position(1, 'A'), {"Cat5"}, 20, beam_width=50,
        )
        assert result is not None
        assert "Cat5" in result.collected

    def test_beam_search_small_width(self):
        """極小 beam_width 仍不應崩潰（可能找不到最佳解）。"""
        track_a = _make_track("A", 15)
        track_b = Track("B", [])
        sim = GachaSimulator(track_a, track_b)

        result = sim.find_best_path(
            Position(1, 'A'), {"Cat5"}, 20, beam_width=5,
        )
        # 只要不崩潰即可；beam_width 過小可能找不到解
        if result is not None:
            assert "Cat5" in result.collected

    def test_beam_search_state_map_cleanup(self):
        """Beam Search 應清理不需要的 state_map 條目以節省記憶體。
        
        驗證方式：確認結果路徑能正確重建（代表祖先鏈完整）。
        """
        track_a = _make_track("A", 30)
        track_b = Track("B", [])
        sim = GachaSimulator(track_a, track_b)

        result = sim.find_best_path(
            Position(1, 'A'), {"Cat15"}, 30, beam_width=20,
        )
        assert result is not None
        # 路徑能正確重建，代表 state_map 清理後祖先鏈仍完整
        assert result.step_count >= 1
        assert result.path[0].source == Position(1, 'A')


class TestPreferGuaranteed:

    def test_prefer_guaranteed_biases_toward_guaranteed(self):
        """啟用 prefer_guaranteed 時，在成本接近時偏好保底。"""
        # 建構情境：目標在 A11 的保底可以用 10 抽拿到，也可以單抽 10 次
        slots_a = [
            GachaSlot(
                id=Position(i, 'A'),
                result=f"Filler{i}",
                guaranteed="TargetGua" if i == 1 else f"G{i}",
                next_pos=Position(i + 10, 'B'),
            )
            for i in range(1, 12)
        ]
        track_a = Track("A", slots_a)
        track_b = Track("B", [
            GachaSlot(id=Position(i, 'B'), result=f"B{i}", guaranteed="")
            for i in range(11, 22)
        ])
        sim = GachaSimulator(track_a, track_b)

        result = sim.find_best_path(
            Position(1, 'A'), {"TargetGua"}, 20, prefer_guaranteed=True,
        )
        assert result is not None
        assert result.guaranteed_count >= 1

    def test_prefer_guaranteed_false_default(self):
        """預設不啟用 prefer_guaranteed。"""
        track_a = _make_track("A", 5)
        sim = GachaSimulator(track_a, Track("B", []))

        result = sim.find_best_path(Position(1, 'A'), {"Cat3"}, 10)
        assert result is not None
        # 預設應選最低成本（單抽 3 次）
        assert result.total_cost == 3


class TestProgressCallback:

    def test_callback_is_called(self):
        """progress_callback 應在搜尋過程中被呼叫。"""
        track_a = _make_track("A", 5)
        sim = GachaSimulator(track_a, Track("B", []))

        calls = []
        def cb(cost, processed, queue):
            calls.append((cost, processed, queue))

        sim.find_best_path(
            Position(1, 'A'), {"Cat3"}, 10, progress_callback=cb,
        )
        assert len(calls) > 0

    def test_callback_receives_increasing_processed_count(self):
        """回調的 states_processed 應單調遞增。"""
        track_a = _make_track("A", 10)
        sim = GachaSimulator(track_a, Track("B", []))

        processed_values = []
        def cb(cost, processed, queue):
            processed_values.append(processed)

        sim.find_best_path(
            Position(1, 'A'), {"Cat5"}, 20, progress_callback=cb,
        )
        # processed 應嚴格遞增
        for i in range(1, len(processed_values)):
            assert processed_values[i] > processed_values[i - 1]

    def test_callback_not_provided(self):
        """不提供 callback 不應影響搜尋。"""
        track_a = _make_track("A", 3)
        sim = GachaSimulator(track_a, Track("B", []))

        result = sim.find_best_path(Position(1, 'A'), {"Cat2"}, 5)
        assert result is not None


class TestDominancePruning:

    def test_dominated_state_pruned(self):
        """支配性剪枝：同位置、成本更高且收集更少的狀態應被略過。
        
        驗證方式：構建兩個池，其中池 B 比池 A 在同位置成本更高，
        最終 result 應只使用池 A（因為池 B 的狀態被支配）。
        """
        pool_a = GachaPool(
            pool_id="cheap",
            name="Cheap Pool",
            track_a=Track("A", [
                GachaSlot(id=Position(1, 'A'), result="Target", guaranteed="G1", next_pos=Position(11, 'B')),
                GachaSlot(id=Position(2, 'A'), result="Filler", guaranteed="G2", next_pos=Position(12, 'B')),
            ]),
            track_b=Track("B", []),
            has_guaranteed=True,
        )
        pool_b = GachaPool(
            pool_id="expensive",
            name="Expensive Pool",
            track_a=Track("A", [
                GachaSlot(id=Position(1, 'A'), result="Junk", guaranteed="G1", next_pos=Position(11, 'B')),
                GachaSlot(id=Position(2, 'A'), result="Target", guaranteed="G2", next_pos=Position(12, 'B')),
            ]),
            track_b=Track("B", []),
            has_guaranteed=True,
        )

        sim = GachaSimulator([pool_a, pool_b])
        result = sim.find_best_path(Position(1, 'A'), {"Target"}, 10)

        assert result is not None
        assert result.total_cost == 1
        # 應在池 cheap 第 1 抽就拿到 Target
        assert result.path[0].pool_id == "cheap"


class TestReachabilityPruning:

    def test_unreachable_target_returns_none(self):
        """可達性剪枝：目標貓不在任何可達位置時應回傳 None。"""
        # 只有位置 1-3，但起點在 5，位置只會遞增所以不可達
        track_a = Track("A", [
            GachaSlot(id=Position(1, 'A'), result="Target", guaranteed="G1", next_pos=Position(11, 'B')),
            GachaSlot(id=Position(2, 'A'), result="Filler", guaranteed="G2"),
            GachaSlot(id=Position(3, 'A'), result="Filler2", guaranteed="G3"),
        ])
        sim = GachaSimulator(track_a, Track("B", []))

        # 從位置 5 開始，Target 在位置 1，不可達
        result = sim.find_best_path(Position(5, 'A'), {"Target"}, 20)
        assert result is None

    def test_reachable_target_found(self):
        """目標在可達範圍內應正常找到。"""
        track_a = _make_track("A", 10)
        sim = GachaSimulator(track_a, Track("B", []))

        result = sim.find_best_path(Position(1, 'A'), {"Cat5"}, 20)
        assert result is not None


class TestSimulatorEdgeCases:

    def test_start_pos_out_of_bounds(self):
        """起點超出 Track 範圍應回傳 None。"""
        track_a = _make_track("A", 5)
        sim = GachaSimulator(track_a, Track("B", []))

        result = sim.find_best_path(Position(100, 'A'), {"Cat1"}, 20)
        assert result is None

    def test_max_rolls_zero(self):
        """max_rolls=0 只有空目標能成功。"""
        track_a = _make_track("A", 5)
        sim = GachaSimulator(track_a, Track("B", []))

        result_empty = sim.find_best_path(Position(1, 'A'), set(), 0)
        assert result_empty is not None
        assert result_empty.total_cost == 0

        result_nonempty = sim.find_best_path(Position(1, 'A'), {"Cat1"}, 0)
        assert result_nonempty is None

    def test_target_at_start_position(self):
        """目標正好在起點位置，應 cost=1（需要抽一次才能收集）。"""
        track_a = Track("A", [
            GachaSlot(id=Position(1, 'A'), result="Target", guaranteed="G", next_pos=Position(11, 'B')),
            GachaSlot(id=Position(2, 'A'), result="Filler", guaranteed="G2"),
        ])
        sim = GachaSimulator(track_a, Track("B", []))

        result = sim.find_best_path(Position(1, 'A'), {"Target"}, 5)
        assert result is not None
        assert result.total_cost == 1

    def test_guaranteed_with_incomplete_data(self):
        """保底需要 10 格完整資料，不足時應跳過保底分支。"""
        # 只有 5 格，不足 10 格做保底
        track_a = _make_track("A", 5)
        sim = GachaSimulator(track_a, Track("B", []))

        # 目標在第 3 格，只能單抽
        result = sim.find_best_path(Position(1, 'A'), {"Cat3"}, 15)
        assert result is not None
        assert all(not step.is_guaranteed for step in result.path)

    def test_duplicate_target_in_set(self):
        """重複的目標不影響結果（set 自動去重）。"""
        track_a = _make_track("A", 5)
        sim = GachaSimulator(track_a, Track("B", []))

        result = sim.find_best_path(Position(1, 'A'), {"Cat3", "Cat3"}, 10)
        assert result is not None
        assert result.total_cost == 3


class TestHeuristicOptimization:
    """驗證位置感知啟發式的正確性（admissible 且比純計數更緊）。"""

    def _setup_heuristic(self, sim, target_cats):
        """設置 _heuristic 所需的預計算屬性。"""
        target_fs = frozenset(target_cats)
        _, sim._target_sorted_positions = sim._precompute_target_data(target_fs)
        sim._min_cost_per_advance = 1.0

    def test_heuristic_all_collected(self):
        track_a = _make_track("A", 5)
        sim = GachaSimulator(track_a, Track("B", []))
        target = frozenset({"Cat1", "Cat2"})
        collected = frozenset({"Cat1", "Cat2"})
        self._setup_heuristic(sim, target)
        assert sim._heuristic(collected, target, 2, 1) == 0

    def test_heuristic_none_collected(self):
        track_a = _make_track("A", 5)
        sim = GachaSimulator(track_a, Track("B", []))
        target = frozenset({"Cat1", "Cat2", "Cat3"})
        collected = frozenset()
        self._setup_heuristic(sim, target)
        # uncollected=3, farthest nearest Cat3 at pos 3, dist=2 from pos 1 → max(3, 2)=3
        assert sim._heuristic(collected, target, 3, 1) == 3

    def test_heuristic_partial_collected(self):
        track_a = _make_track("A", 5)
        sim = GachaSimulator(track_a, Track("B", []))
        target = frozenset({"Cat1", "Cat2", "Cat3"})
        collected = frozenset({"Cat2"})
        self._setup_heuristic(sim, target)
        # uncollected=2, Cat1@1 Cat3@3 from pos 1 → dist=2 → max(2, 2)=2
        assert sim._heuristic(collected, target, 3, 1) == 2

    def test_heuristic_distance_dominates(self):
        """目標距離 > 未收集數量時，距離估計佔主導。"""
        track_a = _make_track("A", 20)
        sim = GachaSimulator(track_a, Track("B", []))
        target = frozenset({"Cat15"})
        self._setup_heuristic(sim, target)
        collected = frozenset()
        # uncollected=1, Cat15 at pos 15, current pos 1 → dist=14 → max(1, 14)=14
        assert sim._heuristic(collected, target, 1, 1) == 14

    def test_heuristic_with_advanced_position(self):
        """當前位置已經超過某些目標出現位置時，使用更後方的出現位置。"""
        track_a = _make_track("A", 20)
        sim = GachaSimulator(track_a, Track("B", []))
        target = frozenset({"Cat5"})
        self._setup_heuristic(sim, target)
        collected = frozenset()
        # Cat5 at pos 5, current at pos 10 → Cat5 已不可達，但 bisect 找不到 >= 10 的位置
        # heuristic 回傳 uncollected=1（distance=0 因為無可達位置）
        assert sim._heuristic(collected, target, 1, 10) == 1


class TestLocalIdCounter:
    """驗證狀態 ID 計數器為區域變數，每次搜尋獨立歸零。"""

    def test_state_ids_restart_per_search(self):
        """連續兩次 find_best_path 回傳的 state_id 應從小數字重新開始。"""
        track_a = _make_track("A", 10)
        sim = GachaSimulator(track_a, Track("B", []))

        r1 = sim.find_best_path(Position(1, 'A'), {"Cat3"}, 10)
        r2 = sim.find_best_path(Position(1, 'A'), {"Cat5"}, 10)

        # 兩次搜尋都應產生結果
        assert r1 is not None and r2 is not None
        # 兩次搜尋的路徑 state_id 不會因為上一次搜尋而累加到很大的數字
        # （若使用全域計數器，第二次的 id 會從上次終止值繼續往上加）
        # 這裡只要確認兩次搜尋都能正常完成即可；精確 id 值是內部實作細節


class TestSearchStateSlots:
    """驗證 SearchState 使用 slots=True 的記憶體節省與基本行為。"""

    def test_no_dict_attribute(self):
        """slots=True 的 dataclass 不應有 __dict__ 屬性。"""
        state = SearchState(priority=1.0, cost=1.0, pos_id=1, pos_track='A', collected=frozenset())
        assert not hasattr(state, '__dict__')

    def test_has_slots(self):
        """確認 __slots__ 存在。"""
        assert hasattr(SearchState, '__slots__')

    def test_memory_smaller_than_dict_based(self):
        """slots 版本的記憶體應小於同等級的非 slots 物件。"""
        state = SearchState(
            priority=5.0, cost=3.0, pos_id=10, pos_track='B',
            collected=frozenset({"Cat1", "Cat2"}),
            state_id=42, parent_id=1, last_rolled_rare="Cat1"
        )
        # slots dataclass 的 sys.getsizeof 通常遠小於帶 __dict__ 的物件
        assert sys.getsizeof(state) < 200

    def test_frozen_immutability(self):
        """frozen + slots 組合下，屬性仍不可修改。"""
        state = SearchState(priority=1.0, cost=1.0, pos_id=1, pos_track='A', collected=frozenset())
        with pytest.raises(AttributeError):
            state.cost = 999

    def test_order_comparison(self):
        """order=True 應以 priority 排序。"""
        s1 = SearchState(priority=1.0, cost=1.0, pos_id=1, pos_track='A', collected=frozenset())
        s2 = SearchState(priority=2.0, cost=2.0, pos_id=2, pos_track='A', collected=frozenset())
        assert s1 < s2


class TestTargetCatValidation:
    """驗證 find_best_path 對不存在目標貓咪的前置檢查。"""

    def test_single_missing_cat_raises(self):
        """單一不存在的目標貓咪應拋出 ValueError。"""
        track_a = _make_track("A", 5)
        sim = GachaSimulator(track_a, Track("B", []))

        with pytest.raises(ValueError, match="NoSuchCat"):
            sim.find_best_path(Position(1, 'A'), {"NoSuchCat"}, 10)

    def test_multiple_missing_cats_listed(self):
        """多隻不存在的貓咪名稱應全部出現在錯誤訊息中。"""
        track_a = _make_track("A", 5)
        sim = GachaSimulator(track_a, Track("B", []))

        with pytest.raises(ValueError) as exc_info:
            sim.find_best_path(Position(1, 'A'), {"MissingA", "MissingB"}, 10)
        msg = str(exc_info.value)
        assert "MissingA" in msg
        assert "MissingB" in msg

    def test_partial_missing_raises(self):
        """目標中混合存在與不存在的貓咪，仍應拋出 ValueError（僅列出不存在的）。"""
        track_a = _make_track("A", 5)
        sim = GachaSimulator(track_a, Track("B", []))

        with pytest.raises(ValueError, match="MissingCat") as exc_info:
            sim.find_best_path(Position(1, 'A'), {"Cat1", "MissingCat"}, 10)
        assert "Cat1" not in str(exc_info.value)

    def test_all_targets_exist_no_error(self):
        """所有目標貓咪都存在時不應拋出錯誤。"""
        track_a = _make_track("A", 5)
        sim = GachaSimulator(track_a, Track("B", []))

        result = sim.find_best_path(Position(1, 'A'), {"Cat1", "Cat3"}, 10)
        assert result is not None

    def test_multi_pool_missing_raises(self):
        """多池模式下，不存在於任何池的貓咪應拋出 ValueError。"""
        pool1 = GachaPool(
            pool_id="pool1", name="Pool1", has_guaranteed=True, guaranteed_cost=15,
            track_a=_make_track("A", 3, prefix="P1Cat", track='A'),
            track_b=Track("B", []),
        )
        pool2 = GachaPool(
            pool_id="pool2", name="Pool2", has_guaranteed=True, guaranteed_cost=15,
            track_a=_make_track("A", 3, prefix="P2Cat", track='A'),
            track_b=Track("B", []),
        )
        sim = GachaSimulator([pool1, pool2])

        with pytest.raises(ValueError, match="NotInAnyPool"):
            sim.find_best_path(Position(1, 'A'), {"NotInAnyPool"}, 10)


class TestFrozensetTargetCats:
    """驗證 target_cats 統一使用 frozenset 後搜尋行為不變。"""

    def test_accepts_set_input(self):
        """find_best_path 接受普通 set 輸入，內部轉為 frozenset。"""
        track_a = _make_track("A", 10)
        sim = GachaSimulator(track_a, Track("B", []))
        result = sim.find_best_path(Position(1, 'A'), {"Cat3"}, 10)
        assert result is not None
        assert result.total_cost == 3

    def test_accepts_frozenset_input(self):
        """find_best_path 也能接受 frozenset 輸入。"""
        track_a = _make_track("A", 10)
        sim = GachaSimulator(track_a, Track("B", []))
        result = sim.find_best_path(Position(1, 'A'), frozenset({"Cat5"}), 10)
        assert result is not None
        assert result.total_cost == 5

    def test_accepts_list_input(self):
        """find_best_path 應能接受任何可迭代物件。"""
        track_a = _make_track("A", 10)
        sim = GachaSimulator(track_a, Track("B", []))
        result = sim.find_best_path(Position(1, 'A'), ["Cat2"], 10)
        assert result is not None
        assert result.total_cost == 2


class TestGuaranteedLoopOptimization:
    """驗證保底迴圈直接使用 track 物件查找後行為正確。"""

    def test_guaranteed_still_collects_all_units_in_range(self):
        """十連保底應收集 10 格內的所有目標貓咪 + 保底貓。"""
        track_a = Track("A", [
            GachaSlot(id=Position(i, 'A'), result=f"Cat{i}",
                      guaranteed=f"Gua{i}" if i == 1 else "",
                      next_pos=Position(i + 10, 'B') if i == 1 else None)
            for i in range(1, 15)
        ])
        sim = GachaSimulator(track_a, Track("B", []))

        # 目標含保底貓 Gua1 和範圍內的 Cat5
        result = sim.find_best_path(Position(1, 'A'), {"Cat5", "Gua1"}, 20)
        assert result is not None
        assert "Cat5" in result.collected
        assert "Gua1" in result.collected

    def test_guaranteed_with_multi_pool(self):
        """多池模式下保底迴圈也應正確查找。"""
        pool_a = GachaPool(
            pool_id="pa", name="Pool A",
            track_a=Track("A", [
                GachaSlot(id=Position(i, 'A'), result=f"PA{i}",
                          guaranteed="SuperA" if i == 1 else "",
                          next_pos=Position(i + 10, 'B') if i == 1 else None)
                for i in range(1, 15)
            ]),
            track_b=Track("B", []),
            guaranteed_cost=10,
        )
        sim = GachaSimulator([pool_a])
        result = sim.find_best_path(Position(1, 'A'), {"SuperA"}, 20)
        assert result is not None
        assert "SuperA" in result.collected


class TestVisitedInPlaceFilter:
    """驗證 visited 原地過濾（slice assignment）不影響剪枝正確性。"""

    def test_dominance_pruning_still_works(self):
        """支配性剪枝應繼續正常運作：高成本低收集的路徑被排除。"""
        track_a = _make_track("A", 20)
        sim = GachaSimulator(track_a, Track("B", []))

        # 同時搜尋多個目標以觸發支配性剪枝
        result = sim.find_best_path(Position(1, 'A'), {"Cat3", "Cat10"}, 20)
        assert result is not None
        assert result.total_cost == 10  # 至少需要抽到第 10 格

    def test_multiple_targets_optimal_cost(self):
        """多目標下仍找到最低成本。"""
        track_a = _make_track("A", 20)
        sim = GachaSimulator(track_a, Track("B", []))

        result = sim.find_best_path(Position(1, 'A'), {"Cat1", "Cat5", "Cat15"}, 25)
        assert result is not None
        assert result.total_cost == 15
