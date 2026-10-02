using BattleCatCalc.Core.Exceptions;
using BattleCatCalc.Core.Models;
using BattleCatCalc.Infrastructure.Parsing;

namespace BattleCatCalc.Infrastructure.Tests.Parsing;

/// <summary>
/// Parser 防禦性測試：驗證面對異常 HTML 結構時的行為。
/// </summary>
public class BCParserEdgeCaseTests
{
    private readonly BCParser _parser = new();

    #region 空/殘缺 HTML

    [Fact]
    public void Parse_EmptyHtml_ThrowsParseException()
    {
        /// 動機：空字串不含 <table>，應明確報錯。
        Assert.Throws<ParseException>(() => _parser.Parse(""));
    }

    [Fact]
    public void Parse_TableWithNoRows_ThrowsParseException()
    {
        /// 動機：表格存在但無資料列時，Track 為空，ValidateTrack 應拋出。
        var html = "<html><body><table></table></body></html>";

        Assert.Throws<ParseException>(() => _parser.Parse(html));
    }

    [Fact]
    public void Parse_RowsWithNoTd_ThrowsParseException()
    {
        /// 動機：有 <tr> 但無 <td>，解析出空 Track。
        var html = "<html><body><table><tr></tr><tr></tr></table></body></html>";

        Assert.Throws<ParseException>(() => _parser.Parse(html));
    }

    #endregion

    #region 欄位數異常

    [Fact]
    public void Parse_SingleColumnRow_NoSlotExtracted()
    {
        /// 動機：列只有一個 <td> 且不含有效 id，不應產生 slot。
        var html = """
            <html><body><table>
            <tr><td>garbage</td></tr>
            </table></body></html>
            """;

        Assert.Throws<ParseException>(() => _parser.Parse(html));
    }

    #endregion

    #region 非連續 ID

    [Fact]
    public void Parse_GapInIds_ThrowsParseException()
    {
        /// 動機：ID 不連續（如 1, 3 缺 2）時 ValidateTrack 應明確拋出。
        var html = """
            <html><body><table>
            <tr>
              <td id="N1A"></td>
              <td><a href="#">Cat1</a></td>
              <td><a href="#">Gua1</a></td>
              <td><a href="#">CatB1</a></td>
              <td><a href="#">GuaB1</a></td>
              <td id="N1B"></td>
            </tr>
            <tr>
              <td id="N3A"></td>
              <td><a href="#">Cat3</a></td>
              <td><a href="#">Gua3</a></td>
              <td><a href="#">CatB3</a></td>
              <td><a href="#">GuaB3</a></td>
              <td id="N3B"></td>
            </tr>
            </table></body></html>
            """;

        var ex = Assert.Throws<ParseException>(() => _parser.Parse(html));
        Assert.Contains("non-sequential", ex.Message);
    }

    #endregion

    #region 多個 <table>

    [Fact]
    public void Parse_MultipleTables_UsesFirstTable()
    {
        /// 動機：HTML 含多個 <table> 時，Parser 應取第一個。
        var html = """
            <html><body>
            <table>
            <tr>
              <td id="N1A"></td>
              <td><a href="#">Cat1</a></td>
              <td><a href="#">Gua1</a></td>
              <td><a href="#">CatB1</a></td>
              <td><a href="#">GuaB1</a></td>
              <td id="N1B"></td>
            </tr>
            </table>
            <table>
            <tr>
              <td id="N1A"></td>
              <td><a href="#">WrongCat</a></td>
              <td><a href="#">WrongGua</a></td>
              <td><a href="#">WrongCatB</a></td>
              <td><a href="#">WrongGuaB</a></td>
              <td id="N1B"></td>
            </tr>
            </table>
            </body></html>
            """;

        var (trackA, _) = _parser.Parse(html);

        Assert.Equal("Cat1", trackA[0].Result.AsCatName());
    }

    #endregion

    #region 特殊字元處理

    [Fact]
    public void Parse_CatNameWithHtmlEntities_DecodesCorrectly()
    {
        /// 動機：HTML 實體（如 &amp;）應被正確解碼為純文字。
        var html = """
            <html><body><table>
            <tr>
              <td id="N1A"></td>
              <td><a href="#">Cat &amp; Dog</a></td>
              <td><a href="#">Gua &lt;1&gt;</a></td>
              <td><a href="#">CatB1</a></td>
              <td><a href="#">GuaB1</a></td>
              <td id="N1B"></td>
            </tr>
            </table></body></html>
            """;

        var (trackA, _) = _parser.Parse(html);

        Assert.Equal("Cat & Dog", trackA[0].Result.AsCatName());
        Assert.Equal("Gua <1>", trackA[0].Guaranteed.AsCatName());
    }

    [Fact]
    public void Parse_PawEmoji_FilteredOut()
    {
        /// 動機：🐾 符號應被 FilterCatNames 過濾，不視為貓咪名稱。
        var html = """
            <html><body><table>
            <tr>
              <td id="N1A"></td>
              <td><a href="#">🐾</a><a href="#">RealCat</a></td>
              <td><a href="#">Gua1</a></td>
              <td><a href="#">CatB1</a></td>
              <td><a href="#">GuaB1</a></td>
              <td id="N1B"></td>
            </tr>
            </table></body></html>
            """;

        var (trackA, _) = _parser.Parse(html);

        Assert.Equal("RealCat", trackA[0].Result.AsCatName());
    }

    [Fact]
    public void Parse_PositionLinks_FilteredFromCatNames()
    {
        /// 動機：<a> 本文為位置格式（如 "5A"、"12B"）不應被視為貓咪名稱。
        var html = """
            <html><body><table>
            <tr>
              <td id="N1A"></td>
              <td><a href="#">1A</a><a href="#">RealCat</a></td>
              <td><a href="#">Gua1</a></td>
              <td><a href="#">CatB1</a></td>
              <td><a href="#">GuaB1</a></td>
              <td id="N1B"></td>
            </tr>
            </table></body></html>
            """;

        var (trackA, _) = _parser.Parse(html);

        Assert.Equal("RealCat", trackA[0].Result.AsCatName());
    }

    [Fact]
    public void Parse_ImgTag_FilteredFromCatNames()
    {
        /// 動機：含 <img> 的 <a> 標籤（圖示連結）不應作為貓咪名稱。
        var html = """
            <html><body><table>
            <tr>
              <td id="N1A"></td>
              <td><a href="#"><img src="icon.png"/></a><a href="#">RealCat</a></td>
              <td><a href="#">Gua1</a></td>
              <td><a href="#">CatB1</a></td>
              <td><a href="#">GuaB1</a></td>
              <td id="N1B"></td>
            </tr>
            </table></body></html>
            """;

        var (trackA, _) = _parser.Parse(html);

        Assert.Equal("RealCat", trackA[0].Result.AsCatName());
    }

    #endregion

    #region Rowspan 處理

    [Fact]
    public void Parse_Rowspan2_ParsesAsOneSlot()
    {
        /// 動機：rowspan=2 代表該 slot 跨兩列，應只產生一個 GachaSlot。
        var html = """
            <html><body><table>
            <tr>
              <td id="N1A" rowspan="2"></td>
              <td><a href="#">Original</a><a href="#">Replaced</a> ->5B</td>
              <td><a href="#">Gua1</a></td>
              <td><a href="#">CatB1</a></td>
              <td><a href="#">GuaB1</a></td>
              <td id="N1B"></td>
            </tr>
            <tr>
              <td><a href="#">SubRow</a></td>
              <td><a href="#">SubGua</a></td>
              <td><a href="#">SubCatB</a></td>
              <td><a href="#">SubGuaB</a></td>
              <td id="N2B"></td>
            </tr>
            <tr>
              <td id="N2A"></td>
              <td><a href="#">Cat2</a></td>
              <td><a href="#">G2</a></td>
              <td><a href="#">CatB3</a></td>
              <td><a href="#">GuaB3</a></td>
              <td id="N3B"></td>
            </tr>
            </table></body></html>
            """;

        var (trackA, _) = _parser.Parse(html);

        // rowspan=2 的 slot 只應產生一個 GachaSlot
        Assert.Equal(2, trackA.Count);
        Assert.Equal(1, trackA[0].Id.Id);
        Assert.Equal(2, trackA[1].Id.Id);
    }

    #endregion

    #region Rowspan 重疊（重複稀有格）

    // 欄位：0=A 編號, 1=A 結果, 2=A 必中, 3=B 結果(Alt.), 4=B 必中(Alt.), 5=B 編號。
    // 仿照真實表格的重疊結構：B1 是重複稀有格（佔第 0~2 列），A1 起點與 B1 的「替換前」同列，
    // A1 的稀有度儲存格跨 2 列，因此第 2 列的兩個 <td> 其實是 B1 的替換結果，不是 A1 的。
    private const string OverlapHtml = """
        <html><body><table>
        <tr><td></td><td></td><td></td><td></td><td></td><td id="N1B" rowspan="3">1B</td></tr>
        <tr><td id="N1A" rowspan="3">1A</td><td rowspan="2"></td><td rowspan="2"></td>
            <td><a href="/u/o">Orig</a></td><td><a href="/u/og">OrigGua</a> &lt;- 11A</td></tr>
        <tr><td><a href="/u/r">Repl</a> &lt;- 3A</td><td><a href="/u/rg">ReplGua</a> -&gt; 12B</td></tr>
        <tr><td><a href="/u/a1">CatA1</a></td><td><a href="/u/ga1">GuaA1</a> -&gt; 11B</td>
            <td></td><td></td><td id="N2B" rowspan="2">2B</td></tr>
        <tr><td id="N2A" rowspan="2">2A</td><td></td><td></td>
            <td><a href="/u/b2">CatB2</a></td><td><a href="/u/gb2">GuaB2</a> -&gt; 13B</td></tr>
        <tr><td><a href="/u/a2">CatA2</a></td><td><a href="/u/ga2">GuaA2</a> -&gt; 12B</td></tr>
        </table></body></html>
        """;

    [Fact]
    public void Parse_OverlappingRowspan_CoveredCellsNotMisattributed()
    {
        /// 動機：A1 的欄位被蓋住的那一列，不應把 B1 的替換結果算成 A1 的。
        ///      舊版以「這一列的前兩個 <td>」取 A 的欄位，會把 Repl 讀成 A1 的原始貓而誤判為重複稀有。
        var (trackA, _) = _parser.Parse(OverlapHtml);
        var slot = trackA.GetSlot(1)!;

        Assert.Equal("CatA1", slot.Result.AsCatName());
        Assert.Equal("GuaA1", slot.Guaranteed.AsCatName());
        Assert.False(slot.IsDuplicateRare);
        Assert.Null(slot.OriginalResult);
        Assert.Equal(new Position(11, "B"), slot.NextPos);
        Assert.Null(slot.SingleNextPos);
    }

    [Fact]
    public void Parse_OverlappingRowspan_DuplicateSlotKeepsBothArrowsApart()
    {
        /// 動機：重複稀有格的結果欄箭頭是單抽落點，必中欄箭頭是必中落點，兩者不可互相覆蓋。
        var (_, trackB) = _parser.Parse(OverlapHtml);
        var slot = trackB.GetSlot(1)!;

        Assert.True(slot.IsDuplicateRare);
        Assert.Equal("Orig", slot.OriginalResult);
        Assert.Equal("Repl", slot.Result.AsCatName());
        Assert.Equal("OrigGua", slot.OriginalGuaranteed);
        Assert.Equal("ReplGua", slot.Guaranteed.AsCatName());
        Assert.Equal(new Position(3, "A"), slot.SingleNextPos);     // 結果欄 "<- 3A"
        Assert.Equal(new Position(12, "B"), slot.NextPos);          // 替換後的必中欄 "-> 12B"
        Assert.Equal(new Position(11, "A"), slot.OriginalNextPos);  // 替換前的必中欄 "<- 11A"
    }

    [Fact]
    public void Parse_OverlappingRowspan_SlotsAfterOverlapAreUnaffected()
    {
        /// 動機：重疊之後的格子仍正確歸屬。
        var (trackA, trackB) = _parser.Parse(OverlapHtml);
        var a2 = trackA.GetSlot(2)!;
        var b2 = trackB.GetSlot(2)!;

        Assert.Equal(("CatA2", "GuaA2", new Position(12, "B")),
            (a2.Result.AsCatName(), a2.Guaranteed.AsCatName(), a2.NextPos));
        Assert.Equal(("CatB2", "GuaB2", new Position(13, "B")),
            (b2.Result.AsCatName(), b2.Guaranteed.AsCatName(), b2.NextPos));
        Assert.False(a2.IsDuplicateRare);
        Assert.False(b2.IsDuplicateRare);
    }

    #endregion

    #region ParseException 附帶上下文

    [Fact]
    public void Parse_NoTable_ExceptionContainsHtmlFragment()
    {
        /// 動機：ParseException 應攜帶 HTML 片段以便偵錯。
        var html = "<html><body>No Table Here</body></html>";

        var ex = Assert.Throws<ParseException>(() => _parser.Parse(html));

        Assert.NotNull(ex.Html);
        Assert.Contains("No Table Here", ex.Html);
        Assert.Equal("table", ex.Selector);
    }

    [Fact]
    public void Parse_LargeHtml_TruncatesInException()
    {
        /// 動機：過長的 HTML 不應完整放入例外訊息，避免記憶體浪費。
        var longHtml = "<html><body>" + new string('x', 1000) + "</body></html>";

        var ex = Assert.Throws<ParseException>(() => _parser.Parse(longHtml));

        Assert.NotNull(ex.Html);
        Assert.True(ex.Html.Length <= 510); // 500 + "..." 容許
    }

    #endregion
}
