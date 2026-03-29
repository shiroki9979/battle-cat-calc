using System.Net;
using BattleCatCalc.Core.Exceptions;
using BattleCatCalc.Core.Models;

namespace BattleCatCalc.Core.Tests.Exceptions;

/// <summary>
/// 各例外型別的建構子與屬性驗證測試。
/// </summary>
public class ExceptionPropertyTests
{
    #region BattleCatCalcException

    [Fact]
    public void BattleCatCalcException_MessageOnly()
    {
        var ex = new BattleCatCalcException("test error");

        Assert.Equal("test error", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void BattleCatCalcException_WithInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new BattleCatCalcException("outer", inner);

        Assert.Equal("outer", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void BattleCatCalcException_IsException()
    {
        /// 動機：驗證繼承鏈正確。
        var ex = new BattleCatCalcException("test");

        Assert.IsAssignableFrom<Exception>(ex);
    }

    #endregion

    #region ParseException

    [Fact]
    public void ParseException_Defaults_HtmlAndSelectorNull()
    {
        var ex = new ParseException("parse failed");

        Assert.Equal("parse failed", ex.Message);
        Assert.Null(ex.Html);
        Assert.Null(ex.Selector);
    }

    [Fact]
    public void ParseException_WithHtmlAndSelector()
    {
        /// 動機：ParseException 應保存 HTML 片段與 CSS 選擇器以利除錯。
        var ex = new ParseException("no table", html: "<div>bad</div>", selector: "table#result");

        Assert.Equal("<div>bad</div>", ex.Html);
        Assert.Equal("table#result", ex.Selector);
    }

    [Fact]
    public void ParseException_WithInnerException()
    {
        var inner = new FormatException("bad format");
        var ex = new ParseException("parse failed", inner, html: "<p>", selector: ".col");

        Assert.Same(inner, ex.InnerException);
        Assert.Equal("<p>", ex.Html);
        Assert.Equal(".col", ex.Selector);
    }

    [Fact]
    public void ParseException_InheritsFromBattleCatCalcException()
    {
        var ex = new ParseException("test");

        Assert.IsAssignableFrom<BattleCatCalcException>(ex);
    }

    #endregion

    #region FetchException

    [Fact]
    public void FetchException_StoresSeedAndEventId()
    {
        var ex = new FetchException("timeout", "12345", "1019");

        Assert.Equal("12345", ex.Seed);
        Assert.Equal("1019", ex.EventId);
        Assert.Null(ex.StatusCode);
    }

    [Fact]
    public void FetchException_WithStatusCode()
    {
        /// 動機：HTTP 錯誤碼應被保存以供上層回傳適當狀態碼。
        var ex = new FetchException("bad gateway", "seed", "event",
            HttpStatusCode.BadGateway);

        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
    }

    [Fact]
    public void FetchException_WithInnerException()
    {
        var inner = new HttpRequestException("connection refused");
        var ex = new FetchException("fetch failed", inner, "seed", "event",
            HttpStatusCode.ServiceUnavailable);

        Assert.Same(inner, ex.InnerException);
        Assert.Equal("seed", ex.Seed);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
    }

    [Fact]
    public void FetchException_InheritsFromBattleCatCalcException()
    {
        var ex = new FetchException("test", "s", "e");

        Assert.IsAssignableFrom<BattleCatCalcException>(ex);
    }

    #endregion

    #region SimulationException

    [Fact]
    public void SimulationException_StoresStartPosAndMaxRolls()
    {
        /// 動機：SimulationException 應攜帶搜尋起點與上限以利除錯。
        var pos = new Position(5, "B");
        var ex = new SimulationException("stuck", pos, 500);

        Assert.Equal(pos, ex.StartPos);
        Assert.Equal(500, ex.MaxRolls);
        Assert.Equal("stuck", ex.Message);
    }

    [Fact]
    public void SimulationException_WithInnerException()
    {
        var inner = new OverflowException("too many states");
        var pos = new Position(1, "A");
        var ex = new SimulationException("overflow", inner, pos, 1000);

        Assert.Same(inner, ex.InnerException);
        Assert.Equal(pos, ex.StartPos);
        Assert.Equal(1000, ex.MaxRolls);
    }

    [Fact]
    public void SimulationException_InheritsFromBattleCatCalcException()
    {
        var ex = new SimulationException("test", new Position(1, "A"), 10);

        Assert.IsAssignableFrom<BattleCatCalcException>(ex);
    }

    #endregion
}
