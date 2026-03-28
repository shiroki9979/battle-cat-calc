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

        res, gua, pos, orig, dup, orig_gua = parser._extract_cols_data(cols)
        assert res == ""
        assert dup is False

    def test_empty_cols(self):
        """空欄位列表不應崩潰。"""
        parser = BCParser.__new__(BCParser)
        parser.regex_next_pos = r'(?:->|<-)\s*(\d+)([AB])'

        res, gua, pos, orig, dup, orig_gua = parser._extract_cols_data([])
        assert res == ""
        assert gua == ""
        assert pos is None

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
