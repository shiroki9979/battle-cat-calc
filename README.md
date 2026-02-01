# battle-cat-calc

Battle Cats 抽卡路徑計算器 — 基於種子追蹤 (Seed Tracking) 的最佳路徑規劃工具。

## 功能特色

- **路徑規劃**：使用 Dijkstra 演算法找出收集目標貓咪的最少抽數路徑
- **資料解析**：自動解析 Godfat 網站的轉蛋資料
- **完整機制支援**：正確處理重複稀有 (Duplicate Rare) 與跳軌邏輯

## 專案架構

```text
battle-cat-calc/
├── src/battle_cat_calc/    # 核心套件
│   ├── fetcher.py          # 網路資料抓取 (async)
│   ├── parser.py           # HTML 解析與資料結構化
│   ├── simulator.py        # Dijkstra 路徑模擬器
│   ├── models.py           # 資料模型定義
│   └── config.py           # 設定檔
├── tests/                  # 測試目錄
├── notebooks/              # Jupyter Notebooks (實驗用)
├── docs/                   # 專案文件
│   ├── api_reference.md    # API 參考文檔
│   ├── design_logic.md     # 設計邏輯說明
│   └── gacha_mechanics_*.md # 抽卡機制說明
└── assets/                 # 靜態資源
```

## 快速開始

### 安裝

```bash
poetry install
```

### 使用範例

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
        print(f"最少需要 {result['total_cost']} 抽")

asyncio.run(main())
```

## 執行測試

```bash
poetry run pytest
```

## 文件

- [API 參考文檔](docs/api_reference.md)
- [設計邏輯](docs/design_logic.md)
- [抽卡機制說明 (玩家版)](docs/gacha_mechanics_player_guide_zh_TW.md)