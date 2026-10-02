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
from battle_cat_calc.models import Position, GachaPool, CatRarity


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

    # 以下重複稀有的期望值取自 HTML 原文（<tr> 以 0 起算；欄位以 rowspan 還原後的網格為準）：
    #   第 19 列  B9 的貓                  「拳擊手貓」
    #   第 21 列  B10 替換前               「拳擊手貓」 必中「<- 21A 白夜姬零」
    #   第 22 列  B10 替換後               「<- 12A 侏羅貓」 必中「EVA初號機 -> 21B」
    #   第 21~23 列 A11（3 列只是配合 B10 的版面拉伸）結果欄只有第 23 列的「明日香貓」

    def test_duplicate_rare_slot_b10(self, parsed):
        """B10 是同軌連續重複：B9、B10 的原始貓都是拳擊手貓，被替換成侏羅貓。"""
        assert parsed.track_b.get_slot(9).result == "拳擊手貓"

        slot = parsed.track_b.get_slot(10)
        assert slot.is_duplicate_rare is True
        assert slot.original_result == "拳擊手貓"
        assert slot.result == "侏羅貓"

    def test_duplicate_rare_slot_b10_arrows(self, parsed):
        """B10 的單抽落點（結果欄 <- 12A）與必中落點（必中欄）是不同的箭頭，不可混用。"""
        slot = parsed.track_b.get_slot(10)
        assert slot.single_next_pos == Position(12, 'A')
        assert slot.next_pos == Position(21, 'B')           # 替換後：EVA初號機 -> 21B
        assert slot.original_next_pos == Position(21, 'A')  # 替換前：白夜姬零 <- 21A
        assert slot.original_guaranteed == "白夜姬零"
        assert slot.guaranteed == "EVA初號機"

    def test_slot_a11_is_not_duplicate(self, parsed):
        """A11 佔 3 列只是版面拉伸，結果欄只有「明日香貓」，不是重複稀有格。

        第 22 列的「侏羅貓」是 B10 的替換結果；舊版 parser 誤把它當成 A11 的原始貓。
        """
        slot = parsed.track_a.get_slot(11)
        assert slot.is_duplicate_rare is False
        assert slot.original_result is None
        assert slot.result == "明日香貓"
        assert slot.guaranteed == "EVA初號機"
        assert slot.next_pos == Position(21, 'B')
        assert slot.single_next_pos is None

    def test_end_of_table_duplicate_a100(self, parsed):
        """A100：A99、A100 的原始貓都是侏羅貓，替換成薰貓；HTML 標 <?>，落點超出表格範圍。"""
        assert parsed.track_a.get_slot(99).result == "侏羅貓"

        slot = parsed.track_a.get_slot(100)
        assert slot.is_duplicate_rare is True
        assert slot.original_result == "侏羅貓"
        assert slot.result == "薰貓"
        assert slot.single_next_pos is None

    def test_slot_b100_is_not_duplicate(self, parsed):
        """B100 的結果欄只有「魔劍士貓」；「薰貓」是 A100 的替換結果，舊版 parser 誤讀進來。"""
        slot = parsed.track_b.get_slot(100)
        assert slot.is_duplicate_rare is False
        assert slot.original_result is None
        assert slot.result == "魔劍士貓"

    def test_duplicate_rare_slots_in_fixture(self, parsed):
        """整份 fixture 只有 B10 與 A100 兩個重複稀有格。

        HTML 中佔 3 列的格子有 A11、B10、A100、B100 四個，其中 A11、B100 只是配合
        相鄰重複稀有格的版面拉伸（見上方各測試）。
        """
        flagged = {
            (s.id.id, s.id.track)
            for track in (parsed.track_a, parsed.track_b)
            for s in track
            if s.is_duplicate_rare
        }
        assert flagged == {(10, 'B'), (100, 'A')}

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

    # ── 重複稀有（B9、B10 的原始貓都是拳擊手貓；HTML 標示 B10 單抽落在 12A）──

    def test_duplicate_rare_single_roll_follows_site_arrow(self, sim):
        """從 B9 連抽：B10 觸發重複稀有，拿到侏羅貓並照網站箭頭落在 12A（換軌且前進 2 格）。

        舊版沒有記住 B9 的貓，不會觸發重複，走到 11B。
        """
        result = sim.find_best_path(Position(9, 'B'), {"侏羅貓"}, 5)
        assert result is not None
        assert result.total_cost == 2

        first, second = result.path
        assert (first.source, first.target, first.unit) == (Position(9, 'B'), Position(10, 'B'), "拳擊手貓")
        assert first.is_duplicate_rare is False
        assert (second.source, second.target, second.unit) == (Position(10, 'B'), Position(12, 'A'), "侏羅貓")
        assert second.is_duplicate_rare is True
        assert second.is_track_switch is True

    def test_duplicate_rare_not_triggered_gives_original_cat(self, sim):
        """直接站在 B10（上一隻不是拳擊手貓）不會觸發重複：拿到替換前的拳擊手貓，同軌前進 1 格。"""
        result = sim.find_best_path(Position(10, 'B'), {"拳擊手貓"}, 2)
        assert result is not None
        assert result.total_cost == 1
        step, = result.path
        assert (step.target, step.unit, step.is_duplicate_rare) == (Position(11, 'B'), "拳擊手貓", False)

        # 替換後的侏羅貓在沒觸發時拿不到
        assert sim.find_best_path(Position(10, 'B'), {"侏羅貓"}, 1) is None

    def test_guaranteed_window_with_duplicate_stays_on_track(self, sim):
        """B1 的必中十連包含 B10 的重複稀有（奇數次），落在 12B 而不是換軌。

        HTML：1B 必中欄「白夜姬零 -> 12B」。
        """
        result = sim.find_best_path(Position(1, 'B'), {"白夜姬零"}, 10)
        assert result is not None
        step, = result.path
        assert step.is_guaranteed is True
        assert step.target == Position(12, 'B')
        assert step.is_track_switch is False

    def test_heuristic_rate_accounts_for_duplicate_advance(self, sim):
        """重複稀有單抽前進 2 格，啟發式的每格最低成本不可高於 0.5，否則不再是下界。"""
        sim.find_best_path(Position(1, 'A'), {"貓咪陰陽師"}, 2)
        assert sim._min_cost_per_advance <= 0.5

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


# ── 重複稀有規則的交叉驗證 ─────────────────────────────────

class TestDuplicateRareCrossCheck:
    """兩種彼此獨立的 HTML 箭頭必須互相一致，用來檢驗模擬器的單抽轉換規則。

    - 照單抽規則（含結果欄的重複稀有落點）走 10 抽，再讓必中超激消耗 1 個亂數：
      A_n 的亂數編號是奇數、B_n 是偶數，所以 A_n → B_n，B_n → A_(n+1)。
    - 與表格「必中欄」的箭頭比對（例如 1A → 11B、1B → 12B、12A → 22B）。

    前者靠結果欄的箭頭（B10 → 12A），後者靠必中欄的箭頭，兩者互不引用。
    若重複稀有的落點判斷錯誤，所有視窗包含 B10 的起點（B1~B10）都會對不上。
    此測試使用 _resolve_single_roll（模擬器沒有對應的公開介面）。
    """

    @pytest.mark.parametrize("fixture_name", ["parsed", "parsed_1020"])
    def test_ten_roll_walk_lands_where_table_says(self, request, fixture_name):
        data = request.getfixturevalue(fixture_name)
        sim = GachaSimulator(data.track_a, data.track_b)
        tracks = {'A': data.track_a, 'B': data.track_b}

        checked, mismatches = 0, []
        for track in tracks.values():
            for slot in track:
                if not isinstance(slot.next_pos, Position):
                    continue  # 落點超出表格範圍
                # 表格的必中欄假設起始格自己的重複稀有被觸發
                last = slot.original_result if slot.is_duplicate_rare else None
                pos = slot.id
                for _ in range(10):
                    current = tracks[pos.track].get_slot(pos.id)
                    outcome = sim._resolve_single_roll(current, last) if current else None
                    if outcome is None:
                        break
                    last, pos = outcome.unit, outcome.next_pos
                else:
                    expected = Position(pos.id, 'B') if pos.track == 'A' else Position(pos.id + 1, 'A')
                    checked += 1
                    if expected != slot.next_pos:
                        mismatches.append((slot.id, expected, slot.next_pos))

        assert mismatches == []
        assert checked >= 150  # 避免因為視窗全被略過而空轉通過


# ── 最優性交叉驗證：A* 必須與關閉啟發式的精確搜尋一致 ────────

class TestOptimalityCrossCheck:
    """啟發式一旦高估，A* 就可能回傳較差的解；用 h=0 的 Dijkstra 當基準做隨機抽查。"""

    @staticmethod
    def _exact(sim, *args, **kwargs):
        original = sim._heuristic
        sim._heuristic = lambda *_: 0.0
        try:
            return sim.find_best_path(*args, **kwargs)
        finally:
            sim._heuristic = original

    def _check(self, sim, cats, trials, seed):
        import random
        rng = random.Random(seed)
        cats = sorted(cats)
        for _ in range(trials):
            targets = set(rng.sample(cats, rng.randint(1, 3)))
            start = Position(rng.randint(1, 60), rng.choice('AB'))
            astar = sim.find_best_path(start, targets, 80)
            exact = self._exact(sim, start, targets, 80)
            assert (astar and astar.total_cost) == (exact and exact.total_cost), (start, targets)

    def test_single_pool(self, parsed):
        self._check(GachaSimulator(parsed.track_a, parsed.track_b), parsed.get_all_cats(), trials=40, seed=11)

    def test_multi_pool(self, multi_pool_sim, parsed, parsed_1020):
        self._check(multi_pool_sim, parsed.get_all_cats() | parsed_1020.get_all_cats(), trials=30, seed=12)


# ── 稀有度：取自頁面目錄，不是儲存格的 CSS class ──────────────

class TestRarityFromCatalog:
    """HTML 目錄（#find_select）：Rare 29 隻、Super 20、Uber 7、Legendary 1。
    儲存格 class 是 godfat 依慶典費率上色的「稀有度帶」：拳擊手貓的 class 是 supa_fest、
    貓咪番長是 uber_fest，目錄卻分別是 Rare、Super。遊戲的連續重複判定要看目錄。"""

    def test_result_rarity_follows_catalog_not_css_class(self, parsed):
        assert parsed.track_b.get_slot(9).result == "拳擊手貓"
        assert parsed.track_b.get_slot(9).rarity is CatRarity.RARE          # class: supa_fest
        assert parsed.track_a.get_slot(13).result == "貓咪番長"
        assert parsed.track_a.get_slot(13).rarity is CatRarity.SUPER_RARE   # class: uber_fest
        assert parsed.track_a.get_slot(1).rarity is CatRarity.RARE          # 貓咪陰陽師
        assert parsed.track_a.get_slot(18).rarity is CatRarity.UBER_RARE    # EVA零號機

    def test_guaranteed_rarity_from_catalog(self, parsed):
        assert parsed.track_a.get_slot(1).guaranteed == "第10使徒"
        assert parsed.track_a.get_slot(1).guaranteed_rarity is CatRarity.UBER_RARE

    def test_duplicate_rare_slots_are_rare(self, parsed):
        for slot in (parsed.track_b.get_slot(10), parsed.track_a.get_slot(100)):
            assert slot.is_duplicate_rare and slot.rarity is CatRarity.RARE


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
        """從 B 軌起始尋找「第9使徒」（1020獨佔），cost=20。

        最佳路徑：單抽 B1~B10（B9 抽到拳擊手貓，B10 觸發重複稀有，落在 12A，1 抽前進 2 格），
        再於 1020 的 12A 做必中十連。1020 的 12A 必中欄為「第9使徒 -> 22B」。
        舊版漏掉重複稀有的換軌，只能走兩次必中而得到 24。
        """
        result = multi_pool_sim.find_best_path(
            Position(1, 'B'), {"第9使徒"}, 30,
        )
        assert result is not None
        assert result.total_cost == 20

        *singles, last = result.path
        assert last.is_guaranteed is True
        assert (last.pool_id, last.source, last.target) == ("1020", Position(12, 'A'), Position(22, 'B'))
        assert any(s.is_duplicate_rare and s.target == Position(12, 'A') for s in singles)

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
