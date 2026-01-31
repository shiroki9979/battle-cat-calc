import pytest
from unittest.mock import MagicMock
from battle_cat_calc.parser import BCParser
from battle_cat_calc.models import Track, GachaSlot

@pytest.fixture
def mock_html():
    return "<html><body><table><tr><td id='N1A'>Cat A1</td><td></td><td id='N1B'>B1</td></tr><tr><td id='N2A'>Cat A2</td><td></td><td></td></tr></table></body></html>"

def test_bc_parser_init_invalid_html():
    """Test that BCParser raises ValueError when no table is found."""
    with pytest.raises(ValueError, match="無法解析 HTML: 找不到轉蛋表格"):
        BCParser("<html><body>No Table Here</body></html>")

def test_validate_track_sequential(mock_html):
    valid_html = "<html><body><table>" \
                 "<tr><td id='N1A'>A1</td><td></td><td></td><td id='N1B'>B1</td></tr>" \
                 "<tr><td id='N2A'>A2</td><td></td><td></td><td id='N2B'>B2</td></tr>" \
                 "</table></body></html>"
    parser = BCParser(valid_html)
    
    slots = [
        GachaSlot(id=(1, 'A'), result="Cat 1", guaranteed=""),
        GachaSlot(id=(2, 'A'), result="Cat 2", guaranteed=""),
        GachaSlot(id=(3, 'A'), result="Cat 3", guaranteed=""),
    ]
    track = Track("A", slots)
    assert parser._validate_track(track) is True

def test_get_all_cats(mock_html):
    valid_html = "<html><body><table>" \
                 "<tr><td id='N1A'>A1</td><td></td><td></td><td id='N1B'>B1</td></tr>" \
                 "<tr><td id='N2A'>A2</td><td></td><td></td><td id='N2B'>B2</td></tr>" \
                 "</table></body></html>"
    parser = BCParser(valid_html)
    
    parser.track_a = Track("A", [
        GachaSlot(id=(1, 'A'), result="Cat 1", guaranteed="Gua 1"),
    ])
    parser.track_b = Track("B", [
        GachaSlot(id=(1, 'B'), result="Cat 1", guaranteed="Gua 2"),
    ])
    
    cats = parser.get_all_cats()
    assert cats == {"Cat 1", "Gua 1", "Gua 2"}

def test_validate_track_empty(mock_html):
    valid_html = "<html><body><table>" \
                 "<tr><td id='N1A'>A1</td><td></td><td></td><td id='N1B'>B1</td></tr>" \
                 "<tr><td id='N2A'>A2</td><td></td><td></td><td id='N2B'>B2</td></tr>" \
                 "</table></body></html>"
    parser = BCParser(valid_html)
    
    track = Track("A", [])
    assert parser._validate_track(track) is False
