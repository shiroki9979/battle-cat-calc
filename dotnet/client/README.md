# BattleCatCalc Client

Battle Cats 種子追蹤計算器前端，以 React + TypeScript + Vite 建構，搭配 shadcn/ui + Tailwind CSS v4。

> 建立日期：2026-03-29  
> 最後更新：2026-03-29

## 技術棧

| 套件                      | 用途                       |
| ------------------------- | -------------------------- |
| React 19 + TypeScript 5.9 | UI 框架                    |
| Vite 8                    | 開發伺服器 + 打包          |
| Tailwind CSS v4           | 樣式系統                   |
| shadcn/ui (base-nova)     | 元件庫                     |
| TanStack Query            | API 請求快取與狀態管理     |
| TanStack Virtual          | 虛擬捲動（100-999 列軌道） |
| Lucide React              | 圖示                       |

## 使用者流程

```text
輸入種子 → 選擇轉蛋池 → 瀏覽軌道 → 標記目標貓咪 → 模擬最佳路徑 → 檢視結果
```

線性但可跳躍：使用者可在瀏覽軌道時反覆切換池子、搜尋貓咪。

## 頁面結構

單頁應用，三區塊佈局：

```text
┌─────────────────────────────────────────────────┐
│  Header：種子輸入 + 池子選擇 + 全域操作          │
├────────────────────────┬────────────────────────┤
│                        │                        │
│     Track A            │     Track B            │
│     (主軌道表格)        │     (副軌道表格)        │
│                        │                        │
├────────────────────────┴────────────────────────┤
│  Bottom Panel：模擬結果 / 路徑步驟               │
└─────────────────────────────────────────────────┘
```

### Header（固定頂部）

| 元素         | 說明                                               |
| ------------ | -------------------------------------------------- |
| 種子輸入框   | 數字輸入，置中醒目，Enter 送出                     |
| 轉蛋池選擇   | 下拉或 chips，支援多選（多池模擬）。每池需 eventId |
| 語言 / 筆數  | 可收合的進階選項（lang=tw，count=100）             |
| 搜尋框       | 即時篩選貓咪名稱，結果高亮在軌道表格中             |
| 「模擬」按鈕 | 有目標貓咪被選中時才啟用                           |

### 軌道表格（核心區域）

雙欄並排，每列一個 slot：

| 欄位         | 說明                   |
| ------------ | ---------------------- |
| #            | 位置 ID                |
| 單抽結果     | 貓咪名稱               |
| 保底結果     | 十連貓咪名稱           |
| 重複稀有標記 | `↔` 圖示（表示會切軌） |

互動：

- **點擊貓咪名稱** → 加入/移除目標清單（toggle，accent 色標記）
- **搜尋高亮** → 符合搜尋結果的格子背景色變化
- **重複稀有列** → 特殊底色 + 箭頭指向對面軌道的接續位置
- **虛擬捲動** → TanStack Virtual 處理大量列
- **起始位置** → 可點擊任意位置設為模擬起點

### 底部面板（模擬結果）

可收合的 drawer/panel：

| 區塊     | 內容                                                              |
| -------- | ----------------------------------------------------------------- |
| 摘要     | 總成本、單抽次數、保底次數、起點→終點                             |
| 路徑步驟 | 每步的 source→target、貓咪名稱、所屬池、是否保底（timeline 呈現） |
| 路徑標記 | 軌道表格上疊加路徑標記（箭頭 + 編號）                             |

## 元件結構

```text
src/
├── components/
│   ├── ui/                    # shadcn 元件
│   ├── header/
│   │   ├── SeedInput.tsx      # 種子輸入
│   │   ├── PoolSelector.tsx   # 轉蛋池選擇（多選）
│   │   └── CatSearch.tsx      # 貓咪搜尋
│   ├── track/
│   │   ├── TrackTable.tsx     # 單軌道表格（virtualized）
│   │   ├── TrackSlotRow.tsx   # 單列
│   │   └── DualTrackView.tsx  # A/B 雙軌並排容器
│   └── simulation/
│       ├── TargetCatList.tsx  # 已選目標貓咪 tag 列表
│       ├── SimulateButton.tsx # 模擬觸發
│       └── ResultPanel.tsx    # 結果面板
├── hooks/
│   ├── useTrackQuery.ts       # GET /tracks
│   ├── useSimulate.ts         # POST /simulate
│   └── useCatSearch.ts        # GET /cats/search（debounced）
├── lib/
│   ├── api.ts                 # fetch wrapper，base URL 設定
│   └── utils.ts               # cn()
└── types/
    └── api.ts                 # 對應後端 DTO 的 TypeScript 型別
```

## 狀態管理

- **伺服器狀態**：TanStack Query（tracks、cats/search、simulate）
- **UI 狀態**：React Context + useReducer（目標貓咪清單、選中起始位置、搜尋關鍵字）
- 規模不大，不需 Zustand 等外部狀態管理庫

## 開發階段

| 階段 | 內容                                              | 交付物                                    |
| ---- | ------------------------------------------------- | ----------------------------------------- |
| P0   | API 型別 + fetch wrapper + TanStack Query 設定    | `types/api.ts`、`lib/api.ts`、QueryClient |
| P1   | SeedInput + PoolSelector + TrackTable（基礎唯讀） | 能輸入種子、看到雙軌道                    |
| P2   | 貓咪搜尋 + 目標選擇互動                           | 能搜尋、點選目標                          |
| P3   | 模擬觸發 + ResultPanel                            | 完整模擬流程                              |
| P4   | 路徑視覺化疊加 + 虛擬捲動優化                     | 效能與體驗打磨                            |

## 對接 API

後端為 ASP.NET Core MVC Controllers，base URL 預設 `/api/v1`：

| 方法   | 路徑                      | 說明                |
| ------ | ------------------------- | ------------------- |
| `GET`  | `/api/v1/tracks`          | 取得 Track A/B 資料 |
| `POST` | `/api/v1/simulate`        | 執行最佳路徑模擬    |
| `GET`  | `/api/v1/cats/search`     | 搜尋貓咪名稱        |
| `GET`  | `/api/v1/cats/guaranteed` | 搜尋保底位置        |
| `GET`  | `/api/v1/health`          | 健康檢查            |

## 開發指令

```bash
npm install       # 安裝依賴
npm run dev       # 啟動開發伺服器（Vite HMR）
npm run build     # TypeScript 編譯 + Vite 生產建置
npm run lint      # ESLint 檢查
npm run preview   # 預覽生產建置
```
