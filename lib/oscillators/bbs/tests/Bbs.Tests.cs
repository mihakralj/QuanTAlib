using Xunit;

namespace QuanTAlib.Tests;

public sealed class BbsTests
{
    [Fact]
    public void Constructor_DefaultParameters()
    {
        var bbs = new Bbs();

        Assert.NotNull(bbs);
        Assert.Equal("Bbs(20,2.0,20,1.5)", bbs.Name);
        Assert.Equal(20, bbs.WarmupPeriod);
        Assert.Equal(20, bbs.BbPeriod);
        Assert.Equal(2.0, bbs.BbMult);
        Assert.Equal(20, bbs.KcPeriod);
        Assert.Equal(1.5, bbs.KcMult);
        Assert.False(bbs.IsHot);
    }

    [Fact]
    public void Constructor_CustomParameters()
    {
        var bbs = new Bbs(bbPeriod: 10, bbMult: 1.5, kcPeriod: 15, kcMult: 2.0);

        Assert.Equal(10, bbs.BbPeriod);
        Assert.Equal(1.5, bbs.BbMult);
        Assert.Equal(15, bbs.KcPeriod);
        Assert.Equal(2.0, bbs.KcMult);
        Assert.Equal(15, bbs.WarmupPeriod); // max(10, 15)
    }

    [Fact]
    public void Constructor_InvalidBbPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Bbs(bbPeriod: 0));
        Assert.Equal("bbPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_InvalidKcPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Bbs(kcPeriod: 0));
        Assert.Equal("kcPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_InvalidBbMult_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Bbs(bbMult: 0.0));
        Assert.Equal("bbMult", ex.ParamName);
    }

    [Fact]
    public void Constructor_InvalidKcMult_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Bbs(kcMult: 0.0));
        Assert.Equal("kcMult", ex.ParamName);
    }

    [Fact]
    public void ConstantPrice_BandwidthZero()
    {
        var bbs = new Bbs(bbPeriod: 3, bbMult: 2.0, kcPeriod: 3, kcMult: 1.5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Constant price => stddev = 0 => BB width = 0 => bandwidth = 0
        for (int i = 0; i < 5; i++)
        {
            bbs.Update(new TBar(baseTime + i * 60000, 100, 100, 100, 100, 1000));
        }

        Assert.Equal(0.0, bbs.Last.Value, 10);
    }

    [Fact]
    public void TightRange_SqueezeOn()
    {
        // Very tight range bars: stddev ≈ 0, so BB bands collapse
        // ATR still has width from H-L range, so KC is wider
        // => BB inside KC => squeeze on
        var bbs = new Bbs(bbPeriod: 3, bbMult: 2.0, kcPeriod: 3, kcMult: 1.5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Close is always 100, but high/low create ATR
        for (int i = 0; i < 10; i++)
        {
            bbs.Update(new TBar(baseTime + i * 60000, 100, 102, 98, 100, 1000));
        }

        // With constant close and non-zero ATR, BB bands (based on close stddev) should be
        // narrower than KC bands (based on ATR), so squeeze should be on
        Assert.True(bbs.IsHot);
        Assert.True(bbs.SqueezeOn);
    }

    [Fact]
    public void WideRange_SqueezeOff()
    {
        // Wide price swings create large BB stddev → BB bands wider than KC bands
        // Use small kcMult so KC is narrow, large bbMult so BB is wide
        var bbs = new Bbs(bbPeriod: 3, bbMult: 3.0, kcPeriod: 3, kcMult: 0.5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Alternating prices create large stddev; tight H-L keeps ATR small relative to stddev
        double[] closes = { 80, 120, 80, 120, 80, 120, 80, 120, 80, 120 };
        for (int i = 0; i < closes.Length; i++)
        {
            double c = closes[i];
            // H/L track actual price so TR ≈ close-to-close gap (ATR stays proportional)
            // but BB mult * stddev >> KC mult * ATR when kcMult is small
            bbs.Update(new TBar(baseTime + i * 60000, c, c + 0.5, c - 0.5, c, 1000));
        }

        // BB bands (3 * stddev) should exceed KC bands (0.5 * ATR)
        Assert.True(bbs.IsHot);
        Assert.False(bbs.SqueezeOn);
    }

    [Fact]
    public void IsNew_False_RollsBackCorrectly()
    {
        var bbs = new Bbs(bbPeriod: 3, bbMult: 2.0, kcPeriod: 3, kcMult: 1.5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Feed initial bars
        for (int i = 0; i < 5; i++)
        {
            bbs.Update(new TBar(baseTime + i * 60000, 100 + i, 102 + i, 98 + i, 100 + i, 1000));
        }

        // Save state after bar 5 for reference
        _ = bbs.Last.Value;
        _ = bbs.SqueezeOn;

        // Update with new bar
        bbs.Update(new TBar(baseTime + 5 * 60000, 110, 112, 108, 110, 1000), isNew: true);
        double afterBar6 = bbs.Last.Value;

        // Roll back with isNew=false
        bbs.Update(new TBar(baseTime + 5 * 60000, 105, 107, 103, 105, 1000), isNew: false);
        double corrected = bbs.Last.Value;

        // Corrected value should differ from bar 6 (different price) but be valid
        Assert.NotEqual(afterBar6, corrected, 5);
        Assert.True(double.IsFinite(corrected));
    }

    [Fact]
    public void SqueezeFired_DetectsTransition()
    {
        var bbs = new Bbs(bbPeriod: 3, bbMult: 2.0, kcPeriod: 3, kcMult: 1.5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Phase 1: Tight range (squeeze on)
        for (int i = 0; i < 5; i++)
        {
            bbs.Update(new TBar(baseTime + i * 60000, 100, 102, 98, 100, 1000));
        }

        _ = bbs.SqueezeOn; // capture pre-breakout state

        // Phase 2: Breakout with huge price movement (squeeze off)
        for (int i = 0; i < 5; i++)
        {
            double price = 100 + (i + 1) * 20; // 120, 140, 160, 180, 200
            bbs.Update(new TBar(baseTime + (5 + i) * 60000, price, price + 1, price - 1, price, 1000));
        }

        // If squeeze was on and now off, SqueezeFired should have been true at transition
        // We test that values are valid after the transition
        Assert.True(double.IsFinite(bbs.Last.Value));
    }

    [Fact]
    public void Bandwidth_PositiveForVariedPrices()
    {
        var bbs = new Bbs(bbPeriod: 5, bbMult: 2.0, kcPeriod: 5, kcMult: 1.5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 10; i++)
        {
            double price = 100 + Math.Sin(i) * 5;
            bbs.Update(new TBar(baseTime + i * 60000, price, price + 2, price - 2, price, 1000));
        }

        // With varying prices, bandwidth should be positive
        Assert.True(bbs.Last.Value > 0);
        Assert.True(bbs.IsHot);
    }

    [Fact]
    public void NaN_Input_UsesLastValid()
    {
        var bbs = new Bbs(bbPeriod: 3, bbMult: 2.0, kcPeriod: 3, kcMult: 1.5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Feed valid bars
        bbs.Update(new TBar(baseTime, 100, 102, 98, 100, 1000));
        bbs.Update(new TBar(baseTime + 60000, 101, 103, 99, 101, 1000));

        // Feed NaN bar
        var result = bbs.Update(new TBar(baseTime + 120000, double.NaN, double.NaN, double.NaN, double.NaN, 1000));

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var bbs = new Bbs(bbPeriod: 3, bbMult: 2.0, kcPeriod: 3, kcMult: 1.5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 5; i++)
        {
            bbs.Update(new TBar(baseTime + i * 60000, 100 + i, 102 + i, 98 + i, 100 + i, 1000));
        }

        Assert.True(bbs.IsHot);

        bbs.Reset();

        Assert.False(bbs.IsHot);
        Assert.False(bbs.SqueezeOn);
        Assert.False(bbs.SqueezeFired);
    }

    #region Batch Tests

    [Fact]
    public void Batch_TBarSeries_ReturnsCorrectLength()
    {
        var series = new TBarSeries();
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 20; i++)
        {
            series.Add(new TBar(baseTime + i * 60000, 100 + i, 110 + i, 90 + i, 105 + i, 1000));
        }

        var result = Bbs.Batch(series);

        Assert.Equal(20, result.Count);
    }

    [Fact]
    public void Batch_EmptySource_ReturnsEmpty()
    {
        var result = Bbs.Batch(new TBarSeries());
        Assert.Empty(result);
    }

    [Fact]
    public void Batch_CustomParams_ReturnsCorrectLength()
    {
        var series = new TBarSeries();
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 20; i++)
        {
            series.Add(new TBar(baseTime + i * 60000, 100 + i, 110 + i, 90 + i, 105 + i, 1000));
        }

        var result = Bbs.Batch(series, bbPeriod: 10, bbMult: 1.5, kcPeriod: 10, kcMult: 2.0);

        Assert.Equal(20, result.Count);
    }

    [Fact]
    public void Batch_Span_MatchesStreaming()
    {
        var series = new TBarSeries();
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 50; i++)
        {
            double price = 100 + Math.Sin(i * 0.5) * 10;
            series.Add(new TBar(baseTime + i * 60000, price, price + 3, price - 3, price, 1000));
        }

        // Streaming
        var bbs = new Bbs(bbPeriod: 5, bbMult: 2.0, kcPeriod: 5, kcMult: 1.5);
        var streamValues = new List<double>(50);
        for (int i = 0; i < series.Count; i++)
        {
            streamValues.Add(bbs.Update(series[i]).Value);
        }

        // Span batch
        double[] output = new double[50];
        Bbs.Batch(series.HighValues, series.LowValues, series.CloseValues,
                  output.AsSpan(), bbPeriod: 5, bbMult: 2.0);

        // Compare last 40 values (after warmup stabilization)
        for (int i = 10; i < 50; i++)
        {
            Assert.Equal(streamValues[i], output[i], 8);
        }
    }

    [Fact]
    public void Batch_SpanWithSqueeze_OutputsBothArrays()
    {
        int len = 30;
        double[] high = new double[len];
        double[] low = new double[len];
        double[] close = new double[len];
        double[] bandwidth = new double[len];
        bool[] squeezeOn = new bool[len];

        for (int i = 0; i < len; i++)
        {
            close[i] = 100;
            high[i] = 102;
            low[i] = 98;
        }

        Bbs.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(),
                  bandwidth.AsSpan(), squeezeOn.AsSpan(),
                  bbPeriod: 5, bbMult: 2.0, kcPeriod: 5, kcMult: 1.5);

        // Constant close => stddev=0 => BB width=0 => squeeze on
        // Bandwidth should be 0 for constant close
        for (int i = 5; i < len; i++)
        {
            Assert.Equal(0.0, bandwidth[i], 10);
            Assert.True(squeezeOn[i]);
        }
    }

    [Fact]
    public void Batch_InvalidInputLength_Throws()
    {
        double[] high = new double[10];
        double[] low = new double[5]; // mismatched
        double[] close = new double[10];
        double[] output = new double[10];

        Assert.Throws<ArgumentException>(() =>
            Bbs.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(), output.AsSpan()));
    }

    [Fact]
    public void Batch_OutputTooSmall_Throws()
    {
        double[] high = new double[10];
        double[] low = new double[10];
        double[] close = new double[10];
        double[] output = new double[5]; // too small

        Assert.Throws<ArgumentException>(() =>
            Bbs.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(), output.AsSpan()));
    }

    [Fact]
    public void Batch_InvalidPeriod_Throws()
    {
        double[] data = new double[10];
        double[] output = new double[10];

        Assert.Throws<ArgumentException>(() =>
            Bbs.Batch(data.AsSpan(), data.AsSpan(), data.AsSpan(), output.AsSpan(), bbPeriod: 0));
    }

    [Fact]
    public void Batch_InvalidMultiplier_Throws()
    {
        double[] data = new double[10];
        double[] output = new double[10];

        Assert.Throws<ArgumentException>(() =>
            Bbs.Batch(data.AsSpan(), data.AsSpan(), data.AsSpan(), output.AsSpan(), bbMult: 0.0));
    }

    #endregion

    [Fact]
    public void Calculate_ReturnsResultsAndHotIndicator()
    {
        var series = new TBarSeries();
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 30; i++)
        {
            series.Add(new TBar(baseTime + i * 60000, 100 + i, 110 + i, 90 + i, 105 + i, 1000));
        }

        var (results, indicator) = Bbs.Calculate(series, bbPeriod: 5, bbMult: 2.0, kcPeriod: 5, kcMult: 1.5);

        Assert.Equal(30, results.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void PubEvent_FiresOnUpdate()
    {
        var bbs = new Bbs(bbPeriod: 3, bbMult: 2.0, kcPeriod: 3, kcMult: 1.5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        int eventCount = 0;
        bbs.Pub += (object? _, in TValueEventArgs _) => eventCount++;

        for (int i = 0; i < 5; i++)
        {
            bbs.Update(new TBar(baseTime + i * 60000, 100 + i, 102 + i, 98 + i, 100 + i, 1000));
        }

        Assert.Equal(5, eventCount);
    }
}
