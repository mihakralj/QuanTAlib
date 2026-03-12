namespace QuanTAlib;

public class RainTests
{
    [Fact]
    public void Constructor_InvalidPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Rain(0));
        Assert.Equal("period", ex.ParamName);

        var ex2 = Assert.Throws<ArgumentException>(() => new Rain(-1));
        Assert.Equal("period", ex2.ParamName);
    }

    [Fact]
    public void Constructor_ValidPeriod_SetsProperties()
    {
        var rain = new Rain(5);

        Assert.Equal("Rain(5)", rain.Name);
        Assert.Equal(50, rain.WarmupPeriod); // 5 * 10 layers
        Assert.False(rain.IsHot);
    }

    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var rain = new Rain(2);
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            rain.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        Assert.True(double.IsFinite(rain.Last.Value));
    }

    [Fact]
    public void IsHot_FlipsAfterWarmup()
    {
        const int period = 3;
        var rain = new Rain(period);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            rain.Update(new TValue(bars[i].Time, bars[i].Close));

            if (i < period - 1)
            {
                // First layer not yet hot
                Assert.False(rain.IsHot);
            }
        }

        // After 100 bars with period=3, all layers should be hot
        Assert.True(rain.IsHot);
    }

    [Fact]
    public void IsNew_BarCorrection_Works()
    {
        var rain = new Rain(5);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            rain.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        // Update with 100th point (isNew=true)
        rain.Update(new TValue(bars[99].Time, bars[99].Close), true);

        // Update with modified 100th point (isNew=false)
        var val2 = rain.Update(new TValue(bars[99].Time, bars[99].Close + 1.0), false);

        // Create new instance and feed up to modified
        var rain2 = new Rain(5);
        for (int i = 0; i < 99; i++)
        {
            rain2.Update(new TValue(bars[i].Time, bars[i].Close));
        }
        var val3 = rain2.Update(new TValue(bars[99].Time, bars[99].Close + 1.0), true);

        Assert.Equal(val3.Value, val2.Value, 1e-9);
    }

    [Fact]
    public void IterativeCorrection_RestoresState()
    {
        var rain = new Rain(3);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed all bars
        for (int i = 0; i < bars.Count; i++)
        {
            rain.Update(new TValue(bars[i].Time, bars[i].Close));
        }
        double afterAll = rain.Last.Value;

        // Now update last bar with isNew=false using same value
        rain.Update(new TValue(bars[^1].Time, bars[^1].Close), false);
        double afterCorrection = rain.Last.Value;

        Assert.Equal(afterAll, afterCorrection, 1e-12);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var rain = new Rain(5);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            rain.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        Assert.True(rain.IsHot);

        rain.Reset();

        Assert.False(rain.IsHot);
        Assert.Equal(default, rain.Last);
    }

    [Fact]
    public void NaN_HandledGracefully()
    {
        var rain = new Rain(3);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed some valid data first
        for (int i = 0; i < 20; i++)
        {
            rain.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        // Feed NaN
        rain.Update(new TValue(bars[20].Time, double.NaN));
        Assert.True(double.IsFinite(rain.Last.Value));

        // Feed Infinity
        rain.Update(new TValue(bars[21].Time, double.PositiveInfinity));
        Assert.True(double.IsFinite(rain.Last.Value));
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        var rain = new Rain(3);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed some valid, then batch of NaN
        for (int i = 0; i < 10; i++)
        {
            rain.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        for (int i = 10; i < 15; i++)
        {
            rain.Update(new TValue(bars[i].Time, double.NaN));
        }

        for (int i = 15; i < 50; i++)
        {
            rain.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        Assert.True(double.IsFinite(rain.Last.Value));
    }

    [Fact]
    public void ModeConsistency_BatchMatchesStreaming()
    {
        const int period = 3;
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Build TSeries from bars
        var series = new TSeries();
        for (int i = 0; i < bars.Count; i++)
        {
            series.Add(new TValue(bars[i].Time, bars[i].Close));
        }

        // Mode 1: Streaming
        var rain1 = new Rain(period);
        for (int i = 0; i < bars.Count; i++)
        {
            rain1.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        // Mode 2: Batch (TSeries)
        var batchResult = Rain.Batch(series, period);

        // Mode 3: Span
        Span<double> spanOut = new double[series.Count];
        Rain.Batch(series.Values, spanOut, period);

        // All should match at the last value
        Assert.Equal(rain1.Last.Value, batchResult[^1].Value, 1e-9);
        Assert.Equal(rain1.Last.Value, spanOut[^1], 1e-9);
    }

    [Fact]
    public void ModeConsistency_EventMatchesStreaming()
    {
        const int period = 3;
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var series = new TSeries();
        var rain = new Rain(series, period);

        // Feed via events
        for (int i = 0; i < bars.Count; i++)
        {
            series.Add(new TValue(bars[i].Time, bars[i].Close));
        }

        // Create fresh streaming
        var rain2 = new Rain(period);
        for (int i = 0; i < bars.Count; i++)
        {
            rain2.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        Assert.Equal(rain2.Last.Value, rain.Last.Value, 1e-9);
    }

    [Fact]
    public void SpanBatch_ArgumentValidation()
    {
        double[] src = new double[10];
        double[] output = new double[5]; // Wrong length

        var ex = Assert.Throws<ArgumentException>(() => Rain.Batch((ReadOnlySpan<double>)src, output, 3));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_InvalidPeriod_Throws()
    {
        double[] src = new double[10];
        double[] output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() => Rain.Batch((ReadOnlySpan<double>)src, output, 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_EmptyInput_NoOp()
    {
        Span<double> src = [];
        Span<double> output = [];

        Rain.Batch((ReadOnlySpan<double>)src, output, 3); // Should not throw
        Assert.True(true); // Explicit assertion for S2699
    }

    [Fact]
    public void SpanBatch_MatchesTSeries()
    {
        const int period = 4;
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var series = new TSeries();
        for (int i = 0; i < bars.Count; i++)
        {
            series.Add(new TValue(bars[i].Time, bars[i].Close));
        }

        var batchResult = Rain.Batch(series, period);

        Span<double> spanOut = new double[series.Count];
        Rain.Batch(series.Values, spanOut, period);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, spanOut[i], 1e-9);
        }
    }

    [Fact]
    public void Chainability_PubFires()
    {
        var rain = new Rain(3);
        int pubCount = 0;
        rain.Pub += Handler;

        // skipcq: CS-R1140 - S2123 false positive: pubCount is captured and read below
        void Handler(object? sender, in TValueEventArgs args) { pubCount++; }

        var gbm = new GBM();
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            rain.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        Assert.Equal(10, pubCount);
    }

    [Fact]
    public void Calculate_ReturnsHotIndicator()
    {
        const int period = 2;
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var series = new TSeries();
        for (int i = 0; i < bars.Count; i++)
        {
            series.Add(new TValue(bars[i].Time, bars[i].Close));
        }

        var (results, indicator) = Rain.Calculate(series, period);

        Assert.Equal(series.Count, results.Count);
        Assert.True(indicator.IsHot);
        Assert.Equal(results[^1].Value, indicator.Last.Value, 1e-12);
    }

    [Fact]
    public void Period1_ReturnsPriceItself()
    {
        // With period=1, each SMA(x, 1) = x, so all 10 layers return the input.
        // Weighted average of same value = that value.
        var rain = new Rain(1);
        var gbm = new GBM();
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            var result = rain.Update(new TValue(bars[i].Time, bars[i].Close));
            Assert.Equal(bars[i].Close, result.Value, 1e-9);
        }
    }

    [Fact]
    public void SpanBatch_LargeData_NoStackOverflow()
    {
        const int period = 10;
        const int size = 10000;
        var gbm = new GBM();
        var bars = gbm.Fetch(size, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] src = new double[size];
        double[] output = new double[size];

        for (int i = 0; i < size; i++)
        {
            src[i] = bars[i].Close;
        }

        Rain.Batch((ReadOnlySpan<double>)src, output, period);

        Assert.True(double.IsFinite(output[^1]));
    }

    [Fact]
    public void Dispose_UnsubscribesFromSource()
    {
        var series = new TSeries();
        var rain = new Rain(series, 3);

        rain.Dispose();

        // Adding to series after dispose should not affect the disposed indicator
        var gbm = new GBM();
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double lastValue = rain.Last.Value;

        for (int i = 0; i < bars.Count; i++)
        {
            series.Add(new TValue(bars[i].Time, bars[i].Close));
        }

        Assert.Equal(lastValue, rain.Last.Value);
    }
}
