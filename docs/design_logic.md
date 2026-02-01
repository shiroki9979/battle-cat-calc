# 模擬器設計邏輯 (Simulator Design Logic)

## 核心演算法

模擬器使用 **A* 演算法**來搜尋達成目標的「最小花費」路徑。

### A* vs Dijkstra

| 演算法 | 優先級 | 特點 |
|--------|--------|------|
| Dijkstra | `cost` | 均勻擴散探索 |
| **A*** | `cost + heuristic` | 朝目標方向優先探索 |

### 啟發式函數

使用**樂觀估計**：還缺幾隻目標貓 = 至少需要幾抽。

```python
def _heuristic(collected, target_cats):
    return len(target_cats - collected)
```

## 狀態定義 (SearchState)

每個搜尋節點 (State) 包含以下資訊：

- **Priority**: A* 的 f 值 (f = g + h)。
- **Cost**: 目前累積的抽卡次數 (g 值)。
- **Position**: 目前所在的格子 (ID 與 Track)。
- **Collected**: 目前已收集到的目標貓咪集合 (frozenset)。
- **state_id**: 唯一狀態識別碼（用於延遲路徑建構）。
- **parent_id**: 父狀態的 state_id（用於回溯路徑）。
- **last_step**: 這一步的 GachaStep 資訊。
- **last_rolled_rare**: 上一抽的稀有貓名稱（用於跨池重複稀有判定）。

### 延遲路徑建構

原本每個狀態儲存完整 path tuple，現改為只記錄 parent_id + last_step：

| 方式 | 記憶體 | 建構成本 |
|------|--------|----------|
| 原本 | O(狀態數 × 路徑長度) | 每次複製 tuple |
| **現在** | O(狀態數) | 只在找到解時回溯 |

## 狀態轉換 (State Transition)

從任何一個狀態出發，玩家可以執行兩種動作：

1. **單抽 (Single Roll)**:
    - 花費 +1。
    - 前進到下一格 (通常是同 Track, 除非遇到跳軌)。
    - 收集該格的貓咪。
2. **十連抽 (Guaranteed Roll)**:
    - 花費 +10。
    - 前進到指定的 `next_pos` (通常會跳軌)。
    - 收集接下來 10 格的貓咪 + 保底貓咪。

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
