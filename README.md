# battle-cat-calc

Battle Cats 抽卡路徑計算器 — 基於種子追蹤 (Seed Tracking) 的最佳路徑規劃工具。

## 功能特色

- **路徑規劃**：使用 Dijkstra / A* 演算法找出收集目標貓咪的最少抽數路徑
- **多池支援**：可跨多個轉蛋池進行聯合路徑最佳化，各池可設定不同保底成本
- **Beam Search**：大規模搜尋時可限制佇列寬度，以記憶體換取可行解
- **資料解析**：自動解析 [bc.godfat.org](https://bc.godfat.org/) 的轉蛋資料
- **完整機制支援**：正確處理重複稀有 (Duplicate Rare)、跳軌邏輯、十連抽保底

## 專案架構

```text
battle-cat-calc/
├── src/battle_cat_calc/    # 核心套件
│   ├── config.py           # 設定常數
│   ├── models.py           # 資料模型定義
│   ├── fetcher.py          # 網路資料抓取 (async httpx)
│   ├── parser.py           # HTML 解析與資料結構化
│   ├── simulator.py        # Dijkstra / A* / Beam Search 路徑模擬器
│   ├── analyzer.py         # 資料分析工具（貓咪查詢、保底搜尋）
│   └── main.py             # 程式進入點（預留）
├── tests/                  # 測試目錄
├── notebooks/              # Jupyter Notebooks (實驗用)
├── docs/                   # 專案文件
│   ├── api_reference.md    # API 參考文檔
│   ├── design_logic.md     # 設計邏輯說明
│   └── gacha_mechanics_*.md # 抽卡機制說明
└── assets/                 # 靜態資源
```

資料流為線性：`fetcher.py` → `parser.py` → `simulator.py`，`analyzer.py` 提供輔助查詢。

## 快速開始

### 安裝

```bash
uv sync               # Python ≥ 3.12
```

### 使用範例

**單池模式：**

```python
import asyncio
from battle_cat_calc.fetcher import BCDataFetcher
from battle_cat_calc.parser import BCParser
from battle_cat_calc.simulator import GachaSimulator
from battle_cat_calc.models import Position

async def main():
    # 1. 抓取資料
    fetcher = BCDataFetcher()
    html = await fetcher.fetch_html(seed="your_seed", event="event_id")
    
    # 2. 解析資料
    parser = BCParser(html)
    track_a, track_b = parser.get_tracks()
    
    # 3. 模擬路徑
    sim = GachaSimulator(track_a, track_b)
    result = sim.find_best_path(
        start_pos=Position(1, 'A'),
        target_cats={"目標貓咪"},
        max_rolls=50
    )
    
    if result:
        print(result.to_summary())

asyncio.run(main())
```

**多池模式：**

```python
from battle_cat_calc.simulator import GachaSimulator
from battle_cat_calc.models import Position, GachaPool

# 各池分別透過 fetcher + parser 取得 track_a / track_b
pools = [
    GachaPool(pool_id="ultra_souls", name="超靈魂祭", track_a=ta1, track_b=tb1),
    GachaPool(pool_id="dark_heroes", name="闇英雄", track_a=ta2, track_b=tb2),
]
sim = GachaSimulator(pools)
result = sim.find_best_path(
    start_pos=Position(1, 'A'),
    target_cats={"影傑貓", "飛翔貓"},
    max_rolls=100,
    beam_width=20000  # 限制記憶體使用
)
```

## 執行測試

```bash
uv run pytest
```

## 文件

- [API 參考文檔](docs/api_reference.md) — 所有類別的方法簽章、參數與範例
- [設計邏輯](docs/design_logic.md) — 演算法選擇、狀態定義、記憶體最佳化
- [抽卡機制說明 (玩家版)](docs/gacha_mechanics_player_guide_zh_TW.md)
- [抽卡機制說明 (技術版)](docs/gacha_mechanics_zh_TW.md)
