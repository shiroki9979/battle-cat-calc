"""測試 GachaResult、GachaNode、ParseStatus 等資料模型的方法與屬性。

動機：GachaResult 具有多個 property 和序列化方法，原先完全未測試。
方法：手工建構已知資料，逐一驗證每個 property、方法的輸出。
"""
import pytest
from battle_cat_calc.models import (
    GachaResult, GachaStep, GachaNode, GachaSlot,
    Position, ParseStatus, Track, GachaPool,
)


# ── GachaResult ─────────────────────────────────────────────

class TestGachaResultProperties:

    @pytest.fixture
    def multi_step_result(self):
        """三步路徑：兩次單抽 + 一次十連，跨兩個池。"""
        steps = (
            GachaStep(
                source=Position(1, 'A'), target=Position(2, 'A'),
                unit='Cat1', pool_id='pool_a',
            ),
            GachaStep(
                source=Position(2, 'A'), target=Position(11, 'B'),
                unit='Super', pool_id='pool_a',
                is_guaranteed=True, is_track_switch=True,
            ),
            GachaStep(
                source=Position(11, 'B'), target=Position(12, 'B'),
                unit='Cat3', pool_id='pool_b',
            ),
        )
        return GachaResult(
            total_cost=12,
            path=steps,
            collected={'Cat1', 'Super', 'Cat3'},
            start_pos=Position(1, 'A'),
            target_cats={'Cat1', 'Super', 'Cat3'},
        )

    def test_step_count(self, multi_step_result):
        assert multi_step_result.step_count == 3

    def test_guaranteed_count(self, multi_step_result):
        assert multi_step_result.guaranteed_count == 1

    def test_single_roll_count(self, multi_step_result):
        assert multi_step_result.single_roll_count == 2

    def test_pools_used(self, multi_step_result):
        assert multi_step_result.pools_used == {'pool_a', 'pool_b'}

    def test_end_pos(self, multi_step_result):
        assert multi_step_result.end_pos == Position(12, 'B')

    def test_repr(self, multi_step_result):
        r = repr(multi_step_result)
        assert 'cost=12' in r
        assert 'steps=3' in r
        assert 'collected=3' in r


class TestGachaResultEmpty:

    @pytest.fixture
    def empty_result(self):
        return GachaResult(
            total_cost=0,
            path=(),
            collected=set(),
            start_pos=Position(1, 'A'),
        )

    def test_empty_step_count(self, empty_result):
        assert empty_result.step_count == 0

    def test_empty_guaranteed_count(self, empty_result):
        assert empty_result.guaranteed_count == 0

    def test_empty_single_roll_count(self, empty_result):
        assert empty_result.single_roll_count == 0

    def test_empty_pools_used(self, empty_result):
        assert empty_result.pools_used == set()

    def test_empty_end_pos_fallback(self, empty_result):
        """空路徑時 end_pos 應回退到 start_pos。"""
        assert empty_result.end_pos == Position(1, 'A')


class TestGachaResultStepsByPool:

    def test_groups_by_pool_id(self):
        steps = (
            GachaStep(source=Position(1, 'A'), target=Position(2, 'A'), unit='C1', pool_id='p1'),
            GachaStep(source=Position(2, 'A'), target=Position(3, 'A'), unit='C2', pool_id='p2'),
            GachaStep(source=Position(3, 'A'), target=Position(4, 'A'), unit='C3', pool_id='p1'),
        )
        r = GachaResult(total_cost=3, path=steps, collected=set())
        by_pool = r.steps_by_pool()

        assert len(by_pool['p1']) == 2
        assert len(by_pool['p2']) == 1

    def test_empty_pool_id_uses_default(self):
        steps = (
            GachaStep(source=Position(1, 'A'), target=Position(2, 'A'), unit='C1', pool_id=''),
        )
        r = GachaResult(total_cost=1, path=steps, collected=set())
        assert 'default' in r.steps_by_pool()

    def test_empty_path(self):
        r = GachaResult(total_cost=0, path=(), collected=set())
        assert r.steps_by_pool() == {}


class TestGachaResultToSummary:

    def test_contains_key_info(self):
        steps = (
            GachaStep(
                source=Position(1, 'A'), target=Position(11, 'B'),
                unit='超激貓', pool_id='test_pool',
                is_guaranteed=True, is_track_switch=True,
            ),
        )
        r = GachaResult(
            total_cost=10, path=steps,
            collected={'超激貓'},
            start_pos=Position(1, 'A'),
            target_cats={'超激貓'},
        )
        summary = r.to_summary()

        assert '模擬結果' in summary
        assert '總花費: 10 抽' in summary
        assert '十連 1' in summary
        assert '超激貓' in summary
        assert '跳軌' in summary
        assert 'test_pool' in summary

    def test_duplicate_rare_annotation(self):
        steps = (
            GachaStep(
                source=Position(1, 'A'), target=Position(2, 'B'),
                unit='替換貓', pool_id='p',
                is_duplicate_rare=True,
            ),
        )
        r = GachaResult(total_cost=1, path=steps, collected={'替換貓'})
        assert '重複稀有' in r.to_summary()


class TestGachaResultToDict:

    def test_required_keys(self):
        r = GachaResult(
            total_cost=5,
            path=(GachaStep(source=Position(1, 'A'), target=Position(2, 'A'), unit='C', pool_id='p'),),
            collected={'C'},
            start_pos=Position(1, 'A'),
        )
        d = r.to_dict()

        expected_keys = {
            'total_cost', 'step_count', 'guaranteed_count',
            'single_roll_count', 'collected', 'pools_used',
            'start_pos', 'end_pos', 'path',
        }
        assert set(d.keys()) == expected_keys

    def test_serializable_types(self):
        """所有值應為 JSON 可序列化的基本型別。"""
        import json
        r = GachaResult(
            total_cost=1,
            path=(GachaStep(source=Position(1, 'A'), target=Position(2, 'A'), unit='C', pool_id='p'),),
            collected={'C'},
            start_pos=Position(1, 'A'),
        )
        # 不應拋出例外
        json_str = json.dumps(r.to_dict(), ensure_ascii=False)
        assert '"total_cost": 1' in json_str

    def test_none_start_pos(self):
        r = GachaResult(total_cost=0, path=(), collected=set(), start_pos=None)
        d = r.to_dict()
        assert d['start_pos'] is None
        assert d['end_pos'] is None


# ── GachaNode ───────────────────────────────────────────────

class TestGachaNode:

    def test_init_with_slot_data(self):
        slot = GachaSlot(id=Position(1, 'A'), result='Cat1', guaranteed='G1')
        node = GachaNode(Position(1, 'A'), slot)
        assert node.position == Position(1, 'A')
        assert node.slot_data.result == 'Cat1'
        assert node.children == []
        assert node.parent is None
        assert node.step_from_parent is None

    def test_init_without_slot_data(self):
        node = GachaNode(Position(1, 'A'))
        assert node.slot_data is None

    def test_add_child(self):
        parent = GachaNode(Position(1, 'A'))
        child = GachaNode(Position(2, 'A'))
        step = GachaStep(source=Position(1, 'A'), target=Position(2, 'A'), unit='Cat1')

        parent.add_child(child, step)

        assert len(parent.children) == 1
        assert child.parent is parent
        assert child.step_from_parent is step

    def test_add_multiple_children(self):
        parent = GachaNode(Position(1, 'A'))
        children = [GachaNode(Position(i, 'A')) for i in range(2, 5)]
        for c in children:
            step = GachaStep(source=Position(1, 'A'), target=c.position, unit=f'Cat{c.position.id}')
            parent.add_child(c, step)

        assert len(parent.children) == 3
        assert all(c.parent is parent for c in children)

    def test_repr_with_slot(self):
        slot = GachaSlot(id=Position(1, 'A'), result='弓箭手貓', guaranteed='G')
        node = GachaNode(Position(1, 'A'), slot)
        r = repr(node)
        assert '1A' in r
        assert '弓箭手貓' in r

    def test_repr_without_slot(self):
        node = GachaNode(Position(5, 'B'))
        r = repr(node)
        assert '5B' in r
        assert 'Unknown' in r


# ── ParseStatus ─────────────────────────────────────────────

class TestParseStatus:

    def test_str_values(self):
        assert str(ParseStatus.MULTIPLE_DATA) == "Multiple Data Found"
        assert str(ParseStatus.INVALID_DATA) == "Invalid Data"
        assert str(ParseStatus.AMBIGUOUS) == "Ambiguous Position"


# ── Track ───────────────────────────────────────────────────

class TestTrack:

    def test_get_slot_found(self):
        slots = [
            GachaSlot(id=Position(1, 'A'), result='C1', guaranteed='G1'),
            GachaSlot(id=Position(2, 'A'), result='C2', guaranteed='G2'),
        ]
        track = Track("A", slots)
        slot = track.get_slot(2)
        assert slot is not None
        assert slot.result == 'C2'

    def test_get_slot_not_found(self):
        track = Track("A", [
            GachaSlot(id=Position(1, 'A'), result='C1', guaranteed='G1'),
        ])
        assert track.get_slot(99) is None

    def test_get_slot_empty_track(self):
        track = Track("A", [])
        assert track.get_slot(1) is None

    def test_repr(self):
        track = Track("A", [
            GachaSlot(id=Position(1, 'A'), result='C1', guaranteed='G1'),
        ])
        r = repr(track)
        assert "name='A'" in r
        assert 'slots=1' in r

    def test_list_operations(self):
        """Track 繼承 UserList，基本列表操作應正常。"""
        slot = GachaSlot(id=Position(1, 'A'), result='C1', guaranteed='G1')
        track = Track("A", [slot])
        assert len(track) == 1
        assert track[0] is slot

    def test_index_built_on_init(self):
        """Track 建構時應建立 _index 字典索引。"""
        slots = [
            GachaSlot(id=Position(1, 'A'), result='C1', guaranteed='G1'),
            GachaSlot(id=Position(5, 'A'), result='C5', guaranteed='G5'),
            GachaSlot(id=Position(10, 'A'), result='C10', guaranteed='G10'),
        ]
        track = Track("A", slots)
        assert hasattr(track, '_index')
        assert isinstance(track._index, dict)
        assert len(track._index) == 3

    def test_get_slot_uses_dict_lookup(self):
        """get_slot 透過 dict 查找而非線性掃描，不連續 id 也能正確取得。"""
        slots = [
            GachaSlot(id=Position(3, 'A'), result='C3', guaranteed='G3'),
            GachaSlot(id=Position(100, 'A'), result='C100', guaranteed='G100'),
        ]
        track = Track("A", slots)
        assert track.get_slot(3).result == 'C3'
        assert track.get_slot(100).result == 'C100'
        assert track.get_slot(50) is None


# ── GachaPool ───────────────────────────────────────────────

class TestGachaPool:

    def test_defaults(self):
        pool = GachaPool(
            pool_id='test', name='Test',
            track_a=Track("A", []), track_b=Track("B", []),
        )
        assert pool.has_guaranteed is True
        assert pool.guaranteed_cost == 10
