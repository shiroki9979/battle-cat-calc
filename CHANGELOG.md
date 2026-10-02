<!-- markdownlint-disable-file MD024 -->
# Changelog

Created: 2026-03-28
Last updated: 2026-10-02

所有重要變更皆記錄於此檔案。格式參考 [Keep a Changelog](https://keepachangelog.com/)。

---

## [Unreleased]

### Fixed

- **搜尋演算法的三項設計修正**（Python 與 .NET 同步）：
  - 啟發式改為真正的下界：每隻貓 `min(1, guaranteed_cost / 11)`、每格
    `min(1 / 最大單抽前進格數, guaranteed_cost / 最大必中前進格數)`，係數由資料算出。
    舊版把兩者寫死為 1，但必中從 B 軌出發前進 11 格、一次必中拿 11 隻，會高估剩餘成本而失去 A* 的最優性保證
    （實測未觀察到錯誤結果，屬理論缺口）。新增以關閉啟發式的 Dijkstra 為基準的隨機交叉驗證測試。
  - 偵測表格沒標、但遊戲會觸發的連續重複稀有：狀態改記住所有目錄標為稀有的貓（`GachaSlot.rarity`，
    取自 `#find_select`，不用儲存格的 CSS class），走到一般格而上一隻就是這格的貓時拒走（回傳 `None`），
    不再假裝沒事。狀態數約 +40%。
  - 排序穩定：佇列元素改為 `(f, -g, state_id, state)`，f 相同先展開 g 大的，結果可重現；
    `SearchState` 不再 `order=True`（舊版 f 相同的不同狀態會被視為相等）。
  - Python 的 `GachaResult.total_cost` 改由路徑重算實際抽數；舊版在 `prefer_guaranteed=True` 時回報 `11.11` 這種加權值，與 .NET 版不一致。
  - .NET：`GachaSlot` 新增 `ResultRarity`（目錄稀有度；`Rarity` 仍是儲存格 class 的上色帶，供前端使用），
    `GachaSimulator` 新增測試用的 `HeuristicEnabled` 接縫與 `MinCostPerAdvance` / `MinCostPerCat`（internal）。
- **重複稀有的解析與模擬**（`src/battle_cat_calc/parser.py`、`simulator.py`、`models.py`）：
  - parser 以 `rowspan` 還原表格網格後才歸屬儲存格。舊版用「同一列的第 N 個 `<td>`」，會把相鄰軌道格子的內容讀成自己的，
    導致 `A11`、`B100` 被誤判為重複稀有格（`B10`、`A100` 才是）。
  - 結果欄箭頭（重複稀有的單抽落點）與必中欄箭頭（必中落點）分開存放為 `single_next_pos` 與 `next_pos`，
    舊版後者會覆蓋前者，使單抽觸發重複時跳到十格之外（`B10` 存成 `21B`，網站標示是 `12A`）。新增 `original_next_pos`。
  - 模擬器每一抽都更新「上一隻貓」。舊版只在被標記的重複稀有格更新，連續兩隻相同稀有貓時重複稀有從不觸發。
  - 重複稀有格沒被觸發時改拿替換前的貓，必中也改用替換前的版本。
  - 必中十連的前 10 抽改為照單抽規則逐抽走。舊版固定取同軌的 `pos+0..pos+9`，遇到重複稀有換軌後會收錯格子；
    落點的 `is_track_switch` 也不再寫死為 `True`（前 10 抽有 1 次重複稀有時會留在原軌，例如 `1B → 12B`）。
  - 啟發式的每格最低成本納入重複稀有的最大前進格數，維持 A* 的下界性質。
  - 影響範例：`test_start_from_track_b` 的最佳成本由 24 變為 20（路徑經過 `B10 → 12A`）。
- **測試**：移除把誤解析當標準答案的斷言（`A11` 原始貓、`B10.next_pos == 21B`），改為取自 HTML 原文的期望值，
  並新增以兩種獨立 HTML 箭頭互相驗證的交叉測試（`TestDuplicateRareCrossCheck`）。
- **.NET 版同步上述修正**（`dotnet/src`、`dotnet/tests`、`dotnet/client`）：
  - `BCParser` 以內部 `CellGrid` 還原 `rowspan` 網格；`GachaSlot` 新增 `SingleNextPos`、`OriginalNextPos`，
    結果欄與必中欄的箭頭分開存放。
  - `GachaSimulator` 新增 `ResolveSingleRoll`：單抽依上一隻貓決定是否觸發重複稀有；必中十連的前 10 抽逐抽走；
    起始格是未觸發的重複稀有格時用替換前的必中；`IsTrackSwitch` 依落點判斷；`_minCostPerAdvance` 納入重複稀有的前進格數。
  - 測試比照 Python 版：期望值取自 HTML，新增重複稀有的快照測試、交叉驗證（`DuplicateRareSnapshotTests`）、
    手工資料的邊界測試（`GachaSimulatorDuplicateRareTests`）與 `SlotDtoTests`。
    `StartFromTrackB` 的最佳成本同樣由 24 變為 20，與 Python 版一致。
- 修正多項 Markdown lint 格式規範，包含 MD032（清單前後空行）、MD040（程式碼區塊指定語言）、MD047（檔案結尾換行）與 MD060（表格分隔列空格）（`docs/api_reference.md`、`docs/design_logic.md`、`.agents/skills/gacha-simulation/SKILL.md`、`README.md`）。

### Changed

- 重新研究轉蛋機制並更新 `docs/gacha_mechanics_zh_TW.md`、`docs/gacha_mechanics_player_guide_zh_TW.md`、
  `docs/api_reference.md`、`docs/design_logic.md` 與兩個 skill：新增種子消耗規則（單抽 2 次、必中 1 次、重抽 1 次）、
  區分 8.5 與 8.6 版的重複稀有替換方式、標示各規則的證據等級，並列出尚未驗證的項目與已知限制。
  外部原頁多被出口代理封鎖，該部分取自搜尋摘要；fixture 實測的部分直接讀 HTML 驗證。
  修正文件原有的錯誤：必中十一連並非必定換軌；B 軌重複稀有前進 2 格（`B10 → 12A`），移除無法驗證的 `102B → 103A` 範例。
- **API 契約**：`GET /api/v1/tracks` 的每個 slot 新增 `singleNextPos`（重複稀有被觸發時單抽的落點，一般格為 `null`）。
  `nextPos` 現在一律是必中十一連的落點。前端 `TrackSlotRow` 改用 `singleNextPos` 顯示重複稀有的落點徽章，
  並同步 `e2e/fixtures.ts` 與 `dotnet/docs/architecture.md` 的範例。
- 將仍適用的開發規則與領域問答流程整理至 `AGENTS.md`、`docs/agents/domain.md`，並將轉蛋模擬與 HTML 解析測試工作流集中至 `.agents/skills/`。

### Removed

- 移除 `.github` 中的 Copilot 專屬指示、agent、prompt 與舊 skill 入口。

---

## 2026/03/29 (4)

### Changed

- **`.github/copilot-instructions.md`**：新增 .NET 子專案段落與文件連結、精簡測試章節（詳細慣例移至 instructions）、補充轉蛋機制文件連結。
- **`.github/prompts/pre-commit.prompt.md`**：每個 commit 對應獨立 CHANGELOG section、commit 指令改用純文字 + inline code 避免 VS Code Chat 折疊。

### Added

- **文件同步檢查 Prompt** (`.github/prompts/docs-audit.prompt.md`)：掃描程式碼與文件交叉比對，找出過時或缺漏的文件並直接修正。

---

## 2026/03/29 (3)

### Changed

- **`dotnet/README.md`**：進度清單大幅更新（Core/Infrastructure/Application/Api 全部已完成）、新增 Desktop/Client 段落、更新相依套件表與開發順序。
- **`dotnet/docs/architecture.md`**：新增 ADR-8（Photino + Kestrel 自託管桌面架構）、更新分層依賴圖（加入 Desktop 與 Client）、DI 註冊策略調整。
- **`dotnet/docs/migration-mapping.md`**：模組狀態全面更新（待實作→已完成）、新增 Desktop 與 Client 對應列。

---

## 2026/03/29 (2)

### Added

#### .NET 全層實作

- **`BattleCatCalc.Core`**：領域模型（`Position`、`GachaSlot`、`SlotValue`、`Track`、`GachaPool`、`GachaStep`、`GachaResult`）、`ParseStatus` enum、4 個介面（`IFetcher`、`IParser`、`ISimulator`、`IAnalyzer`）、例外階層（`BattleCatCalcException` → `ParseException` / `FetchException` / `SimulationException`）。
- **`BattleCatCalc.Infrastructure`**：`BCDataFetcher`（HttpClient）、`BCParser`（AngleSharp HTML 解析）。
- **`BattleCatCalc.Application`**：`GachaSimulator`（A* / Beam Search，單池＋多池，支配性與可達性剪枝）、`SeedAnalyzer`（貓咪查詢、保底位置搜尋）、`SearchState` readonly record struct。
- **`BattleCatCalc.Api`**：ASP.NET Core Minimal API（Kestrel 自託管），含 Controllers、Contracts（DTO）、`ExceptionHandlingMiddleware`（Problem Details RFC 9457）、OpenAPI、NLog、Static Files 中介軟體。
- **`BattleCatCalc.slnx`**：Solution 檔案（slnx 格式）。
- **.NET 測試合計**：162 tests 全數通過（Core 47 + Infrastructure 44 + Application 33 + Api 38）。

---

## 2026/03/29 (1)

### Added

#### AI 輔助開發

- **領域專家 Agent** (`.github/agents/domain-expert.agent.md`)：貓咪大戰爭轉蛋機制領域專家，提供種子追蹤、軌道切換、保底機制等問答。
- **測試慣例指令** (`.github/instructions/test-conventions.instructions.md`)：`tests/**` 自動注入的測試撰寫規範，含三段式 docstring、fixture 載入、非同步測試模式。
- **.NET 開發指令** (`.github/instructions/dotnet.instructions.md`)：`dotnet/**` 自動注入的 C# 開發規範，含命名、模型、分層、日誌、錯誤處理慣例。
- **HTML 解析測試產生 Prompt** (`.github/prompts/parse-html.prompt.md`)：從 HTML fixture 自動產生 BCParser 測試案例。
- **提交前準備 Prompt** (`.github/prompts/pre-commit.prompt.md`)：掃描變更、撰寫 CHANGELOG、產生 commit message 的工作流。
- **Gacha Simulation Skill** (`.github/skills/gacha-simulation/SKILL.md`)：GachaSimulator 演算法修改工作流，含 SearchState 欄位、9 項不變量、常見陷阱。

#### .NET 遷移規劃

- **`dotnet/README.md`**：.NET 子專案概述，含目標框架、專案結構、相依套件、開發順序與進度追蹤清單。
- **`dotnet/docs/architecture.md`**：架構決策紀錄（7 項 ADR），含 Clean Architecture 分層、模型策略、錯誤處理層級、API 端點設計。
- **`dotnet/docs/migration-mapping.md`**：Python → C# 模組對應、型別轉換、介面定義、行為差異注意事項。
- **`dotnet/.gitignore`**：.NET 專案 gitignore。

### Changed

- **`.github/copilot-instructions.md`**：補充無 linter/formatter 說明、`models.py` 描述更新（Track 繼承 UserList、ParseStatus 列舉）、新增測試 docstring 三段式與 fixture 慣例。

---

## 2026/03/28

### Added

- **專案規範** (`.github/copilot-instructions.md`)：統一正體中文撰寫的開發指引，涵蓋架構、程式碼風格、測試原則與領域術語。
- **`.gitattributes`**：統一換行符為 LF，避免跨平台 CRLF 警告。
- **`uv.lock`**：鎖定依賴版本，確保環境可重現。

#### 模擬器 (`simulator.py`)

- A* 搜尋演算法多項效能優化：
  - `Track.get_slot` O(1) 字典索引查找（取代線性掃描）。
  - `SearchState` 使用 `frozen=True, slots=True` 降低記憶體開銷。
  - `_precompute_target_data` 單次掃描合併目標貓咪位置資料與最大位置。
  - 位置感知啟發式（`bisect`）提供更緊的 admissible 下界。
  - `itertools.count()` 區域計數器取代全域狀態。
  - `target_cats` 統一為 `frozenset`，避免重複轉換。
  - visited 集合原地過濾（in-place filter）。
  - 移除冗餘 `str()` 呼叫。
- `find_best_path` 新增目標貓咪存在性前置驗證，不存在的目標拋出 `ValueError` 並列出缺失名稱。

#### 測試

- 新增 7 個測試檔案，測試數從 26 提升至 **171**：
  - `test_analyzer.py`：SeedAnalyzer 查詢與分析（12 tests）。
  - `test_fetcher_errors.py`：HTTP 錯誤、逾時、連線失敗等 sad path（6 tests）。
  - `test_models.py`：GachaResult 屬性、序列化、GachaNode、Track 索引等（34 tests）。
  - `test_parser_edge.py`：解析器邊界情況與異常處理（13 tests）。
  - `test_simulator_advanced.py`：Beam Search、保底偏好、進度回調、剪枝、啟發式、目標驗證等（39 tests）。
  - `test_snapshot.py`：真實 HTML fixture 的端到端快照驗證，含單池與多池情境（41 tests）。
- 新增測試 fixture：`seed_2127986414_event_1019.html`、`seed_2127986414_event_1020.html`。

### Changed

- **`pyproject.toml`**：從 Poetry 遷移至 **uv**，更新依賴版本約束。
- **`models.py`**：移除未使用的 `# noqa: F401` 標記（`Set` 確實有使用）。
- **`parser.py`**：統一 6 處 return type 為 `Tuple` / `Set`（from `typing`），與專案其他模組一致。
- **`analyzer.py`**：
  - `find_cat_by_name` 改為跳過 `ParseStatus` 格子（原本轉字串會誤匹配）。
  - 移除未使用的 `Union` import。
  - docstring 改為正體中文。
- **`README.md`**：更新快速開始指南與專案說明。
- **`docs/api_reference.md`**、**`docs/design_logic.md`**：同步至當前程式碼架構。
- **`notebooks/sim.ipynb`**：加入進度條即時更新（每 2 秒顯示速率與狀態數）。

### Fixed

- `test_snapshot.py`：修復 `TestMultiPoolSnapshot` 類別名稱重複導致 13 個測試被覆蓋的問題（重新命名為 `TestSinglePoolAsMultiPool`）。

### Removed

- `.prompts/` 目錄（6 個檔案）：已整合至 `.github/copilot-instructions.md`。
- `notebooks/cross_pool_demo.ipynb`：功能已合併至 `sim.ipynb`。
