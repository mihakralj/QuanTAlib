namespace QuanTAlib.Tests;

public class IsFallingTests
{
    [Fact]
    public void Constructor_Default_ColdBeforeFirstUpdate()
    {
        var f = new IsFalling(3);
        Assert.Equal("IsFalling(3)", f.Name);
        Assert.Equal(4, f.WarmupPeriod);
        Assert.False(f.IsHot);
        Assert.True(double.IsNaN(f.Last.Value));
    }

    [Fact]
    public void Constructor_InvalidN_Throws()
    {
        Assert.Throws<ArgumentException>(() => new IsFalling(0));
    }

    [Fact]
    public void N1_CurrentBelowImmediatePrior_ReturnsOne()
    {
        var f = new IsFalling(1);
        var time = DateTime.UtcNow;

        f.Update(new TValue(time, 20.0), true);
        var result = f.Update(new TValue(time.AddSeconds(1), 10.0), true);

        Assert.True(f.IsHot);
        Assert.Equal(1.0, result.Value, 1e-10);
    }

    [Fact]
    public void N1_CurrentAboveImmediatePrior_ReturnsZero()
    {
        var f = new IsFalling(1);
        var time = DateTime.UtcNow;

        f.Update(new TValue(time, 10.0), true);
        var result = f.Update(new TValue(time.AddSeconds(1), 20.0), true);

        Assert.Equal(0.0, result.Value, 1e-10);
    }

    [Fact]
    public void N1_Tie_ReturnsZero_StrictComparison()
    {
        var f = new IsFalling(1);
        var time = DateTime.UtcNow;

        f.Update(new TValue(time, 10.0), true);
        var result = f.Update(new TValue(time.AddSeconds(1), 10.0), true);

        Assert.Equal(0.0, result.Value, 1e-10);
    }

    [Fact]
    public void CurrentBarExcludedFromComparison()
    {
        var f = new IsFalling(2);
        var time = DateTime.UtcNow;

        f.Update(new TValue(time, 30.0), true);
        f.Update(new TValue(time.AddSeconds(1), 20.0), true); // prior window becomes [30, 20] once bar 3 opens

        var result = f.Update(new TValue(time.AddSeconds(2), 10.0), true);
        Assert.Equal(1.0, result.Value, 1e-10); // 10 < min(30, 20) = 20
    }

    [Fact]
    public void IsNew_False_DoesNotAffectConfirmedWindow()
    {
        var f = new IsFalling(1);
        var time = DateTime.UtcNow;

        f.Update(new TValue(time, 15.0), true);
        var opened = f.Update(new TValue(time.AddSeconds(1), 10.0), true);
        Assert.Equal(1.0, opened.Value, 1e-10);

        var corrected = f.Update(new TValue(time.AddSeconds(1), 100.0), false);
        Assert.Equal(0.0, corrected.Value, 1e-10); // 100 < 15? no
    }

    [Fact]
    public void Reset_ReturnsToCold()
    {
        var f = new IsFalling(1);
        var time = DateTime.UtcNow;
        f.Update(new TValue(time, 20.0), true);
        f.Update(new TValue(time.AddSeconds(1), 10.0), true);
        Assert.True(f.IsHot);

        f.Reset();
        Assert.False(f.IsHot);
        Assert.True(double.IsNaN(f.Last.Value));
    }

    [Fact]
    public void Batch_MatchesStreaming()
    {
        var series = new TSeries();
        var time = DateTime.UtcNow;
        double[] values = [30.0, 25.0, 28.0, 10.0, 15.0, 5.0, 40.0, 2.0];
        for (int i = 0; i < values.Length; i++)
        {
            series.Add(new TValue(time.AddSeconds(i), values[i]), true);
        }

        int n = 2;
        var batch = IsFalling.Batch(series, n);

        var stream = new IsFalling(n);
        for (int i = 0; i < series.Count; i++)
        {
            var r = stream.Update(series[i], true);
            if (double.IsNaN(batch[i].Value))
            {
                Assert.True(double.IsNaN(r.Value));
            }
            else
            {
                Assert.Equal(batch[i].Value, r.Value, 1e-10);
            }
        }
    }
}
