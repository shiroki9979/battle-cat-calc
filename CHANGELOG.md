# Changelog

所有重要變更皆記錄於此檔案。格式參考 [Keep a Changelog](https://keepachangelog.com/)。

---

## 2026/03/29

### Added

#### AI 輔助開發

- **領域專家 Agent** (`.github/agents/domain-expert.agent.md`)：貓咪大戰爭轉蛋機制領域專家，提供種子追蹤、軌道切換、保底機制等問答。
- **測試慣例指令** (`.github/instructions/test-conventions.instructions.md`)：`tests/**` 自動注入的測試撰寫規範，含三段式 docstring、fixture 載入、非同步測試模式。
- **.NET 開發指令** (`.github/instructions/dotnet.instructions.md`)：`dotnet/**` 自動注入的 C# 開發規範，含命名、模型、分層、日誌、錯誤處理慣例。
- **HTML 解析測試產生 Prompt** (`.github/prompts/parse-html.prompt.md`)：從 HTML fixture 自動產生 BCParser 測試案例。
- **提交前準備 Prompt** (`.github/prompts/pre-commit.prompt.md`)：掃描變更、撰寫 CHANGELOG、產生 commit message 的工作流。
- **Gacha Simulation Skill** (`.github/skills/gacha-simulation/SKILL.md`)：GachaSimulator 演算法修改工作流，含 SearchState 欄位、9 項不變量、常見陷阱。

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
