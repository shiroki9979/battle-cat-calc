# 專案規範

## 概述

Battle Cats 種子追蹤計算器 — 透過種子追蹤與 Dijkstra/A* 演算法，找出最低成本的轉蛋路徑以收集目標貓咪。資料來源為 bc.godfat.org。

## 語言與回覆

- 無論使用者的提問或貼上的內容為何種語言，除非特別指定，預設一律使用**正體中文**回覆。
- 不使用表情符號。
- 客觀分析，直接切入重點，不需要讚美、鼓勵性語句或多餘的禮貌用語。
- 預設使用精簡回覆；若問題複雜或使用者明確要求，才展開詳細說明。
- 資訊不足或需求不明確時，主動追問關鍵問題，確認後再產出內容。
- 修改現有程式碼時，重點輸出修改的片段與關鍵邏輯；除非使用者明確要求，否則省略未更動的部分。

## 建置與測試

```bash
uv sync               # 安裝所有依賴（Python ≥ 3.12）
uv run pytest         # 執行測試
```

專案未配置 linter 或 formatter（無 ruff、black、mypy）。型別正確性以 Type Hints 與 code review 維護。

## 架構

資料流為線性：**擷取 → 解析 → 模擬**，另有分析工具輔助。

| 模組 | 職責 |
|------|------|
| `config.py` | 常數（`BASE_URL`、`DEFAULT_LANG`、`DEFAULT_COUNT`） |
| `models.py` | 所有資料結構 — `@dataclass` 型別、`Track`（繼承 `UserList`，O(1) 查找）、`GachaSlot`、`Position`、`GachaPool`、`ParseStatus` 列舉（解析異常哨兵值） |
| `fetcher.py` | `BCDataFetcher` — 非同步 httpx 客戶端，爬取 bc.godfat.org |
| `parser.py` | `BCParser` — BeautifulSoup 將 HTML 解析為 Track A / Track B |
| `simulator.py` | `GachaSimulator` — Dijkstra / A* / Beam Search 路徑最佳化 |
| `analyzer.py` | `SeedAnalyzer` — 貓咪查詢、保底位置搜尋 |
| `main.py` | 程式進入點（預留） |

演算法細節見 [docs/design_logic.md](../docs/design_logic.md)。API 規格見 [docs/api_reference.md](../docs/api_reference.md)。

### .NET 子專案（dotnet/）

Python 版本的 C# 遷移，採 Clean Architecture 四層（Core → Infrastructure → Application → Api）。前端為 React + TypeScript（Vite），桌面版透過 Photino 封裝自託管 Kestrel + WebView 視窗。

- 架構決策：[dotnet/docs/architecture.md](../dotnet/docs/architecture.md)
- 模組對應：[dotnet/docs/migration-mapping.md](../dotnet/docs/migration-mapping.md)
- C# 慣例：`.github/instructions/dotnet.instructions.md`（自動套用於 `dotnet/**`）

## 程式碼風格

### 通用原則

- 以 **DRY、KISS、YAGNI、SOLID** 為優先原則。
- 命名需具備語意化，業界通用縮寫（如 HTTP、URL、ctx、req）除外。
- 優先使用原生 API。若建議引入第三方套件，請簡要說明其必要性與優勢。
- 產出程式碼需預防常見安全漏洞（如 SQL Injection、XSS）以及效能瓶頸（如 N+1 Query、無限迴圈風險）。

### 型別與資料模型

- **嚴格型別定義**：落實 Python Type Hints，避免使用 `Any` 或模糊型別。
- 所有模型使用 **Dataclasses**。不可變搜尋狀態使用 `frozen=True`；可雜湊集合使用 `frozenset`。
- 軌道以 `'A'` / `'B'` 字串表示。位置格式：`Position(id=1, track='A')`。

### 註解與文件

- 註解以**正體中文**撰寫，重點說明「為何如此設計（Why）」，切勿逐行翻譯程式碼（What）。
- 註解與內部文件不使用第一、第二人稱，以「使用者」與「開發者」等客觀字眼為主。
- 程式碼內部字串、錯誤訊息與輸出文字使用**英文**。
- 編輯專案文件（如 Markdown）時，善用 Mermaid、Table、GFM 等輔助說明。
- 撰寫文件時，在文件開頭或結尾標注建立日期、最後更新日期及作者（若已知）等 metadata。

### 錯誤處理

- 不允許靜默失敗（No Silent Failure）；錯誤需明確拋出或回傳可追蹤的狀態，並附帶充足的上下文資訊與適當的錯誤級別。
- 解析失敗使用 `ValueError`，搜尋未命中回傳 `Optional`，網路錯誤使用 `httpx.HTTPStatusError`。

### 非同步

- fetcher 為非同步（httpx）。測試使用 `@pytest.mark.asyncio(loop_scope="function")`。

## 測試

```bash
uv run pytest         # 執行全部測試
```

- 使用 pytest + pytest-asyncio；以 `unittest.mock`（`AsyncMock`、`patch`）模擬 HTTP 請求。
- `tests/conftest.py` 將 `src/` 加入 `sys.path`。
- 測試檔案對應原始模組（`test_fetcher.py`、`test_parser.py`、`test_simulator.py` 等）。
- 整合測試使用 `tests/fixtures/` 下的真實 HTML 回應檔案（如 `seed_*_event_*.html`）。
- 若提交包含核心邏輯的程式碼，主動提供對應的單元測試。

詳細測試慣例（docstring 格式、fixture 載入、覆蓋範圍原則）見 `.github/instructions/test-conventions.instructions.md`（自動套用於 `tests/**`）。

## 領域術語

| 術語 | 說明 |
|------|------|
| 種子追蹤 | 透過已知種子值進行確定性亂數預測 |
| 重複稀有 | 連續抽到相同稀有貓 → 觸發軌道切換 A↔B |
| 保底（十連抽保底） | 特殊抽卡機制，具有固定成本 |
| 多池（Multi-pool） | 合併多個轉蛋池，各池可有不同保底成本 |

機制詳解見 [docs/gacha_mechanics_zh_TW.md](../docs/gacha_mechanics_zh_TW.md)（技術向）與 [docs/gacha_mechanics_player_guide_zh_TW.md](../docs/gacha_mechanics_player_guide_zh_TW.md)（玩家向）。
