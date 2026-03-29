using BattleCatCalc.Core.Exceptions;
using BattleCatCalc.Core.Models;
using BattleCatCalc.Infrastructure.Parsing;

namespace BattleCatCalc.Infrastructure.Tests.Parsing;

public class BCParserTests
{
    private readonly BCParser _parser = new();

    [Fact]
    public void Parse_NoTable_ThrowsParseException()
    {
        /// 動機：缺少 <table> 時應明確報錯，不應回傳空結果。
        /// 方法：傳入無表格的 HTML。
        /// 驗證：拋出 ParseException。
        Assert.Throws<ParseException>(() =>
            _parser.Parse("<html><body>No Table</body></html>"));
    }

    [Fact]
    public void Parse_MinimalValidHtml_ReturnsTracks()
    {
        /// 動機：最小可行 HTML 應可正確解析。
        /// 方法：建構包含單一列、Track A/B 各一格的 HTML。
        /// 驗證：兩條軌道各有 1 個 slot，ID 正確。
        var html = """
            <html><body><table>
            <tr>
              <td id="N1A"><a href="#">CatA</a></td>
              <td><a href="#">GuaA</a></td>
              <td><a href="#">GuaB</a></td>
              <td id="N1B"><a href="#">CatB</a></td>
            </tr>
            </table></body></html>
            """;

        var (trackA, trackB) = _parser.Parse(html);

        Assert.Single(trackA);
        Assert.Single(trackB);
        Assert.Equal(new Position(1, "A"), trackA[0].Id);
        Assert.Equal(new Position(1, "B"), trackB[0].Id);
    }

    [Fact]
    public void Parse_ExtractsCatNames()
    {
        /// 動機：確認 <a> 標籤中的貓咪名稱被正確擷取。
        /// 方法：建構符合真實格式的 HTML（id td 含序號連結，result/guaranteed 各一欄）。
        /// 驗證：result 與 guaranteed 對應正確的名稱。
        var html = """
            <html><body><table>
            <tr>
              <td id="N1A"><a href="#">1A</a></td>
              <td><a href="#">Jizo</a></td>
              <td><a href="#">Gao</a></td>
              <td><a href="#">Mitama</a></td>
              <td id="N1B"><a href="#">1B</a></td>
            </tr>
            </table></body></html>
            """;

        var (trackA, trackB) = _parser.Parse(html);

        Assert.Equal("Jizo", trackA[0].Result.AsCatName());
        Assert.Equal("Gao", trackA[0].Guaranteed.AsCatName());
    }

    [Fact]
    public void Parse_SequentialIds_TwoRows()
    {
        /// 動機：多列 HTML 應解析出連續 ID。
        /// 方法：建構 2 列的 HTML。
        /// 驗證：Track A/B 各有 2 個 slot，ID 為 1、2。
        var html = """
            <html><body><table>
            <tr>
              <td id="N1A"><a href="#">Cat1</a></td>
              <td><a href="#">G1</a></td>
              <td><a href="#">G1B</a></td>
              <td id="N1B"><a href="#">CatB1</a></td>
            </tr>
            <tr>
              <td id="N2A"><a href="#">Cat2</a></td>
              <td><a href="#">G2</a></td>
              <td><a href="#">G2B</a></td>
              <td id="N2B"><a href="#">CatB2</a></td>
            </tr>
            </table></body></html>
            """;

        var (trackA, trackB) = _parser.Parse(html);

        Assert.Equal(2, trackA.Count);
        Assert.Equal(2, trackB.Count);
        Assert.Equal(1, trackA[0].Id.Id);
        Assert.Equal(2, trackA[1].Id.Id);
        Assert.Equal(1, trackB[0].Id.Id);
        Assert.Equal(2, trackB[1].Id.Id);
    }
}
