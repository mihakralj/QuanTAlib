namespace QuanTAlib.Tests;

public class VwadTests
{
    [Fact]
    public void Vwad_Constructor_DefaultPeriod_Is20()
    {
        var vwad = new Vwad();
        Assert.Equal("VWAD(20)", vwad.Name);
        Assert.Equal(20, vwad.WarmupPeriod);
    }

    [Fact]
    public void Vwad_Constructor_CustomPeriod_SetsCorrectly()
    {
        var vwad = new Vwad(10);
        Assert.Equal("VWAD(10)", vwad.Name);
        Assert.Equal(10, vwad.WarmupPeriod);
    }

    [Fact]
    public void Vwad_Constructor_InvalidPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Vwad(0));
        Assert.Equal("period", ex.ParamName);

        ex = Assert.Throws<ArgumentException>(() => new Vwad(-1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Vwad_BasicCalculation_ReturnsExpectedValues()
    {
        // VWAD with period 3 for easy manual verification
        var vwad = new Vwad(3);
        var time = DateTime.UtcNow;

        // Bar 1: Close=10, High=12, Low=8. Range=4.
        // MFM = ((10-8) - (12-10)) / 4 = (2 - 2) / 4 = 0
        // Vol = 100. SumVol = 100. VolWeight = 100/100 = 1
        // WeightedMFV = 100 * 0 * 1 = 0
        // VWAD = 0
        var bar1 = new TBar(time, 10, 12, 8, 10, 100);
        var val1 = vwad.Update(bar1);
        Assert.Equal(0, val1.Value);

        // Bar 2: Close=12, High=12, Low=8. Range=4.
        // MFM = ((12-8) - (12-12)) / 4 = (4 - 0) / 4 = 1
        // Vol = 200. SumVol = 100 + 200 = 300. VolWeight = 200/300 = 0.6667
        // WeightedMFV = 200 * 1 * 0.6667 = 133.33
        // VWAD = 0 + 133.33 = 133.33
        var bar2 = new TBar(time.AddMinutes(1), 10, 12, 8, 12, 200);
        var val2 = vwad.Update(bar2);
        double expectedMfv2 = 200.0 * 1.0 * (200.0 / 300.0);
        Assert.Equal(expectedMfv2, val2.Value, 6);

        // Bar 3: Close=8, High=12, Low=8. Range=4.
        // MFM = ((8-8) - (12-8)) / 4 = (0 - 4) / 4 = -1
        // Vol = 100. SumVol = 100 + 200 + 100 = 400. VolWeight = 100/400 = 0.25
        // WeightedMFV = 100 * (-1) * 0.25 = -25
        // VWAD = 133.33 + (-25) = 108.33
        var bar3 = new TBar(time.AddMinutes(2), 12, 12, 8, 8, 100);
        var val3 = vwad.Update(bar3);
        double expectedMfv3 = 100.0 * (-1.0) * (100.0 / 400.0);
        Assert.Equal(expectedMfv2 + expectedMfv3, val3.Value, 6);
    }

    [Fact]
    public void Vwad_RollingSumDropsOldestValue()
    {
        var vwad = new Vwad(2);
        var time = DateTime.UtcNow;

        // Bar 1: MFM=1, Vol=100
        var bar1 = new TBar(time, 10, 12, 8, 12, 100);
        vwad.Update(bar1);

        // Bar 2: MFM=-1, Vol=100
        var bar2 = new TBar(time.AddMinutes(1), 12, 12, 8, 8, 100);
        vwad.Update(bar2);

        // Bar 3: MFM=1, Vol=100
        // Period=2, so bar1 drops out of volume sum
        // SumVol = 100 + 100 = 200 (bar2 + bar3)
        var bar3 = new TBar(time.AddMinutes(2), 8, 12, 8, 12, 100);
        var val3 = vwad.Update(bar3);

        // VWAD should continue accumulating
        Assert.True(double.IsFinite(val3.Value));
    }

    [Fact]
    public void Vwad_IsNew_False_UpdatesSameBar()
    {
        var vwad = new Vwad(3);
        var time = DateTime.UtcNow;

        // Initial update: MFM = 1, Vol = 100
        var bar1 = new TBar(time, 10, 12, 8, 12, 100);
        vwad.Update(bar1, isNew: true);
        double value1 = vwad.Last.Value;

        // Update same bar with different volume
        var bar1Update = new TBar(time, 10, 12, 8, 12, 200);
        vwad.Update(bar1Update, isNew: false);
        double value2 = vwad.Last.Value;

        // Values should differ because volume weight changed
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Vwad_IterativeCorrections_RestoreState()
    {
        var vwad = new Vwad(3);
        var time = DateTime.UtcNow;

        // Build up some state
        vwad.Update(new TBar(time, 10, 12, 8, 12, 100), isNew: true);
        vwad.Update(new TBar(time.AddMinutes(1), 10, 12, 8, 10, 100), isNew: true);

        // Add bar 3 and record state
        var bar3 = new TBar(time.AddMinutes(2), 10, 12, 8, 11, 100);
        vwad.Update(bar3, isNew: true);
        double valueAfterBar3 = vwad.Last.Value;

        // Multiple corrections to bar 3
        vwad.Update(new TBar(time.AddMinutes(2), 10, 12, 8, 8, 100), isNew: false);
        vwad.Update(new TBar(time.AddMinutes(2), 10, 12, 8, 9, 100), isNew: false);
        vwad.Update(new TBar(time.AddMinutes(2), 10, 12, 8, 12, 100), isNew: false);

        // Restore original bar 3
        vwad.Update(bar3, isNew: false);

        // Should match original state after bar 3
        Assert.Equal(valueAfterBar3, vwad.Last.Value, 10);
    }

    [Fact]
    public void Vwad_Reset_ClearsState()
    {
        var vwad = new Vwad(3);
        var bar = new TBar(DateTime.UtcNow, 10, 12, 8, 12, 100);
        vwad.Update(bar);

        Assert.NotEqual(0, vwad.Last.Value);

        vwad.Reset();
        Assert.False(vwad.IsHot);
        Assert.Equal(0, vwad.Last.Value);
    }

    [Fact]
    public void Vwad_IsHot_TrueAfterFirstBar()
    {
        var vwad = new Vwad(3);
        var time = DateTime.UtcNow;

        Assert.False(vwad.IsHot);

        vwad.Update(new TBar(time, 10, 12, 8, 10, 100));
        Assert.True(vwad.IsHot);
    }

    [Fact]
    public void Vwad_HighEqualsLow_HandlesDivisionByZero()
    {
        var vwad = new Vwad(3);
        // High = Low = 10. Range = 0. MFM should be 0.
        var bar = new TBar(DateTime.UtcNow, 10, 10, 10, 10, 100);
        var val = vwad.Update(bar);
        Assert.Equal(0, val.Value);
    }

    [Fact]
    public void Vwad_ZeroVolume_HandlesDivisionByZero()
    {
        var vwad = new Vwad(3);
        var bar = new TBar(DateTime.UtcNow, 10, 12, 8, 10, 0);
        var val = vwad.Update(bar);
        Assert.Equal(0, val.Value); // 0 volume weight = 0 contribution
    }

    [Fact]
    public void Vwad_TValueUpdate_ThrowsNotSupportedException()
    {
        var vwad = new Vwad();
        Assert.Throws<NotSupportedException>(() => vwad.Update(new TValue(DateTime.UtcNow, 15)));
    }

    [Fact]
    public void Vwad_PubEvent_FiresOnUpdate()
    {
        var vwad = new Vwad();
        bool eventFired = false;
        vwad.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        vwad.Update(new TBar(DateTime.UtcNow, 10, 12, 8, 10, 100));
        Assert.True(eventFired);
    }

    [Fact]
    public void Vwad_UpdateTBarSeries_ReturnsCorrectSeries()
    {
        var vwad = new Vwad(3);
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;

        bars.Add(new TBar(time, 10, 12, 8, 10, 100));
        bars.Add(new TBar(time.AddMinutes(1), 10, 12, 8, 12, 200));
        bars.Add(new TBar(time.AddMinutes(2), 12, 12, 8, 8, 100));

        var result = vwad.Update(bars);

        Assert.Equal(3, result.Count);
        Assert.True(double.IsFinite(result[0].Value));
        Assert.True(double.IsFinite(result[1].Value));
        Assert.True(double.IsFinite(result[2].Value));
    }

    [Fact]
    public void Vwad_CalculateTBarSeries_ReturnsCorrectSeries()
    {
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;

        bars.Add(new TBar(time, 10, 12, 8, 10, 100));
        bars.Add(new TBar(time.AddMinutes(1), 10, 12, 8, 12, 200));
        bars.Add(new TBar(time.AddMinutes(2), 12, 12, 8, 8, 100));

        var result = Vwad.Batch(bars, 3);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Vwad_CalculateSpan_ReturnsCorrectValues()
    {
        double[] high = [12, 12, 12];
        double[] low = [8, 8, 8];
        double[] close = [10, 12, 8]; // MFM: 0, 1, -1
        double[] volume = [100, 200, 100];
        double[] output = new double[3];

        Vwad.Batch(high, low, close, volume, output, 3);

        // Bar 0: MFM=0, Vol=100, SumVol=100, VolWeight=1, WeightedMFV=0
        Assert.Equal(0, output[0]);

        // Bar 1: MFM=1, Vol=200, SumVol=300, VolWeight=200/300
        // WeightedMFV = 200 * 1 * (200/300) = 133.33
        double expectedBar1 = 200.0 * 1.0 * (200.0 / 300.0);
        Assert.Equal(expectedBar1, output[1], 6);

        // Bar 2: MFM=-1, Vol=100, SumVol=400, VolWeight=100/400
        // WeightedMFV = 100 * (-1) * (100/400) = -25
        double expectedBar2 = expectedBar1 + (100.0 * (-1.0) * (100.0 / 400.0));
        Assert.Equal(expectedBar2, output[2], 6);
    }

    [Fact]
    public void Vwad_CalculateSpan_ThrowsOnMismatchedLengths()
    {
        double[] high = [10, 11];
        double[] low = [9, 10];
        double[] close = [9.5, 10.5];
        double[] volume = [100]; // Short
        double[] output = new double[2];

        Assert.Throws<ArgumentException>(() =>
            Vwad.Batch(high, low, close, volume, output, 3));
    }

    [Fact]
    public void Vwad_CalculateSpan_ThrowsOnInvalidPeriod()
    {
        double[] high = [10];
        double[] low = [9];
        double[] close = [9.5];
        double[] volume = [100];
        double[] output = new double[1];

        Assert.Throws<ArgumentException>(() =>
            Vwad.Batch(high, low, close, volume, output, 0));
    }

    [Fact]
    public void Vwad_Calculate_EmptySeries_ReturnsEmpty()
    {
        var bars = new TBarSeries();
        var result = Vwad.Batch(bars);
        Assert.Empty(result);
    }

    [Fact]
    public void Vwad_StreamingMatchesBatch()
    {
        var bars = new TBarSeries();
        var gbm = new GBM(seed: 42);

        for (int i = 0; i < 100; i++)
        {
            bars.Add(gbm.Next());
        }

        // Streaming
        var vwadStreaming = new Vwad(20);
        var streamingValues = new List<double>();
        foreach (var bar in bars)
        {
            streamingValues.Add(vwadStreaming.Update(bar).Value);
        }

        // Batch
        var batchResult = Vwad.Batch(bars, 20);

        // Compare all values
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(batchResult[i].Value, streamingValues[i], 9);
        }
    }

    [Fact]
    public void Vwad_NaN_Input_UsesLastValidValue()
    {
        var vwad = new Vwad(5);
        var time = DateTime.UtcNow;

        // Feed some valid values
        vwad.Update(new TBar(time, 10, 12, 8, 10, 100));
        vwad.Update(new TBar(time.AddMinutes(1), 10, 12, 8, 11, 100));

        // Feed NaN close - should use last valid
        var resultAfterNaN = vwad.Update(new TBar(time.AddMinutes(2), 10, 12, 8, double.NaN, 100));

        // Result should be finite
        Assert.True(double.IsFinite(resultAfterNaN.Value));
    }

    [Fact]
    public void Vwad_Infinity_Input_UsesLastValidValue()
    {
        var vwad = new Vwad(5);
        var time = DateTime.UtcNow;

        // Feed some valid values
        vwad.Update(new TBar(time, 10, 12, 8, 10, 100));
        vwad.Update(new TBar(time.AddMinutes(1), 10, 12, 8, 11, 100));

        // Feed positive infinity volume - should use last valid
        var result = vwad.Update(new TBar(time.AddMinutes(2), 10, 12, 8, 10, double.PositiveInfinity));
        Assert.True(double.IsFinite(result.Value));

        // Feed negative infinity close - should use last valid
        result = vwad.Update(new TBar(time.AddMinutes(3), 10, 12, 8, double.NegativeInfinity, 100));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Vwad_BatchCalc_HandlesNaN()
    {
        double[] high = [12, 12, double.NaN, 12, 12];
        double[] low = [8, 8, 8, 8, 8];
        double[] close = [10, 12, 10, 8, 10];
        double[] volume = [100, 200, 100, double.PositiveInfinity, 100];
        double[] output = new double[5];

        Vwad.Batch(high, low, close, volume, output, 3);

        // All outputs should be finite
        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void Vwad_CumulativeNature_ValuesContinueGrowing()
    {
        var bars = new TBarSeries();
        var gbm = new GBM(seed: 123, mu: 0.05); // Bullish trend

        for (int i = 0; i < 50; i++)
        {
            bars.Add(gbm.Next());
        }

        var result = Vwad.Batch(bars, 10);

        // In a bullish trend, VWAD should generally be positive and growing
        // (this is a statistical expectation, not a guarantee)
        double firstHalf = result[24].Value;
        double secondHalf = result[49].Value;

        // VWAD is cumulative, values should continue evolving
        Assert.NotEqual(firstHalf, secondHalf);
    }

    [Fact]
    public void Vwad_AllModes_ProduceSameResult()
    {
        // Arrange
        int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // 1. Batch Mode
        var batchSeries = Vwad.Batch(bars, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var spanOutput = new double[bars.Count];
        Vwad.Batch(bars.High.Values, bars.Low.Values, bars.Close.Values, bars.Volume.Values, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Vwad(period);
        for (int i = 0; i < bars.Count; i++)
        {
            streamingInd.Update(bars[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // Assert - precision 9 due to potential accumulation differences
        Assert.Equal(expected, spanResult, precision: 9);
        Assert.Equal(expected, streamingResult, precision: 9);
    }
}
