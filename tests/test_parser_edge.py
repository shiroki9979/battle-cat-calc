"""測試 BCParser 的邊界條件與未覆蓋路徑。

動機：原有 parser 測試僅覆蓋基本解析和驗證；get_tracks、畸形 HTML、
     邊界 rowspan 等情境未涵蓋。
方法：構造各種邊界 HTML 結構，驗證解析行為。
"""
import pytest
from battle_cat_calc.parser import BCParser
from battle_cat_calc.models import Position, Track, GachaSlot


class TestGetTracks:

    def test_get_tracks_returns_both(self):
        """get_tracks() 應回傳與 track_a/track_b 相同的物件。"""
        html = (
            "<html><body><table>"
            "<tr><td id='N1A'><a href='/u/1'>CatA1</a></td>"
            "<td><a href='/u/g1'>GuaA1</a> -> 11B</td>"
            "<td><a href='/u/g1'>GuaB1</a> -> 12B</td>"
            "<td id='N1B'><a href='/u/2'>CatB1</a></td></tr>"
            "<tr><td id='N2A'><a href='/u/3'>CatA2</a></td>"
            "<td><a href='/u/g2'>GuaA2</a> -> 12B</td>"
            "<td><a href='/u/g2'>GuaB2</a> -> 13B</td>"
            "<td id='N2B'><a href='/u/4'>CatB2</a></td></tr>"
            "</table></body></html>"
        )
        parser = BCParser(html)
        ta, tb = parser.get_tracks()

        assert ta is parser.track_a
        assert tb is parser.track_b


class TestParserEdgeCases:

    def test_no_a_tags_in_cell(self):
        """格子內無 <a> 標籤時，結果應為空字串（不崩潰）。"""
        parser = BCParser.__new__(BCParser)
        parser.regex_next_pos = r'(?:->|<-)\s*(\d+)([AB])'

        from bs4 import BeautifulSoup
        col_html = '<td>純文字無連結</td>'
        cols = [BeautifulSoup(col_html, "html.parser").td]

        data = parser._extract_cols_data(cols)
        assert data.result == ""
        assert data.is_duplicate is False

    def test_empty_cols(self):
        """空欄位列表不應崩潰。"""
        parser = BCParser.__new__(BCParser)
        parser.regex_next_pos = r'(?:->|<-)\s*(\d+)([AB])'

        data = parser._extract_cols_data([])
        assert data.result == ""
        assert data.guaranteed == ""
        assert data.result_pos is None
        assert data.guaranteed_pos is None

    def test_missing_cell_is_skipped(self):
        """欄位被 rowspan 蓋住時以 None 表示，不應崩潰，也不應影響另一欄。"""
        parser = BCParser.__new__(BCParser)
        parser.regex_next_pos = r'(?:->|<-)\s*(\d+)([AB])'

        from bs4 import BeautifulSoup
        gua = BeautifulSoup('<td><a href="">超激A</a> -> 11B</td>', "html.parser").td

        data = parser._extract_cols_data([None, gua])
        assert data.result == ""
        assert data.guaranteed == "超激A"
        assert data.guaranteed_pos == Position(11, 'B')
        assert data.result_pos is None

    def test_position_link_filtered(self):
        """位置連結（如 '21B'）應被過濾，不視為貓咪名稱。"""
        parser = BCParser.__new__(BCParser)

        from bs4 import BeautifulSoup
        html = '<td><a href="">21B</a><a href="">真正的貓</a></td>'
        tags = BeautifulSoup(html, "html.parser").find_all("a")
        names = parser._filter_cat_names(tags)

        assert names == ["真正的貓"]

    def test_icon_tag_filtered(self):
        """含🐾或 <img> 的標籤應被過濾。"""
        parser = BCParser.__new__(BCParser)

        from bs4 import BeautifulSoup
        html = '<td><a href="">🐾</a><a href=""><img src="x.png"/></a><a href="">有效貓</a></td>'
        tags = BeautifulSoup(html, "html.parser").find_all("a")
        names = parser._filter_cat_names(tags)

        assert names == ["有效貓"]

    def test_validate_track_non_sequential_ids(self):
        """非連續 ID（如 1, 2, 4）驗證應失敗。"""
        parser = BCParser.__new__(BCParser)

        track = Track("A", [
            GachaSlot(id=Position(1, 'A'), result='C1', guaranteed=''),
            GachaSlot(id=Position(2, 'A'), result='C2', guaranteed=''),
            GachaSlot(id=Position(4, 'A'), result='C4', guaranteed=''),
        ])
        assert parser._validate_track(track) is False

    def test_finalize_results_single(self):
        """單一結果不觸發重複稀有。"""
        parser = BCParser.__new__(BCParser)
        res, orig, is_dup = parser._finalize_results(["CatA"], False)
        assert res == "CatA"
        assert orig is None
        assert is_dup is False

    def test_finalize_results_multiple(self):
        """多個結果觸發重複稀有：最後一個為 result，第一個為 original。"""
        parser = BCParser.__new__(BCParser)
        res, orig, is_dup = parser._finalize_results(["Original", "Replacement"], False)
        assert res == "Replacement"
        assert orig == "Original"
        assert is_dup is True

    def test_finalize_results_empty(self):
        """空結果列表回傳空字串。"""
        parser = BCParser.__new__(BCParser)
        res, orig, is_dup = parser._finalize_results([], False)
        assert res == ""
        assert orig is None

    def test_finalize_guaranteed_single(self):
        """單一保底正常回傳。"""
        parser = BCParser.__new__(BCParser)
        gua, orig_gua = parser._finalize_guaranteed(["超激A"])
        assert gua == "超激A"
        assert orig_gua is None

    def test_finalize_guaranteed_multiple(self):
        """多個保底時，最後一個為 guaranteed，第一個為 original。"""
        parser = BCParser.__new__(BCParser)
        gua, orig_gua = parser._finalize_guaranteed(["OrigGua", "FinalGua"])
        assert gua == "FinalGua"
        assert orig_gua == "OrigGua"

    def test_finalize_guaranteed_empty(self):
        """空保底列表回傳空字串。"""
        parser = BCParser.__new__(BCParser)
        gua, orig_gua = parser._finalize_guaranteed([])
        assert gua == ""
        assert orig_gua is None

    def test_get_all_cats_excludes_parse_status(self):
        """get_all_cats 應排除 ParseStatus 類型的值。"""
        from battle_cat_calc.models import ParseStatus
        html = (
            "<html><body><table>"
            "<tr><td id='N1A'><a href='/u/1'>CatA</a></td>"
            "<td><a href='/u/g'>GuaA</a> -> 11B</td>"
            "<td><a href='/u/g'>GuaB</a> -> 12B</td>"
            "<td id='N1B'><a href='/u/2'>CatB</a></td></tr>"
            "<tr><td id='N2A'><a href='/u/3'>CatA2</a></td>"
            "<td><a href='/u/g2'>GuaA2</a> -> 12B</td>"
            "<td><a href='/u/g2'>GuaB2</a> -> 13B</td>"
            "<td id='N2B'><a href='/u/4'>CatB2</a></td></tr>"
            "</table></body></html>"
        )
        parser = BCParser(html)
        # 手動注入 ParseStatus
        parser.track_a = Track("A", [
            GachaSlot(id=Position(1, 'A'), result="有效貓", guaranteed="有效保底"),
            GachaSlot(id=Position(2, 'A'), result=ParseStatus.INVALID_DATA, guaranteed=""),
        ])
        parser.track_b = Track("B", [])

        cats = parser.get_all_cats()
        assert "有效貓" in cats
        assert "有效保底" in cats
        assert str(ParseStatus.INVALID_DATA) not in cats


# 欄位：0=A 編號, 1=A 結果, 2=A 必中, 3=B 結果(Alt.), 4=B 必中(Alt.), 5=B 編號。
# 仿照真實表格的重疊結構：B1 是重複稀有格（佔第 0~2 列），A1 起點與 B1 的「替換前」同列，
# A1 的稀有度儲存格跨 2 列，因此第 2 列的兩個 <td> 其實是 B1 的替換結果，不是 A1 的。
OVERLAP_HTML = (
    "<html><body><table>"
    "<tr><td></td><td></td><td></td><td></td><td></td><td id='N1B' rowspan='3'>1B</td></tr>"
    "<tr><td id='N1A' rowspan='3'>1A</td><td rowspan='2'></td><td rowspan='2'></td>"
    "<td><a href='/u/o'>Orig</a></td><td><a href='/u/og'>OrigGua</a> <- 11A</td></tr>"
    "<tr><td><a href='/u/r'>Repl</a> <- 3A</td><td><a href='/u/rg'>ReplGua</a> -> 12B</td></tr>"
    "<tr><td><a href='/u/a1'>CatA1</a></td><td><a href='/u/ga1'>GuaA1</a> -> 11B</td>"
    "<td></td><td></td><td id='N2B' rowspan='2'>2B</td></tr>"
    "<tr><td id='N2A' rowspan='2'>2A</td><td></td><td></td>"
    "<td><a href='/u/b2'>CatB2</a></td><td><a href='/u/gb2'>GuaB2</a> -> 13B</td></tr>"
    "<tr><td><a href='/u/a2'>CatA2</a></td><td><a href='/u/ga2'>GuaA2</a> -> 12B</td></tr>"
    "</table></body></html>"
)


class TestRowspanOverlap:
    """rowspan 讓同一列的第 N 個 <td> 不一定是第 N 欄。"""

    def test_covered_cells_not_misattributed(self):
        """A1 的欄位被蓋住的那一列，不應把 B1 的替換結果算成 A1 的。

        舊版以「這一列的前兩個 <td>」取 A 的欄位，會把 Repl 讀成 A1 的原始貓而誤判為重複稀有。
        """
        slot = BCParser(OVERLAP_HTML).track_a.get_slot(1)

        assert slot.result == "CatA1"
        assert slot.guaranteed == "GuaA1"
        assert slot.is_duplicate_rare is False
        assert slot.original_result is None
        assert slot.next_pos == Position(11, 'B')
        assert slot.single_next_pos is None

    def test_duplicate_slot_keeps_both_arrows_apart(self):
        """重複稀有格：結果欄箭頭是單抽落點，必中欄箭頭是必中落點，兩者不可互相覆蓋。"""
        slot = BCParser(OVERLAP_HTML).track_b.get_slot(1)

        assert slot.is_duplicate_rare is True
        assert (slot.original_result, slot.result) == ("Orig", "Repl")
        assert (slot.original_guaranteed, slot.guaranteed) == ("OrigGua", "ReplGua")
        assert slot.single_next_pos == Position(3, 'A')     # 結果欄 "<- 3A"
        assert slot.next_pos == Position(12, 'B')           # 替換後的必中欄 "-> 12B"
        assert slot.original_next_pos == Position(11, 'A')  # 替換前的必中欄 "<- 11A"

    def test_slots_after_overlap_are_unaffected(self):
        """重疊之後的格子仍正確歸屬。"""
        parser = BCParser(OVERLAP_HTML)
        a2, b2 = parser.track_a.get_slot(2), parser.track_b.get_slot(2)

        assert (a2.result, a2.guaranteed, a2.next_pos) == ("CatA2", "GuaA2", Position(12, 'B'))
        assert (b2.result, b2.guaranteed, b2.next_pos) == ("CatB2", "GuaB2", Position(13, 'B'))
        assert a2.is_duplicate_rare is False and b2.is_duplicate_rare is False
