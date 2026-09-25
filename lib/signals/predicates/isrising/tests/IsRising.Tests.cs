namespace QuanTAlib.Tests;

public class IsRisingTests
{
    [Fact]
    public void Constructor_Default_ColdBeforeFirstUpdate()
    {
        var r = new IsRising(3);
        Assert.Equal("IsRising(3)", r.Name);
        Assert.Equal(4, r.WarmupPeriod);
        Assert.False(r.IsHot);
        Assert.True(double.IsNaN(r.Last.Value));
    }

    [Fact]
    public void Constructor_InvalidN_Throws()
    {
        Assert.Throws<ArgumentException>(() => new IsRising(0));
        Assert.Throws<ArgumentException>(() => new IsRising(-1));
    }

    [Fact]
    public void Warmup_ReturnsNaN_UntilNPriorBarsConfirmed()
    {
        var r = new IsRising(2);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 2; i++)
        {
            var result = r.Update(new TValue(time.AddSeconds(i), 100.0 + i), true);
            Assert.True(double.IsNaN(result.Value));
            Assert.False(r.IsHot);
        }
    }

    [Fact]
    public void N1_CurrentGreaterThanImmediatePrior_ReturnsOne()
    {
        var r = new IsRising(1);
        var time = DateTime.UtcNow;

        r.Update(new TValue(time, 10.0), true);
        var result = r.Update(new TValue(time.AddSeconds(1), 20.0), true);

        Assert.True(r.IsHot);
        Assert.Equal(1.0, result.Value, 1e-10);
    }

    [Fact]
    public void N1_CurrentBelowImmediatePrior_ReturnsZero()
    {
        var r = new IsRising(1);
        var time = DateTime.UtcNow;

        r.Update(new TValue(time, 20.0), true);
        var result = r.Update(new TValue(time.AddSeconds(1), 10.0), true);

        Assert.Equal(0.0, result.Value, 1e-10);
    }

    [Fact]
    public void N1_Tie_ReturnsZero_StrictComparison()
    {
        var r = new IsRising(1);
        var time = DateTime.UtcNow;

        r.Update(new TValue(time, 10.0), true);
        var result = r.Update(new TValue(time.AddSeconds(1), 10.0), true);

        Assert.Equal(0.0, result.Value, 1e-10);
    }

    [Fact]
    public void CurrentBarExcludedFromComparison()
    {
        // IsRising(2): window is the previous 2 bars only, never the current one.
        var r = new IsRising(2);
        var time = DateTime.UtcNow;

        r.Update(new TValue(time, 10.0), true);
        r.Update(new TValue(time.AddSeconds(1), 20.0), true); // prior window becomes [10, 20] once bar 3 opens

        // Current bar (30) is higher than max(10, 20) = 20.
        var result = r.Update(new TValue(time.AddSeconds(2), 30.0), true);
        Assert.Equal(1.0, result.Value, 1e-10);
    }

    [Fact]
    public void IsNew_False_DoesNotAffectConfirmedWindow()
    {
        var r = new IsRising(1);
        var time = DateTime.UtcNow;

        r.Update(new TValue(time, 15.0), true); // bar 1 confirmed pending = 15
        var opened = r.Update(new TValue(time.AddSeconds(1), 20.0), true); // bar 2 opens, pushes 15, compares 20>15
        Assert.Equal(1.0, opened.Value, 1e-10);

        // Correct bar 2 downward; the confirmed window (still just [15]) is unaffected.
        var corrected = r.Update(new TValue(time.AddSeconds(1), 5.0), false);
        Assert.Equal(0.0, corrected.Value, 1e-10); // 5 > 15? no
    }

    [Fact]
    public void HandlesNaN_SubstitutesLastValid()
    {
        var r = new IsRising(1);
        var time = DateTime.UtcNow;

        r.Update(new TValue(time, 10.0), true);
        r.Update(new TValue(time.AddSeconds(1), 20.0), true);

        var result = r.Update(new TValue(time.AddSeconds(2), double.NaN), true);
        // Substituted with last valid (20.0); window is now [20] (from bar 2's confirmation).
        Assert.Equal(0.0, result.Value, 1e-10); // 20 > 20? no
    }

    [Fact]
    public void Reset_ReturnsToCold()
    {
        var r = new IsRising(1);
        var time = DateTime.UtcNow;
        r.Update(new TValue(time, 10.0), true);
        r.Update(new TValue(time.AddSeconds(1), 20.0), true);
        Assert.True(r.IsHot);

        r.Reset();
        Assert.False(r.IsHot);
        Assert.True(double.IsNaN(r.Last.Value));
    }

    [Fact]
    public void Batch_MatchesStreaming()
    {
        var series = new TSeries();
        var time = DateTime.UtcNow;
        double[] values = [10.0, 15.0, 12.0, 20.0, 18.0, 25.0, 5.0, 30.0];
        for (int i = 0; i < values.Length; i++)
        {
            series.Add(new TValue(time.AddSeconds(i), values[i]), true);
        }

        int n = 2;
        var batch = IsRising.Batch(series, n);

        var stream = new IsRising(n);
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
