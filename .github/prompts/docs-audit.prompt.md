---
description: "掃描專案狀態，判斷哪些文件已過時並直接更新。Use when: 檢查文件、更新文件、docs audit、文件同步、確認文件是否最新"
agent: "agent"
tools: [execute/runInTerminal, read/readFile, edit/editFiles, search]
---

# 文件同步檢查

掃描專案程式碼的實際狀態，與現有文件交叉比對，找出已過時或缺漏的文件並直接修正。

## 範圍

包含但不限於以下文件：

| 類別 | 路徑 |
|------|------|
| 專案總覽 | [README.md](../../README.md) |
| 變更記錄 | [CHANGELOG.md](../../CHANGELOG.md) |
| 專案規範 | [copilot-instructions.md](../copilot-instructions.md) |
| Python API 文件 | [docs/api_reference.md](../../docs/api_reference.md) |
| 演算法設計 | [docs/design_logic.md](../../docs/design_logic.md) |
| 抽卡機制（技術） | [docs/gacha_mechanics_zh_TW.md](../../docs/gacha_mechanics_zh_TW.md) |
| 抽卡機制（玩家） | [docs/gacha_mechanics_player_guide_zh_TW.md](../../docs/gacha_mechanics_player_guide_zh_TW.md) |
| .NET 總覽 | [dotnet/README.md](../../dotnet/README.md) |
| .NET 架構 | [dotnet/docs/architecture.md](../../dotnet/docs/architecture.md) |
| .NET 遷移對應 | [dotnet/docs/migration-mapping.md](../../dotnet/docs/migration-mapping.md) |
| .NET 指令 | [.github/instructions/dotnet.instructions.md](../instructions/dotnet.instructions.md) |
| 測試指令 | [.github/instructions/test-conventions.instructions.md](../instructions/test-conventions.instructions.md) |

## 步驟

### 1. 收集程式碼現狀

執行以下指令掌握專案整體狀態（不修改任何檔案）：

```bash
git log --oneline -20                    # 近期 commit，了解變更脈絡
git diff --stat HEAD~10 HEAD 2>/dev/null # 近 10 筆 commit 的檔案變動統計
```

同時讀取關鍵程式碼，建立當前實作的理解：

- **Python**：`src/battle_cat_calc/` 下各模組的 class、function 簽名、資料結構
- **.NET**：`dotnet/src/` 下各專案的 class、interface、record 定義
- **測試**：`tests/` 與 `dotnet/tests/` 的測試檔案清單與涵蓋範圍

### 2. 逐一比對文件

針對每份文件，檢查以下面向：

| 面向 | 檢查項目 |
|------|----------|
| 模組清單 | 文件列出的模組/class 是否與程式碼一致（有無新增、刪除、改名） |
| API 簽名 | 函式參數、回傳型別、預設值是否與實作吻合 |
| 架構描述 | 資料流、依賴關係、分層說明是否反映現況 |
| 指令正確性 | 建置/測試/執行指令是否仍可正常運作 |
| 範例與術語 | 範例程式碼、術語表、欄位名稱是否仍正確 |
| Metadata | 最後更新日期是否需要刷新 |

### 3. 產出診斷報告

以表格列出所有文件的檢查結果：

```
| 文件 | 狀態 | 說明 |
|------|------|------|
| docs/api_reference.md | 需更新 | 缺少 SeedAnalyzer.find_guaranteed_positions() |
| dotnet/docs/architecture.md | 最新 | — |
| ... | ... | ... |
```

狀態分類：
- **最新**：內容與程式碼一致
- **需更新**：有具體的過時或缺漏項目
- **需確認**：無法僅從程式碼判斷，需使用者決定

### 4. 直接修正

對所有「需更新」的文件，直接進行修改。修改遵循以下原則：

- 保留文件原有的格式、章節結構、語氣風格
- 僅修改過時的部分，不重寫未受影響的內容
- 若文件含 metadata（建立日期、更新日期），一併更新「最後更新日期」為今日
- 修改完成後列出每份文件的 diff 摘要（修改了哪些段落、原因）

對「需確認」的項目，列出問題與選項供使用者決定，不自行修改。

## 輸出格式

最終輸出包含兩部分：

1. **診斷報告表格**（全部文件的狀態總覽）
2. **修改摘要**（每份已更新文件的變更重點，3-5 行以內）

若所有文件皆為最新，僅輸出一句確認即可。
