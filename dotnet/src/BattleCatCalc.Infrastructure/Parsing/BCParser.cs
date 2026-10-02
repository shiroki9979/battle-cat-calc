using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using AngleSharp.Dom;
using BattleCatCalc.Core.Enums;
using BattleCatCalc.Core.Exceptions;
using BattleCatCalc.Core.Interfaces;
using BattleCatCalc.Core.Models;

namespace BattleCatCalc.Infrastructure.Parsing;

/// <summary>
/// Parses bc.godfat.org HTML into structured Track A / Track B data using AngleSharp.
/// </summary>
public sealed partial class BCParser : IParser
{
    // 編譯期產生的正規表達式，對應 Python 版的 regex 模式
    [GeneratedRegex(@"^N(\d+)(A)$")]
    private static partial Regex TrackAPattern();

    [GeneratedRegex(@"^N(\d+)(B)$")]
    private static partial Regex TrackBPattern();

    [GeneratedRegex(@"(?:->|<-)\s*(\d+)([AB])")]
    private static partial Regex NextPosPattern();

    [GeneratedRegex(@"^\d+[AB]$")]
    private static partial Regex PositionLinkPattern();

    public (Track TrackA, Track TrackB) Parse(string html)
    {
        var parser = new HtmlParser();
        var document = parser.ParseDocument(html);

        var table = document.QuerySelector("table")
            ?? throw new ParseException("Cannot parse HTML: gacha table not found", html: TruncateHtml(html), selector: "table");

        var rows = table.QuerySelectorAll("tr").ToList();

        // 重複稀有格會讓相鄰儲存格以 rowspan 互相重疊，同一列的第 N 個 <td> 不一定是第 N 欄，
        // 必須先還原網格才能正確歸屬儲存格。
        var grid = CellGrid.Build(rows);

        var trackA = ParseTrack(rows, grid, "A");
        var trackB = ParseTrack(rows, grid, "B");

        ValidateTrack(trackA);
        ValidateTrack(trackB);

        var catRarities = ReadCatRarities(document);
        return (ApplyCatRarities(trackA, catRarities), ApplyCatRarities(trackB, catRarities));
    }

    private static IReadOnlyDictionary<string, CatRarity> ReadCatRarities(IDocument document)
    {
        var rarities = new Dictionary<string, CatRarity>(StringComparer.Ordinal);
        foreach (var group in document.QuerySelectorAll("#find_select optgroup"))
        {
            CatRarity? rarity = group.GetAttribute("label")?.Trim().TrimEnd(':') switch
            {
                "Rare" => CatRarity.Rare,
                "Super" => CatRarity.SuperRare,
                "Uber" => CatRarity.UberRare,
                "Legendary" => CatRarity.Legend,
                _ => null
            };
            if (rarity is null) continue;

            foreach (var option in group.QuerySelectorAll("option"))
                rarities[option.TextContent.Trim()] = rarity.Value;
        }
        return rarities;
    }

    private static Track ApplyCatRarities(Track track, IReadOnlyDictionary<string, CatRarity> rarities) =>
        new(track.Name, track.Select(slot => slot with
        {
            // Guaranteed cells inherit roll colors; the cat catalog identifies the cat's rarity.
            GuaranteedRarity = rarities.TryGetValue(slot.Guaranteed.ToString(), out var rarity) ? rarity : null
        }));

    private Track ParseTrack(List<IElement> rows, CellGrid grid, string trackId)
    {
        var pattern = trackId == "A" ? TrackAPattern() : TrackBPattern();
        var slots = new List<GachaSlot>();
        var rowIndex = 0;

        while (rowIndex < rows.Count)
        {
            var tds = rows[rowIndex].QuerySelectorAll("td").ToList();
            if (tds.Count == 0)
            {
                rowIndex++;
                continue;
            }

            // Track A 從第一個 td 取 id；Track B 從最後一個 td 取 id
            var idTd = trackId == "A" ? tds[0] : tds[^1];
            var htmlId = idTd.GetAttribute("id") ?? "";
            var match = pattern.Match(htmlId);

            if (match.Success)
            {
                var slot = ParseSlot(grid, rowIndex, idTd, trackId);
                slots.Add(slot);

                var rowspan = int.TryParse(idTd.GetAttribute("rowspan"), out var rs) ? rs : 1;
                rowIndex += rowspan;
            }
            else
            {
                rowIndex++;
            }
        }

        return new Track(trackId, slots);
    }

    private GachaSlot ParseSlot(CellGrid grid, int startIdx, IElement idTd, string track)
    {
        var pattern = track == "A" ? TrackAPattern() : TrackBPattern();
        var match = pattern.Match(idTd.GetAttribute("id") ?? "");
        var slotId = int.Parse(match.Groups[1].Value);
        var rowspan = CellGrid.ParseSpan(idTd.GetAttribute("rowspan"));

        // A 軌的結果/必中欄緊接在 id 右側；B 軌（Alt. 欄）則在 id 左側。
        var idCol = grid.ColumnOf(idTd);
        var (resultCol, guaranteedCol) = track == "A"
            ? (idCol + 1, idCol + 2)
            : (idCol - 2, idCol - 1);

        var allResults = new List<string>();
        var allGuaranteed = new List<string>();
        var isDupFlag = false;
        Position? resultPos = null;
        // 依列順序：重複稀有格的第一個是替換前、最後一個是替換後
        var guaranteedArrows = new List<Position>();
        CatRarity? rarity = null;

        for (var i = 0; i < rowspan; i++)
        {
            var rowIdx = startIdx + i;
            var colData = ExtractColsData(grid.CellAt(rowIdx, resultCol), grid.CellAt(rowIdx, guaranteedCol));

            if (colData.OriginalResult is not null) allResults.Add(colData.OriginalResult);
            if (colData.Result is not null) allResults.Add(colData.Result);

            if (colData.OriginalGuaranteed is not null) allGuaranteed.Add(colData.OriginalGuaranteed);
            if (colData.Guaranteed is not null) allGuaranteed.Add(colData.Guaranteed);

            if (colData.IsDuplicate) isDupFlag = true;
            if (colData.ResultPos is not null) resultPos = colData.ResultPos;
            if (colData.GuaranteedPos is Position gp) guaranteedArrows.Add(gp);
            rarity ??= colData.Rarity;
        }

        var (resultValue, originalResult, isDuplicate) = FinalizeResults(allResults, isDupFlag);
        var (guaranteedValue, originalGuaranteed) = FinalizeGuaranteed(allGuaranteed);

        return new GachaSlot(
            Id: new Position(slotId, track),
            Result: resultValue,
            Guaranteed: guaranteedValue,
            NextPos: guaranteedArrows.Count > 0 ? guaranteedArrows[^1] : null,
            OriginalResult: originalResult,
            OriginalGuaranteed: originalGuaranteed,
            IsDuplicateRare: isDuplicate,
            Rarity: rarity,
            SingleNextPos: resultPos,
            OriginalNextPos: guaranteedArrows.Count > 1 ? guaranteedArrows[0] : null);
    }

    /// <summary>
    /// 單列內「結果欄 + 必中欄」解析出的原始資料。
    /// 結果欄的箭頭是重複稀有時單抽的落點，必中欄的箭頭是必中十一連的落點，兩者必須分開，不能互相覆蓋。
    /// </summary>
    private record ColsData(
        string? Result,
        string? Guaranteed,
        Position? ResultPos,
        Position? GuaranteedPos,
        string? OriginalResult,
        bool IsDuplicate,
        string? OriginalGuaranteed,
        CatRarity? Rarity);

    /// <param name="resultCol">結果欄；被 rowspan 蓋住而不存在時為 null。</param>
    /// <param name="guaranteedCol">必中欄；被 rowspan 蓋住而不存在時為 null。</param>
    private ColsData ExtractColsData(IElement? resultCol, IElement? guaranteedCol)
    {
        string? resText = null;
        string? guaText = null;
        Position? resultPos = null;
        Position? guaranteedPos = null;
        string? origRes = null;
        var isDup = false;
        string? origGua = null;
        CatRarity? rarity = null;

        if (resultCol is not null)
        {
            var validNames = FilterCatNames(resultCol.QuerySelectorAll("a").ToList());

            if (validNames.Count > 0)
            {
                resText = validNames[^1];
                if (validNames.Count > 1)
                {
                    origRes = validNames[0];
                    isDup = true;
                }
            }

            resultPos = FindArrow(resultCol);
            rarity = ParseRarityFromClass(resultCol);
        }

        if (guaranteedCol is not null)
        {
            var guaNames = FilterCatNames(guaranteedCol.QuerySelectorAll("a").ToList());

            if (guaNames.Count > 0)
            {
                guaText = guaNames[^1];
                if (guaNames.Count > 1)
                {
                    origGua = guaNames[0];
                }
            }

            guaranteedPos = FindArrow(guaranteedCol);
        }

        return new ColsData(resText, guaText, resultPos, guaranteedPos, origRes, isDup, origGua, rarity);
    }

    /// <summary>讀取儲存格內的位置箭頭（如 "-&gt; 21B"、"&lt;- 12A"）。</summary>
    private static Position? FindArrow(IElement cell)
    {
        var posMatch = NextPosPattern().Match(cell.TextContent.Trim());
        return posMatch.Success
            ? new Position(int.Parse(posMatch.Groups[1].Value), posMatch.Groups[2].Value)
            : null;
    }

    /// <summary>
    /// 還原表格的 (列, 欄) 網格，處理 rowspan / colspan 造成的欄位位移。
    /// </summary>
    private sealed class CellGrid
    {
        private readonly Dictionary<(int Row, int Col), IElement> _cells = [];
        private readonly Dictionary<IElement, int> _columns = new(ReferenceEqualityComparer.Instance);

        public static CellGrid Build(IReadOnlyList<IElement> rows)
        {
            var grid = new CellGrid();
            var occupied = new HashSet<(int, int)>();

            for (var rowIdx = 0; rowIdx < rows.Count; rowIdx++)
            {
                var col = 0;
                foreach (var cell in rows[rowIdx].QuerySelectorAll("td, th"))
                {
                    while (occupied.Contains((rowIdx, col))) col++;

                    var rowspan = ParseSpan(cell.GetAttribute("rowspan"));
                    var colspan = ParseSpan(cell.GetAttribute("colspan"));
                    for (var dr = 0; dr < rowspan; dr++)
                        for (var dc = 0; dc < colspan; dc++)
                            occupied.Add((rowIdx + dr, col + dc));

                    grid._cells[(rowIdx, col)] = cell;
                    grid._columns[cell] = col;
                    col += colspan;
                }
            }

            return grid;
        }

        public static int ParseSpan(string? value) =>
            int.TryParse(value, out var span) && span > 0 ? span : 1;

        /// <summary>以起始位置查詢儲存格；該位置被其他儲存格的 rowspan 蓋住時回傳 null。</summary>
        public IElement? CellAt(int row, int col) => _cells.GetValueOrDefault((row, col));

        public int ColumnOf(IElement cell) => _columns[cell];
    }

    private static List<string> FilterCatNames(List<IElement> aTags)
    {
        var names = new List<string>();
        foreach (var a in aTags)
        {
            var text = a.TextContent.Trim();
            if (PositionLinkPattern().IsMatch(text)) continue;
            if (a.QuerySelector("img") is not null) continue;
            if (text is "" or "🐾") continue;
            names.Add(text);
        }
        return names;
    }

    /// <summary>
    /// 從 td 的 CSS class 解析貓咪稀有度。
    /// HTML 格式：class="cat pick {rarity}" 或 class="cat pick {rarity} next_position"
    /// </summary>
    private static CatRarity? ParseRarityFromClass(IElement td)
    {
        var classList = td.ClassList;
        if (!classList.Contains("cat")) return null;

        if (classList.Contains("legend")) return CatRarity.Legend;
        if (classList.Contains("exclusive")) return CatRarity.Exclusive;
        if (classList.Contains("uber_fest")) return CatRarity.UberRareFest;
        if (classList.Contains("uber")) return CatRarity.UberRare;
        if (classList.Contains("supa_fest")) return CatRarity.SuperRareFest;
        if (classList.Contains("supa")) return CatRarity.SuperRare;
        if (classList.Contains("rare")) return CatRarity.Rare;

        return null;
    }

    private static (SlotValue Value, string? Original, bool IsDuplicate) FinalizeResults(
        List<string> allResults, bool isDupFlag)
    {
        if (allResults.Count == 0)
            return (new SlotValue.CatName(""), null, isDupFlag);

        if (allResults.Count > 1)
            return (new SlotValue.CatName(allResults[^1]), allResults[0], true);

        return (new SlotValue.CatName(allResults[0]), null, isDupFlag);
    }

    private static (SlotValue Value, string? Original) FinalizeGuaranteed(List<string> allGua)
    {
        if (allGua.Count == 0)
            return (new SlotValue.CatName(""), null);

        if (allGua.Count > 1)
            return (new SlotValue.CatName(allGua[^1]), allGua[0]);

        return (new SlotValue.CatName(allGua[0]), null);
    }

    private static void ValidateTrack(Track track)
    {
        if (track.Count == 0)
            throw new ParseException($"Track {track.Name} is empty after parsing");

        var expectedId = track[0].Id.Id;
        foreach (var slot in track)
        {
            if (slot.Id.Id != expectedId)
                throw new ParseException(
                    $"Track {track.Name} has non-sequential IDs: expected {expectedId}, got {slot.Id.Id}");
            expectedId++;
        }
    }

    private static string TruncateHtml(string html, int maxLength = 500) =>
        html.Length <= maxLength ? html : html[..maxLength] + "...";

    public IReadOnlyList<EventInfo> ParseEventList(string html)
    {
        var parser = new HtmlParser();
        var document = parser.ParseDocument(html);

        var select = document.QuerySelector("#event_select")
            ?? throw new ParseException("Cannot parse event list: #event_select not found");

        var events = new List<EventInfo>();
        var currentGroup = "Unknown";

        foreach (var child in select.Children)
        {
            if (child.TagName.Equals("OPTGROUP", StringComparison.OrdinalIgnoreCase))
            {
                // optgroup label 格式："Upcoming:"、"Custom:"、"Past:"
                currentGroup = (child.GetAttribute("label") ?? "Unknown").TrimEnd(':');

                foreach (var option in child.QuerySelectorAll("option"))
                {
                    var value = option.GetAttribute("value")?.Trim();
                    var label = option.TextContent.Trim();
                    if (!string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(label))
                    {
                        events.Add(new EventInfo(value, label, currentGroup));
                    }
                }
            }
            else if (child.TagName.Equals("OPTION", StringComparison.OrdinalIgnoreCase))
            {
                // 不在 optgroup 內的散落 option
                var value = child.GetAttribute("value")?.Trim();
                var label = child.TextContent.Trim();
                if (!string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(label))
                {
                    events.Add(new EventInfo(value, label, currentGroup));
                }
            }
        }

        return events;
    }
}
