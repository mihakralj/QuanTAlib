namespace QuanTAlib.Tests;

public class AtHighestTests
{
    [Fact]
    public void Constructor_Default_ColdBeforeFirstUpdate()
    {
        var h = new AtHighest(3);
        Assert.Equal("AtHighest(3)", h.Name);
        Assert.Equal(3, h.WarmupPeriod);
        Assert.False(h.IsHot);
        Assert.True(double.IsNaN(h.Last.Value));
    }

    [Fact]
    public void Constructor_InvalidN_Throws()
    {
        Assert.Throws<ArgumentException>(() => new AtHighest(0));
    }

    [Fact]
    public void Warmup_ReturnsNaN_UntilNBarsObserved()
    {
        var h = new AtHighest(3);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 2; i++)
        {
            var r = h.Update(new TValue(time.AddSeconds(i), 100.0 + i), true);
            Assert.True(double.IsNaN(r.Value));
            Assert.False(h.IsHot);
        }
    }

    [Fact]
    public void CurrentBarIsTheMax_ReturnsOne()
    {
        var h = new AtHighest(3);
        var time = DateTime.UtcNow;
        double[] values = [10.0, 20.0, 30.0]; // current (30) is the window max

        TValue r = default;
        for (int i = 0; i < values.Length; i++)
        {
            r = h.Update(new TValue(time.AddSeconds(i), values[i]), true);
        }

        Assert.True(h.IsHot);
        Assert.Equal(1.0, r.Value, 1e-10);
    }

    [Fact]
    public void CurrentBarIsNotTheMax_ReturnsZero()
    {
        var h = new AtHighest(3);
        var time = DateTime.UtcNow;
        double[] values = [10.0, 30.0, 20.0]; // current (20) is not the window max

        TValue r = default;
        for (int i = 0; i < values.Length; i++)
        {
            r = h.Update(new TValue(time.AddSeconds(i), values[i]), true);
        }

        Assert.Equal(0.0, r.Value, 1e-10);
    }

    [Fact]
    public void Tie_MostRecentOccurrenceWins()
    {
        var h = new AtHighest(2);
        var time = DateTime.UtcNow;

        h.Update(new TValue(time, 50.0), true);
        var r = h.Update(new TValue(time.AddSeconds(1), 50.0), true); // tie with the prior bar

        Assert.Equal(1.0, r.Value, 1e-10); // current bar wins the tie
    }

    [Fact]
    public void Correction_RebuildsWindow()
    {
        var h = new AtHighest(2);
        var time = DateTime.UtcNow;

        h.Update(new TValue(time, 10.0), true);
        var forming = h.Update(new TValue(time.AddSeconds(1), 5.0), true); // not the max ([10, 5])
        Assert.Equal(0.0, forming.Value, 1e-10);

        var corrected = h.Update(new TValue(time.AddSeconds(1), 50.0), false); // now the max ([10, 50])
        Assert.Equal(1.0, corrected.Value, 1e-10);
    }

    [Fact]
    public void Reset_ReturnsToCold()
    {
        var h = new AtHighest(1);
        h.Update(new TValue(DateTime.UtcNow, 10.0), true);
        Assert.True(h.IsHot);

        h.Reset();
        Assert.False(h.IsHot);
        Assert.True(double.IsNaN(h.Last.Value));
    }

    [Fact]
    public void N1_AlwaysAtHighest()
    {
        // With a window of 1, the current bar is trivially always the (only) maximum.
        var h = new AtHighest(1);
        var time = DateTime.UtcNow;

        var r1 = h.Update(new TValue(time, 5.0), true);
        var r2 = h.Update(new TValue(time.AddSeconds(1), 1.0), true);

        Assert.Equal(1.0, r1.Value, 1e-10);
        Assert.Equal(1.0, r2.Value, 1e-10);
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

        int n = 3;
        var batch = AtHighest.Batch(series, n);

        var stream = new AtHighest(n);
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
