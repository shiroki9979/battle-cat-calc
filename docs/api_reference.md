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
|------|------|------|--------|------|
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
|------|------|------|--------|------|
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
|------|------|------|------|
| `html_content` | `str` | 是 | 從 Godfat 抓取的 HTML 內容 |

**可能的錯誤**:

- `ValueError`: 找不到轉蛋表格或軌道驗證失敗

### 屬性

| 屬性 | 型別 | 說明 |
|------|------|------|
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

使用 Dijkstra 演算法搜尋最佳抽卡路徑。

### 建構子

```python
GachaSimulator(track_a: Track, track_b: Track)
```

| 參數 | 型別 | 必填 | 說明 |
|------|------|------|------|
| `track_a` | `Track` | 是 | Track A 資料 |
| `track_b` | `Track` | 是 | Track B 資料 |

### 方法

#### `find_best_path`

```python
def find_best_path(
    start_pos: Position,
    target_cats: Set[str],
    max_rolls: int
) -> Optional[Dict]
```

搜尋花費最少的抽卡路徑來收集所有目標貓咪。

| 參數 | 型別 | 必填 | 說明 |
|------|------|------|------|
| `start_pos` | `Position` | 是 | 起始位置 |
| `target_cats` | `Set[str]` | 是 | 目標貓咪名稱集合 |
| `max_rolls` | `int` | 是 | 最大抽卡次數限制 |

**回傳值**: `Optional[Dict]`

成功時回傳:

```python
{
    "total_cost": int,        # 總花費抽數
    "path": Tuple[GachaStep], # 路徑步驟
    "collected": Set[str]     # 收集到的貓咪
}
```

找不到路徑時回傳 `None`。

### 使用範例

```python
from battle_cat_calc.simulator import GachaSimulator
from battle_cat_calc.models import Position

# 從 parser 取得 tracks
sim = GachaSimulator(track_a, track_b)

result = sim.find_best_path(
    start_pos=Position(1, 'A'),
    target_cats={"影傑貓", "飛翔貓"},
    max_rolls=50
)

if result:
    print(f"最少需要 {result['total_cost']} 抽")
    for step in result['path']:
        print(f"  {step.source} -> {step.target}: 獲得 {step.unit}")
else:
    print("在限制內找不到路徑")
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
    next_pos: Optional[Position] = None       # 保底後的跳轉位置
    original_result: Optional[str] = None     # 替換前的原始結果
    original_guaranteed: Optional[str] = None # 替換前的原始保底
    is_duplicate_rare: bool = False           # 是否為重複稀有格
```

### GachaStep

描述模擬過程中的單一步驟。

```python
@dataclass
class GachaStep:
    source: Position           # 起始位置
    target: Position           # 目標位置
    unit: str                  # 獲得的貓咪
    is_guaranteed: bool        # 是否為保底抽
    is_track_switch: bool      # 是否發生跳軌
    context_data: Optional[dict] = None  # 額外上下文資訊
```

### Track

繼承自 `UserList`，包含一系列 `GachaSlot` 的容器。

```python
class Track(UserList):
    name: str  # 軌道名稱 ('A' 或 'B')
    
    def get_slot(index: int) -> Optional[GachaSlot]
        """取得指定 ID 的格子 (1-based)"""
```
