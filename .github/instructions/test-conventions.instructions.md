---
description: "Use when: 撰寫或修改測試案例、新增 test_ 檔案、為核心邏輯補充單元測試"
applyTo: "tests/**"
---

# 測試慣例

## 結構

- 測試檔案對應原始模組：`test_fetcher.py`、`test_parser.py`、`test_simulator.py` 等
- Fixture HTML 存放於 `tests/fixtures/`，命名格式 `seed_{seed}_event_{event}.html`
- `tests/conftest.py` 負責 `sys.path` 設定，不放共用 fixture

## Docstring 三段式

每個測試方法的 docstring 採用：

```python
def test_track_length(self, parser):
    """驗證兩條軌道的長度。

    動機：確保解析器正確識別所有 slot，未遺漏或重複。
    方法：解析 fixture HTML，檢查 track_a 與 track_b 長度。
    驗證：兩條軌道長度皆為預期值。
    """
```

## Fixture 載入

```python
import pathlib

FIXTURE_DIR = pathlib.Path(__file__).parent / "fixtures"

html = (FIXTURE_DIR / "seed_2127986414_event_1019.html").read_text(encoding="utf-8")
```

## 非同步測試

```python
@pytest.mark.asyncio(loop_scope="function")
async def test_fetch_html():
    ...
```

Mock HTTP 客戶端使用 `patch('httpx.AsyncClient')` + `AsyncMock()` 搭配 `__aenter__`/`__aexit__`。

## 規則

- 每個測試只驗證單一行為
- 禁止測試之間共享可變狀態
- 使用 mock/stub 隔離外部依賴（網路、時間），確保確定性
- 錯誤訊息與程式碼字串使用英文，註解使用正體中文
- 覆蓋 Happy Path、Sad Path（`pytest.raises`）、Edge Cases
