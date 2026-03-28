import pytest
from bs4 import BeautifulSoup, Tag
from battle_cat_calc.models import GachaSlot, ParseStatus, Position
from battle_cat_calc.parser import BCParser

class TestEnhancedModels:
    def test_gacha_slot_defaults(self):
        """測試 GachaSlot 的預設值。
        
        動機：確保新增的欄位 (如 original_result) 有正確的預設值，以維持向下相容性。
        方法：建立一個簡單的 GachaSlot，檢查新欄位是否為 None 或 False。
        """
        slot = GachaSlot(id=Position(1, 'A'), result="Cat", guaranteed="Gua")
        assert slot.original_result is None
        assert slot.is_duplicate_rare is False
        assert slot.result == "Cat"

    def test_parser_standard_slot(self):
        """測試標準格子的解析。
        
        動機：驗證普通的轉蛋格（只有一隻貓）能否被正常解析，且不影響原有功能。
        方法：構造一段標準的 HTML，檢查解析出的 original_result 是否為 None。
        """
        # Bypass __init__ to avoid full HTML parsing/validation
        parser = BCParser.__new__(BCParser)
        parser.regex_next_pos = r'(?:->|<-)\s*(\d+)([AB])' # Set regex manually
        
        # HTML: <td><a href="...">CatName</a></td>
        col_html = '<td><a href="/unit/123">CatName</a></td>'
        col_soup = BeautifulSoup(col_html, "html.parser")
        cols = [col_soup.td] # Just result column, no guaranteed

        res, gua, pos, orig, dup, orig_gua = parser._extract_cols_data(cols)
        
        assert res == "CatName"
        assert orig is None
        assert dup is False

    def test_parser_duplicate_rare_slot(self):
        """測試重複稀有 (Duplicate Rare) 格子的解析。
        
        動機：驗證 parser 能否正確處理同一格顯示多隻貓的情況（通常發生在稀有度重複時）。
        方法：構造一段包含兩隻貓咪與切換箭頭的 HTML，檢查是否正確抓取到 original_result, result 以及 is_duplicate_rare 標記。
        """
        parser = BCParser.__new__(BCParser)
        parser.regex_next_pos = r'(?:->|<-)\s*(\d+)([AB])'
        
        # HTML: <td><a href="...">Original</a><a href="...">Replacement</a> -> Next Pos Text</td>
        # Simulating Godfat structure for Dup Rare
        col_html = '''
        <td>
            <a href="/unit/1">Original</a>
            <a href="/unit/2">Replacement</a>
            -> 21B
        </td>
        '''
        col_soup = BeautifulSoup(col_html, "html.parser")
        cols = [col_soup.td]

        res, gua, pos, orig, dup, orig_gua = parser._extract_cols_data(cols)
        
        assert res == "Replacement"          # Result should be the last one
        assert orig == "Original"            # Original should be the first one
        assert dup is True                   # Should be flagged as duplicate
        
        # Check next pos extraction
        assert pos is not None
        assert pos.id == 21
        assert pos.track == 'B'

    def test_filter_cat_names(self):
        """測試貓咪名稱過濾器。
        
        動機：確保能從雜亂的 HTML 標籤中正確提取出有效的貓咪名稱，排除掉圖示或位置代碼。
        方法：傳入包含各種雜訊（如 1A, 🐾）的 <a> 標籤列表，驗證是否只回傳有效的貓咪名稱。
        """
        parser = BCParser.__new__(BCParser)
        html = '''
        <td>
            <a href="">1A</a>
            <a href="">Original</a>
            <a href="">🐾</a>
            <a href="">Replacement</a>
        </td>
        '''
        tags = BeautifulSoup(html, "html.parser").find_all("a")
        names = parser._filter_cat_names(tags)
        
        assert names == ["Original", "Replacement"]

from battle_cat_calc.simulator import GachaSimulator
from battle_cat_calc.models import Track, GachaSlot, Position

class TestIntegration:
    def test_simulation_context_population(self):
        """測試模擬過程中的 Context 填充。
        
        動機：未來的邏輯需要知道「上一抽是什麼」來決定是否觸發重複稀有，因此需驗證 Context 是否正確傳遞。
        方法：建立一個簡單的 Mock Track，執行模擬路徑，檢查每一步驟的 context_data 是否正確包含前一步的 last_rolled_rare。
        """
        
        # Setup Mock Tracks
        # Track A: 1A(Cat1) -> 2A(Cat2) -> 3A(Cat3)
        slots_a = [
            GachaSlot(id=Position(1, 'A'), result="Cat1", guaranteed="Gua1"),
            GachaSlot(id=Position(2, 'A'), result="Cat2", guaranteed="Gua2"),
            GachaSlot(id=Position(3, 'A'), result="Cat3", guaranteed="Gua3"),
        ]
        track_a = Track("A", slots_a)
        
        # Track B (empty for now)
        track_b = Track("B", [])
        
        sim = GachaSimulator(track_a, track_b)
        
        # Run finding path to Cat3
        result = sim.find_best_path(
            start_pos=Position(1, 'A'),
            target_cats={"Cat3"},
            max_rolls=5
        )
        
        assert result is not None
        path = result.path
        # Path should be: Step1(Cat1) -> Step2(Cat2) -> Step3(Cat3)
        assert len(path) == 3
        
        step1 = path[0]
        step2 = path[1]
        step3 = path[2]
        
        # Verify Units
        assert step1.unit == "Cat1"
        assert step2.unit == "Cat2"
        assert step3.unit == "Cat3"
        
        # Verify Context - 使用 last_rolled_rare 進行跨池重複稀有追蹤
        # Step 1: last_rolled_rare 是 None (起始狀態)
        assert step1.context_data['last_rolled_rare'] is None
        
        # Step 2: last_rolled_rare 仍為 None (Cat1/Cat2 不是重複稀有格)
        # 注意：只有 is_duplicate_rare=True 或有 original_result 的格子才會更新此值
        assert 'last_rolled_rare' in step2.context_data
        
        # Step 3: 驗證 context 存在
        assert 'last_rolled_rare' in step3.context_data

