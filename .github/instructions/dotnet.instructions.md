---
description: "Use when: 撰寫或修改 dotnet 子專案中的 C# 程式碼、建立 .csproj、設定 DI、實作模型或服務"
applyTo: "dotnet/**"
---

# .NET 子專案慣例

## 語言與框架

- C# 最新穩定版、.NET 10+
- 啟用 `<Nullable>enable</Nullable>` 與 `<ImplicitUsings>enable</ImplicitUsings>`

## 命名

- 命名空間對應目錄結構：`BattleCatCalc.Core.Models`、`BattleCatCalc.Infrastructure.Fetching`
- 介面加 `I` 前綴：`IFetcher`、`IParser`、`ISimulator`
- async 方法加 `Async` 後綴：`FetchHtmlAsync`
- 私有欄位用 `_camelCase`

## 模型

- 不可變值物件用 `readonly record struct`（如 `Position`、`SearchState`）
- 領域實體用 `record class`（如 `GachaSlot`、`GachaPool`、`GachaStep`）
- 集合屬性公開為 `IReadOnlyList<T>` 或 `FrozenSet<T>`，不暴露可變集合

## 分層依賴

```
Core ← Infrastructure
Core ← Application
Core + Infrastructure + Application ← Api
```

- **Core** 零外部 NuGet 依賴
- **Infrastructure** 可引用 AngleSharp、NLog 等
- **Application** 僅依賴 Core 介面與模型
- 禁止反向依賴（Application 不得引用 Infrastructure）

## 日誌

- 程式碼中僅依賴 `ILogger<T>`，不直接使用 NLog API
- 日誌 Provider（NLog）僅在 Api 層或程式進入點註冊

## 錯誤處理

- 解析失敗：`ParseException`（自訂，繼承 `Exception`）
- 搜尋未命中：回傳 `null`（nullable reference type）
- 網路錯誤：`HttpRequestException`
- 不允許靜默失敗

## 註解

- 公開 API 使用 XML doc（`<summary>`）以英文撰寫
- 內部實作註解以正體中文說明「為何如此設計」
- 程式碼內字串、錯誤訊息使用英文

## 測試

- 測試框架待定（xUnit 或 NUnit）
- 測試方法命名：`MethodName_Scenario_ExpectedResult`
- Fixture HTML 放 `dotnet/tests/fixtures/`，與 Python 版共用
- Mock 使用 NSubstitute 或 Moq

## 參考文件

- 架構決策：[docs/architecture.md](../../dotnet/docs/architecture.md)
- 模組對應：[docs/migration-mapping.md](../../dotnet/docs/migration-mapping.md)
