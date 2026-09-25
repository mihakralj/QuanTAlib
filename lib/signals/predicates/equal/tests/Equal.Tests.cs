namespace QuanTAlib.Tests;

public class EqualTests
{
    [Fact]
    public void Constructor_Default_ColdBeforeFirstUpdate()
    {
        var eq = new Equal();
        Assert.Equal("Equal", eq.Name);
        Assert.False(eq.IsHot);
        Assert.True(double.IsNaN(eq.Last.Value));
    }

    [Fact]
    public void Constructor_InvalidEpsilon_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Equal(-1.0));
        Assert.Throws<ArgumentException>(() => new Equal(double.NaN));
        Assert.Throws<ArgumentException>(() => new Equal(double.PositiveInfinity));
    }

    [Fact]
    public void Update_ExactlyEqual_ReturnsOne()
    {
        var eq = new Equal();
        var r = eq.Update(5.0, 5.0);
        Assert.Equal(1.0, r.Value, 1e-10);
    }

    [Fact]
    public void Update_WithinDefaultEpsilon_ReturnsOne()
    {
        var eq = new Equal();
        var r = eq.Update(5.0, 5.0 + 1e-11);
        Assert.Equal(1.0, r.Value, 1e-10);
    }

    [Fact]
    public void Update_BeyondDefaultEpsilon_ReturnsZero()
    {
        var eq = new Equal();
        var r = eq.Update(5.0, 5.1);
        Assert.Equal(0.0, r.Value, 1e-10);
    }

    [Fact]
    public void Update_CustomEpsilon_WidensTolerance()
    {
        var eq = new Equal(epsilon: 0.2);
        var r = eq.Update(5.0, 5.1);
        Assert.Equal(1.0, r.Value, 1e-10);
    }

    [Fact]
    public void ScalarOverload_ComparesAgainstFixedValue()
    {
        var source = new TSeries();
        var eq = new Equal(source, 100.0);

        source.Add(new TValue(DateTime.UtcNow, 100.0), true);
        Assert.Equal(1.0, eq.Last.Value, 1e-10);

        source.Add(new TValue(DateTime.UtcNow.AddSeconds(1), 99.0), true);
        Assert.Equal(0.0, eq.Last.Value, 1e-10);
    }

    [Fact]
    public void Chaining_TimeJoin_TwoStreams()
    {
        var a = new TSeries();
        var b = new TSeries();
        var eq = new Equal(a, b);

        var time = DateTime.UtcNow;
        a.Add(new TValue(time, 42.0), true);
        b.Add(new TValue(time, 42.0), true);

        Assert.Equal(1.0, eq.Last.Value, 1e-10);
    }
}
