namespace QuanTAlib.Tests;

public class ChannelPositionTests
{
    [Fact]
    public void Constructor_Default_ColdBeforeFirstUpdate()
    {
        var cp = new ChannelPosition();
        Assert.False(cp.IsHot);
        Assert.True(double.IsNaN(cp.Last.Value));
    }

    [Fact]
    public void Update_AtLowerBound_ReturnsZero()
    {
        var cp = new ChannelPosition();
        var time = DateTime.UtcNow;
        var r = cp.Update(new TValue(time, 40.0), new TValue(time, 40.0), new TValue(time, 60.0));
        Assert.Equal(0.0, r.Value, 1e-10);
        Assert.True(cp.IsHot);
    }

    [Fact]
    public void Update_AtUpperBound_ReturnsOne()
    {
        var cp = new ChannelPosition();
        var time = DateTime.UtcNow;
        var r = cp.Update(new TValue(time, 60.0), new TValue(time, 40.0), new TValue(time, 60.0));
        Assert.Equal(1.0, r.Value, 1e-10);
    }

    [Fact]
    public void Update_Midpoint_ReturnsOneHalf()
    {
        var cp = new ChannelPosition();
        var time = DateTime.UtcNow;
        var r = cp.Update(new TValue(time, 50.0), new TValue(time, 40.0), new TValue(time, 60.0));
        Assert.Equal(0.5, r.Value, 1e-10);
    }

    [Fact]
    public void Update_ZeroWidthChannel_PublishesNaN_RegardlessOfPolicy()
    {
        var cpCold = new ChannelPosition(OutOfRangePolicy.Cold);
        var cpSat = new ChannelPosition(OutOfRangePolicy.Saturate);
        var time = DateTime.UtcNow;

        var rCold = cpCold.Update(new TValue(time, 50.0), new TValue(time, 50.0), new TValue(time, 50.0));
        var rSat = cpSat.Update(new TValue(time, 50.0), new TValue(time, 50.0), new TValue(time, 50.0));

        Assert.True(double.IsNaN(rCold.Value));
        Assert.False(cpCold.IsHot);
        Assert.True(double.IsNaN(rSat.Value));
        Assert.False(cpSat.IsHot);
    }

    [Fact]
    public void Update_ValueAboveUpper_ColdPolicy_PublishesNaN()
    {
        var cp = new ChannelPosition(OutOfRangePolicy.Cold);
        var time = DateTime.UtcNow;
        var r = cp.Update(new TValue(time, 70.0), new TValue(time, 40.0), new TValue(time, 60.0));

        Assert.True(double.IsNaN(r.Value));
        Assert.False(cp.IsHot);
    }

    [Fact]
    public void Update_ValueAboveUpper_SaturatePolicy_ClampsToOne()
    {
        var cp = new ChannelPosition(OutOfRangePolicy.Saturate);
        var time = DateTime.UtcNow;
        var r = cp.Update(new TValue(time, 70.0), new TValue(time, 40.0), new TValue(time, 60.0));

        Assert.Equal(1.0, r.Value, 1e-10);
        Assert.True(cp.IsHot);
    }

    [Fact]
    public void Update_ValueBelowLower_SaturatePolicy_ClampsToZero()
    {
        var cp = new ChannelPosition(OutOfRangePolicy.Saturate);
        var time = DateTime.UtcNow;
        var r = cp.Update(new TValue(time, 10.0), new TValue(time, 40.0), new TValue(time, 60.0));

        Assert.Equal(0.0, r.Value, 1e-10);
    }

    [Fact]
    public void IsHot_RecoversOnNextValidBar_AfterColdPolicyBar()
    {
        var cp = new ChannelPosition(OutOfRangePolicy.Cold);
        var time = DateTime.UtcNow;

        cp.Update(new TValue(time, 70.0), new TValue(time, 40.0), new TValue(time, 60.0));
        Assert.False(cp.IsHot);

        var r = cp.Update(new TValue(time.AddSeconds(1), 50.0), new TValue(time.AddSeconds(1), 40.0), new TValue(time.AddSeconds(1), 60.0), isNew: true);
        Assert.True(cp.IsHot);
        Assert.Equal(0.5, r.Value, 1e-10);
    }

    [Fact]
    public void ScalarBounds_FixedOscillatorZone()
    {
        var source = new TSeries();
        var cp = new ChannelPosition(source, 0.0, 100.0);

        source.Add(new TValue(DateTime.UtcNow, 25.0), true);
        Assert.Equal(0.25, cp.Last.Value, 1e-10);
    }

    [Fact]
    public void Reset_ReturnsToCold()
    {
        var cp = new ChannelPosition();
        cp.Update(50.0, 40.0, 60.0);
        Assert.True(cp.IsHot);

        cp.Reset();
        Assert.False(cp.IsHot);
        Assert.True(double.IsNaN(cp.Last.Value));
    }
}
