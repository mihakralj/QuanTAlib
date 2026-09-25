using Xunit;

namespace QuanTAlib.Tests;

public class LagTests
{
    private readonly GBM _gbm = new(sigma: 0.5, mu: 0.0, seed: 46);

    [Fact]
    public void Lag_Constructor_Default()
    {
        var lag = new Lag();
        Assert.Equal("Lag(1)", lag.Name);
        Assert.Equal(2, lag.WarmupPeriod);
        Assert.False(lag.IsHot);
    }

    [Fact]
    public void Lag_Constructor_InvalidN_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => new Lag(-1));
    }

    [Fact]
    public void Lag_ZeroDelay_IsIdentity()
    {
        var lag = new Lag(0);
        var time = DateTime.UtcNow;

        var r1 = lag.Update(new TValue(time, 10.0));
        Assert.Equal(10.0, r1.Value, 1e-10);
        Assert.True(lag.IsHot);

        var r2 = lag.Update(new TValue(time.AddSeconds(1), 20.0));
        Assert.Equal(20.0, r2.Value, 1e-10);
    }

    [Fact]
    public void Lag_Warmup_ReturnsZero()
    {
        var lag = new Lag(3);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 3; i++)
        {
            var r = lag.Update(new TValue(time.AddSeconds(i), 100.0 + i));
            Assert.Equal(0.0, r.Value, 1e-10);
            Assert.False(lag.IsHot);
        }
    }

    [Fact]
    public void Lag_AfterWarmup_ReturnsDelayedValue()
    {
        var lag = new Lag(2);
        var time = DateTime.UtcNow;
        double[] values = [10.0, 20.0, 30.0, 40.0, 50.0];
        double[] expected = [0.0, 0.0, 10.0, 20.0, 30.0];

        for (int i = 0; i < values.Length; i++)
        {
            var r = lag.Update(new TValue(time.AddSeconds(i), values[i]));
            Assert.Equal(expected[i], r.Value, 1e-10);
        }

        Assert.True(lag.IsHot);
    }

    [Fact]
    public void Lag_HandlesNaN()
    {
        var lag = new Lag(1);
        var time = DateTime.UtcNow;

        lag.Update(new TValue(time, 10.0));
        lag.Update(new TValue(time.AddSeconds(1), double.NaN)); // substituted with 10.0 before entering window
        var r = lag.Update(new TValue(time.AddSeconds(2), 30.0));

        Assert.Equal(10.0, r.Value, 1e-10);
    }

    [Fact]
    public void Lag_IsNew_False_CorrectsSameBar()
    {
        var lag = new Lag(1);
        var time = DateTime.UtcNow;

        lag.Update(new TValue(time, 10.0), isNew: true);
        var r1 = lag.Update(new TValue(time.AddSeconds(1), 20.0), isNew: true);
        Assert.Equal(10.0, r1.Value, 1e-10);

        var r2 = lag.Update(new TValue(time.AddSeconds(1), 25.0), isNew: false);
        Assert.Equal(10.0, r2.Value, 1e-10); // delayed value unaffected by correcting the current bar

        var r3 = lag.Update(new TValue(time.AddSeconds(2), 99.0), isNew: true);
        Assert.Equal(25.0, r3.Value, 1e-10); // corrected value (25.0), not the rolled-back 20.0
    }

    [Fact]
    public void Lag_Reset_ClearsState()
    {
        var lag = new Lag(1);
        lag.Update(10.0);
        lag.Update(20.0);
        Assert.Equal(10.0, lag.Last.Value, 1e-10);

        lag.Reset();
        Assert.False(lag.IsHot);
        Assert.Equal(0.0, lag.Last.Value);
    }

    [Fact]
    public void Lag_Chaining_Constructor()
    {
        var source = new TSeries();
        var lag = new Lag(source, 1);

        var time = DateTime.UtcNow;
        source.Add(new TValue(time, 10.0), true);
        source.Add(new TValue(time.AddSeconds(1), 20.0), true);

        Assert.Equal(10.0, lag.Last.Value, 1e-10);
    }

    [Fact]
    public void Lag_Static_Batch_Span()
    {
        double[] source = [1.0, 2.0, 3.0, 4.0, 5.0];
        double[] output = new double[5];

        Lag.Batch(source, output, 2);

        Assert.Equal(0.0, output[0], 1e-10);
        Assert.Equal(0.0, output[1], 1e-10);
        Assert.Equal(1.0, output[2], 1e-10);
        Assert.Equal(2.0, output[3], 1e-10);
        Assert.Equal(3.0, output[4], 1e-10);
    }

    [Fact]
    public void Lag_Batch_Stream_Consistency()
    {
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        int n = 5;

        var batchResult = Lag.Batch(series, n);

        var stream = new Lag(n);
        for (int i = 0; i < series.Count; i++)
        {
            var r = stream.Update(series[i], true);
            Assert.Equal(batchResult[i].Value, r.Value, 1e-10);
        }
    }
}
