namespace QuanTAlib.Tests;

public class OutsideChannelTests
{
    [Fact]
    public void Constructor_Default_ColdBeforeFirstUpdate()
    {
        var oc = new OutsideChannel();
        Assert.False(oc.IsHot);
        Assert.True(double.IsNaN(oc.Last.Value));
    }

    [Fact]
    public void Update_ValueAboveUpper_ReturnsOne()
    {
        var oc = new OutsideChannel();
        var time = DateTime.UtcNow;
        var r = oc.Update(new TValue(time, 70.0), new TValue(time, 40.0), new TValue(time, 60.0));
        Assert.Equal(1.0, r.Value, 1e-10);
    }

    [Fact]
    public void Update_ValueBelowLower_ReturnsOne()
    {
        var oc = new OutsideChannel();
        var time = DateTime.UtcNow;
        var r = oc.Update(new TValue(time, 10.0), new TValue(time, 40.0), new TValue(time, 60.0));
        Assert.Equal(1.0, r.Value, 1e-10);
    }

    [Fact]
    public void Update_ValueInsideBounds_ReturnsZero()
    {
        var oc = new OutsideChannel();
        var time = DateTime.UtcNow;
        var r = oc.Update(new TValue(time, 50.0), new TValue(time, 40.0), new TValue(time, 60.0));
        Assert.Equal(0.0, r.Value, 1e-10);
    }

    [Fact]
    public void Update_ValueOnBound_ReturnsZero_ComplementOfInsideChannel()
    {
        var oc = new OutsideChannel();
        var time = DateTime.UtcNow;
        var onLower = oc.Update(new TValue(time, 40.0), new TValue(time, 40.0), new TValue(time, 60.0));
        Assert.Equal(0.0, onLower.Value, 1e-10);
    }

    [Fact]
    public void ScalarBounds_FixedBreakoutZone()
    {
        var source = new TSeries();
        var oc = new OutsideChannel(source, 30.0, 70.0);

        source.Add(new TValue(DateTime.UtcNow, 50.0), true);
        Assert.Equal(0.0, oc.Last.Value, 1e-10);

        source.Add(new TValue(DateTime.UtcNow.AddSeconds(1), 80.0), true);
        Assert.Equal(1.0, oc.Last.Value, 1e-10);
    }

    [Fact]
    public void Chaining_TimeJoin_ThreeStreams()
    {
        var value = new TSeries();
        var lower = new TSeries();
        var upper = new TSeries();
        var oc = new OutsideChannel(value, lower, upper);

        var time = DateTime.UtcNow;
        value.Add(new TValue(time, 70.0), true);
        lower.Add(new TValue(time, 40.0), true);
        upper.Add(new TValue(time, 60.0), true);

        Assert.True(oc.IsHot);
        Assert.Equal(1.0, oc.Last.Value, 1e-10);
    }

    [Fact]
    public void ComplementOfInsideChannel_AcrossManySamples()
    {
        var oc = new OutsideChannel();
        var ic = new InsideChannel();
        var time = DateTime.UtcNow;
        double[] values = [35.0, 40.0, 45.0, 50.0, 55.0, 60.0, 65.0];

        foreach (var v in values)
        {
            var t = new TValue(time, v);
            var lower = new TValue(time, 40.0);
            var upper = new TValue(time, 60.0);

            var insideResult = ic.Update(t, lower, upper);
            var outsideResult = oc.Update(t, lower, upper);

            // Exactly one of the two must be true for every sample (bounds are inclusive to Inside).
            Assert.NotEqual(insideResult.Value, outsideResult.Value);
            time = time.AddSeconds(1);
        }
    }

    [Fact]
    public void Reset_ReturnsToCold()
    {
        var oc = new OutsideChannel();
        oc.Update(70.0, 40.0, 60.0);
        Assert.True(oc.IsHot);

        oc.Reset();
        Assert.False(oc.IsHot);
        Assert.True(double.IsNaN(oc.Last.Value));
    }
}
