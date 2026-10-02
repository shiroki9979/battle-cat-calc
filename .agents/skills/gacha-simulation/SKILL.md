---
name: gacha-simulation
description: "修改或擴展 GachaSimulator 演算法的工作流。Use when: 調整 Dijkstra/A*/Beam Search、修改狀態轉換邏輯、變更啟發式函數、擴展多池處理、優化搜尋效能"
---

# Gacha Simulation 演算法修改工作流

## 適用時機

- 修改 `GachaSimulator` 的搜尋演算法（Dijkstra / A* / Beam Search）
- 調整狀態轉換規則（單抽、保底、軌道切換）
- 變更或新增啟發式函數
- 擴展多池處理邏輯
- 優化記憶體或搜尋效能

## 參考文件

修改前務必閱讀：

- [design_logic.md](../../../docs/design_logic.md) — 演算法設計決策、延遲路徑建構、狀態定義
- [api_reference.md](../../../docs/api_reference.md) — `GachaSimulator` 公開 API 規格
- [gacha_mechanics_zh_TW.md](../../../docs/gacha_mechanics_zh_TW.md) — 遊戲機制規則（種子、軌道、保底）

## 步驟

### 1. 理解現有狀態結構

`SearchState` 為 `@dataclass(frozen=True, order=True, slots=True)`：

| 欄位 | 型別 | 用途 |
| --- | --- | --- |
| `priority` | `float` | A* f-value (`cost + heuristic`)；佇列元素為 `(f, -g, state_id, state)`，排序鍵不在狀態上 |
| `cost` | `float` | 累計抽卡成本（g-value） |
| `pos_id` | `int` | 當前位置 ID |
| `pos_track` | `str` | 當前軌道 `'A'` / `'B'` |
| `collected` | `frozenset` | 已收集的目標貓咪 |
| `state_id` | `int` | 唯一狀態 ID（延遲路徑建構用） |
| `parent_id` | `Optional[int]` | 父狀態 ID（回溯路徑用） |
| `last_step` | `Optional[GachaStep]` | 本次操作資訊 |
| `last_rolled_rare` | `Optional[str]` | 上一抽實際拿到的貓，只記目錄標為稀有的貓（無目錄時記曾被替換掉的貓）；跨池重複稀有偵測 |

### 2. 確認不變量

修改任何邏輯前，確保以下不變量不被破壞：

1. **位置單調遞增** — `pos_id` 只增不減（包含保底跳躍和軌道切換）
2. **可達性剪枝** — 所有剩餘目標的 `max_pos < current_pos` 時跳過分支
3. **支配性剪枝** — 同 `(pos_id, track)` 只保留非被支配的 `(cost, collected)` 組合
4. **SearchState 不可變** — frozen dataclass，`collected` 使用 `frozenset`
5. **延遲路徑鏈完整** — 每個狀態的 `parent_id` 鏈可追溯至起始狀態（`None`）
6. **Beam Search 祖先保留** — 剪枝 `state_map` 時必須保留 PQ 中所有狀態的祖先鏈
7. **跨池 last_rolled_rare** — 上一抽拿到的貓跨池持續，用於偵測跨池重複稀有；每一抽（含必中超激）都要更新，
   不能只在重複稀有格更新，否則連續兩次抽到同一隻稀有貓時永遠不會觸發
8. **保底成本一致** — 同池所有路徑使用相同 `pool.guaranteed_cost`
9. **啟發式可容許性** — `heuristic(s) <= actual_remaining_cost(s)`，保證 A* 最優解。兩個係數由 `_scan_rules`
   從資料算出：每隻貓 `min(1, guaranteed_cost / 11)`、每格 `min(1 / 最大單抽前進格數, guaranteed_cost / 最大必中前進格數)`；
   新增任何「一抽拿更多」或「一抽走更遠」的規則都要重算，`TestOptimalityCrossCheck` 會以 h=0 的 Dijkstra 比對
10. **未標記重複拒走** — 一般格而上一隻就是這格的貓時 `_resolve_single_roll` 回傳 `None`；不要改成「拿原始貓繼續走」
11. **回報整數成本** — `GachaResult.total_cost` 由 `_actual_cost(path)` 重算，`prefer_guaranteed` 的 1.11 只用於排序
12. **排序穩定** — 佇列元素 `(f, -g, state_id, state)`，f 相同先展開 g 大的，結果可重現

### 3. 執行修改

- 修改搜尋邏輯 → 編輯 `src/battle_cat_calc/simulator.py`
- 修改資料結構 → 同步更新 `src/battle_cat_calc/models.py` 中對應的 dataclass
- 新增欄位至 `SearchState` → 確保 `compare=False`（除非有意影響 PQ 排序）

### 4. 撰寫測試

測試檔案：`tests/test_simulator.py`（基礎）、`tests/test_simulator_advanced.py`（進階）

Mock Track 建構模式：

```python
track_a = Track("A", [
    GachaSlot(id=Position(i, 'A'), result=f"Cat{i}",
              guaranteed=f"Gua{i}", next_pos=Position(i+10, 'B'))
    for i in range(1, count+1)
])
```

多池測試：

```python
pool = GachaPool(
    pool_id="pool_name", name="Display Name",
    track_a=track_a, track_b=track_b,
    has_guaranteed=True, guaranteed_cost=10
)
sim = GachaSimulator([pool_a, pool_b])
```

必須覆蓋：

- 修改的轉換規則產出正確的 next state
- 不變量未被破壞（位置單調、路徑鏈完整）
- 邊界條件（空軌道、單目標、全目標已收集、max_rolls 耗盡）

### 5. 驗證

```bash
uv run pytest tests/test_simulator.py tests/test_simulator_advanced.py -v
```

若修改影響多池或解析：

```bash
uv run pytest tests/test_multi_pool.py tests/test_snapshot.py -v
```

## 效能考量

| 參數 | 值 | 說明 |
| --- | --- | --- |
| `beam_width=None` | 無限 | 保證最優解，記憶體無上限 |
| `beam_width=50000` | ~500MB | 通常接近最優 |
| `beam_width=10000` | ~100MB | 可能次優 |

Beam Search 觸發條件：`len(pq) > beam_width * 2` → 保留最佳 `beam_width` 個狀態。

## 常見陷阱

- 把排序鍵放回 `SearchState`（`order=True`）→ f 相同的不同狀態會被視為相等；排序鍵只放在佇列元素
- 修改啟發式使其不可容許（高估）→ A* 不再保證最優解
- Beam Search 剪枝未保留祖先鏈 → 路徑回溯時 `KeyError`
- 保底分支未檢查 10 個連續有效 slot → 無效狀態進入 PQ
- 保底視窗固定取同軌的 `pos+0..pos+9` → 前 10 抽遇到重複稀有會換軌，必須照單抽規則（`_resolve_single_roll`）逐抽走
- 把必中欄的 `next_pos` 當成重複稀有的單抽落點 → 單抽會跳到十格之外；單抽落點是 `single_next_pos`
- 重複稀有格沒被觸發卻拿替換後的貓 → 未觸發時要拿 `original_result`，必中用 `original_guaranteed` / `original_next_pos`
- 新增會讓單抽一次前進多格的規則 → 同步更新 `_min_cost_per_advance`，否則啟發式不再是下界
