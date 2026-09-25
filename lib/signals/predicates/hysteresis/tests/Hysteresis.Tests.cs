namespace QuanTAlib.Tests;

public class HysteresisTests
{
    [Fact]
    public void Constructor_Default_ColdBeforeFirstUpdate()
    {
        var h = new Hysteresis(enter: 70.0, exit: 65.0);
        Assert.False(h.IsHot);
        Assert.True(double.IsNaN(h.Last.Value));
    }

    [Fact]
    public void Constructor_ExitGreaterThanEnter_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Hysteresis(enter: 65.0, exit: 70.0));
    }

    [Fact]
    public void Constructor_NonFiniteThresholds_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Hysteresis(double.NaN, 0.0));
        Assert.Throws<ArgumentException>(() => new Hysteresis(0.0, double.NegativeInfinity));
    }

    [Fact]
    public void BelowExit_StaysFalse()
    {
        var h = new Hysteresis(enter: 70.0, exit: 65.0);
        var r = h.Update(50.0);
        Assert.Equal(0.0, r.Value, 1e-10);
    }

    [Fact]
    public void ReachesEnter_LatchesTrue()
    {
        var h = new Hysteresis(enter: 70.0, exit: 65.0);
        var r = h.Update(70.0);
        Assert.Equal(1.0, r.Value, 1e-10);
    }

    [Fact]
    public void DeadBand_HoldsPreviousState_NoFlicker()
    {
        var h = new Hysteresis(enter: 70.0, exit: 65.0);
        var time = DateTime.UtcNow;

        h.Update(new TValue(time, 70.0), true); // latch true
        var r1 = h.Update(new TValue(time.AddSeconds(1), 67.0), true); // inside dead band
        Assert.Equal(1.0, r1.Value, 1e-10); // stays true

        var r2 = h.Update(new TValue(time.AddSeconds(2), 68.0), true); // still inside dead band
        Assert.Equal(1.0, r2.Value, 1e-10);
    }

    [Fact]
    public void ReachesExit_LatchesFalse()
    {
        var h = new Hysteresis(enter: 70.0, exit: 65.0);
        var time = DateTime.UtcNow;

        h.Update(new TValue(time, 70.0), true);
        var r = h.Update(new TValue(time.AddSeconds(1), 65.0), true);
        Assert.Equal(0.0, r.Value, 1e-10);
    }

    [Fact]
    public void PlainThreshold_WhenEnterEqualsExit()
    {
        var h = new Hysteresis(enter: 50.0, exit: 50.0);
        var time = DateTime.UtcNow;

        var r1 = h.Update(new TValue(time, 51.0), true);
        Assert.Equal(1.0, r1.Value, 1e-10);

        var r2 = h.Update(new TValue(time.AddSeconds(1), 49.0), true);
        Assert.Equal(0.0, r2.Value, 1e-10);
    }

    [Fact]
    public void NonFiniteInput_HoldsLastLatchedState()
    {
        var h = new Hysteresis(enter: 70.0, exit: 65.0);
        var time = DateTime.UtcNow;

        h.Update(new TValue(time, 70.0), true); // latch true
        var r = h.Update(new TValue(time.AddSeconds(1), double.NaN), true);
        Assert.Equal(1.0, r.Value, 1e-10);
    }

    [Fact]
    public void IsNew_False_CorrectsSameBar()
    {
        var h = new Hysteresis(enter: 70.0, exit: 65.0);
        var time = DateTime.UtcNow;

        var r1 = h.Update(new TValue(time, 70.0), isNew: true);
        Assert.Equal(1.0, r1.Value, 1e-10);

        var r2 = h.Update(new TValue(time, 40.0), isNew: false);
        Assert.Equal(0.0, r2.Value, 1e-10); // corrected bar never reached enter; not latched
    }

    [Fact]
    public void Chaining_Constructor()
    {
        var source = new TSeries();
        var h = new Hysteresis(source, 70.0, 65.0);

        source.Add(new TValue(DateTime.UtcNow, 70.0), true);
        Assert.Equal(1.0, h.Last.Value, 1e-10);
    }

    [Fact]
    public void Reset_ReturnsToCold()
    {
        var h = new Hysteresis(enter: 70.0, exit: 65.0);
        h.Update(70.0);
        Assert.True(h.IsHot);

        h.Reset();
        Assert.False(h.IsHot);
        Assert.True(double.IsNaN(h.Last.Value));
    }
}
