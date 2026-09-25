namespace QuanTAlib.Tests;

public class AboveTests
{
    [Fact]
    public void Constructor_Default_ColdBeforeFirstUpdate()
    {
        var above = new Above();
        Assert.Equal("Above", above.Name);
        Assert.False(above.IsHot);
        Assert.True(double.IsNaN(above.Last.Value));
    }

    [Fact]
    public void Update_AGreaterThanB_ReturnsOne()
    {
        var above = new Above();
        var r = above.Update(10.0, 5.0);
        Assert.Equal(1.0, r.Value, 1e-10);
        Assert.True(above.IsHot);
    }

    [Fact]
    public void Update_ALessThanB_ReturnsZero()
    {
        var above = new Above();
        var r = above.Update(3.0, 5.0);
        Assert.Equal(0.0, r.Value, 1e-10);
    }

    [Fact]
    public void Update_AEqualsB_ReturnsZero_StrictInequality()
    {
        var above = new Above();
        var r = above.Update(5.0, 5.0);
        Assert.Equal(0.0, r.Value, 1e-10);
    }

    [Fact]
    public void Update_HandlesNaN_SubstitutesLastValid()
    {
        var above = new Above();
        var time = DateTime.UtcNow;
        above.Update(new TValue(time, 10.0), new TValue(time, 5.0));

        var r = above.Update(new TValue(time.AddSeconds(1), double.NaN), new TValue(time.AddSeconds(1), 5.0));
        Assert.Equal(1.0, r.Value, 1e-10); // a substituted with last valid (10.0)
    }

    [Fact]
    public void IsNew_False_CorrectsSameBar()
    {
        var above = new Above();
        var time = DateTime.UtcNow;

        var r1 = above.Update(new TValue(time, 10.0), new TValue(time, 5.0), isNew: true);
        Assert.Equal(1.0, r1.Value, 1e-10);

        var r2 = above.Update(new TValue(time, 2.0), new TValue(time, 5.0), isNew: false);
        Assert.Equal(0.0, r2.Value, 1e-10);
    }

    [Fact]
    public void ScalarOverload_ComparesAgainstFixedThreshold()
    {
        var source = new TSeries();
        var above = new Above(source, 30.0);

        source.Add(new TValue(DateTime.UtcNow, 25.0), true);
        Assert.Equal(0.0, above.Last.Value, 1e-10);

        source.Add(new TValue(DateTime.UtcNow.AddSeconds(1), 35.0), true);
        Assert.Equal(1.0, above.Last.Value, 1e-10);
    }

    [Fact]
    public void Chaining_TimeJoin_TwoStreams()
    {
        var a = new TSeries();
        var b = new TSeries();
        var above = new Above(a, b);

        var time = DateTime.UtcNow;
        a.Add(new TValue(time, 10.0), true);
        Assert.False(above.IsHot); // b not yet reported for this bar

        b.Add(new TValue(time, 5.0), true);
        Assert.True(above.IsHot);
        Assert.Equal(1.0, above.Last.Value, 1e-10);
    }

    [Fact]
    public void Reset_ReturnsToCold()
    {
        var above = new Above();
        above.Update(10.0, 5.0);
        Assert.True(above.IsHot);

        above.Reset();
        Assert.False(above.IsHot);
        Assert.True(double.IsNaN(above.Last.Value));
    }
}
