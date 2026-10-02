# 模擬器設計邏輯 (Simulator Design Logic)

## 核心演算法

模擬器使用 **Dijkstra 演算法**來搜尋達成目標的「最小花費」路徑。

### 演算法選擇

| 演算法 | 優先級 | 特點 |
| --- | --- | --- |
| **Dijkstra** | `cost` | 保證最佳解，均勻擴散探索 |
| A* | `cost + heuristic` | 較快但需要啟發式函數 |

選擇 Dijkstra 的原因：**簡單、穩定、保證最佳解**。

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
  每一抽都會更新，但只保留可能觸發重複的貓（曾在某個重複稀有格被替換掉的貓），其餘存 `None`，以免狀態空間膨脹。

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
2. **十連抽 (Guaranteed Roll)**:
    - 花費 +10。
    - 前 10 抽照單抽規則走（遇到重複稀有會換軌），收集沿途的貓咪，再加上保底貓咪。
    - 前進到起始格必中欄的落點（`next_pos`，起始格是未觸發的重複稀有格時用 `original_next_pos`）。
      通常會跳軌，但前 10 抽內有一次重複稀有時會留在原軌。

### 啟發式與重複稀有

`_heuristic` 以「剩餘距離 × 每格最低成本」估計下界。重複稀有讓單抽一次前進多格
（B10 → 12A 前進 2 格），每格成本因此更低，所以 `_min_cost_per_advance` 必須納入
`1 / 最大單抽前進格數`，否則會高估剩餘成本而破壞 A* 的最優性。

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
