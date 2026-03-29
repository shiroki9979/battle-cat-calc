# BattleCatCalc (.NET)

Battle Cats 種子追蹤計算器的 .NET 重寫版本。原始 Python 版本位於上層目錄。

> 建立日期：2026-03-29  
> 最後更新：2026-03-29

## 目標

將 Python 版核心功能遷移至 C#，取得以下優勢：

- 型別安全（record struct 取代 frozen dataclass）
- 搜尋效能提升（Dijkstra/A* 在 JIT 編譯下預期快 10-50 倍）
- 獨立部署（Kestrel 自託管 + Photino 桌面殼，或獨立 Web API）

## 目標框架

- .NET 10+（開發環境 Windows 上已安裝 .NET 10.0.200）
- C# 最新穩定版

## 專案結構

```text
dotnet/
├── BattleCatCalc.slnx
├── README.md
├── src/
│   ├── BattleCatCalc.Core/              # 領域模型、介面、純邏輯（零外部依賴）
│   │   ├── Models/                      # Position, GachaSlot, Track, GachaPool, GachaStep, GachaResult
│   │   ├── Enums/                       # ParseStatus
│   │   └── Interfaces/                  # IFetcher, IParser, ISimulator, IAnalyzer
│   ├── BattleCatCalc.Infrastructure/    # 外部依賴實作
│   │   ├── Fetching/                    # BCDataFetcher（HttpClient → bc.godfat.org）
│   │   └── Parsing/                     # BCParser（AngleSharp HTML 解析）
│   ├── BattleCatCalc.Application/       # 業務邏輯編排
│   │   ├── Simulation/                  # GachaSimulator（Dijkstra / A* / Beam Search）
│   │   └── Analysis/                    # SeedAnalyzer（貓咪查詢、保底搜尋）
│   ├── BattleCatCalc.Api/              # ASP.NET Core MVC Controllers（Kestrel 自託管）
│   │   ├── Controllers/                 # TrackController, SimulateController, CatController
│   │   ├── Contracts/                   # DTO（Request / Response）
│   │   └── Middleware/                  # ExceptionHandlingMiddleware
│   └── BattleCatCalc.Desktop/          # Photino 桌面殼（啟動 Kestrel + WebView 視窗）
├── client/                              # React + TypeScript 前端（Vite）
│   ├── src/
│   ├── package.json
│   └── vite.config.ts
├── tests/
│   ├── BattleCatCalc.Core.Tests/
│   ├── BattleCatCalc.Infrastructure.Tests/
│   ├── BattleCatCalc.Application.Tests/
│   ├── BattleCatCalc.Api.Tests/
│   └── fixtures/                        # 共用 HTML fixture（與 Python 版共用）
└── docs/
    ├── architecture.md                  # 架構決策紀錄
    └── migration-mapping.md             # Python → C# 模組對應
```

## 相依套件（預定）

| 套件                         | 用途                             | 對應 Python 套件 |
| ---------------------------- | -------------------------------- | ---------------- |
| AngleSharp                   | HTML 解析（CSS selector）        | BeautifulSoup4   |
| System.Net.Http              | HTTP 客戶端（內建）              | httpx            |
| Microsoft.Extensions.Logging | 日誌抽象層（內建）               | logging          |
| NLog.Extensions.Logging      | 日誌 Provider                    | —                |
| Photino.NET                  | 桌面視窗殼（系統 WebView 封裝）  | —                |
| xUnit                        | 單元測試                         | pytest           |
| NSubstitute / Moq            | Mock 框架                        | unittest.mock    |

### 前端（client/）

| 套件           | 用途                   |
| -------------- | ---------------------- |
| React 19+      | UI 框架                |
| TypeScript     | 型別安全               |
| Vite           | 開發伺服器 + 打包      |
| TanStack Query | API 資料快取與請求管理 |

## 建置與測試

```bash
dotnet build
dotnet test
```

## 開發順序

依據依賴方向，由內而外：

1. **Core** — 模型與介面（無外部依賴，可獨立驗證）
2. **Infrastructure** — Fetcher + Parser（依賴 Core 介面）
3. **Application** — Simulator + Analyzer（依賴 Core 模型）
4. **Api** — ASP.NET Core Minimal API（Kestrel 自託管，Swagger 驗證）
5. **Client** — React + TypeScript 前端（Vite，對接 Api 層）
6. **Desktop** — Photino 桌面殼（啟動 Kestrel + WebView 視窗）

詳見 [docs/architecture.md](docs/architecture.md) 與 [docs/migration-mapping.md](docs/migration-mapping.md)。

## 領域知識

本專案涉及貓咪大戰爭的轉蛋機制，包含種子追蹤、雙軌道系統、重複稀有跳軌、十連抽保底等。相關文件位於 Python 版的 `docs/` 目錄：

| 文件                                                                                   | 內容                                          | 目標讀者   |
| -------------------------------------------------------------------------------------- | --------------------------------------------- | ---------- |
| [gacha_mechanics_zh_TW.md](../docs/gacha_mechanics_zh_TW.md)                           | 種子 PRNG、雙軌道、移動規則的技術細節         | 開發者     |
| [gacha_mechanics_player_guide_zh_TW.md](../docs/gacha_mechanics_player_guide_zh_TW.md) | 轉蛋機制玩家版指南                            | 初次接觸者 |
| [design_logic.md](../docs/design_logic.md)                                             | Dijkstra/A* 演算法設計、SearchState 狀態轉換  | 開發者     |
| [api_reference.md](../docs/api_reference.md)                                           | Python 版 API 規格（C# 版介面設計的參考基準） | 開發者     |

建議閱讀順序：玩家指南 → 技術細節 → 演算法設計。

## 進度追蹤

### Core

- [x] `Position` readonly record struct
- [x] `ParseStatus` enum
- [x] `GachaSlot` record class
- [x] `Track` 容器（IReadOnlyList + O(1) 查找）
- [x] `GachaPool` record class
- [x] `GachaStep` record class
- [x] `GachaResult` record class
- [x] `SlotValue` discriminated union（`CatName` / `Status`）
- [x] `IFetcher` 介面
- [x] `IParser` 介面
- [x] `ISimulator` 介面
- [x] `IAnalyzer` 介面
- [x] `BattleCatCalcException` 基底例外
- [x] `ParseException`、`FetchException`、`SimulationException`
- [x] Core.Tests 單元測試（24 tests passing）

### Infrastructure

- [x] `BCDataFetcher`（HttpClient 實作）
- [x] `BCParser`（AngleSharp 實作）
- [x] `ParseException`、`FetchException`
- [x] Infrastructure.Tests 單元測試 + 快照整合測試（17 tests passing）

### Application

- [x] `SearchState` readonly record struct（內部搜尋狀態）
- [x] `GachaSimulator`（A* / Beam Search，單池 + 多池，支配性/可達性剪枝）
- [x] `SeedAnalyzer`（貓咪查詢、保底位置搜尋）
- [x] Application.Tests 單元測試（22 tests passing）

### Api

- [x] ASP.NET Core Minimal API 專案建置
- [x] DI 註冊（Fetcher、Parser）
- [x] `GET /api/v1/tracks`
- [x] `POST /api/v1/simulate`
- [x] `GET /api/v1/cats/search`
- [x] `GET /api/v1/cats/guaranteed`
- [x] `GET /api/v1/health`
- [x] 全域例外處理（Problem Details RFC 9457）
- [x] Static Files 中介軟體（serve React build 產出物）
- [x] OpenAPI（`/openapi/v1.json`，開發環境）
- [x] NLog 設定（Console + File，ADR-7）
- [x] Api.Tests 整合測試（WebApplicationFactory，14 tests passing）

### Client（React 前端）

- [ ] Vite + React + TypeScript 專案初始化
- [ ] API client 型別定義（對接後端 endpoints）
- [ ] 種子輸入 + 活動選擇頁面
- [ ] 軌道資料表格展示
- [ ] 模擬結果（路徑步驟）展示
- [ ] 貓咪搜尋功能

### Desktop（Photino 桌面殼）

- [ ] Photino.NET 專案建置
- [ ] Kestrel 自託管（`localhost:0` 隨機 port）
- [ ] PhotinoWindow 載入 React UI
- [ ] 生命週期管理（視窗關閉 → Kestrel shutdown）
- [ ] 單一可執行檔打包

### 基礎建設

- [x] 目錄結構
- [x] README
- [x] 架構決策文件
- [x] 模組對應文件
- [x] .gitignore
- [x] AI 輔助開發指令（dotnet.instructions.md）
- [x] .slnx + .csproj 檔案建立
- [ ] CI workflow（GitHub Actions）
