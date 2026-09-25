namespace QuanTAlib.Tests;

public class AtLowestTests
{
    [Fact]
    public void Constructor_Default_ColdBeforeFirstUpdate()
    {
        var l = new AtLowest(3);
        Assert.Equal("AtLowest(3)", l.Name);
        Assert.Equal(3, l.WarmupPeriod);
        Assert.False(l.IsHot);
        Assert.True(double.IsNaN(l.Last.Value));
    }

    [Fact]
    public void Constructor_InvalidN_Throws()
    {
        Assert.Throws<ArgumentException>(() => new AtLowest(0));
    }

    [Fact]
    public void CurrentBarIsTheMin_ReturnsOne()
    {
        var l = new AtLowest(3);
        var time = DateTime.UtcNow;
        double[] values = [30.0, 20.0, 10.0]; // current (10) is the window min

        TValue r = default;
        for (int i = 0; i < values.Length; i++)
        {
            r = l.Update(new TValue(time.AddSeconds(i), values[i]), true);
        }

        Assert.True(l.IsHot);
        Assert.Equal(1.0, r.Value, 1e-10);
    }

    [Fact]
    public void CurrentBarIsNotTheMin_ReturnsZero()
    {
        var l = new AtLowest(3);
        var time = DateTime.UtcNow;
        double[] values = [30.0, 10.0, 20.0];

        TValue r = default;
        for (int i = 0; i < values.Length; i++)
        {
            r = l.Update(new TValue(time.AddSeconds(i), values[i]), true);
        }

        Assert.Equal(0.0, r.Value, 1e-10);
    }

    [Fact]
    public void Tie_MostRecentOccurrenceWins()
    {
        var l = new AtLowest(2);
        var time = DateTime.UtcNow;

        l.Update(new TValue(time, 50.0), true);
        var r = l.Update(new TValue(time.AddSeconds(1), 50.0), true);

        Assert.Equal(1.0, r.Value, 1e-10);
    }

    [Fact]
    public void Correction_RebuildsWindow()
    {
        var l = new AtLowest(2);
        var time = DateTime.UtcNow;

        l.Update(new TValue(time, 10.0), true);
        var forming = l.Update(new TValue(time.AddSeconds(1), 50.0), true); // not the min ([10, 50])
        Assert.Equal(0.0, forming.Value, 1e-10);

        var corrected = l.Update(new TValue(time.AddSeconds(1), 1.0), false); // now the min ([10, 1])
        Assert.Equal(1.0, corrected.Value, 1e-10);
    }

    [Fact]
    public void Reset_ReturnsToCold()
    {
        var l = new AtLowest(1);
        l.Update(new TValue(DateTime.UtcNow, 10.0), true);
        Assert.True(l.IsHot);

        l.Reset();
        Assert.False(l.IsHot);
        Assert.True(double.IsNaN(l.Last.Value));
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

        int n = 3;
        var batch = AtLowest.Batch(series, n);

        var stream = new AtLowest(n);
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
