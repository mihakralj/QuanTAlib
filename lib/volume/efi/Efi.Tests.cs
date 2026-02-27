namespace QuanTAlib.Tests;

public class EfiTests
{
    [Fact]
    public void Efi_Constructor_DefaultPeriod_Is13()
    {
        var efi = new Efi();
        Assert.Equal("EFI(13)", efi.Name);
        Assert.Equal(13, efi.WarmupPeriod);
    }

    [Fact]
    public void Efi_Constructor_CustomPeriod_SetsCorrectly()
    {
        var efi = new Efi(20);
        Assert.Equal("EFI(20)", efi.Name);
        Assert.Equal(20, efi.WarmupPeriod);
    }

    [Fact]
    public void Efi_Constructor_InvalidPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Efi(0));
        Assert.Equal("period", ex.ParamName);

        ex = Assert.Throws<ArgumentException>(() => new Efi(-1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Efi_BasicCalculation_ReturnsExpectedValues()
    {
        var efi = new Efi(3);
        var time = DateTime.UtcNow;

        // Bar 1: No previous close, raw force = 0, EFI = 0
        var bar1 = new TBar(time, 10, 12, 8, 10, 100);
        var val1 = efi.Update(bar1);
        Assert.Equal(0, val1.Value);

        // Bar 2: Close=12, PrevClose=10, Vol=200
        // Raw Force = (12-10) * 200 = 400
        // alpha = 2/(3+1) = 0.5
        // EMA: 0.5 * (400 - 0) + 0 = 200
        // e = 1 * 0.5 = 0.5
        // c = 1/(1-0.5) = 2
        // Result = 2 * 200 = 400
        var bar2 = new TBar(time.AddMinutes(1), 10, 14, 9, 12, 200);
        var val2 = efi.Update(bar2);
        Assert.Equal(400, val2.Value, 6);

        // Bar 3: Close=8, PrevClose=12, Vol=100
        // Raw Force = (8-12) * 100 = -400
        // EMA: 0.5 * (-400 - 200) + 200 = -100
        // e = 0.5 * 0.5 = 0.25
        // c = 1/(1-0.25) = 1.333...
        // Result = 1.333... * -100 = -133.333...
        var bar3 = new TBar(time.AddMinutes(2), 12, 12, 7, 8, 100);
        var val3 = efi.Update(bar3);
        Assert.Equal(-100.0 / 0.75, val3.Value, 6);
    }

    [Fact]
    public void Efi_IsNew_False_UpdatesSameBar()
    {
        var efi = new Efi(3);
        var time = DateTime.UtcNow;

        // Initial bar
        var bar1 = new TBar(time, 10, 12, 8, 10, 100);
        efi.Update(bar1, isNew: true);

        // Second bar
        var bar2 = new TBar(time.AddMinutes(1), 10, 14, 9, 12, 200);
        var val2 = efi.Update(bar2, isNew: true);
        double originalValue = val2.Value;

        // Update same bar with different values
        var bar2Update = new TBar(time.AddMinutes(1), 10, 14, 9, 14, 200);
        var val2Update = efi.Update(bar2Update, isNew: false);

        // Values should differ since close changed
        Assert.NotEqual(originalValue, val2Update.Value);
    }

    [Fact]
    public void Efi_IterativeCorrections_RestoreState()
    {
        var efi = new Efi(3);
        var time = DateTime.UtcNow;

        // Build up some state
        efi.Update(new TBar(time, 10, 12, 8, 10, 100), isNew: true);
        efi.Update(new TBar(time.AddMinutes(1), 10, 12, 8, 12, 100), isNew: true);

        // Multiple corrections to bar 3
        efi.Update(new TBar(time.AddMinutes(2), 10, 12, 8, 8, 100), isNew: true);
        efi.Update(new TBar(time.AddMinutes(2), 10, 12, 8, 9, 100), isNew: false);
        efi.Update(new TBar(time.AddMinutes(2), 10, 12, 8, 11, 100), isNew: false);
        var finalVal = efi.Update(new TBar(time.AddMinutes(2), 10, 12, 8, 12, 100), isNew: false);

        // Final bar close=12, prev close=12, so raw force = 0
        // Value should be finite
        Assert.True(double.IsFinite(finalVal.Value));
    }

    [Fact]
    public void Efi_Reset_ClearsState()
    {
        var efi = new Efi(3);
        var time = DateTime.UtcNow;

        efi.Update(new TBar(time, 10, 12, 8, 10, 100));
        efi.Update(new TBar(time.AddMinutes(1), 10, 14, 9, 12, 200));

        Assert.NotEqual(0, efi.Last.Value);

        efi.Reset();
        Assert.False(efi.IsHot);
        Assert.Equal(0, efi.Last.Value);
    }

    [Fact]
    public void Efi_IsHot_FlipsAtPeriod()
    {
        var efi = new Efi(3);
        var time = DateTime.UtcNow;

        Assert.False(efi.IsHot);

        efi.Update(new TBar(time, 10, 12, 8, 10, 100));
        Assert.False(efi.IsHot);

        efi.Update(new TBar(time.AddMinutes(1), 10, 12, 8, 11, 100));
        Assert.False(efi.IsHot);

        efi.Update(new TBar(time.AddMinutes(2), 10, 12, 8, 12, 100));
        Assert.True(efi.IsHot);
    }

    [Fact]
    public void Efi_ZeroVolume_ReturnsZeroForce()
    {
        var efi = new Efi(3);
        var time = DateTime.UtcNow;

        efi.Update(new TBar(time, 10, 12, 8, 10, 100));
        // Zero volume: raw force = (12-10) * 0 = 0
        var val = efi.Update(new TBar(time.AddMinutes(1), 10, 14, 9, 12, 0));
        Assert.Equal(0, val.Value);
    }

    [Fact]
    public void Efi_NaNClose_UsesLastValidValue()
    {
        var efi = new Efi(3);
        var time = DateTime.UtcNow;

        efi.Update(new TBar(time, 10, 12, 8, 10, 100));
        efi.Update(new TBar(time.AddMinutes(1), 10, 14, 9, 12, 200));

        // NaN close should use last valid (12)
        var val = efi.Update(new TBar(time.AddMinutes(2), 10, 14, 9, double.NaN, 100));
        // raw force = (12-12) * 100 = 0
        Assert.True(double.IsFinite(val.Value));
    }

    [Fact]
    public void Efi_InfinityVolume_TreatedAsZero()
    {
        var efi = new Efi(3);
        var time = DateTime.UtcNow;

        efi.Update(new TBar(time, 10, 12, 8, 10, 100));
        var val = efi.Update(new TBar(time.AddMinutes(1), 10, 14, 9, 12, double.PositiveInfinity));
        // Infinity volume is treated as 0
        Assert.Equal(0, val.Value);
    }

    [Fact]
    public void Efi_TValueUpdate_ThrowsNotSupportedException()
    {
        var efi = new Efi();
        Assert.Throws<NotSupportedException>(() => efi.Update(new TValue(DateTime.UtcNow, 15)));
    }

    [Fact]
    public void Efi_PubEvent_FiresOnUpdate()
    {
        var efi = new Efi();
        bool eventFired = false;
        efi.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        efi.Update(new TBar(DateTime.UtcNow, 10, 12, 8, 10, 100));
        Assert.True(eventFired);
    }

    [Fact]
    public void Efi_UpdateTBarSeries_ReturnsCorrectSeries()
    {
        var efi = new Efi(3);
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;

        bars.Add(new TBar(time, 10, 12, 8, 10, 100));
        bars.Add(new TBar(time.AddMinutes(1), 10, 12, 8, 12, 200));
        bars.Add(new TBar(time.AddMinutes(2), 12, 12, 8, 8, 100));

        var result = efi.Update(bars);

        Assert.Equal(3, result.Count);
        Assert.True(double.IsFinite(result[0].Value));
        Assert.True(double.IsFinite(result[1].Value));
        Assert.True(double.IsFinite(result[2].Value));
    }

    [Fact]
    public void Efi_CalculateTBarSeries_ReturnsCorrectSeries()
    {
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;

        bars.Add(new TBar(time, 10, 12, 8, 10, 100));
        bars.Add(new TBar(time.AddMinutes(1), 10, 12, 8, 12, 200));
        bars.Add(new TBar(time.AddMinutes(2), 12, 12, 8, 8, 100));

        var result = Efi.Batch(bars, 3);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Efi_CalculateSpan_ReturnsCorrectValues()
    {
        // close prices: 10, 12, 8
        // volumes: 100, 200, 100
        // raw forces: 0, (12-10)*200=400, (8-12)*100=-400
        double[] close = { 10, 12, 8 };
        double[] volume = { 100, 200, 100 };
        double[] output = new double[3];

        Efi.Batch(close, volume, output, 3);

        // Bar 0: raw force = 0, result = 0
        Assert.Equal(0, output[0]);
        // Bar 1: EMA = 0.5*(400-0)+0 = 200, e = 0.5, c = 2, result = 400
        Assert.Equal(400, output[1], 6);
        // Bar 2: EMA = 0.5*(-400-200)+200 = -100, e = 0.25, c = 1.333..., result = -133.333...
        Assert.Equal(-100.0 / 0.75, output[2], 6);
    }

    [Fact]
    public void Efi_CalculateSpan_ThrowsOnMismatchedLengths()
    {
        double[] close = { 10, 11 };
        double[] volume = { 100 }; // Short
        double[] output = new double[2];

        Assert.Throws<ArgumentException>(() =>
            Efi.Batch(close, volume, output, 3));
    }

    [Fact]
    public void Efi_CalculateSpan_ThrowsOnInvalidPeriod()
    {
        double[] close = { 10 };
        double[] volume = { 100 };
        double[] output = new double[1];

        Assert.Throws<ArgumentException>(() =>
            Efi.Batch(close, volume, output, 0));
    }

    [Fact]
    public void Efi_Calculate_EmptySeries_ReturnsEmpty()
    {
        var bars = new TBarSeries();
        var result = Efi.Batch(bars);
        Assert.Empty(result);
    }

    [Fact]
    public void Efi_StreamingMatchesBatch()
    {
        var bars = new TBarSeries();
        var gbm = new GBM(seed: 42);

        for (int i = 0; i < 100; i++)
        {
            bars.Add(gbm.Next());
        }

        // Streaming
        var efiStreaming = new Efi(13);
        var streamingValues = new List<double>();
        foreach (var bar in bars)
        {
            streamingValues.Add(efiStreaming.Update(bar).Value);
        }

        // Batch
        var batchResult = Efi.Batch(bars, 13);

        // Compare all values
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(batchResult[i].Value, streamingValues[i], 9);
        }
    }

    [Fact]
    public void Efi_PositiveForceOnPriceIncrease()
    {
        var efi = new Efi(3);
        var time = DateTime.UtcNow;

        efi.Update(new TBar(time, 10, 12, 8, 10, 100));
        // Price increases from 10 to 15, volume = 500
        // Raw force = (15-10) * 500 = 2500 (positive)
        var val = efi.Update(new TBar(time.AddMinutes(1), 10, 16, 9, 15, 500));
        Assert.True(val.Value > 0);
    }

    [Fact]
    public void Efi_NegativeForceOnPriceDecrease()
    {
        var efi = new Efi(3);
        var time = DateTime.UtcNow;

        efi.Update(new TBar(time, 10, 12, 8, 10, 100));
        // Price decreases from 10 to 5, volume = 500
        // Raw force = (5-10) * 500 = -2500 (negative)
        var val = efi.Update(new TBar(time.AddMinutes(1), 10, 11, 4, 5, 500));
        Assert.True(val.Value < 0);
    }
}
