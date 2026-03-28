"""快照整合測試：使用真實 HTML fixture 驗證 parser → simulator 完整資料流。

資料來源：
  seed=2127986414, event=2025-12-12_1019, lang=tw, count=100
  存放於 tests/fixtures/seed_2127986414_event_1019.html

動機：單元測試使用手工建構的小型資料，無法覆蓋真實 HTML 的複雜結構
     （如 rowspan、重複稀有、多欄保底）。快照測試以預錄 HTML 為輸入，
     驗證解析和模擬結果是否與已知答案一致，兼具真實性與確定性。
方法：讀取 fixture 檔案，執行 parser 和 simulator，與預先驗證的期望值比對。
"""
import pathlib
import pytest
from battle_cat_calc.parser import BCParser
from battle_cat_calc.simulator import GachaSimulator
from battle_cat_calc.analyzer import SeedAnalyzer
from battle_cat_calc.models import Position, GachaPool


FIXTURE_DIR = pathlib.Path(__file__).parent / "fixtures"
SNAPSHOT_HTML = FIXTURE_DIR / "seed_2127986414_event_1019.html"


@pytest.fixture(scope="module")
def parsed():
    """一次性解析快照 HTML，供所有測試共用。"""
    html = SNAPSHOT_HTML.read_text(encoding="utf-8")
    return BCParser(html)


# ── Parser 快照驗證 ────────────────────────────────────────

class TestParserSnapshot:

    def test_track_lengths(self, parsed):
        """Track A/B 應各有 100 個格子（count=100）。"""
        assert len(parsed.track_a) == 100
        assert len(parsed.track_b) == 100

    def test_track_a_first_slot(self, parsed):
        """Track A 第 1 格的解析結果。"""
        slot = parsed.track_a[0]
        assert slot.id == Position(1, 'A')
        assert slot.result == "貓咪陰陽師"
        assert slot.guaranteed == "第10使徒"
        assert slot.next_pos == Position(11, 'B')
        assert slot.is_duplicate_rare is False
        assert slot.original_result is None

    def test_track_b_first_slot(self, parsed):
        """Track B 第 1 格的解析結果。"""
        slot = parsed.track_b[0]
        assert slot.id == Position(1, 'B')
        assert slot.result == "貓咪陰陽師"
        assert slot.guaranteed == "白夜姬零"
        assert slot.next_pos == Position(12, 'B')

    def test_track_a_sequential_ids(self, parsed):
        """Track A 的 ID 應為 1~100 連續整數。"""
        ids = [s.id.id for s in parsed.track_a]
        assert ids == list(range(1, 101))

    def test_track_b_sequential_ids(self, parsed):
        """Track B 的 ID 應為 1~100 連續整數。"""
        ids = [s.id.id for s in parsed.track_b]
        assert ids == list(range(1, 101))

    def test_duplicate_rare_slot_a11(self, parsed):
        """A11 應為重複稀有格（original=侏羅貓, result=明日香貓, next=21B）。"""
        slot = parsed.track_a.get_slot(11)
        assert slot is not None
        assert slot.is_duplicate_rare is True
        assert slot.original_result == "侏羅貓"
        assert slot.result == "明日香貓"
        assert slot.next_pos == Position(21, 'B')

    def test_duplicate_rare_slot_b10(self, parsed):
        """B10 應為重複稀有格。"""
        slot = parsed.track_b.get_slot(10)
        assert slot is not None
        assert slot.is_duplicate_rare is True
        assert slot.original_result == "拳擊手貓"
        assert slot.result == "侏羅貓"
        assert slot.next_pos == Position(21, 'B')

    def test_specific_slots_spot_check(self, parsed):
        """抽樣驗證幾個已知位置的解析正確性。"""
        # A2
        a2 = parsed.track_a.get_slot(2)
        assert a2.result == "拳擊手貓"
        assert a2.guaranteed == "白夜姬零"

        # A5
        a5 = parsed.track_a.get_slot(5)
        assert a5.result == "占卜貓"
        assert a5.guaranteed == "EVA零號機"

        # B4
        b4 = parsed.track_b.get_slot(4)
        assert b4.result == "薰貓"
        assert b4.guaranteed == "EVA零號機"

    def test_get_all_cats(self, parsed):
        """get_all_cats 應回傳非空集合，包含已知貓咪。"""
        cats = parsed.get_all_cats()
        assert len(cats) > 0
        assert "貓咪陰陽師" in cats
        assert "第10使徒" in cats
        assert "明日香貓" in cats

    def test_get_all_cats_count(self, parsed):
        """此種子/活動共 54 種不同貓咪。"""
        assert len(parsed.get_all_cats()) == 54


# ── Simulator 快照驗證 ─────────────────────────────────────

class TestSimulatorSnapshot:

    @pytest.fixture
    def sim(self, parsed):
        return GachaSimulator(parsed.track_a, parsed.track_b)

    def test_guaranteed_path_for_target(self, sim):
        """「第10使徒」在 A1 保底，最佳路徑應為 cost=10（一次十連）。"""
        result = sim.find_best_path(Position(1, 'A'), {"第10使徒"}, 20)
        assert result is not None
        assert result.total_cost == 10
        assert result.guaranteed_count == 1
        assert "第10使徒" in result.collected

    def test_multi_target_path(self, sim):
        """「第10使徒」+「EVA零號機」的最佳路徑 cost=21。"""
        result = sim.find_best_path(
            Position(1, 'A'), {"第10使徒", "EVA零號機"}, 50,
        )
        assert result is not None
        assert result.total_cost == 21
        assert {"第10使徒", "EVA零號機"} <= result.collected

    def test_single_roll_optimal_path(self, sim):
        """「明日香貓」cost=11（1 單抽 + 1 十連，或其他等價路徑）。"""
        result = sim.find_best_path(Position(1, 'A'), {"明日香貓"}, 20)
        assert result is not None
        assert result.total_cost == 11

    def test_start_from_track_b(self, sim):
        """從 B1 起始尋找「薰貓」（B4），cost=4。"""
        result = sim.find_best_path(Position(1, 'B'), {"薰貓"}, 20)
        assert result is not None
        assert result.total_cost == 4

    def test_beam_search_snapshot(self, sim):
        """Beam Search 在此快照上應找到相同或相近的結果。"""
        result = sim.find_best_path(
            Position(1, 'A'), {"第10使徒", "EVA零號機"}, 50,
            beam_width=100,
        )
        assert result is not None
        assert {"第10使徒", "EVA零號機"} <= result.collected
        # Beam Search 可能非最佳解，但不應偏離太多
        assert result.total_cost <= 30

    def test_progress_callback_snapshot(self, sim):
        """progress_callback 應在真實資料上正常運作。"""
        call_count = 0
        def cb(cost, processed, queue):
            nonlocal call_count
            call_count += 1

        sim.find_best_path(
            Position(1, 'A'), {"貓咪陰陽師"}, 5,
            progress_callback=cb,
        )
        assert call_count > 0

    def test_prefer_guaranteed_snapshot(self, sim):
        """prefer_guaranteed 在真實資料上應偏好保底路徑。"""
        result = sim.find_best_path(
            Position(1, 'A'), {"第10使徒"}, 20,
            prefer_guaranteed=True,
        )
        assert result is not None
        assert result.guaranteed_count >= 1

    def test_unreachable_target_snapshot(self, sim):
        """搜尋不存在的貓咪應拋出 ValueError。"""
        with pytest.raises(ValueError, match="完全不存在的貓咪XYZ"):
            sim.find_best_path(
                Position(1, 'A'), {"完全不存在的貓咪XYZ"}, 50,
            )

    def test_result_summary_format(self, sim):
        """to_summary() 在真實結果上應包含關鍵欄位。"""
        result = sim.find_best_path(Position(1, 'A'), {"第10使徒"}, 20)
        summary = result.to_summary()
        assert "模擬結果" in summary
        assert "總花費: 10 抽" in summary
        assert "第10使徒" in summary

    def test_result_to_dict_snapshot(self, sim):
        """to_dict() 在真實結果上應可序列化。"""
        import json
        result = sim.find_best_path(Position(1, 'A'), {"第10使徒"}, 20)
        d = result.to_dict()
        # 不應拋出例外
        json_str = json.dumps(d, ensure_ascii=False)
        assert "第10使徒" in json_str


# ── Analyzer 快照驗證 ──────────────────────────────────────

class TestAnalyzerSnapshot:

    @pytest.fixture
    def analyzer_a(self, parsed):
        return SeedAnalyzer(parsed.track_a)

    def test_find_cat_exact(self, analyzer_a):
        """精確搜尋「明日香貓」應命中多個位置。"""
        results = analyzer_a.find_cat_by_name("明日香貓")
        assert len(results) >= 2
        ids = [r.id.id for r in results]
        assert 11 in ids  # A11 已知有明日香貓

    def test_find_cat_partial(self, analyzer_a):
        """部分搜尋「明日香」應包含「明日香貓」和「小小明日香」。"""
        results = analyzer_a.find_cat_by_name("明日香")
        names = [r.result for r in results]
        assert "明日香貓" in names
        assert "小小明日香" in names

    def test_find_cat_no_match(self, analyzer_a):
        """搜尋不存在的貓應回傳空列表。"""
        results = analyzer_a.find_cat_by_name("不存在的貓")
        assert results == []

    def test_get_next_guaranteed(self, analyzer_a):
        """A1 的保底「第10使徒」應為第一個有效保底。"""
        slot = analyzer_a.get_next_guaranteed()
        assert slot is not None
        assert slot.id.id == 1
        assert slot.guaranteed == "第10使徒"


# ── 多池快照驗證 ───────────────────────────────────────────
# 資料來源：
#   Pool 1019: seed=2127986414, event=2025-12-12_1019 (EVA合作轉蛋)
#   Pool 1020: seed=2127986414, event=2025-12-12_1020 (EVA合作轉蛋2nd)
# 兩池各自擁有獨佔角色，需跨池才能收齊特定目標。

SNAPSHOT_HTML_1020 = FIXTURE_DIR / "seed_2127986414_event_1020.html"


@pytest.fixture(scope="module")
def parsed_1020():
    """解析第二池（event 1020）的 HTML。"""
    html = SNAPSHOT_HTML_1020.read_text(encoding="utf-8")
    return BCParser(html)


@pytest.fixture(scope="module")
def multi_pool_sim(parsed, parsed_1020):
    """建立雙池模擬器（對應 sim.ipynb 的使用方式）。"""
    pool_a = GachaPool(
        pool_id="1019", name="EVA合作轉蛋",
        track_a=parsed.track_a, track_b=parsed.track_b,
        has_guaranteed=True,
    )
    pool_b = GachaPool(
        pool_id="1020", name="EVA合作轉蛋2nd",
        track_a=parsed_1020.track_a, track_b=parsed_1020.track_b,
        has_guaranteed=True,
    )
    return GachaSimulator([pool_a, pool_b])


class TestParserSnapshot1020:
    """第二池的解析快照驗證。"""

    def test_track_lengths(self, parsed_1020):
        assert len(parsed_1020.track_a) == 100
        assert len(parsed_1020.track_b) == 100

    def test_track_a_first_slot(self, parsed_1020):
        slot = parsed_1020.track_a[0]
        assert slot.id == Position(1, 'A')
        assert slot.result == "貓咪陰陽師"
        assert slot.guaranteed == "第9使徒"
        assert slot.next_pos == Position(11, 'B')

    def test_track_b_first_slot(self, parsed_1020):
        slot = parsed_1020.track_b[0]
        assert slot.id == Position(1, 'B')
        assert slot.result == "貓咪陰陽師"
        assert slot.guaranteed == "獨眼少女明日香"

    def test_exclusive_cats(self, parsed_1020):
        """Pool 1020 應包含其獨佔角色，不包含 Pool 1019 獨佔角色。"""
        cats = parsed_1020.get_all_cats()
        # 1020 獨佔
        assert "第9使徒" in cats
        assert "獨眼少女明日香" in cats
        assert "EVA2號機" in cats
        assert "EVA第13號機" in cats
        # 1019 獨佔，不應出現
        assert "第10使徒" not in cats
        assert "EVA零號機" not in cats
        assert "白夜姬零" not in cats


class TestMultiPoolSnapshot:
    """多池模擬器的快照驗證（對應 sim.ipynb 的跨池搜尋情境）。"""

    def test_cross_pool_two_targets(self, multi_pool_sim):
        """跨池目標：「第10使徒」(1019獨佔) + 「第9使徒」(1020獨佔)，cost=25。"""
        result = multi_pool_sim.find_best_path(
            Position(1, 'A'), {"第10使徒", "第9使徒"}, 50,
        )
        assert result is not None
        assert result.total_cost == 25
        assert {"第10使徒", "第9使徒"} <= result.collected
        assert result.pools_used == {"1019", "1020"}

    def test_cross_pool_path_details(self, multi_pool_sim):
        """跨池路徑應包含來自不同池的步驟。"""
        result = multi_pool_sim.find_best_path(
            Position(1, 'A'), {"第10使徒", "第9使徒"}, 50,
        )
        pool_ids_in_path = {step.pool_id for step in result.path}
        assert "1019" in pool_ids_in_path
        assert "1020" in pool_ids_in_path

    def test_single_target_1019_only(self, multi_pool_sim):
        """「第10使徒」為 1019 獨佔超激，多池模式下 cost=10（一次十連）。"""
        result = multi_pool_sim.find_best_path(
            Position(1, 'A'), {"第10使徒"}, 20,
        )
        assert result is not None
        assert result.total_cost == 10
        assert result.guaranteed_count == 1
        assert "1019" in result.pools_used

    def test_single_target_1020_guaranteed(self, multi_pool_sim):
        """「獨眼少女明日香」在 1020 的 A2 保底可得，cost=11。"""
        result = multi_pool_sim.find_best_path(
            Position(1, 'A'), {"獨眼少女明日香"}, 20,
        )
        assert result is not None
        assert result.total_cost == 11

    def test_four_target_cross_pool(self, multi_pool_sim):
        """4 目標跨池（各 2 隻獨佔），cost=46。"""
        targets = {"第10使徒", "EVA零號機", "第9使徒", "獨眼少女明日香"}
        result = multi_pool_sim.find_best_path(
            Position(1, 'A'), targets, 60,
        )
        assert result is not None
        assert result.total_cost == 46
        assert targets <= result.collected
        assert result.pools_used == {"1019", "1020"}

    def test_start_from_track_b(self, multi_pool_sim):
        """從 B 軌起始尋找「第9使徒」（1020獨佔），cost=24。"""
        result = multi_pool_sim.find_best_path(
            Position(1, 'B'), {"第9使徒"}, 30,
        )
        assert result is not None
        assert result.total_cost == 24

    def test_beam_search_multi_pool(self, multi_pool_sim):
        """Beam Search 在多池模式下應仍能找到結果。"""
        result = multi_pool_sim.find_best_path(
            Position(1, 'A'), {"第10使徒", "第9使徒"}, 50,
            beam_width=100,
        )
        assert result is not None
        assert {"第10使徒", "第9使徒"} <= result.collected
        assert result.total_cost <= 30

    def test_prefer_guaranteed_multi_pool(self, multi_pool_sim):
        """prefer_guaranteed 在多池模式下應偏好保底。"""
        result = multi_pool_sim.find_best_path(
            Position(1, 'A'), {"第10使徒"}, 20,
            prefer_guaranteed=True,
        )
        assert result is not None
        assert result.guaranteed_count >= 1

    def test_unreachable_target_multi_pool(self, multi_pool_sim):
        """兩池都不存在的貓咪應拋出 ValueError。"""
        with pytest.raises(ValueError, match="完全不存在的XYZ"):
            multi_pool_sim.find_best_path(
                Position(1, 'A'), {"完全不存在的XYZ"}, 50,
            )

    def test_to_summary_multi_pool(self, multi_pool_sim):
        """to_summary() 在多池結果上應包含兩個池子名稱。"""
        result = multi_pool_sim.find_best_path(
            Position(1, 'A'), {"第10使徒", "第9使徒"}, 50,
        )
        summary = result.to_summary()
        assert "模擬結果" in summary
        assert "總花費: 25 抽" in summary
        assert "1019" in summary
        assert "1020" in summary

    def test_to_dict_multi_pool(self, multi_pool_sim):
        """to_dict() 在多池結果上應可 JSON 序列化。"""
        import json
        result = multi_pool_sim.find_best_path(
            Position(1, 'A'), {"第10使徒", "第9使徒"}, 50,
        )
        d = result.to_dict()
        json_str = json.dumps(d, ensure_ascii=False)
        assert "第10使徒" in json_str
        assert "第9使徒" in json_str

    def test_progress_callback_multi_pool(self, multi_pool_sim):
        """progress_callback 在多池搜尋中應正常觸發。"""
        call_count = 0
        def cb(cost, processed, queue):
            nonlocal call_count
            call_count += 1

        multi_pool_sim.find_best_path(
            Position(1, 'A'), {"第10使徒"}, 20,
            progress_callback=cb,
        )
        assert call_count > 0

# ── 單池包裝為多池驗證 ─────────────────────────────────────

class TestSinglePoolAsMultiPool:

    def test_single_pool_as_gacha_pool(self, parsed):
        """將解析結果包裝為 GachaPool 進行多池模擬。"""
        pool = GachaPool(
            pool_id="event_1019",
            name="EVA合作",
            track_a=parsed.track_a,
            track_b=parsed.track_b,
            has_guaranteed=True,
        )
        sim = GachaSimulator([pool])
        result = sim.find_best_path(Position(1, 'A'), {"第10使徒"}, 20)
        assert result is not None
        assert result.total_cost == 10
        assert result.path[0].pool_id == "event_1019"
