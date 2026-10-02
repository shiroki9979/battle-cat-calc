# 模擬器設計邏輯 (Simulator Design Logic)

## 核心演算法

模擬器使用 **A\* 演算法**（可容許啟發式 + 支配性剪枝）搜尋達成目標的「最小花費」路徑。

### 演算法選擇

| 演算法 | 優先級 | 特點 |
| --- | --- | --- |
| **Dijkstra** | `cost` | 保證最佳解，均勻擴散探索 |
| A* | `cost + heuristic` | 較快但需要啟發式函數 |

目前實作是 A\*：啟發式是剩餘成本的下界（見下方「啟發式」），因此同樣保證最佳解，但展開的狀態少得多。
關閉啟發式（h = 0）就退化成 Dijkstra，測試用它當最優性的比對基準。

## 狀態定義 (SearchState)

每個搜尋節點 (State) 包含以下資訊：

- **Priority**: A* 的 f 值 (f = g + h)。
- **Cost**: 目前累積的抽卡次數 (g 值)。
- **Position**: 目前所在的格子 (ID 與 Track)。
- **Collected**: 目前已收集到的目標貓咪集合 (frozenset)。
- **state_id**: 唯一狀態識別碼（用於延遲路徑建構）。
- **parent_id**: 父狀態的 state_id（用於回溯路徑）。
- **last_step**: 這一步的 GachaStep 資訊。
- **last_rolled_rare**: 上一抽實際拿到的貓（用於跨池重複稀有判定）。
  每一抽都會更新，但只記住可能觸發重複的貓：目錄（`#find_select`）標為稀有的貓，
  沒有目錄的資料（手工測試）退而記「曾在重複稀有格被替換掉的貓」；其餘存 `None`，以免狀態空間膨脹。
  記住所有稀有貓比只記候選多展開約 40% 狀態（fixture 實測 2212 → 3186），換來能偵測表格沒標的重複（見下）。

### 延遲路徑建構

原本每個狀態儲存完整 path tuple，現改為只記錄 parent_id + last_step：

| 方式 | 記憶體 | 建構成本 |
| --- | --- | --- |
| 原本 | O(狀態數 × 路徑長度) | 每次複製 tuple |
| **現在** | O(狀態數) | 只在找到解時回溯 |

## 狀態轉換 (State Transition)

從任何一個狀態出發，玩家可以執行兩種動作：

1. **單抽 (Single Roll)**:
    - 花費 +1。
    - 前進到下一格 (通常是同 Track；觸發重複稀有時落在該格的 `single_next_pos`)。
    - 收集該格的貓咪。重複稀有格依 `last_rolled_rare` 決定是替換後（觸發）還是替換前（未觸發）的貓，
      由 `_resolve_single_roll` 統一計算。
    - **拒走**：一般格（表格沒標重複）但上一隻就是這格的貓時，遊戲會重抽、表格卻沒有替換結果，
      `_resolve_single_roll` 回傳 `None`，這條路不展開。寧可找不到路，也不給出會在這裡分岔的計畫。
2. **十連抽 (Guaranteed Roll)**:
    - 花費 +10。
    - 前 10 抽照單抽規則走（遇到重複稀有會換軌），收集沿途的貓咪，再加上保底貓咪。
    - 前進到起始格必中欄的落點（`next_pos`，起始格是未觸發的重複稀有格時用 `original_next_pos`）。
      通常會跳軌，但前 10 抽內有一次重複稀有時會留在原軌。

### 啟發式（必須是下界）

`_heuristic` 取兩項的最大值，兩個係數都由 `_scan_rules` 從資料與池設定算出，不能寫死為 1：

| 項目 | 公式 | 為什麼不是 1 |
| --- | --- | --- |
| 未收集數 × `_min_cost_per_cat` | `min(1, guaranteed_cost / 11)` | 一次必中以 `guaranteed_cost` 拿 11 隻（10 + 必中超激） |
| 剩餘距離 × `_min_cost_per_advance` | `min(1 / 最大單抽前進格數, guaranteed_cost / 最大必中前進格數)` | 重複稀有單抽可前進多格（B10 → 12A 是 2 格）；必中從 B 軌出發前進 11 格（B_n → A_(n+11)） |

寫死為 1 時，從 `1B` 找 54A 的單抽結果：真實成本 51（5 次必中 + 1 單抽），估計卻是 53，A* 的最優性保證就不成立。
實測上 heapq 的平手順序剛好掩蓋了這個高估（3,000 筆隨機案例沒有算錯），但那是運氣不是設計。
`tests/test_snapshot.py::TestOptimalityCrossCheck` 以關閉啟發式的 Dijkstra 當基準做隨機抽查，作為安全網。

### 排序與平手

優先佇列的元素是 `(f, -g, state_id, state)`：f 相同時先展開 g 大的（離目標較近），再以 `state_id`
保證順序穩定、結果可重現。排序鍵不放在 `SearchState` 上，所以兩個 f 相同的狀態不會被當成相等。

### 回報的成本

`prefer_guaranteed=True` 時單抽在搜尋中以 1.11 計價，只用來讓「10 單抽」排在「1 次必中」之後；
`GachaResult.total_cost` 一律由路徑重算實際抽數（單抽 1、必中 `guaranteed_cost`），與 .NET 版一致。

## 流程圖

```mermaid
graph TD
    Start([開始狀態 Start State]) --> PQ{Priority Queue}
    PQ -->|取出最小 Cost 狀態| CheckGoal{是否收集所有目標?}
    
    CheckGoal -- Yes --> Found([回傳最佳路徑])
    CheckGoal -- No --> CheckVisited{是否已訪問過?}
    
    CheckVisited -- Yes (且 Cost 更高) --> PQ
    CheckVisited -- No --> Expand[展開分支]
    
    Expand --> SingleRoll["單抽分支 (Cost +1)"]
    Expand --> GuaRoll["保底分支 (Cost +10)"]
    
    SingleRoll -->|更新狀態/收集貓咪| GenericPush[推入 PQ]
    GuaRoll -->|檢查資料完整性| GenericPush
    
    GenericPush --> PQ
```

---

## 記憶體優化技術

### 問題：狀態爆炸

多池模式下，狀態數量呈指數成長：

| 因素 | 影響 |
| --- | --- |
| N 個池子 | 分支因子 × N |
| 單抽 + 保底 | 分支因子 × 2 |
| 目標貓咪組合 | 2^目標數 種狀態 |

**範例**：2 池、200 步、10 個目標 → 可能產生數百萬狀態 → OOM

---

### 解決方案：Beam Search

Beam Search 是 A* 的**記憶體受限變體**，透過限制佇列大小來控制資源使用。

#### 原理

```text
完整 A*：保留所有可能的狀態
Beam Search：只保留「最有希望」的 K 個狀態
```

#### 實作

```python
# find_best_path 參數
beam_width: Optional[int] = None  # None = 完整 A*, 數值 = 限制

# 觸發條件：佇列超過 2 倍寬度時清理
if beam_width is not None and len(pq) > beam_width * 2:
    pq = heapq.nsmallest(beam_width, pq)  # 只保留最好的
    heapq.heapify(pq)
```

#### 祖先鏈保留

清理佇列時，同步清理 `state_map`，但**必須保留祖先鏈**：

```text
pq 中的狀態 → 回溯所有 parent_id → 這些都要保留
其他狀態 → 可以刪除
```

這確保 `_reconstruct_path()` 能正確回溯路徑。

#### 使用建議

| beam_width | 記憶體 | 解的品質 |
| --- | --- | --- |
| `None` | 無限制 | 保證最佳 |
| `50000` | ~500MB | 通常接近最佳 |
| `10000` | ~100MB | 可能略差 |

#### 權衡

> [!IMPORTANT]
> Beam Search **不保證找到最佳解**，但可以：
>
> - 避免 OOM 崩潰
> - 在合理時間內找到「還不錯」的解

---

## 技術總覽

| 技術 | 目的 | 實作位置 |
| --- | --- | --- |
| **A* 搜尋** | 快速找到最佳解 | `find_best_path` |
| **啟發式函數** | 引導搜尋方向 | `_heuristic` |
| **延遲路徑建構** | 減少記憶體 | `parent_id` + `_reconstruct_path` |
| **Beam Search** | 限制狀態數量 | `beam_width` 參數 |
| **祖先鏈保留** | 確保路徑可回溯 | `_collect_ancestor_ids` |
