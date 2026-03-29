using BattleCatCalc.Core.Enums;
using BattleCatCalc.Core.Models;

namespace BattleCatCalc.Core.Tests.Models;

public class SlotValueTests
{
    [Fact]
    public void CatName_StoresValue()
    {
        var val = new SlotValue.CatName("Jizo");

        Assert.Equal("Jizo", val.Value);
        Assert.Equal("Jizo", val.ToString());
    }

    [Fact]
    public void CatName_IsValid_ReturnsTrue()
    {
        SlotValue val = new SlotValue.CatName("Jizo");

        Assert.True(val.IsValid);
    }

    [Fact]
    public void CatName_AsCatName_ReturnsValue()
    {
        SlotValue val = new SlotValue.CatName("Jizo");

        Assert.Equal("Jizo", val.AsCatName());
    }

    [Fact]
    public void Status_StoresParseStatus()
    {
        var val = new SlotValue.Status(ParseStatus.MultipleData);

        Assert.Equal(ParseStatus.MultipleData, val.Value);
    }

    [Fact]
    public void Status_IsValid_ReturnsFalse()
    {
        SlotValue val = new SlotValue.Status(ParseStatus.InvalidData);

        Assert.False(val.IsValid);
    }

    [Fact]
    public void Status_AsCatName_ReturnsNull()
    {
        SlotValue val = new SlotValue.Status(ParseStatus.Ambiguous);

        Assert.Null(val.AsCatName());
    }

    [Fact]
    public void Equality_SameCatName_AreEqual()
    {
        SlotValue a = new SlotValue.CatName("Jizo");
        SlotValue b = new SlotValue.CatName("Jizo");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentTypes_AreNotEqual()
    {
        SlotValue a = new SlotValue.CatName("Jizo");
        SlotValue b = new SlotValue.Status(ParseStatus.InvalidData);

        Assert.NotEqual(a, b);
    }
}
