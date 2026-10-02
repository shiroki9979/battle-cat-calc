<!-- markdownlint-disable MD024 -->
# API 參考文檔 (API Reference)

本文件提供 `battle_cat_calc` 套件核心類別的 API 說明。

---

## BCDataFetcher

從 BC Godfat 網站抓取轉蛋資料的非同步客戶端。

### 建構子

```python
BCDataFetcher(timeout: int = 10)
```

| 參數 | 型別 | 必填 | 預設值 | 說明 |
| --- | --- | --- | --- | --- |
| `timeout` | `int` | 否 | `10` | HTTP 請求逾時秒數 |

### 方法

#### `fetch_html`

```python
async def fetch_html(
    seed: str,
    event: str,
    lang: str = "tw",
    count: int = 100
) -> str
```

從 Godfat 抓取指定種子與活動的 HTML 內容。

| 參數 | 型別 | 必填 | 預設值 | 說明 |
| --- | --- | --- | --- | --- |
| `seed` | `str` | 是 | - | 玩家的種子編號 |
| `event` | `str` | 是 | - | 轉蛋活動 ID |
| `lang` | `str` | 否 | `"tw"` | 語言代碼 (tw/jp/en) |
| `count` | `int` | 否 | `100` | 要抓取的格子數量 |

**回傳值**: `str` — 原始 HTML 內容

**可能的錯誤**:

- `httpx.HTTPStatusError`: HTTP 請求失敗

### 使用範例

```python
import asyncio
from battle_cat_calc.fetcher import BCDataFetcher

async def main():
    fetcher = BCDataFetcher(timeout=15)
    html = await fetcher.fetch_html(
        seed="1234567890",
        event="2024-01-01_123",
        lang="tw",
        count=50
    )
    print(f"抓取到 {len(html)} 字元")

asyncio.run(main())
```

---

## BCParser

將 Godfat HTML 解析為結構化的 Track 資料。

### 建構子

```python
BCParser(html_content: str)
```

| 參數 | 型別 | 必填 | 說明 |
| --- | --- | --- | --- |
| `html_content` | `str` | 是 | 從 Godfat 抓取的 HTML 內容 |

**可能的錯誤**:

- `ValueError`: 找不到轉蛋表格或軌道驗證失敗

### 屬性

| 屬性 | 型別 | 說明 |
| --- | --- | --- |
| `track_a` | `Track` | Track A 的所有格子 |
| `track_b` | `Track` | Track B 的所有格子 |

### 方法

#### `get_tracks`

```python
def get_tracks() -> tuple[Track, Track]
```

**回傳值**: `(track_a, track_b)` — 兩條軌道的 tuple

#### `get_all_cats`

```python
def get_all_cats() -> set[str]
```

**回傳值**: `set[str]` — 所有出現過的貓咪名稱集合

### 使用範例

```python
from battle_cat_calc.parser import BCParser

html = "..."  # 從 fetcher 取得
parser = BCParser(html)

track_a, track_b = parser.get_tracks()
print(f"Track A 有 {len(track_a)} 格")
print(f"Track B 有 {len(track_b)} 格")

all_cats = parser.get_all_cats()
print(f"共有 {len(all_cats)} 種不同的貓咪")
```

---

## GachaSimulator

使用 A*/Dijkstra 演算法搜尋最佳抽卡路徑。支援單池與多池模式。

### 建構子

```python
GachaSimulator(track_a_or_pools: Union[Track, List[GachaPool]], track_b: Optional[Track] = None)
```

**單池模式：**

| 參數 | 型別 | 必填 | 說明 |
| --- | --- | --- | --- |
| `track_a_or_pools` | `Track` | 是 | Track A 資料 |
| `track_b` | `Track` | 是 | Track B 資料 |

**多池模式：**

| 參數 | 型別 | 必填 | 說明 |
| --- | --- | --- | --- |
| `track_a_or_pools` | `List[GachaPool]` | 是 | 轉蛋池列表 |

### 方法

#### `find_best_path`

```python
def find_best_path(
    start_pos: Position,
    target_cats: Set[str],
    max_rolls: int,
    progress_callback: Optional[Callable] = None,
    beam_width: Optional[int] = None,
    prefer_guaranteed: bool = False
) -> Optional[GachaResult]
```

搜尋花費最少的抽卡路徑來收集所有目標貓咪。

| 參數 | 型別 | 必填 | 預設值 | 說明 |
| --- | --- | --- | --- | --- |
| `start_pos` | `Position` | 是 | - | 起始位置 |
| `target_cats` | `Set[str]` | 是 | - | 目標貓咪名稱集合 |
| `max_rolls` | `int` | 是 | - | 最大抽卡次數限制 |
| `progress_callback` | `Optional[Callable]` | 否 | `None` | 進度回調，傳入 `(current_cost, states_processed, queue_size)` |
| `beam_width` | `Optional[int]` | 否 | `None` | Beam Search 寬度限制。`None` 為完整 A* 搜尋。建議值：10000–50000。使用 Beam Search 可能無法找到最佳解 |
| `prefer_guaranteed` | `bool` | 否 | `False` | 是否偏好十連抽。為 `True` 時單抽成本加小懲罰 (1.11)，讓演算法在成本相近時優先選擇十連抽 |

**回傳值**: `Optional[GachaResult]`

成功時回傳 `GachaResult` 物件（見下方資料模型），找不到路徑時回傳 `None`。

### 使用範例

**單池模式（向後相容）：**

```python
from battle_cat_calc.simulator import GachaSimulator
from battle_cat_calc.models import Position

sim = GachaSimulator(track_a, track_b)
result = sim.find_best_path(
    start_pos=Position(1, 'A'),
    target_cats={"影傑貓", "飛翔貓"},
    max_rolls=50
)

if result:
    print(result.to_summary())
else:
    print("在限制內找不到路徑")
```

**多池模式：**

```python
from battle_cat_calc.simulator import GachaSimulator
from battle_cat_calc.models import Position, GachaPool

pools = [
    GachaPool(pool_id="ultra_souls", name="超靈魂祭", track_a=ta1, track_b=tb1),
    GachaPool(pool_id="dark_heroes", name="闇英雄", track_a=ta2, track_b=tb2, guaranteed_cost=15),
]
sim = GachaSimulator(pools)
result = sim.find_best_path(
    start_pos=Position(1, 'A'),
    target_cats={"影傑貓", "飛翔貓"},
    max_rolls=100,
    beam_width=20000
)
```

---

## 資料模型 (Data Models)

### Position

代表軌道上的位置。

```python
@dataclass
class Position:
    id: int      # 格子編號 (1-based)
    track: str   # 軌道 ('A' 或 'B')
```

### GachaSlot

代表轉蛋表格中的單一格子。

```python
@dataclass
class GachaSlot:
    id: Position                              # 位置
    result: Union[str, ParseStatus]           # 單抽結果
    guaranteed: Union[str, ParseStatus]       # 保底結果
    next_pos: Optional[Union[Position, ParseStatus]] = None       # 保底後的落點（重複稀有格為替換後的版本）
    original_result: Optional[str] = None     # 替換前的原始結果
    original_guaranteed: Optional[str] = None # 替換前的原始保底
    is_duplicate_rare: bool = False           # 是否為重複稀有格
    single_next_pos: Optional[Position] = None    # 重複稀有被觸發時，單抽後的落點（HTML 結果欄的箭頭）
    original_next_pos: Optional[Position] = None  # 重複稀有沒被觸發時的保底落點（對應 original_guaranteed）
```

`next_pos` 與 `single_next_pos` 來自 HTML 的不同欄位（必中欄 vs 結果欄），不可混用。
`single_next_pos` 為 `None` 表示 HTML 沒有標示落點（例如落點超出表格範圍時標 `<?>`）。

### GachaStep

描述模擬過程中的單一步驟。

```python
@dataclass
class GachaStep:
    source: Position           # 起始位置
    target: Position           # 目標位置
    unit: str                  # 獲得的貓咪
    pool_id: str = ""          # 在哪個池抽的
    is_guaranteed: bool        # 是否為保底抽
    is_track_switch: bool      # 落點的軌道是否與起點不同（必中前 10 抽有重複稀有時可能為 False）
    is_duplicate_rare: bool    # 這一抽是否觸發重複稀有
    context_data: Optional[dict] = None  # 額外上下文資訊
```

### GachaPool

代表一個轉蛋活動池，用於多池模式。

```python
@dataclass
class GachaPool:
    pool_id: str              # 池子識別碼 (e.g., "ultra_souls")
    name: str                 # 顯示名稱 (e.g., "超靈魂祭")
    track_a: Track            # Track A 資料
    track_b: Track            # Track B 資料
    has_guaranteed: bool = True  # 是否有必中選項
    guaranteed_cost: int = 10    # 必中的抽數成本
```

### GachaResult

模擬結果的結構化封裝，包含便利屬性與序列化方法。

```python
@dataclass
class GachaResult:
    total_cost: int                     # 總花費抽數
    path: Tuple[GachaStep, ...]         # 路徑步驟
    collected: Set[str]                 # 收集到的貓咪
    start_pos: Optional[Position]       # 起始位置
    target_cats: Optional[Set[str]]     # 目標貓咪
```

**屬性：**

| 屬性 | 型別 | 說明 |
| --- | --- | --- |
| `step_count` | `int` | 路徑中的步數 |
| `guaranteed_count` | `int` | 十連抽的次數 |
| `single_roll_count` | `int` | 單抽的次數 |
| `pools_used` | `Set[str]` | 使用到的池子 ID 集合 |
| `end_pos` | `Optional[Position]` | 結束位置 |

**方法：**

| 方法 | 回傳值 | 說明 |
| --- | --- | --- |
| `to_summary()` | `str` | 生成易讀的摘要字串 |
| `to_dict()` | `Dict` | 轉換為可序列化的字典 |
| `steps_by_pool()` | `Dict[str, List[GachaStep]]` | 依池子分組的步驟 |

### Track

繼承自 `UserList`，包含一系列 `GachaSlot` 的專業容器。
內部會自動建立 `id -> slot` 索引，將查找時間複雜度降為 O(1)。

```python
class Track(UserList):
    def __init__(self, name: str, slots: List[GachaSlot] = None):
        """初始化軌道，並建立 O(1) 索引"""
    
    def get_slot(self, index: int) -> Optional[GachaSlot]
        """取得指定 ID 的格子 (1-based)，時間複雜度為 O(1)"""
```

---

## SeedAnalyzer

分析已解析的轉蛋資料，提供查詢功能。

### 建構子

```python
SeedAnalyzer(slots: Track)
```

| 參數 | 型別 | 必填 | 說明 |
| --- | --- | --- | --- |
| `slots` | `Track` | 是 | 軌道資料 |

### 方法

#### `find_cat_by_name`

```python
def find_cat_by_name(name: str) -> List[GachaSlot]
```

以名稱搜尋貓咪在軌道中出現的所有位置（模糊比對）。

**回傳值**: `List[GachaSlot]` — 符合的格子列表

#### `get_next_guaranteed`

```python
def get_next_guaranteed() -> Optional[GachaSlot]
```

找出下一個有保底結果的格子。

**回傳值**: `Optional[GachaSlot]` — 找到的格子或 `None`
