using BattleCatCalc.Core.Models;

namespace BattleCatCalc.Core.Tests.Models;

/// <summary>
/// GachaStep 與 GachaPool record 型別測試。
/// </summary>
public class GachaStepAndPoolTests
{
    #region GachaStep

    [Fact]
    public void GachaStep_DefaultValues_AreCorrect()
    {
        /// 動機：GachaStep 的可選參數應有合理預設值。
        var step = new GachaStep(
            Source: new Position(1, "A"),
            Target: new Position(2, "A"),
            Unit: "Jizo");

        Assert.Equal("", step.PoolId);
        Assert.False(step.IsGuaranteed);
        Assert.False(step.IsTrackSwitch);
        Assert.False(step.IsDuplicateRare);
    }

    [Fact]
    public void GachaStep_AllParametersSet_StoresCorrectly()
    {
        /// 動機：所有欄位都設值時應正確儲存。
        var step = new GachaStep(
            Source: new Position(1, "A"),
            Target: new Position(11, "B"),
            Unit: "Gao",
            PoolId: "ultra",
            IsGuaranteed: true,
            IsTrackSwitch: true,
            IsDuplicateRare: true);

        Assert.Equal(new Position(1, "A"), step.Source);
        Assert.Equal(new Position(11, "B"), step.Target);
        Assert.Equal("Gao", step.Unit);
        Assert.Equal("ultra", step.PoolId);
        Assert.True(step.IsGuaranteed);
        Assert.True(step.IsTrackSwitch);
        Assert.True(step.IsDuplicateRare);
    }

    [Fact]
    public void GachaStep_RecordEquality_SameValues()
    {
        /// 動機：record class 的值相等性驗證。
        var a = new GachaStep(new Position(1, "A"), new Position(2, "A"), "Cat", "p1", true);
        var b = new GachaStep(new Position(1, "A"), new Position(2, "A"), "Cat", "p1", true);

        Assert.Equal(a, b);
    }

    [Fact]
    public void GachaStep_RecordEquality_DifferentValues()
    {
        var a = new GachaStep(new Position(1, "A"), new Position(2, "A"), "Cat");
        var b = new GachaStep(new Position(1, "A"), new Position(2, "A"), "Dog");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void GachaStep_WithExpression_CreatesModifiedCopy()
    {
        /// 動機：record 的 with 表達式應正確建立修改後的副本。
        var original = new GachaStep(new Position(1, "A"), new Position(2, "A"), "Cat");
        var modified = original with { IsGuaranteed = true, PoolId = "pool1" };

        Assert.False(original.IsGuaranteed);
        Assert.True(modified.IsGuaranteed);
        Assert.Equal("pool1", modified.PoolId);
        Assert.Equal(original.Source, modified.Source);
    }

    #endregion

    #region GachaPool

    [Fact]
    public void GachaPool_DefaultValues_AreCorrect()
    {
        /// 動機：GachaPool 的預設值 HasGuaranteed=true, GuaranteedCost=10。
        var trackA = new Track("A", []);
        var trackB = new Track("B", []);
        var pool = new GachaPool("id", "Name", trackA, trackB);

        Assert.True(pool.HasGuaranteed);
        Assert.Equal(10, pool.GuaranteedCost);
    }

    [Fact]
    public void GachaPool_CustomValues_StoresCorrectly()
    {
        var trackA = new Track("A", []);
        var trackB = new Track("B", []);
        var pool = new GachaPool("ultra", "Ultra Souls", trackA, trackB,
            HasGuaranteed: false, GuaranteedCost: 15);

        Assert.Equal("ultra", pool.PoolId);
        Assert.Equal("Ultra Souls", pool.Name);
        Assert.False(pool.HasGuaranteed);
        Assert.Equal(15, pool.GuaranteedCost);
    }

    [Fact]
    public void GachaPool_RecordEquality_SameTrackInstances()
    {
        /// 動機：相同 Track 實例的 Pool 應視為相等。
        var trackA = new Track("A", []);
        var trackB = new Track("B", []);
        var a = new GachaPool("id", "Name", trackA, trackB);
        var b = new GachaPool("id", "Name", trackA, trackB);

        Assert.Equal(a, b);
    }

    [Fact]
    public void GachaPool_RecordEquality_DifferentPoolId()
    {
        var trackA = new Track("A", []);
        var trackB = new Track("B", []);
        var a = new GachaPool("id1", "Name", trackA, trackB);
        var b = new GachaPool("id2", "Name", trackA, trackB);

        Assert.NotEqual(a, b);
    }

    #endregion
}
