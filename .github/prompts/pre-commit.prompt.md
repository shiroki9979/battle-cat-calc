---
description: "準備提交前的工作流：掃描變更、撰寫 changelog、產生 commit message、建議拆分策略。Use when: git commit、準備提交、review 變更、寫 changelog、commit message"
agent: "agent"
tools: [execute/runInTerminal, read/readFile, edit/editFiles]
---

# 提交前準備

掃描 workspace 的 git 變更狀態，分析後產出 changelog 條目、commit message，並在適當時建議拆分提交。

## 步驟

### 1. 掃描變更

執行以下指令收集資訊（不修改任何檔案）：

```bash
git status --short
git diff --stat
git diff --cached --stat
git log --oneline -5         # 最近 5 筆 commit，掌握上下文
```

若有 staged 與 unstaged 混合，分別標示。若無任何變更，直接告知並結束。

### 2. 分析變更範圍

讀取每個變更檔案的 diff（`git diff <file>` 或 `git diff --cached <file>`），歸納：

- **變更類型**：新增功能、修改行為、修復 bug、重構、文件更新、測試、設定
- **影響模組**：依專案架構分組（如 `simulator.py`、`parser.py`、`dotnet/`、`tests/`、`docs/`）
- **關聯性**：哪些變更屬於同一邏輯單元，哪些彼此獨立

### 3. 判斷是否建議拆分 commit

以下情況建議拆分：

- 同時包含**功能變更**與**不相關的重構或格式調整**
- 跨越**多個獨立模組**且各自可獨立運作
- 同時包含**程式碼變更**與**文件/設定變更**且邏輯上無強耦合
- **測試修復**與**功能新增**混在一起

若建議拆分，明確列出每個 commit 應包含哪些檔案，並提供對應的 staging 指令（`git add <files>`）。

若變更具高度關聯性（如一個功能 + 其測試 + 其文件），保持單一 commit。

### 4. 為每個 commit 撰寫 CHANGELOG 條目與 commit message

每個 commit 各自對應一組 CHANGELOG 條目與 commit message，一併產出。

#### CHANGELOG 格式

讀取 [CHANGELOG.md](../../CHANGELOG.md) 的既有格式，嚴格遵循：

- 日期標題：`## YYYY/MM/DD`
- 若同一天有多筆 commit，以 `## YYYY/MM/DD (N)` 區分（N 為當日第 N 個區塊）
- 分類：`### Added` / `### Changed` / `### Fixed` / `### Removed`
- 需要時以模組子標題分組：`#### 模組名稱 (\`file.py\`)`
- 條目格式：`- **名稱**：說明。`
- 語言：正體中文

每個 commit 寫入一個獨立的 CHANGELOG section，依 commit 順序由新到舊排列於檔案中。

#### Commit message 格式

遵循 [Conventional Commits](https://www.conventionalcommits.org/)：`<type>(<scope>): <簡述>`

規則：
- `type`：feat / fix / refactor / test / docs / chore / perf
- `scope`：影響的主要模組（simulator、parser、dotnet 等），可省略
- 簡述：**正體中文**，50 字元內
- 若有 breaking change 或重要細節，附加 body（空一行後以正體中文撰寫）

### 5. 提供 commit 指令

完成 CHANGELOG 更新與 staging 後，**不自動執行 `git commit`**。將完整的 commit 指令輸出供使用者手動執行。

**輸出限制**：不使用 fenced code block（VS Code Chat 會折疊隱藏）。改用純文字搭配 inline code 格式，例如：

**Commit 1 staging：** `git add file1 file2`

**Commit 1 message：** feat(scope): 簡述

**Commit 1 body：** 補充說明（若有）

## 輸出格式

輸出分為兩大區塊。第一區塊為變更掃描與拆分建議：

**變更掃描** — staged/unstaged 狀態摘要，按模組分組的變更列表。

**拆分建議** — 若適用，列出每個 commit 的檔案分組；若不需拆分，簡述原因。

第二區塊依每個 commit 逐一列出 CHANGELOG 條目、commit message 與 staging 指令。格式如下：

**Commit N — 簡述**

CHANGELOG 條目（直接寫入 CHANGELOG.md 後確認）

staging 指令與 commit message（純文字 + inline code，不使用 fenced code block）
