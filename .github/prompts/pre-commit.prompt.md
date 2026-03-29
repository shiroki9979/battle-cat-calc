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

### 4. 撰寫 CHANGELOG 條目

讀取 [CHANGELOG.md](../../CHANGELOG.md) 的既有格式，嚴格遵循：

- 日期標題：`## YYYY/MM/DD`
- 分類：`### Added` / `### Changed` / `### Fixed` / `### Removed`
- 需要時以模組子標題分組：`#### 模組名稱 (\`file.py\`)`
- 條目格式：`- **名稱**：說明。`
- 語言：正體中文

產出的條目直接可貼入 CHANGELOG.md 對應日期區塊下方。若當日區塊已存在，合併至既有分類。

### 5. 產生 commit message

格式遵循 [Conventional Commits](https://www.conventionalcommits.org/)：

```
<type>(<scope>): <簡述>
```

規則：
- `type`：feat / fix / refactor / test / docs / chore / perf
- `scope`：影響的主要模組（simulator、parser、dotnet 等），可省略
- 簡述：**正體中文**，50 字元內
- 若有 breaking change 或重要細節，附加 body（空一行後以正體中文撰寫）

若建議拆分 commit，為每個 commit 各產生一則 message。

### 6. 提供 commit 指令

完成 CHANGELOG 更新與 staging 後，**不自動執行 `git commit`**。將完整的 `git commit` 指令（含 message）輸出供使用者手動執行。

## 輸出格式

```
## 變更掃描

<staged/unstaged 狀態摘要>
<按模組分組的變更列表>

## 拆分建議

<若適用，列出每個 commit 的檔案與 staging 指令>
<若不需拆分，說明原因>

## CHANGELOG 條目

<可直接貼入的 markdown 文字>

## Commit Message

<一或多則 commit message>
```
