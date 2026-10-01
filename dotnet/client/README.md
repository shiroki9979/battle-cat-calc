# BattleCatCalc Client

Created: 2026-03-29
Last updated: 2026-10-02

React、TypeScript 與 Vite 前端，搭配 TanStack Query、TanStack Virtual 及 Tailwind CSS。

## 容器工作流程

從 repository root 執行，主機只需 Docker、Compose 與 GNU Make：

```bash
make dev
make client-build
make test-e2e
make test-e2e E2E_ARGS='--project chromium'
make build
make test
make up
make health
make down
```

`make dev` 啟動 API watch 與 Vite；前端位於 `http://localhost:5173`，API 位於
`http://localhost:5096`。Dev Container 自動啟動 Vite，API 可使用 VS Code 的
`watch API` 或 debugger。`build client` 與 `test client` tasks 也在容器內執行。

建置先執行 `npm ci`，再執行 TypeScript checking 與 Vite build。`node_modules`
與 `dist` 存放於 Docker volumes。Playwright 的 Chromium、Firefox、WebKit 與
系統相依套件由 E2E image 提供，報告寫入主機 `test-results/`。建置或測試前需停止
使用共用 volumes 的開發服務。完整設定與清理方式見 [容器文件](../docs/containers.md)。

Production image 將 Vite 產物放入 API 的 `wwwroot`，由同一個 ASP.NET Core
服務提供前端與 `/api/v1`，主機入口為 `http://localhost:5096`。

## 使用者流程

輸入種子、語言與活動後，按下 `Fetch Tracks` 取得雙軌資料。可透過貓咪選擇器或
單抽／保底名稱加入目標，再使用 `Find Optimal Path` 搜尋路徑。模擬固定從 `1A`
開始，目前介面使用單一活動。重新送出查詢時清空目標；更換已送出的查詢或目標後，
先前模擬結果會失效。

## 程式結構

- `src/App.tsx`：URL 查詢參數、已送出查詢、目標與模擬結果。
- `src/components/EventSelector.tsx`：活動載入、分組與篩選。
- `src/components/CatPicker.tsx`、`TargetList.tsx`：目標選擇與移除。
- `src/components/DualTrackView.tsx`、`TrackSlotRow.tsx`：虛擬捲動雙軌與稀有度配色。
- `src/components/ResultPanel.tsx`：模擬摘要與路徑。
- `src/hooks/`：TanStack Query 查詢與 mutation。
- `src/lib/api.ts`、`src/types/api.ts`：API 呼叫與 DTO 型別。
- `e2e/`：以 API mocks 隔離上游的 Playwright 使用者流程測試。

`CatSearch`、`PoolSelector` 與 `TrackTable` 尚未接入目前的 `App`。

## API

| 方法 | 路徑 | 用途 |
| --- | --- | --- |
| GET | `/api/v1/events` | 活動清單；lang 支援 tw、en、jp、kr |
| POST | `/api/v1/events/refresh` | 重新取得指定語言的活動清單 |
| GET | `/api/v1/tracks` | 單一活動的雙軌資料 |
| POST | `/api/v1/simulate` | 搜尋目標貓的抽取路徑 |
| GET | `/api/v1/cats/search` | API client 提供名稱搜尋，目前 App 未使用 |
| GET | `/api/v1/health` | API 健康檢查 |

開發時 Vite 的 `API_PROXY_TARGET` 指向 `http://sdk:8080`；未提供環境變數時，
預設為 `http://localhost:5096`。Production 使用相同 origin，不需 Vite proxy。
