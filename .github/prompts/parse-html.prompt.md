---
description: "從 HTML fixture 產生 BCParser 測試案例。Use when: 新增轉蛋池 fixture、驗證解析邏輯、bc.godfat.org 頁面結構變動時"
agent: "agent"
tools: [search]
---

# 產生 BCParser 測試案例

根據指定的 HTML fixture 檔案，為 `BCParser` 產生 pytest 測試案例。

## 輸入

- **Fixture 檔案**：`tests/fixtures/` 下的 HTML 檔案（格式 `seed_{seed}_event_{event}.html`）
- **驗證重點**（選填）：使用者指定要驗證的欄位或行為，例如特定 slot 的 `result`、`guaranteed`、`next_pos`、`is_duplicate_rare`

若未指定 fixture，列出 `tests/fixtures/` 中現有的 HTML 檔案供選擇。

## 產出規範

1. **先讀取 fixture HTML**，用 `BCParser` 實際解析，確認 Track A / Track B 的內容後再撰寫斷言
2. 參考 [test_parser.py](../../tests/test_parser.py) 與 [test_snapshot.py](../../tests/test_snapshot.py) 的既有模式
3. 遵循專案測試慣例：
   - Fixture 載入：`pathlib.Path(__file__).parent / "fixtures" / "<filename>"`，以 `read_text(encoding="utf-8")` 讀取
   - 每個測試驗證單一行為
   - Docstring 三段式：**動機**（Why）→ **方法**（How）→ **驗證**（Assert）
   - 錯誤訊息與程式碼字串使用英文，註解使用正體中文

## 測試類型

依 fixture 內容產生以下測試（視情況選取）：

- **Track 結構**：`len(track_a)`、`len(track_b)` 是否符合預期
- **Slot 欄位正確性**：抽樣檢查首筆、末筆、中間筆的 `id`、`result`、`guaranteed`、`next_pos`
- **重複稀有跳軌**：`is_duplicate_rare == True` 的 slot，驗證 `next_pos` 指向對面軌道
- **ParseStatus 哨兵值**：若存在 `MULTIPLE_DATA`、`INVALID_DATA`、`AMBIGUOUS`，驗證其正確標記
- **邊界條件**：空值欄位、最後一筆 slot 的 `next_pos` 處理

## 輸出範例

```python
import pathlib
import pytest
from battle_cat_calc.parser import BCParser
from battle_cat_calc.models import Position

FIXTURE_DIR = pathlib.Path(__file__).parent / "fixtures"


class TestParserSeed2127986414Event1019:
    """seed=2127986414, event=1019 的解析驗證。"""

    @pytest.fixture
    def parser(self):
        html = (FIXTURE_DIR / "seed_2127986414_event_1019.html").read_text(encoding="utf-8")
        return BCParser(html)

    def test_track_length(self, parser):
        """驗證兩條軌道的長度。

        動機：確保解析器正確識別所有 slot，未遺漏或重複。
        方法：解析 fixture HTML，檢查 track_a 與 track_b 長度。
        驗證：兩條軌道長度皆為預期值。
        """
        assert len(parser.track_a) == 100
        assert len(parser.track_b) == 100

    def test_first_slot_fields(self, parser):
        """驗證首筆 slot 的各欄位。

        動機：首筆資料為解析起點，欄位錯誤會影響後續所有計算。
        方法：取 track_a 第一筆 slot，逐欄位比對。
        驗證：id、result、guaranteed、next_pos 皆符合 HTML 原始資料。
        """
        slot = parser.track_a[0]
        assert slot.id == Position(1, 'A')
        assert slot.result == "預期貓咪名稱"
        assert slot.next_pos == Position(2, 'A')
```
