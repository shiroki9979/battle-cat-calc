using BattleCatCalc.Core.Exceptions;
using BattleCatCalc.Infrastructure.Parsing;

namespace BattleCatCalc.Infrastructure.Tests.Parsing;

public class BCParserEventListTests
{
    private readonly BCParser _parser = new();

    [Fact]
    public void ParseEventList_NoSelect_ThrowsParseException()
    {
        /// 動機：缺少 #event_select 時應明確報錯。
        /// 方法：傳入無 select 的 HTML。
        /// 驗證：拋出 ParseException。
        Assert.Throws<ParseException>(() =>
            _parser.ParseEventList("<html><body>No Select</body></html>"));
    }

    [Fact]
    public void ParseEventList_WithOptgroups_ReturnsGroupedEvents()
    {
        /// 動機：含 optgroup 的標準結構應正確解析群組名稱與選項。
        /// 方法：建構含 2 個 optgroup（Upcoming、Past）的 HTML。
        /// 驗證：回傳 3 筆 EventInfo，群組名稱正確。
        var html = """
            <html><body>
            <select id="event_select">
              <optgroup label="Upcoming:">
                <option value="2026-03-30_943">2026-03-30 ~ 2026-04-01: Event A</option>
                <option value="2026-04-01_944">2026-04-01 ~ 2026-04-03: Event B</option>
              </optgroup>
              <optgroup label="Past:">
                <option value="2025-01-01_100">2025-01-01 ~ 2025-01-03: Old Event</option>
              </optgroup>
            </select>
            </body></html>
            """;

        var events = _parser.ParseEventList(html);

        Assert.Equal(3, events.Count);
        Assert.Equal("Upcoming", events[0].Group);
        Assert.Equal("Upcoming", events[1].Group);
        Assert.Equal("Past", events[2].Group);
    }

    [Fact]
    public void ParseEventList_EventValues_Correct()
    {
        /// 動機：確認 option value 與 label 被正確擷取。
        /// 方法：建構單一 optgroup、單一 option 的 HTML。
        /// 驗證：EventInfo 的 Value 與 Label 符合原始 HTML。
        var html = """
            <html><body>
            <select id="event_select">
              <optgroup label="Custom:">
                <option value="2026-05-01_999">2026-05-01 ~ 2026-05-03: Custom Pool</option>
              </optgroup>
            </select>
            </body></html>
            """;

        var events = _parser.ParseEventList(html);

        Assert.Single(events);
        Assert.Equal("2026-05-01_999", events[0].Value);
        Assert.Equal("2026-05-01 ~ 2026-05-03: Custom Pool", events[0].Label);
        Assert.Equal("Custom", events[0].Group);
    }

    [Fact]
    public void ParseEventList_OptgroupLabelTrimmed()
    {
        /// 動機：bc.godfat.org 的 optgroup label 可能帶冒號（如 "Upcoming:"），應去除。
        /// 方法：建構帶冒號結尾的 label。
        /// 驗證：群組名稱不含尾部冒號。
        var html = """
            <html><body>
            <select id="event_select">
              <optgroup label="Upcoming:">
                <option value="v1">Event</option>
              </optgroup>
            </select>
            </body></html>
            """;

        var events = _parser.ParseEventList(html);

        Assert.Equal("Upcoming", events[0].Group);
    }

    [Fact]
    public void ParseEventList_DirectOptions_UsesUnknownGroup()
    {
        /// 動機：不在 optgroup 內的 option 應歸類為 "Unknown"。
        /// 方法：建構不含 optgroup 的 select。
        /// 驗證：EventInfo 的 Group 為 "Unknown"。
        var html = """
            <html><body>
            <select id="event_select">
              <option value="v1">Standalone Event</option>
            </select>
            </body></html>
            """;

        var events = _parser.ParseEventList(html);

        Assert.Single(events);
        Assert.Equal("Unknown", events[0].Group);
    }

    [Fact]
    public void ParseEventList_EmptyValueSkipped()
    {
        /// 動機：空 value 的 option 應被過濾（如 placeholder）。
        /// 方法：建構含空 value option 的 HTML。
        /// 驗證：回傳結果不含空 value 的項目。
        var html = """
            <html><body>
            <select id="event_select">
              <optgroup label="Test:">
                <option value="">-- Select --</option>
                <option value="v1">Valid Event</option>
              </optgroup>
            </select>
            </body></html>
            """;

        var events = _parser.ParseEventList(html);

        Assert.Single(events);
        Assert.Equal("v1", events[0].Value);
    }

    [Fact]
    public void ParseEventList_EmptySelect_ReturnsEmpty()
    {
        /// 動機：空的 select 應回傳空清單，不拋例外。
        /// 方法：建構無 option 的 select。
        /// 驗證：回傳空清單。
        var html = """
            <html><body>
            <select id="event_select"></select>
            </body></html>
            """;

        var events = _parser.ParseEventList(html);

        Assert.Empty(events);
    }
}
