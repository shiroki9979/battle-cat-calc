using System.Text.Json;
using BattleCatCalc.Api.Contracts;
using BattleCatCalc.Core.Models;

namespace BattleCatCalc.Api.Tests;

/// <summary>
/// SlotDto 契約：nextPos 是必中十一連的落點，singleNextPos 是重複稀有被觸發時單抽的落點，兩者不可混用。
/// </summary>
public class SlotDtoTests
{
    [Fact]
    public void FromModel_DuplicateRareSlot_MapsBothLandingsSeparately()
    {
        /// 驗證：B10 的必中落點 21B 與單抽落點 12A 各自對應，不互相覆蓋。
        var slot = new GachaSlot(
            new Position(10, "B"),
            new SlotValue.CatName("侏羅貓"),
            new SlotValue.CatName("EVA初號機"),
            NextPos: new Position(21, "B"),
            OriginalResult: "拳擊手貓",
            IsDuplicateRare: true,
            SingleNextPos: new Position(12, "A"));

        var dto = SlotDto.FromModel(slot);

        Assert.Equal(new PositionDto { Id = 21, Track = "B" }.ToModel(), dto.NextPos!.ToModel());
        Assert.Equal(new Position(12, "A"), dto.SingleNextPos!.ToModel());
        Assert.True(dto.IsDuplicateRare);
    }

    [Fact]
    public void FromModel_OrdinarySlot_HasNoSingleNextPos()
    {
        var slot = new GachaSlot(
            new Position(1, "A"),
            new SlotValue.CatName("貓咪陰陽師"),
            new SlotValue.CatName("第10使徒"),
            NextPos: new Position(11, "B"));

        var dto = SlotDto.FromModel(slot);

        Assert.Equal(new Position(11, "B"), dto.NextPos!.ToModel());
        Assert.Null(dto.SingleNextPos);
    }

    [Fact]
    public void Serialize_UsesCamelCaseSingleNextPos()
    {
        /// 動機：前端以 singleNextPos 讀取，欄位名稱必須是 camelCase。
        var slot = new GachaSlot(
            new Position(10, "B"),
            new SlotValue.CatName("侏羅貓"),
            new SlotValue.CatName("EVA初號機"),
            IsDuplicateRare: true,
            SingleNextPos: new Position(12, "A"));

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(SlotDto.FromModel(slot)));

        var single = doc.RootElement.GetProperty("singleNextPos");
        Assert.Equal(12, single.GetProperty("id").GetInt32());
        Assert.Equal("A", single.GetProperty("track").GetString());
    }
}
