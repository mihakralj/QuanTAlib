namespace QuanTAlib.Tests;

public class TTradeContextTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var ctx = new TTradeContext(
            EntryTime: 12345,
            EntryPrice: 100.0,
            Side: Direction.Long,
            InitialStop: 95.0,
            PointValue: 1.0,
            TickSize: 0.01);

        Assert.Equal(12345, ctx.EntryTime);
        Assert.Equal(100.0, ctx.EntryPrice);
        Assert.Equal(Direction.Long, ctx.Side);
        Assert.Equal(95.0, ctx.InitialStop);
        Assert.Equal(1.0, ctx.PointValue);
        Assert.Equal(0.01, ctx.TickSize);
    }

    [Fact]
    public void OneR_IsAbsoluteDistanceFromEntryToInitialStop()
    {
        var longCtx = new TTradeContext(0, 100.0, Direction.Long, 95.0, 1.0, 0.01);
        double oneR = Math.Abs(longCtx.EntryPrice - longCtx.InitialStop);
        Assert.Equal(5.0, oneR, 1e-10);

        var shortCtx = new TTradeContext(0, 100.0, Direction.Short, 105.0, 1.0, 0.01);
        double oneRShort = Math.Abs(shortCtx.EntryPrice - shortCtx.InitialStop);
        Assert.Equal(5.0, oneRShort, 1e-10);
    }

    [Fact]
    public void RecordEquality_UsesAllProperties()
    {
        var a = new TTradeContext(1, 100.0, Direction.Long, 95.0, 1.0, 0.01);
        var same = new TTradeContext(1, 100.0, Direction.Long, 95.0, 1.0, 0.01);
        var different = new TTradeContext(1, 100.0, Direction.Short, 95.0, 1.0, 0.01);

        Assert.Equal(a, same);
        Assert.NotEqual(a, different);
    }

    [Fact]
    public void BracketFlags_CombinesAsFlags()
    {
        var both = BracketFlags.StopTouched | BracketFlags.TargetTouched;
        Assert.Equal(BracketFlags.BothTouched, both);
        Assert.True(both.HasFlag(BracketFlags.StopTouched));
        Assert.True(both.HasFlag(BracketFlags.TargetTouched));
    }

    [Fact]
    public void BracketFlags_NoneIsZero()
    {
        Assert.Equal((byte)0, (byte)BracketFlags.None);
    }

    [Fact]
    public void BracketFlags_EntryBarAndGapThroughAreIndependentBits()
    {
        var combined = BracketFlags.GapThrough | BracketFlags.EntryBar;
        Assert.True(combined.HasFlag(BracketFlags.GapThrough));
        Assert.True(combined.HasFlag(BracketFlags.EntryBar));
        Assert.False(combined.HasFlag(BracketFlags.StopTouched));
    }

    [Fact]
    public void ExitReason_NoneIsDefaultZero()
    {
        Assert.Equal(default, ExitReason.None);
        Assert.Equal((byte)0, (byte)ExitReason.None);
    }

    [Theory]
    [InlineData(ExitReason.InitialStop)]
    [InlineData(ExitReason.Structural)]
    [InlineData(ExitReason.Trail)]
    [InlineData(ExitReason.Breakeven)]
    [InlineData(ExitReason.Target)]
    [InlineData(ExitReason.Time)]
    [InlineData(ExitReason.Stale)]
    [InlineData(ExitReason.Session)]
    public void ExitReason_AllValuesDistinctFromNone(ExitReason reason)
    {
        Assert.NotEqual(ExitReason.None, reason);
    }
}
