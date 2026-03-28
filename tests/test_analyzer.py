"""測試 SeedAnalyzer 的查詢與分析功能。

動機：analyzer.py 原先零覆蓋，需驗證名稱搜尋、保底查詢等核心邏輯。
方法：以手工建構的 Track 資料隔離測試，涵蓋 happy / sad / edge cases。
"""
import pytest
from battle_cat_calc.analyzer import SeedAnalyzer
from battle_cat_calc.models import GachaSlot, Position, Track, ParseStatus


@pytest.fixture
def sample_track():
    """建立含有多種狀態格子的測試軌道。"""
    return Track("A", [
        GachaSlot(id=Position(1, 'A'), result="弓箭手貓", guaranteed="超激A"),
        GachaSlot(id=Position(2, 'A'), result="劍士貓", guaranteed="超激B"),
        GachaSlot(id=Position(3, 'A'), result="弓箭手貓", guaranteed=""),
        GachaSlot(id=Position(4, 'A'), result="魔法師貓", guaranteed=ParseStatus.INVALID_DATA),
        GachaSlot(id=Position(5, 'A'), result=ParseStatus.MULTIPLE_DATA, guaranteed="超激C"),
    ])


class TestFindCatByName:

    def test_exact_match(self, sample_track):
        """精確名稱應回傳所有出現位置。"""
        analyzer = SeedAnalyzer(sample_track)
        results = analyzer.find_cat_by_name("弓箭手貓")
        assert len(results) == 2
        assert results[0].id.id == 1
        assert results[1].id.id == 3

    def test_partial_match(self, sample_track):
        """部分名稱也應命中（使用 `in` 比對）。"""
        analyzer = SeedAnalyzer(sample_track)
        results = analyzer.find_cat_by_name("弓箭手")
        assert len(results) == 2

    def test_no_match(self, sample_track):
        """搜尋不存在的貓應回傳空列表。"""
        analyzer = SeedAnalyzer(sample_track)
        results = analyzer.find_cat_by_name("不存在的貓")
        assert results == []

    def test_empty_name(self, sample_track):
        """空字串應命中所有非 ParseStatus 的格子。"""
        analyzer = SeedAnalyzer(sample_track)
        results = analyzer.find_cat_by_name("")
        # slot 5 的 result 為 ParseStatus，應被跳過
        assert len(results) == 4

    def test_parse_status_result(self, sample_track):
        """result 為 ParseStatus 時應被跳過，不應拋出例外也不應命中。"""
        analyzer = SeedAnalyzer(sample_track)
        results = analyzer.find_cat_by_name("Multiple Data Found")
        assert len(results) == 0

    def test_single_result(self, sample_track):
        """只出現一次的貓應回傳長度為 1 的列表。"""
        analyzer = SeedAnalyzer(sample_track)
        results = analyzer.find_cat_by_name("劍士貓")
        assert len(results) == 1

    def test_empty_track(self):
        """空軌道搜尋應回傳空列表。"""
        analyzer = SeedAnalyzer(Track("A", []))
        assert analyzer.find_cat_by_name("任意") == []


class TestGetNextGuaranteed:

    def test_has_guaranteed(self, sample_track):
        """第一個有效保底格應為 id=1（guaranteed='超激A'）。"""
        analyzer = SeedAnalyzer(sample_track)
        slot = analyzer.get_next_guaranteed()
        assert slot is not None
        assert slot.id.id == 1
        assert slot.guaranteed == "超激A"

    def test_no_guaranteed(self):
        """所有保底都為空字串或 ParseStatus 時，應回傳 None。"""
        track = Track("A", [
            GachaSlot(id=Position(1, 'A'), result="Cat1", guaranteed=""),
            GachaSlot(id=Position(2, 'A'), result="Cat2", guaranteed=ParseStatus.INVALID_DATA),
        ])
        analyzer = SeedAnalyzer(track)
        assert analyzer.get_next_guaranteed() is None

    def test_empty_track(self):
        """空軌道應回傳 None。"""
        analyzer = SeedAnalyzer(Track("A", []))
        assert analyzer.get_next_guaranteed() is None

    def test_skip_parse_status(self):
        """應跳過 ParseStatus 類型的保底，回傳第一個字串保底。"""
        track = Track("A", [
            GachaSlot(id=Position(1, 'A'), result="Cat1", guaranteed=ParseStatus.AMBIGUOUS),
            GachaSlot(id=Position(2, 'A'), result="Cat2", guaranteed="超激X"),
        ])
        analyzer = SeedAnalyzer(track)
        slot = analyzer.get_next_guaranteed()
        assert slot.id.id == 2
        assert slot.guaranteed == "超激X"

    def test_skip_empty_string(self):
        """應跳過空字串保底。"""
        track = Track("A", [
            GachaSlot(id=Position(1, 'A'), result="Cat1", guaranteed=""),
            GachaSlot(id=Position(2, 'A'), result="Cat2", guaranteed="超激Y"),
        ])
        analyzer = SeedAnalyzer(track)
        slot = analyzer.get_next_guaranteed()
        assert slot.id.id == 2
