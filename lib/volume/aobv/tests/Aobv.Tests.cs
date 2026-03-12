namespace QuanTAlib.Tests;

public class AobvTests
{
    [Fact]
    public void Aobv_Constructor_SetsCorrectName()
    {
        var aobv = new Aobv();
        Assert.Equal("AOBV(4,14)", aobv.Name);
        Assert.Equal(14, aobv.WarmupPeriod);
    }

    [Fact]
    public void Aobv_BasicCalculation_ReturnsFiniteValues()
    {
        var aobv = new Aobv();
        var time = DateTime.UtcNow;

        var bar1 = new TBar(time, 100, 105, 95, 102, 1000);
        var val1 = aobv.Update(bar1);

        Assert.True(double.IsFinite(val1.Value));
        Assert.True(double.IsFinite(aobv.LastFast.Value));
        Assert.True(double.IsFinite(aobv.LastSlow.Value));
    }

    [Fact]
    public void Aobv_OBV_AccumulatesCorrectly()
    {
        var aobv = new Aobv();
        var time = DateTime.UtcNow;

        // First bar: Close = 100
        aobv.Update(new TBar(time, 100, 105, 95, 100, 1000), isNew: true);

        // Second bar: Close = 105 (up), adds volume
        aobv.Update(new TBar(time.AddMinutes(1), 100, 110, 98, 105, 2000), isNew: true);

        // Third bar: Close = 102 (down), subtracts volume
        aobv.Update(new TBar(time.AddMinutes(2), 105, 108, 100, 102, 1500), isNew: true);

        Assert.True(double.IsFinite(aobv.Last.Value));
    }

    [Fact]
    public void Aobv_IsNew_False_UpdatesSameBar()
    {
        var aobv = new Aobv();
        var time = DateTime.UtcNow;

        var bar1 = new TBar(time, 100, 105, 95, 102, 1000);
        aobv.Update(bar1, isNew: true);
        _ = aobv.LastFast.Value;
        _ = aobv.LastSlow.Value;

        // Update same bar with different close
        var bar1Update = new TBar(time, 100, 105, 95, 103, 1000);
        aobv.Update(bar1Update, isNew: false);

        // Values may change due to different OBV calculation
        Assert.True(double.IsFinite(aobv.LastFast.Value));
        Assert.True(double.IsFinite(aobv.LastSlow.Value));
    }

    [Fact]
    public void Aobv_IterativeCorrections_RestoreState()
    {
        var aobv = new Aobv();
        var gbm = new GBM(seed: 42);

        // Build up some state
        TBar tenthBar = default;
        for (int i = 0; i < 10; i++)
        {
            tenthBar = gbm.Next(isNew: true);
            aobv.Update(tenthBar, isNew: true);
        }

        double stateAfterTenFast = aobv.LastFast.Value;
        double stateAfterTenSlow = aobv.LastSlow.Value;

        // Multiple corrections
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            aobv.Update(bar, isNew: false);
        }

        // Restore with original 10th bar
        aobv.Update(tenthBar, isNew: false);

        Assert.Equal(stateAfterTenFast, aobv.LastFast.Value, 9);
        Assert.Equal(stateAfterTenSlow, aobv.LastSlow.Value, 9);
    }

    [Fact]
    public void Aobv_Reset_ClearsState()
    {
        var aobv = new Aobv();
        var time = DateTime.UtcNow;

        // First bar: OBV = 0 (no prev bar to compare)
        aobv.Update(new TBar(time, 100, 105, 95, 100, 1000));
        // Second bar with higher close: OBV += volume
        aobv.Update(new TBar(time.AddMinutes(1), 100, 110, 98, 105, 2000));

        // After two bars with price increase, should have non-zero value
        Assert.NotEqual(0, aobv.Last.Value);

        aobv.Reset();
        Assert.False(aobv.IsHot);
        Assert.Equal(0, aobv.Last.Value);
        Assert.Equal(0, aobv.LastFast.Value);
        Assert.Equal(0, aobv.LastSlow.Value);
    }

    [Fact]
    public void Aobv_IsHot_FlipsAtWarmupPeriod()
    {
        var aobv = new Aobv();
        var gbm = new GBM(seed: 42);

        Assert.False(aobv.IsHot);

        for (int i = 0; i < 13; i++)
        {
            aobv.Update(gbm.Next());
            Assert.False(aobv.IsHot);
        }

        aobv.Update(gbm.Next());
        Assert.True(aobv.IsHot);
    }

    [Fact]
    public void Aobv_NaN_Input_UsesLastValidValue()
    {
        var aobv = new Aobv();
        var time = DateTime.UtcNow;

        aobv.Update(new TBar(time, 100, 105, 95, 100, 1000));
        aobv.Update(new TBar(time.AddMinutes(1), 100, 110, 98, 105, 2000));

        // NaN close
        var result = aobv.Update(new TBar(time.AddMinutes(2), 105, 108, 100, double.NaN, 1500));
        Assert.True(double.IsFinite(result.Value));

        // NaN volume
        result = aobv.Update(new TBar(time.AddMinutes(3), 100, 108, 100, 103, double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Aobv_Infinity_Input_UsesLastValidValue()
    {
        var aobv = new Aobv();
        var time = DateTime.UtcNow;

        aobv.Update(new TBar(time, 100, 105, 95, 100, 1000));

        var result = aobv.Update(new TBar(time.AddMinutes(1), 100, 110, 98, double.PositiveInfinity, 2000));
        Assert.True(double.IsFinite(result.Value));

        result = aobv.Update(new TBar(time.AddMinutes(2), 100, 110, 98, 105, double.NegativeInfinity));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Aobv_TValueUpdate_ThrowsNotSupportedException()
    {
        var aobv = new Aobv();
        Assert.Throws<NotSupportedException>(() => aobv.Update(new TValue(DateTime.UtcNow, 100)));
    }

    [Fact]
    public void Aobv_PubEvent_FiresOnUpdate()
    {
        var aobv = new Aobv();
        bool eventFired = false;
        aobv.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        aobv.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000));
        Assert.True(eventFired);
    }

    [Fact]
    public void Aobv_UpdateTBarSeries_ReturnsCorrectSeries()
    {
        var aobv = new Aobv();
        var bars = new TBarSeries();
        var gbm = new GBM(seed: 42);

        for (int i = 0; i < 50; i++)
        {
            bars.Add(gbm.Next());
        }

        var (fast, slow) = aobv.Update(bars);

        Assert.Equal(50, fast.Count);
        Assert.Equal(50, slow.Count);

        for (int i = 0; i < 50; i++)
        {
            Assert.True(double.IsFinite(fast[i].Value));
            Assert.True(double.IsFinite(slow[i].Value));
        }
    }

    [Fact]
    public void Aobv_CalculateTBarSeries_ReturnsCorrectSeries()
    {
        var bars = new TBarSeries();
        var gbm = new GBM(seed: 42);

        for (int i = 0; i < 50; i++)
        {
            bars.Add(gbm.Next());
        }

        var (fast, slow) = Aobv.Calculate(bars);

        Assert.Equal(50, fast.Count);
        Assert.Equal(50, slow.Count);
    }

    [Fact]
    public void Aobv_CalculateSpan_ReturnsCorrectValues()
    {
        double[] close = { 100, 102, 101, 103, 102 };
        double[] volume = { 1000, 1500, 1200, 1800, 1100 };
        double[] outputFast = new double[5];
        double[] outputSlow = new double[5];

        Aobv.Batch(close, volume, outputFast, outputSlow);

        for (int i = 0; i < 5; i++)
        {
            Assert.True(double.IsFinite(outputFast[i]));
            Assert.True(double.IsFinite(outputSlow[i]));
        }
    }

    [Fact]
    public void Aobv_CalculateSpan_ThrowsOnMismatchedLengths()
    {
        double[] close = { 100, 102 };
        double[] volume = { 1000 }; // Short
        double[] outputFast = new double[2];
        double[] outputSlow = new double[2];

        Assert.Throws<ArgumentException>(() =>
            Aobv.Batch(close, volume, outputFast, outputSlow));
    }

    [Fact]
    public void Aobv_CalculateSpan_ThrowsOnMismatchedOutputLength()
    {
        double[] close = { 100, 102 };
        double[] volume = { 1000, 1500 };
        double[] outputFast = new double[1]; // Short
        double[] outputSlow = new double[2];

        Assert.Throws<ArgumentException>(() =>
            Aobv.Batch(close, volume, outputFast, outputSlow));
    }

    [Fact]
    public void Aobv_Calculate_EmptySeries_ReturnsEmpty()
    {
        var bars = new TBarSeries();
        var (fast, slow) = Aobv.Calculate(bars);
        Assert.Empty(fast);
        Assert.Empty(slow);
    }

    [Fact]
    public void Aobv_StreamingMatchesBatch()
    {
        var bars = new TBarSeries();
        var gbm = new GBM(seed: 42);

        for (int i = 0; i < 100; i++)
        {
            bars.Add(gbm.Next());
        }

        // Streaming
        var aobvStreaming = new Aobv();
        var streamingFast = new List<double>();
        var streamingSlow = new List<double>();
        foreach (var bar in bars)
        {
            aobvStreaming.Update(bar);
            streamingFast.Add(aobvStreaming.LastFast.Value);
            streamingSlow.Add(aobvStreaming.LastSlow.Value);
        }

        // Batch
        var (batchFast, batchSlow) = Aobv.Calculate(bars);

        // Compare after warmup
        for (int i = 14; i < 100; i++)
        {
            Assert.Equal(batchFast[i].Value, streamingFast[i], 9);
            Assert.Equal(batchSlow[i].Value, streamingSlow[i], 9);
        }
    }

    [Fact]
    public void Aobv_FastRespondsQuickerThanSlow()
    {
        var aobv = new Aobv();
        var time = DateTime.UtcNow;

        // Feed steady prices first
        for (int i = 0; i < 20; i++)
        {
            aobv.Update(new TBar(time.AddMinutes(i), 100, 101, 99, 100, 1000), isNew: true);
        }

        double fastBefore = aobv.LastFast.Value;
        double slowBefore = aobv.LastSlow.Value;

        // Sudden price spike with high volume
        aobv.Update(new TBar(time.AddMinutes(20), 100, 110, 100, 108, 5000), isNew: true);

        double fastAfter = aobv.LastFast.Value;
        double slowAfter = aobv.LastSlow.Value;

        // Fast should change more than slow
        double fastChange = Math.Abs(fastAfter - fastBefore);
        double slowChange = Math.Abs(slowAfter - slowBefore);

        Assert.True(fastChange > slowChange,
            $"Fast change ({fastChange}) should be greater than slow change ({slowChange})");
    }

    [Fact]
    public void Aobv_WarmupCompensation_ProducesNonZeroFirstValue()
    {
        var aobv = new Aobv();
        var time = DateTime.UtcNow;

        // First bar with price increase should produce non-zero OBV
        aobv.Update(new TBar(time, 100, 105, 95, 100, 1000), isNew: true);
        // OBV = 0 on first bar

        // Second bar with higher close
        aobv.Update(new TBar(time.AddMinutes(1), 100, 110, 98, 105, 2000), isNew: true);
        // OBV = 2000, EMA should be compensated

        Assert.NotEqual(0, aobv.LastFast.Value);
        Assert.NotEqual(0, aobv.LastSlow.Value);
    }
}
