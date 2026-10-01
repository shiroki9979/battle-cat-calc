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

        var trackA = ParseTrack(rows, "A");
        var trackB = ParseTrack(rows, "B");

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

    private Track ParseTrack(List<IElement> rows, string trackId)
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
                var slot = ParseSlot(rows, rowIndex, idTd, trackId);
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

    private GachaSlot ParseSlot(List<IElement> allRows, int startIdx, IElement idTd, string track)
    {
        var pattern = track == "A" ? TrackAPattern() : TrackBPattern();
        var match = pattern.Match(idTd.GetAttribute("id") ?? "");
        var slotId = int.Parse(match.Groups[1].Value);
        var rowspan = int.TryParse(idTd.GetAttribute("rowspan"), out var rs) ? rs : 1;

        var allResults = new List<string>();
        var allGuaranteed = new List<string>();
        var isDupFlag = false;
        Position? nextPos = null;
        CatRarity? rarity = null;

        for (var i = 0; i < rowspan; i++)
        {
            var currentRow = allRows[startIdx + i];
            var currentTds = currentRow.QuerySelectorAll("td").ToList();
            var targetCols = GetTargetCols(currentTds, track, isFirstRow: i == 0);

            var colData = ExtractColsData(targetCols);

            if (colData.OriginalResult is not null) allResults.Add(colData.OriginalResult);
            if (colData.Result is not null) allResults.Add(colData.Result);

            if (colData.OriginalGuaranteed is not null) allGuaranteed.Add(colData.OriginalGuaranteed);
            if (colData.Guaranteed is not null) allGuaranteed.Add(colData.Guaranteed);

            if (colData.IsDuplicate) isDupFlag = true;
            if (colData.NextPos is not null) nextPos = colData.NextPos;
            rarity ??= colData.Rarity;
        }

        var (resultValue, originalResult, isDuplicate) = FinalizeResults(allResults, isDupFlag);
        var (guaranteedValue, originalGuaranteed) = FinalizeGuaranteed(allGuaranteed);

        return new GachaSlot(
            Id: new Position(slotId, track),
            Result: resultValue,
            Guaranteed: guaranteedValue,
            NextPos: nextPos,
            OriginalResult: originalResult,
            OriginalGuaranteed: originalGuaranteed,
            IsDuplicateRare: isDuplicate,
            Rarity: rarity);
    }

    /// <summary>
    /// 根據軌道類型與列位置，取得包含結果與保底的欄位。
    /// Track A: 結果和保底在列的前段。
    /// Track B: 結果和保底在列的後段。
    /// </summary>
    private static List<IElement> GetTargetCols(List<IElement> tds, string track, bool isFirstRow)
    {
        if (track == "A")
        {
            var startOffset = isFirstRow ? 1 : 0;
            return tds.Skip(startOffset).Take(2).ToList();
        }
        else
        {
            var endOffset = isFirstRow ? 1 : 0;
            var count = tds.Count;
            var start = count - endOffset - 2;
            if (start < 0) start = 0;
            return tds.Skip(start).Take(2).ToList();
        }
    }

    private record ColsData(
        string? Result,
        string? Guaranteed,
        Position? NextPos,
        string? OriginalResult,
        bool IsDuplicate,
        string? OriginalGuaranteed,
        CatRarity? Rarity);

    private ColsData ExtractColsData(List<IElement> targetCols)
    {
        string? resText = null;
        string? guaText = null;
        Position? nextPos = null;
        string? origRes = null;
        var isDup = false;
        string? origGua = null;
        CatRarity? rarity = null;

        if (targetCols.Count > 0)
        {
            var aTags = targetCols[0].QuerySelectorAll("a").ToList();
            var validNames = FilterCatNames(aTags);

            if (validNames.Count > 0)
            {
                resText = validNames[^1];
                if (validNames.Count > 1)
                {
                    origRes = validNames[0];
                    isDup = true;
                }
            }

            var fullText = targetCols[0].TextContent.Trim();
            var posMatch = NextPosPattern().Match(fullText);
            if (posMatch.Success)
            {
                nextPos = new Position(int.Parse(posMatch.Groups[1].Value), posMatch.Groups[2].Value);
            }

            rarity = ParseRarityFromClass(targetCols[0]);
        }

        if (targetCols.Count > 1)
        {
            var aTags = targetCols[1].QuerySelectorAll("a").ToList();
            var guaNames = FilterCatNames(aTags);

            if (guaNames.Count > 0)
            {
                guaText = guaNames[^1];
                if (guaNames.Count > 1)
                {
                    origGua = guaNames[0];
                }
            }

            var fullText = targetCols[1].TextContent.Trim();
            var posMatch = NextPosPattern().Match(fullText);
            if (posMatch.Success)
            {
                nextPos = new Position(int.Parse(posMatch.Groups[1].Value), posMatch.Groups[2].Value);
            }

        }

        return new ColsData(resText, guaText, nextPos, origRes, isDup, origGua, rarity);
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
