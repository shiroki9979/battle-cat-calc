# Python → C# 模組對應

> 建立日期：2026-03-29

本文件記錄 Python 版各模組在 C# 中的對應位置、型別轉換與需注意的差異。

## 模組對應總覽

| Python 模組    | C# 專案        | C# 命名空間                             | 狀態   |
| -------------- | -------------- | --------------------------------------- | ------ |
| `models.py`    | Core           | `BattleCatCalc.Core.Models`             | 已完成 |
| `config.py`    | Core           | `BattleCatCalc.Core`                    | 待實作 |
| `fetcher.py`   | Infrastructure | `BattleCatCalc.Infrastructure.Fetching` | 已完成 |
| `parser.py`    | Infrastructure | `BattleCatCalc.Infrastructure.Parsing`  | 已完成 |
| `simulator.py` | Application    | `BattleCatCalc.Application.Simulation`  | 已完成 |
| `analyzer.py`  | Application    | `BattleCatCalc.Application.Analysis`    | 已完成 |
| `main.py`      | Desktop        | `BattleCatCalc.Desktop`                 | 待實作 |
| —              | Api            | `BattleCatCalc.Api`                     | 已完成 |
| —              | Client (React) | `dotnet/client/`                        | 待實作 |

## 型別對應

### 基礎模型（Core.Models）

```text
Python                                  C#
──────────────────────────────────────────────────────────────
Position (dataclass)                →   readonly record struct Position(int Id, string Track)
ParseStatus (Enum)                  →   enum ParseStatus { MultipleData, InvalidData, Ambiguous }
GachaSlot (dataclass)               →   record class GachaSlot { ... }
Track (UserList)                    →   class Track : IReadOnlyList<GachaSlot>
GachaPool (dataclass)               →   record class GachaPool { ... }
GachaStep (dataclass)               →   record class GachaStep { ... }
GachaResult (dataclass)             →   record class GachaResult { ... }
```

### Simulator 內部型別（Application.Simulation）

```text
Python                                  C#
──────────────────────────────────────────────────────────────
SearchState (frozen dataclass)      →   readonly record struct SearchState : IComparable<SearchState>
  .priority (float)                 →   float Priority (排序欄位)
  .cost (float)                     →   float Cost
  .pos_id (int)                     →   int PosId
  .pos_track (str)                  →   string PosTrack
  .collected (frozenset)            →   FrozenSet<string> Collected
  .state_id (int)                   →   int StateId
  .parent_id (Optional[int])        →   int? ParentId
  .last_step (Optional[GachaStep])  →   GachaStep? LastStep
  .last_rolled_rare (Optional[str]) →   string? LastRolledRare
```

### 集合型別

```text
Python                                  C#
──────────────────────────────────────────────────────────────
frozenset[str]                      →   FrozenSet<string>（不可變、可雜湊）
Set[str]                            →   HashSet<string>
Dict[int, GachaSlot]                →   Dictionary<int, GachaSlot>
List[GachaStep]                     →   List<GachaStep> 或 ImmutableArray<GachaStep>
Tuple[GachaStep, ...]               →   ImmutableArray<GachaStep>
Optional[T]                         →   T?（nullable）
Union[str, ParseStatus]             →   OneOf<string, ParseStatus> 或自訂 SlotValue
```

## 介面定義（Core.Interfaces）

```text
Python 類別                          C# 介面
──────────────────────────────────────────────────────────────
BCDataFetcher                       →   IFetcher
  .fetch_html(seed, event, ...)     →   Task<string> FetchHtmlAsync(string seed, string eventId, ...)

BCParser                            →   IParser
  .track_a / .track_b               →   (Track TrackA, Track TrackB) Parse(string html)

GachaSimulator                      →   ISimulator
  .find_best_path(start, targets)   →   GachaResult? FindBestPath(Position start, ISet<string> targets, ...)

SeedAnalyzer                        →   IAnalyzer
  .find_cat(name)                   →   IEnumerable<GachaSlot> FindCat(string name)
  .find_guaranteed_positions(name)  →   IEnumerable<Position> FindGuaranteedPositions(string name)
```

## 行為差異注意事項

### Track 容器

| Python                                  | C#                                                |
| --------------------------------------- | ------------------------------------------------- |
| `Track` 繼承 `UserList`，直接當 list 用 | `Track` 實作 `IReadOnlyList<GachaSlot>`，組合模式 |
| `track[0]` 取第一個 slot                | `track[0]` 同（透過 indexer）                     |
| `track.get_slot(1)` O(1) 查找           | `track.GetSlot(1)` O(1) 查找（內部 Dictionary）   |
| `len(track)`                            | `track.Count`                                     |
| `for slot in track`                     | `foreach (var slot in track)`                     |

### Union 型別處理

Python 的 `Union[str, ParseStatus]` 在 C# 中沒有直接對應。建議方案：

```csharp
// 方案 A：自訂 discriminated union（推薦）
public abstract record SlotValue
{
    public sealed record CatName(string Value) : SlotValue;
    public sealed record Status(ParseStatus Value) : SlotValue;
}

// 方案 B：使用 OneOf 套件
public OneOf<string, ParseStatus> Result { get; init; }

// 方案 C：nullable + enum 旗標（最簡but 最弱）
public string? Result { get; init; }
public ParseStatus? ResultStatus { get; init; }
```

### Heuristic 函數

Python 版使用 `bisect.bisect_left` 做二分搜尋。C# 對應：

```text
bisect.bisect_left(positions, current_pos)
→ List<int>.BinarySearch(currentPos)  // 回傳值含補碼，需處理
→ 或 Array.BinarySearch(positions, currentPos)
```

注意 `BinarySearch` 找不到時回傳負值（`~index`），需手動轉換。

## 測試對應

| Python 測試檔                | C# 測試類別                      | 層級           |
| ---------------------------- | -------------------------------- | -------------- |
| `test_models.py`             | `Core.Tests/Models/`             | Core           |
| `test_enhanced_models.py`    | `Core.Tests/Models/`             | Core           |
| `test_fetcher.py`            | `Infrastructure.Tests/Fetching/` | Infrastructure |
| `test_fetcher_errors.py`     | `Infrastructure.Tests/Fetching/` | Infrastructure |
| `test_parser.py`             | `Infrastructure.Tests/Parsing/`  | Infrastructure |
| `test_parser_edge.py`        | `Infrastructure.Tests/Parsing/`  | Infrastructure |
| `test_simulator.py`          | `Application.Tests/Simulation/`  | Application    |
| `test_simulator_advanced.py` | `Application.Tests/Simulation/`  | Application    |
| `test_multi_pool.py`         | `Application.Tests/Simulation/`  | Application    |
| `test_analyzer.py`           | `Application.Tests/Analysis/`    | Application    |
| `test_snapshot.py`           | `Infrastructure.Tests/Parsing/`  | Infrastructure |

Fixture 檔案（`tests/fixtures/seed_*.html`）可與 Python 版共用，透過相對路徑或複製至 .NET 測試目錄。
