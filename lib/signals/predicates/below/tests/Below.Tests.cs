namespace QuanTAlib.Tests;

public class BelowTests
{
    [Fact]
    public void Constructor_Default_ColdBeforeFirstUpdate()
    {
        var below = new Below();
        Assert.Equal("Below", below.Name);
        Assert.False(below.IsHot);
        Assert.True(double.IsNaN(below.Last.Value));
    }

    [Fact]
    public void Update_ALessThanB_ReturnsOne()
    {
        var below = new Below();
        var r = below.Update(3.0, 5.0);
        Assert.Equal(1.0, r.Value, 1e-10);
    }

    [Fact]
    public void Update_AGreaterThanB_ReturnsZero()
    {
        var below = new Below();
        var r = below.Update(10.0, 5.0);
        Assert.Equal(0.0, r.Value, 1e-10);
    }

    [Fact]
    public void Update_AEqualsB_ReturnsZero_StrictInequality()
    {
        var below = new Below();
        var r = below.Update(5.0, 5.0);
        Assert.Equal(0.0, r.Value, 1e-10);
    }

    [Fact]
    public void ScalarOverload_ComparesAgainstFixedThreshold()
    {
        var source = new TSeries();
        var below = new Below(source, 30.0);

        source.Add(new TValue(DateTime.UtcNow, 35.0), true);
        Assert.Equal(0.0, below.Last.Value, 1e-10);

        source.Add(new TValue(DateTime.UtcNow.AddSeconds(1), 25.0), true);
        Assert.Equal(1.0, below.Last.Value, 1e-10);
    }

    [Fact]
    public void Chaining_TimeJoin_TwoStreams()
    {
        var a = new TSeries();
        var b = new TSeries();
        var below = new Below(a, b);

        var time = DateTime.UtcNow;
        a.Add(new TValue(time, 3.0), true);
        b.Add(new TValue(time, 5.0), true);

        Assert.Equal(1.0, below.Last.Value, 1e-10);
    }

    [Fact]
    public void Reset_ReturnsToCold()
    {
        var below = new Below();
        below.Update(3.0, 5.0);
        Assert.True(below.IsHot);

        below.Reset();
        Assert.False(below.IsHot);
        Assert.True(double.IsNaN(below.Last.Value));
    }
}
