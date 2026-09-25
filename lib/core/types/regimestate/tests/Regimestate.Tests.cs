namespace QuanTAlib.Tests;

public class RegimeStateTests
{
    [Fact]
    public void TrendState_UnknownIsDefaultZero()
    {
        Assert.Equal(default, TrendState.Unknown);
        Assert.Equal((sbyte)0, (sbyte)TrendState.Unknown);
    }

    [Fact]
    public void TrendState_UpAndDownAreSignedOpposites()
    {
        Assert.Equal((sbyte)1, (sbyte)TrendState.Up);
        Assert.Equal((sbyte)(-1), (sbyte)TrendState.Down);
    }

    [Fact]
    public void TrendState_RangeIsDistinctFromUnknown()
    {
        Assert.NotEqual(TrendState.Unknown, TrendState.Range);
    }

    [Fact]
    public void VolState_UnknownIsDefaultZero()
    {
        Assert.Equal(default, VolState.Unknown);
        Assert.Equal((sbyte)0, (sbyte)VolState.Unknown);
    }

    [Fact]
    public void VolState_OrdersCompressionNormalExpansion()
    {
        Assert.True(VolState.Compression < VolState.Normal);
        Assert.True(VolState.Normal < VolState.Expansion);
    }

    [Fact]
    public void TrendState_And_VolState_AreIndependentEnums()
    {
        // Orthogonal axes: a TrendState value carries no VolState information and vice versa.
        // This test documents the design intent from the strategy-primitives spec (Section 8.2).
        var trend = TrendState.Up;
        var vol = VolState.Compression;

        Assert.Equal(TrendState.Up, trend);
        Assert.Equal(VolState.Compression, vol);
    }
}
