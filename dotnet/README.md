# BattleCatCalc (.NET)

Battle Cats 種子追蹤計算器的 .NET 重寫版本。原始 Python 版本位於上層目錄。

> 建立日期：2026-03-29

## 目標

將 Python 版核心功能遷移至 C#，取得以下優勢：

- 型別安全（record struct 取代 frozen dataclass）
- 搜尋效能提升（Dijkstra/A* 在 JIT 編譯下預期快 10-50 倍）
- 獨立部署（AOT 編譯為單一可執行檔，或搭 ASP.NET Core 做 Web API）

## 目標框架

- .NET 10+（開發環境 Windows 上已安裝 .NET 10.0.200）
- C# 最新穩定版

## 專案結構

```text
dotnet/
├── BattleCatCalc.sln
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
│   └── BattleCatCalc.Api/              # ASP.NET Core Web API（後期加入）
├── tests/
│   ├── BattleCatCalc.Core.Tests/
│   ├── BattleCatCalc.Infrastructure.Tests/
│   ├── BattleCatCalc.Application.Tests/
│   └── fixtures/                        # 共用 HTML fixture（與 Python 版共用）
└── docs/
    ├── architecture.md                  # 架構決策紀錄
    └── migration-mapping.md             # Python → C# 模組對應
```

## 相依套件（預定）

| 套件 | 用途 | 對應 Python 套件 |
|------|------|-----------------|
| AngleSharp | HTML 解析（CSS selector） | BeautifulSoup4 |
| System.Net.Http | HTTP 客戶端（內建） | httpx || Microsoft.Extensions.Logging | 日誌抽象層（內建） | logging |
| NLog.Extensions.Logging | 日誌 Provider | — || xUnit / NUnit | 單元測試 | pytest |
| NSubstitute / Moq | Mock 框架 | unittest.mock |

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
4. **Api** — Web API（依賴以上所有，後期加入）

詳見 [docs/architecture.md](docs/architecture.md) 與 [docs/migration-mapping.md](docs/migration-mapping.md)。

## 領域知識

本專案涉及貓咪大戰爭的轉蛋機制，包含種子追蹤、雙軌道系統、重複稀有跳軌、十連抽保底等。相關文件位於 Python 版的 `docs/` 目錄：

| 文件 | 內容 | 目標讀者 |
|------|------|---------|
| [gacha_mechanics_zh_TW.md](../docs/gacha_mechanics_zh_TW.md) | 種子 PRNG、雙軌道、移動規則的技術細節 | 開發者 |
| [gacha_mechanics_player_guide_zh_TW.md](../docs/gacha_mechanics_player_guide_zh_TW.md) | 轉蛋機制玩家版指南 | 初次接觸者 |
| [design_logic.md](../docs/design_logic.md) | Dijkstra/A* 演算法設計、SearchState 狀態轉換 | 開發者 |
| [api_reference.md](../docs/api_reference.md) | Python 版 API 規格（C# 版介面設計的參考基準） | 開發者 |

建議閱讀順序：玩家指南 → 技術細節 → 演算法設計。

## 進度追蹤

### Core

- [ ] `Position` readonly record struct
- [ ] `ParseStatus` enum
- [ ] `GachaSlot` record class
- [ ] `Track` 容器（IReadOnlyList + O(1) 查找）
- [ ] `GachaPool` record class
- [ ] `GachaStep` record class
- [ ] `GachaResult` record class
- [ ] `IFetcher` 介面
- [ ] `IParser` 介面
- [ ] `ISimulator` 介面
- [ ] `IAnalyzer` 介面
- [ ] `BattleCatCalcException` 基底例外
- [ ] Core.Tests 單元測試

### Infrastructure

- [ ] `BCDataFetcher`（HttpClient 實作）
- [ ] `BCParser`（AngleSharp 實作）
- [ ] `ParseException`、`FetchException`
- [ ] Infrastructure.Tests 單元測試（含 HTML fixture）

### Application

- [ ] `GachaSimulator`（Dijkstra / A* / Beam Search）
- [ ] `SeedAnalyzer`（貓咪查詢、保底搜尋）
- [ ] `SimulationException`
- [ ] Application.Tests 單元測試

### Api

- [ ] ASP.NET Core 專案建置
- [ ] `GET /api/v1/tracks`
- [ ] `POST /api/v1/simulate`
- [ ] `GET /api/v1/cats/search`
- [ ] `GET /api/v1/cats/guaranteed`
- [ ] 全域例外處理（Problem Details）
- [ ] DI 註冊
- [ ] NLog 設定

### 基礎建設

- [x] 目錄結構
- [x] README
- [x] 架構決策文件
- [x] 模組對應文件
- [x] .gitignore
- [x] AI 輔助開發指令（dotnet.instructions.md）
- [ ] .sln + .csproj 檔案建立
- [ ] CI workflow（GitHub Actions）
