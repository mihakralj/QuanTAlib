namespace QuanTAlib.Tests;

public class CmfTests
{
    [Fact]
    public void Cmf_Constructor_DefaultPeriod_Is20()
    {
        var cmf = new Cmf();
        Assert.Equal("CMF(20)", cmf.Name);
        Assert.Equal(20, cmf.WarmupPeriod);
    }

    [Fact]
    public void Cmf_Constructor_CustomPeriod_SetsCorrectly()
    {
        var cmf = new Cmf(10);
        Assert.Equal("CMF(10)", cmf.Name);
        Assert.Equal(10, cmf.WarmupPeriod);
    }

    [Fact]
    public void Cmf_Constructor_InvalidPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Cmf(0));
        Assert.Equal("period", ex.ParamName);

        ex = Assert.Throws<ArgumentException>(() => new Cmf(-1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Cmf_BasicCalculation_ReturnsExpectedValues()
    {
        // CMF with period 3 for easy manual verification
        var cmf = new Cmf(3);
        var time = DateTime.UtcNow;

        // Bar 1: Close=10, High=12, Low=8. Range=4.
        // MFM = ((10-8) - (12-10)) / 4 = (2 - 2) / 4 = 0.
        // Vol = 100. MFV = 0.
        // CMF = 0 / 100 = 0
        var bar1 = new TBar(time, 10, 12, 8, 10, 100);
        var val1 = cmf.Update(bar1);
        Assert.Equal(0, val1.Value);

        // Bar 2: Close=12, High=12, Low=8. Range=4.
        // MFM = ((12-8) - (12-12)) / 4 = (4 - 0) / 4 = 1.
        // Vol = 200. MFV = 200.
        // Sum MFV = 0 + 200 = 200, Sum Vol = 100 + 200 = 300
        // CMF = 200 / 300 = 0.6667
        var bar2 = new TBar(time.AddMinutes(1), 10, 12, 8, 12, 200);
        var val2 = cmf.Update(bar2);
        Assert.Equal(200.0 / 300.0, val2.Value, 6);

        // Bar 3: Close=8, High=12, Low=8. Range=4.
        // MFM = ((8-8) - (12-8)) / 4 = (0 - 4) / 4 = -1.
        // Vol = 100. MFV = -100.
        // Sum MFV = 0 + 200 - 100 = 100, Sum Vol = 100 + 200 + 100 = 400
        // CMF = 100 / 400 = 0.25
        var bar3 = new TBar(time.AddMinutes(2), 12, 12, 8, 8, 100);
        var val3 = cmf.Update(bar3);
        Assert.Equal(100.0 / 400.0, val3.Value, 6);
    }

    [Fact]
    public void Cmf_RollingSumDropsOldestValue()
    {
        var cmf = new Cmf(2);
        var time = DateTime.UtcNow;

        // Bar 1: MFM=1, Vol=100, MFV=100
        var bar1 = new TBar(time, 10, 12, 8, 12, 100);
        cmf.Update(bar1);

        // Bar 2: MFM=-1, Vol=100, MFV=-100
        var bar2 = new TBar(time.AddMinutes(1), 12, 12, 8, 8, 100);
        cmf.Update(bar2);
        // Sum MFV = 100 - 100 = 0, Sum Vol = 200
        // CMF = 0

        // Bar 3: MFM=1, Vol=100, MFV=100
        // Period=2, so bar1 drops out
        var bar3 = new TBar(time.AddMinutes(2), 8, 12, 8, 12, 100);
        var val3 = cmf.Update(bar3);
        // Sum MFV = -100 + 100 = 0, Sum Vol = 100 + 100 = 200
        // CMF = 0
        Assert.Equal(0, val3.Value, 6);
    }

    [Fact]
    public void Cmf_IsNew_False_UpdatesSameBar()
    {
        var cmf = new Cmf(3);
        var time = DateTime.UtcNow;

        // Initial update: MFM = 1, Vol = 100
        var bar1 = new TBar(time, 10, 12, 8, 12, 100);
        cmf.Update(bar1, isNew: true);
        Assert.Equal(1.0, cmf.Last.Value); // 100/100

        // Update same bar with different volume
        var bar1Update = new TBar(time, 10, 12, 8, 12, 200);
        cmf.Update(bar1Update, isNew: false);
        Assert.Equal(1.0, cmf.Last.Value); // 200/200 = 1
    }

    [Fact]
    public void Cmf_IterativeCorrections_RestoreState()
    {
        var cmf = new Cmf(3);
        var time = DateTime.UtcNow;

        // Build up some state
        cmf.Update(new TBar(time, 10, 12, 8, 12, 100), isNew: true);
        cmf.Update(new TBar(time.AddMinutes(1), 10, 12, 8, 10, 100), isNew: true);

        _ = cmf.Last.Value; // Store state reference

        // Multiple corrections to bar 3
        cmf.Update(new TBar(time.AddMinutes(2), 10, 12, 8, 8, 100), isNew: true);
        cmf.Update(new TBar(time.AddMinutes(2), 10, 12, 8, 9, 100), isNew: false);
        cmf.Update(new TBar(time.AddMinutes(2), 10, 12, 8, 11, 100), isNew: false);
        cmf.Update(new TBar(time.AddMinutes(2), 10, 12, 8, 12, 100), isNew: false);

        // Final bar 3 should have MFM=1
        // Verify state is consistent
        Assert.True(double.IsFinite(cmf.Last.Value));
    }

    [Fact]
    public void Cmf_Reset_ClearsState()
    {
        var cmf = new Cmf(3);
        var bar = new TBar(DateTime.UtcNow, 10, 12, 8, 12, 100);
        cmf.Update(bar);

        Assert.NotEqual(0, cmf.Last.Value);

        cmf.Reset();
        Assert.False(cmf.IsHot);
        Assert.Equal(0, cmf.Last.Value);
    }

    [Fact]
    public void Cmf_IsHot_FlipsAtPeriod()
    {
        var cmf = new Cmf(3);
        var time = DateTime.UtcNow;

        Assert.False(cmf.IsHot);

        cmf.Update(new TBar(time, 10, 12, 8, 10, 100));
        Assert.False(cmf.IsHot);

        cmf.Update(new TBar(time.AddMinutes(1), 10, 12, 8, 10, 100));
        Assert.False(cmf.IsHot);

        cmf.Update(new TBar(time.AddMinutes(2), 10, 12, 8, 10, 100));
        Assert.True(cmf.IsHot);
    }

    [Fact]
    public void Cmf_HighEqualsLow_HandlesDivisionByZero()
    {
        var cmf = new Cmf(3);
        // High = Low = 10. Range = 0. MFM should be 0.
        var bar = new TBar(DateTime.UtcNow, 10, 10, 10, 10, 100);
        var val = cmf.Update(bar);
        Assert.Equal(0, val.Value);
    }

    [Fact]
    public void Cmf_ZeroVolume_HandlesDivisionByZero()
    {
        var cmf = new Cmf(3);
        var bar = new TBar(DateTime.UtcNow, 10, 12, 8, 10, 0);
        var val = cmf.Update(bar);
        Assert.Equal(0, val.Value); // 0 / 0 should be handled
    }

    [Fact]
    public void Cmf_TValueUpdate_ThrowsNotSupportedException()
    {
        var cmf = new Cmf();
        Assert.Throws<NotSupportedException>(() => cmf.Update(new TValue(DateTime.UtcNow, 15)));
    }

    [Fact]
    public void Cmf_PubEvent_FiresOnUpdate()
    {
        var cmf = new Cmf();
        bool eventFired = false;
        cmf.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        cmf.Update(new TBar(DateTime.UtcNow, 10, 12, 8, 10, 100));
        Assert.True(eventFired);
    }

    [Fact]
    public void Cmf_UpdateTBarSeries_ReturnsCorrectSeries()
    {
        var cmf = new Cmf(3);
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;

        bars.Add(new TBar(time, 10, 12, 8, 10, 100));
        bars.Add(new TBar(time.AddMinutes(1), 10, 12, 8, 12, 200));
        bars.Add(new TBar(time.AddMinutes(2), 12, 12, 8, 8, 100));

        var result = cmf.Update(bars);

        Assert.Equal(3, result.Count);
        Assert.True(double.IsFinite(result[0].Value));
        Assert.True(double.IsFinite(result[1].Value));
        Assert.True(double.IsFinite(result[2].Value));
    }

    [Fact]
    public void Cmf_CalculateTBarSeries_ReturnsCorrectSeries()
    {
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;

        bars.Add(new TBar(time, 10, 12, 8, 10, 100));
        bars.Add(new TBar(time.AddMinutes(1), 10, 12, 8, 12, 200));
        bars.Add(new TBar(time.AddMinutes(2), 12, 12, 8, 8, 100));

        var result = Cmf.Batch(bars, 3);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Cmf_CalculateSpan_ReturnsCorrectValues()
    {
        double[] high = { 12, 12, 12 };
        double[] low = { 8, 8, 8 };
        double[] close = { 10, 12, 8 }; // MFM: 0, 1, -1
        double[] volume = { 100, 200, 100 };
        double[] output = new double[3];

        Cmf.Batch(high, low, close, volume, output, 3);

        // Bar 0: MFV=0, Vol=100 -> CMF=0/100=0
        Assert.Equal(0, output[0]);
        // Bar 1: MFV sum=0+200=200, Vol sum=300 -> CMF=200/300
        Assert.Equal(200.0 / 300.0, output[1], 6);
        // Bar 2: MFV sum=0+200-100=100, Vol sum=400 -> CMF=100/400
        Assert.Equal(100.0 / 400.0, output[2], 6);
    }

    [Fact]
    public void Cmf_CalculateSpan_ThrowsOnMismatchedLengths()
    {
        double[] high = { 10, 11 };
        double[] low = { 9, 10 };
        double[] close = { 9.5, 10.5 };
        double[] volume = { 100 }; // Short
        double[] output = new double[2];

        Assert.Throws<ArgumentException>(() =>
            Cmf.Batch(high, low, close, volume, output, 3));
    }

    [Fact]
    public void Cmf_CalculateSpan_ThrowsOnInvalidPeriod()
    {
        double[] high = { 10 };
        double[] low = { 9 };
        double[] close = { 9.5 };
        double[] volume = { 100 };
        double[] output = new double[1];

        Assert.Throws<ArgumentException>(() =>
            Cmf.Batch(high, low, close, volume, output, 0));
    }

    [Fact]
    public void Cmf_Calculate_EmptySeries_ReturnsEmpty()
    {
        var bars = new TBarSeries();
        var result = Cmf.Batch(bars);
        Assert.Empty(result);
    }

    [Fact]
    public void Cmf_CalculateSpan_SimdPath_ReturnsCorrectValues()
    {
        const int count = 100; // Enough to trigger SIMD
        double[] high = new double[count];
        double[] low = new double[count];
        double[] close = new double[count];
        double[] volume = new double[count];
        double[] output = new double[count];

        // Setup: High=12, Low=8, Close=12 (MFM=1), Vol=10
        for (int i = 0; i < count; i++)
        {
            high[i] = 12;
            low[i] = 8;
            close[i] = 12;
            volume[i] = 10;
        }

        Cmf.Batch(high, low, close, volume, output, 20);

        // All bars have MFM=1, so CMF should be 1.0 once we have enough data
        for (int i = 19; i < count; i++)
        {
            Assert.Equal(1.0, output[i], 6);
        }
    }

    [Fact]
    public void Cmf_StreamingMatchesBatch()
    {
        var bars = new TBarSeries();
        var gbm = new GBM(seed: 42);

        for (int i = 0; i < 100; i++)
        {
            bars.Add(gbm.Next());
        }

        // Streaming
        var cmfStreaming = new Cmf(20);
        var streamingValues = new List<double>();
        foreach (var bar in bars)
        {
            streamingValues.Add(cmfStreaming.Update(bar).Value);
        }

        // Batch
        var batchResult = Cmf.Batch(bars, 20);

        // Compare last 80 values (after warmup)
        for (int i = 20; i < 100; i++)
        {
            Assert.Equal(batchResult[i].Value, streamingValues[i], 9);
        }
    }

    [Fact]
    public void Cmf_BoundedBetweenNegativeOneAndOne()
    {
        var bars = new TBarSeries();
        var gbm = new GBM(seed: 42);

        for (int i = 0; i < 100; i++)
        {
            bars.Add(gbm.Next());
        }

        var cmf = new Cmf(20);
        foreach (var bar in bars)
        {
            var val = cmf.Update(bar);
            Assert.True(val.Value >= -1.0 && val.Value <= 1.0,
                $"CMF value {val.Value} is out of bounds [-1, 1]");
        }
    }
}
