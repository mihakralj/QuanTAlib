using Xunit;

namespace QuanTAlib.Tests;

public sealed class BbbTests
{
    [Fact]
    public void Constructor_ValidParameters()
    {
        var bbb = new Bbb(period: 20, multiplier: 2.0);

        Assert.NotNull(bbb);
        Assert.Equal("Bbb(20,2.0)", bbb.Name);
        Assert.Equal(20, bbb.WarmupPeriod);
        Assert.False(bbb.IsHot);
    }

    [Fact]
    public void Constructor_InvalidPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Bbb(period: 0, multiplier: 2.0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_InvalidMultiplier_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Bbb(period: 20, multiplier: 0.0));
        Assert.Equal("multiplier", ex.ParamName);
    }

    [Fact]
    public void ZeroWidth_ReturnsNeutral()
    {
        var bbb = new Bbb(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        bbb.Update(new TValue(time, 10.0), isNew: true);
        bbb.Update(new TValue(time.AddSeconds(1), 10.0), isNew: true);
        var result = bbb.Update(new TValue(time.AddSeconds(2), 10.0), isNew: true);

        Assert.Equal(0.5, result.Value, 10);
    }

    [Fact]
    public void PercentB_AtMiddle_IsHalf()
    {
        var bbb = new Bbb(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        // Window [0, 3, 1.5] has mean 1.5 and non-zero stddev.
        bbb.Update(new TValue(time, 0.0), isNew: true);
        bbb.Update(new TValue(time.AddSeconds(1), 3.0), isNew: true);
        var result = bbb.Update(new TValue(time.AddSeconds(2), 1.5), isNew: true);

        Assert.Equal(0.5, result.Value, 10);
    }

    [Fact]
    public void IsNew_False_RollsBackCorrectly()
    {
        var bbb = new Bbb(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        bbb.Update(new TValue(time, 10.0), isNew: true);
        bbb.Update(new TValue(time.AddSeconds(1), 12.0), isNew: true);
        bbb.Update(new TValue(time.AddSeconds(2), 14.0), isNew: true);
        double before = bbb.Last.Value;

        bbb.Update(new TValue(time.AddSeconds(2), 15.0), isNew: false);
        double after = bbb.Last.Value;

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void NaN_HandledGracefully()
    {
        var bbb = new Bbb(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        bbb.Update(new TValue(time, 10.0), isNew: true);
        bbb.Update(new TValue(time.AddSeconds(1), 12.0), isNew: true);
        bbb.Update(new TValue(time.AddSeconds(2), double.NaN), isNew: true);

        Assert.True(double.IsFinite(bbb.Last.Value));
    }

    [Fact]
    public void Infinity_HandledGracefully()
    {
        var bbb = new Bbb(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        bbb.Update(new TValue(time, 10.0), isNew: true);
        bbb.Update(new TValue(time.AddSeconds(1), 12.0), isNew: true);
        bbb.Update(new TValue(time.AddSeconds(2), double.PositiveInfinity), isNew: true);

        Assert.True(double.IsFinite(bbb.Last.Value));
    }

    [Fact]
    public void WarmupPeriod_IsHotTransition()
    {
        var bbb = new Bbb(period: 5, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        for (int i = 0; i < 4; i++)
        {
            bbb.Update(new TValue(time.AddSeconds(i), 10.0 + i));
            Assert.False(bbb.IsHot);
        }

        bbb.Update(new TValue(time.AddSeconds(4), 14.0));
        Assert.True(bbb.IsHot);
    }

    [Fact]
    public void UpdateTSeries_ReturnsValidSeries()
    {
        int period = 5;
        var bbb = new Bbb(period, multiplier: 2.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        TSeries result = bbb.Update(source);

        Assert.Equal(source.Count, result.Count);
        Assert.True(bbb.IsHot);

        var streaming = new Bbb(period, multiplier: 2.0);
        for (int i = Math.Max(0, source.Count - period); i < source.Count; i++)
        {
            streaming.Update(source[i], isNew: true);
        }
        Assert.Equal(streaming.Last.Value, result[^1].Value, 8);
    }

    [Fact]
    public void Batch_MatchesStreaming()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 7);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        var streaming = new Bbb(period: 20, multiplier: 2.0);
        foreach (var item in source)
        {
            streaming.Update(item);
        }

        TSeries batch = Bbb.Batch(source, period: 20, multiplier: 2.0);

        Assert.Equal(batch[^1].Value, streaming.Last.Value, 8);
    }

    [Fact]
    public void SpanBatch_EmptyArrays_DoesNotThrow()
    {
        double[] source = [];
        double[] output = [];

        var ex = Record.Exception(() => Bbb.Batch(source.AsSpan(), output.AsSpan(), 20, 2.0));
        Assert.Null(ex);
    }

    [Fact]
    public void SpanBatch_InvalidLength_Throws()
    {
        double[] source = new double[10];
        double[] output = new double[9];

        var ex = Assert.Throws<ArgumentException>(() => Bbb.Batch(source.AsSpan(), output.AsSpan(), 20, 2.0));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_InvalidPeriod_Throws()
    {
        double[] source = new double[10];
        double[] output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() => Bbb.Batch(source.AsSpan(), output.AsSpan(), 0, 2.0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_InvalidMultiplier_Throws()
    {
        double[] source = new double[10];
        double[] output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() => Bbb.Batch(source.AsSpan(), output.AsSpan(), 20, 0.0));
        Assert.Equal("multiplier", ex.ParamName);
    }

    [Fact]
    public void Calculate_ReturnsResultsAndHotIndicator()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        var (results, indicator) = Bbb.Calculate(source, period: 5, multiplier: 2.0);

        Assert.Equal(50, results.Count);
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(indicator.Last.Value));
    }
}
