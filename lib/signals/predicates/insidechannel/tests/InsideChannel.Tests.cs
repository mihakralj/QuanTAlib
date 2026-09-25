namespace QuanTAlib.Tests;

public class InsideChannelTests
{
    [Fact]
    public void Constructor_Default_ColdBeforeFirstUpdate()
    {
        var ic = new InsideChannel();
        Assert.False(ic.IsHot);
        Assert.True(double.IsNaN(ic.Last.Value));
    }

    [Fact]
    public void Update_ValueInsideBounds_ReturnsOne()
    {
        var ic = new InsideChannel();
        var time = DateTime.UtcNow;
        var r = ic.Update(new TValue(time, 50.0), new TValue(time, 40.0), new TValue(time, 60.0));
        Assert.Equal(1.0, r.Value, 1e-10);
    }

    [Fact]
    public void Update_ValueOutsideBounds_ReturnsZero()
    {
        var ic = new InsideChannel();
        var time = DateTime.UtcNow;
        var r = ic.Update(new TValue(time, 70.0), new TValue(time, 40.0), new TValue(time, 60.0));
        Assert.Equal(0.0, r.Value, 1e-10);
    }

    [Fact]
    public void Update_ValueOnLowerBound_ReturnsOne_Inclusive()
    {
        var ic = new InsideChannel();
        var time = DateTime.UtcNow;
        var r = ic.Update(new TValue(time, 40.0), new TValue(time, 40.0), new TValue(time, 60.0));
        Assert.Equal(1.0, r.Value, 1e-10);
    }

    [Fact]
    public void Update_ValueOnUpperBound_ReturnsOne_Inclusive()
    {
        var ic = new InsideChannel();
        var time = DateTime.UtcNow;
        var r = ic.Update(new TValue(time, 60.0), new TValue(time, 40.0), new TValue(time, 60.0));
        Assert.Equal(1.0, r.Value, 1e-10);
    }

    [Fact]
    public void ScalarBounds_FixedOscillatorZone()
    {
        var source = new TSeries();
        var ic = new InsideChannel(source, 30.0, 70.0);

        source.Add(new TValue(DateTime.UtcNow, 50.0), true);
        Assert.Equal(1.0, ic.Last.Value, 1e-10);

        source.Add(new TValue(DateTime.UtcNow.AddSeconds(1), 80.0), true);
        Assert.Equal(0.0, ic.Last.Value, 1e-10);
    }

    [Fact]
    public void Chaining_TimeJoin_ThreeStreams()
    {
        var value = new TSeries();
        var lower = new TSeries();
        var upper = new TSeries();
        var ic = new InsideChannel(value, lower, upper);

        var time = DateTime.UtcNow;
        value.Add(new TValue(time, 50.0), true);
        Assert.False(ic.IsHot);

        lower.Add(new TValue(time, 40.0), true);
        Assert.False(ic.IsHot);

        upper.Add(new TValue(time, 60.0), true);
        Assert.True(ic.IsHot);
        Assert.Equal(1.0, ic.Last.Value, 1e-10);
    }

    [Fact]
    public void IsNew_False_CorrectsSameBar()
    {
        var ic = new InsideChannel();
        var time = DateTime.UtcNow;

        var r1 = ic.Update(new TValue(time, 50.0), new TValue(time, 40.0), new TValue(time, 60.0), isNew: true);
        Assert.Equal(1.0, r1.Value, 1e-10);

        var r2 = ic.Update(new TValue(time, 70.0), new TValue(time, 40.0), new TValue(time, 60.0), isNew: false);
        Assert.Equal(0.0, r2.Value, 1e-10);
    }

    [Fact]
    public void HandlesNaN_SubstitutesLastValid()
    {
        var ic = new InsideChannel();
        var time = DateTime.UtcNow;
        ic.Update(new TValue(time, 50.0), new TValue(time, 40.0), new TValue(time, 60.0));

        var r = ic.Update(new TValue(time.AddSeconds(1), double.NaN), new TValue(time.AddSeconds(1), 40.0), new TValue(time.AddSeconds(1), 60.0));
        Assert.Equal(1.0, r.Value, 1e-10); // value substituted with last valid (50.0), still inside
    }

    [Fact]
    public void Reset_ReturnsToCold()
    {
        var ic = new InsideChannel();
        ic.Update(50.0, 40.0, 60.0);
        Assert.True(ic.IsHot);

        ic.Reset();
        Assert.False(ic.IsHot);
        Assert.True(double.IsNaN(ic.Last.Value));
    }
}
