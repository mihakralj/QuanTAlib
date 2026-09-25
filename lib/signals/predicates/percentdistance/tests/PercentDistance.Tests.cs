namespace QuanTAlib.Tests;

public class PercentDistanceTests
{
    [Fact]
    public void Constructor_Default_ColdBeforeFirstUpdate()
    {
        var pd = new PercentDistance();
        Assert.Equal("PercentDistance", pd.Name);
        Assert.False(pd.IsHot);
        Assert.True(double.IsNaN(pd.Last.Value));
    }

    [Fact]
    public void Update_ComputesRelativeDistance()
    {
        var pd = new PercentDistance();
        var r = pd.Update(110.0, 100.0);
        Assert.Equal(0.10, r.Value, 1e-10);
    }

    [Fact]
    public void Update_NegativeDistance_WhenBelow()
    {
        var pd = new PercentDistance();
        var r = pd.Update(90.0, 100.0);
        Assert.Equal(-0.10, r.Value, 1e-10);
    }

    [Fact]
    public void Update_ZeroDenominator_ReturnsZero_NotNaN()
    {
        var pd = new PercentDistance();
        var r = pd.Update(10.0, 0.0);
        Assert.Equal(0.0, r.Value, 1e-10);
    }

    [Fact]
    public void Update_ZeroDenominator_DoesNotPoisonSubsequentBars()
    {
        var pd = new PercentDistance();
        var time = DateTime.UtcNow;

        pd.Update(new TValue(time, 10.0), new TValue(time, 0.0), isNew: true);
        var next = pd.Update(new TValue(time.AddSeconds(1), 110.0), new TValue(time.AddSeconds(1), 100.0), isNew: true);

        Assert.Equal(0.10, next.Value, 1e-10);
        Assert.False(double.IsNaN(next.Value));
    }

    [Fact]
    public void FeedsAbove_ForThresholdDistanceCondition()
    {
        // "More than 2% above its moving average": Above(PercentDistance(price, ma), 0.02).
        var price = new TSeries();
        var ma = new TSeries();
        var pd = new PercentDistance(price, ma);
        var above = new Above(pd, 0.02);

        var time = DateTime.UtcNow;
        price.Add(new TValue(time, 103.0), true);
        ma.Add(new TValue(time, 100.0), true);

        Assert.Equal(1.0, above.Last.Value, 1e-10); // 3% > 2%
    }

    [Fact]
    public void IsNew_False_CorrectsSameBar()
    {
        var pd = new PercentDistance();
        var time = DateTime.UtcNow;

        var r1 = pd.Update(new TValue(time, 110.0), new TValue(time, 100.0), isNew: true);
        Assert.Equal(0.10, r1.Value, 1e-10);

        var r2 = pd.Update(new TValue(time, 120.0), new TValue(time, 100.0), isNew: false);
        Assert.Equal(0.20, r2.Value, 1e-10);
    }

    [Fact]
    public void Reset_ReturnsToCold()
    {
        var pd = new PercentDistance();
        pd.Update(110.0, 100.0);
        Assert.True(pd.IsHot);

        pd.Reset();
        Assert.False(pd.IsHot);
        Assert.True(double.IsNaN(pd.Last.Value));
    }

    [Fact]
    public void Chaining_TimeJoin_TwoStreams()
    {
        var a = new TSeries();
        var b = new TSeries();
        var pd = new PercentDistance(a, b);

        var time = DateTime.UtcNow;
        a.Add(new TValue(time, 105.0), true);
        b.Add(new TValue(time, 100.0), true);

        Assert.Equal(0.05, pd.Last.Value, 1e-10);
    }
}
