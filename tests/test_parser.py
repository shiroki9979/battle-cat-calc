import pytest
from unittest.mock import MagicMock
from battle_cat_calc.parser import BCParser
from battle_cat_calc.models import Track, GachaSlot, Position

@pytest.fixture
def mock_html():
    return "<html><body><table><tr><td id='N1A'>Cat A1</td><td></td><td id='N1B'>B1</td></tr><tr><td id='N2A'>Cat A2</td><td></td><td></td></tr></table></body></html>"

def test_bc_parser_init_invalid_html():
    """測試無效 HTML 的處理。
    
    動機：確保當輸入無法辨識的 HTML（如缺少表格）時，Parser 能丟出適當的錯誤。
    方法：傳入一段沒有 <table> 標籤的 HTML，預期會拋出 ValueError。
    """
    with pytest.raises(ValueError, match="無法解析 HTML: 找不到轉蛋表格"):
        BCParser("<html><body>No Table Here</body></html>")

def test_validate_track_sequential(mock_html):
    """測試軌道 ID 的連續性驗證。
    
    動機：轉蛋軌道的 ID 必須是連續的整數 (1, 2, 3...)，若不連續代表解析可能出錯。
    方法：構造一段 ID 連續的 Mock HTML 與對應的 Track 資料，驗證 _validate_track 回傳 True。
    """
    valid_html = "<html><body><table>" \
                 "<tr><td id='N1A'>A1</td><td></td><td></td><td id='N1B'>B1</td></tr>" \
                 "<tr><td id='N2A'>A2</td><td></td><td></td><td id='N2B'>B2</td></tr>" \
                 "</table></body></html>"
    parser = BCParser(valid_html)
    
    slots = [
        GachaSlot(id=Position(1, 'A'), result="Cat 1", guaranteed=""),
        GachaSlot(id=Position(2, 'A'), result="Cat 2", guaranteed=""),
        GachaSlot(id=Position(3, 'A'), result="Cat 3", guaranteed=""),
    ]
    track = Track("A", slots)
    assert parser._validate_track(track) is True

def test_get_all_cats(mock_html):
    """測試獲取所有貓咪名稱的功能。
    
    動機：模擬器需要知道整個卡池有哪些貓，以供搜尋使用。
    方法：建立包含不同貓咪與保底貓的 Track，呼叫 get_all_cats，檢查回傳的集合是否包含所有出現過的貓。
    """
    valid_html = "<html><body><table>" \
                 "<tr><td id='N1A'>A1</td><td></td><td></td><td id='N1B'>B1</td></tr>" \
                 "<tr><td id='N2A'>A2</td><td></td><td></td><td id='N2B'>B2</td></tr>" \
                 "</table></body></html>"
    parser = BCParser(valid_html)
    
    parser.track_a = Track("A", [
        GachaSlot(id=Position(1, 'A'), result="Cat 1", guaranteed="Gua 1"),
    ])
    parser.track_b = Track("B", [
        GachaSlot(id=Position(1, 'B'), result="Cat 1", guaranteed="Gua 2"),
    ])
    
    cats = parser.get_all_cats()
    assert cats == {"Cat 1", "Gua 1", "Gua 2"}

def test_validate_track_empty(mock_html):
    """測試空軌道的驗證。
    
    動機：確保 Track 不為空，避免後續邏輯崩潰。
    方法：建立一個空的 Track，驗證 _validate_track 回傳 False。
    """
    valid_html = "<html><body><table>" \
                 "<tr><td id='N1A'>A1</td><td></td><td></td><td id='N1B'>B1</td></tr>" \
                 "<tr><td id='N2A'>A2</td><td></td><td></td><td id='N2B'>B2</td></tr>" \
                 "</table></body></html>"
    parser = BCParser(valid_html)
    
    track = Track("A", [])
    assert parser._validate_track(track) is False
