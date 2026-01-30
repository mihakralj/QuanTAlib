using Xunit;

namespace QuanTAlib.Tests;

public class VfTests
{
    private const double Tolerance = 1e-10;
    private const int DefaultPeriod = 14;

    #region Constructor Tests

    [Fact]
    public void Constructor_DefaultPeriod_SetsCorrectProperties()
    {
        var vf = new Vf();

        Assert.Equal("Vf(14)", vf.Name);
        Assert.Equal(14, vf.WarmupPeriod);
        Assert.False(vf.IsHot);
    }

    [Fact]
    public void Constructor_CustomPeriod_SetsCorrectProperties()
    {
        var vf = new Vf(period: 20);

        Assert.Equal("Vf(20)", vf.Name);
        Assert.Equal(20, vf.WarmupPeriod);
    }

    [Fact]
    public void Constructor_PeriodLessThanOne_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Vf(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Vf(period: -5));
        Assert.Equal("period", ex.ParamName);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_FirstBar_ReturnsZero()
    {
        var vf = new Vf();
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);

        var result = vf.Update(bar);

        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void Update_PriceIncrease_ReturnsPositiveValue()
    {
        var vf = new Vf();
        var time = DateTime.UtcNow;

        vf.Update(new TBar(time, 100, 105, 95, 100, 1000));
        var result = vf.Update(new TBar(time.AddMinutes(1), 100, 110, 98, 105, 2000)); // +5 price change

        Assert.True(result.Value > 0, "VF should be positive when price increases");
    }

    [Fact]
    public void Update_PriceDecrease_ReturnsNegativeValue()
    {
        var vf = new Vf();
        var time = DateTime.UtcNow;

        vf.Update(new TBar(time, 100, 105, 95, 100, 1000));
        var result = vf.Update(new TBar(time.AddMinutes(1), 100, 102, 90, 95, 2000)); // -5 price change

        Assert.True(result.Value < 0, "VF should be negative when price decreases");
    }

    [Fact]
    public void Update_NoPriceChange_ReturnsZeroOrNearZero()
    {
        var vf = new Vf();
        var time = DateTime.UtcNow;

        vf.Update(new TBar(time, 100, 105, 95, 100, 1000));
        var result = vf.Update(new TBar(time.AddMinutes(1), 100, 105, 95, 100, 2000)); // 0 price change

        Assert.Equal(0, result.Value, Tolerance);
    }

    [Fact]
    public void Update_ReturnsCorrectTime()
    {
        var vf = new Vf();
        var expectedTime = DateTime.UtcNow;
        var bar = new TBar(expectedTime, 100, 105, 95, 102, 1000);

        var result = vf.Update(bar);

        Assert.Equal(expectedTime.Ticks, result.Time);
    }

    #endregion

    #region Formula Verification Tests

    [Fact]
    public void Update_SecondBar_AppliesEmaWithWarmupCompensation()
    {
        var vf = new Vf(period: 10);
        var time = DateTime.UtcNow;

        // First bar
        vf.Update(new TBar(time, 100, 105, 95, 100, 1000));

        // Second bar: price change = 110 - 100 = 10, raw_vf = 10 * 2000 = 20000
        var result = vf.Update(new TBar(time.AddMinutes(1), 108, 115, 105, 110, 2000));

        // Expected: ~20000 (the warmup compensation should give us the raw value initially)
        Assert.True(Math.Abs(result.Value - 20000) < 1, "VF should be approximately 20000 with warmup compensation");
    }

    [Fact]
    public void Update_MultipleBarSequence_CalculatesCorrectly()
    {
        var vf = new Vf(period: 3);
        var time = DateTime.UtcNow;

        // Bar 1: establishes baseline
        vf.Update(new TBar(time, 100, 105, 95, 100, 1000));

        // Bar 2: price +10, volume 1000 -> raw_vf = 10000
        vf.Update(new TBar(time.AddMinutes(1), 100, 115, 98, 110, 1000));

        // Bar 3: price -5, volume 500 -> raw_vf = -2500
        vf.Update(new TBar(time.AddMinutes(2), 108, 112, 103, 105, 500));

        // Bar 4: price +5, volume 2000 -> raw_vf = 10000
        var result = vf.Update(new TBar(time.AddMinutes(3), 105, 115, 104, 110, 2000));

        // Result should be a smoothed positive value (EMA of 10000, -2500, 10000)
        Assert.True(result.Value > 0, "VF should be positive given more positive raw_vf values");
    }

    #endregion

    #region IsHot Tests

    [Fact]
    public void IsHot_BeforeWarmup_ReturnsFalse()
    {
        var vf = new Vf(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 4; i++)
        {
            vf.Update(new TBar(time.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i, 1000));
        }

        Assert.False(vf.IsHot);
    }

    [Fact]
    public void IsHot_AtWarmup_ReturnsTrue()
    {
        var vf = new Vf(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            vf.Update(new TBar(time.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i, 1000));
        }

        Assert.True(vf.IsHot);
    }

    [Fact]
    public void IsHot_AfterWarmup_ReturnsTrue()
    {
        var vf = new Vf(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            vf.Update(new TBar(time.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i, 1000));
        }

        Assert.True(vf.IsHot);
    }

    #endregion

    #region Bar Correction (isNew=false) Tests

    [Fact]
    public void Update_IsNewFalse_RollsBackState()
    {
        var vf = new Vf();
        var time = DateTime.UtcNow;

        vf.Update(new TBar(time, 100, 105, 95, 100, 1000));
        var valueAfterFirst = vf.Update(new TBar(time.AddMinutes(1), 100, 110, 98, 105, 2000));

        // Update same bar with different data (isNew=false)
        var valueAfterCorrection = vf.Update(new TBar(time.AddMinutes(1), 100, 108, 96, 103, 1500), isNew: false);

        // Values should differ because the bar was corrected
        Assert.NotEqual(valueAfterFirst.Value, valueAfterCorrection.Value);
    }

    [Fact]
    public void Update_MultipleCorrections_MaintainsConsistency()
    {
        var vf = new Vf();
        var time = DateTime.UtcNow;

        vf.Update(new TBar(time, 100, 105, 95, 100, 1000));

        // First update
        vf.Update(new TBar(time.AddMinutes(1), 100, 110, 98, 105, 2000));

        // Multiple corrections
        vf.Update(new TBar(time.AddMinutes(1), 100, 108, 96, 103, 1500), isNew: false);
        vf.Update(new TBar(time.AddMinutes(1), 100, 112, 97, 108, 2500), isNew: false);
        var finalValue = vf.Update(new TBar(time.AddMinutes(1), 100, 110, 98, 105, 2000), isNew: false);

        // Final correction back to original should match
        vf.Reset();
        vf.Update(new TBar(time, 100, 105, 95, 100, 1000));
        var expectedValue = vf.Update(new TBar(time.AddMinutes(1), 100, 110, 98, 105, 2000));

        Assert.Equal(expectedValue.Value, finalValue.Value, Tolerance);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoreOriginalState()
    {
        var vf = new Vf();
        var time = DateTime.UtcNow;

        // Build up state
        vf.Update(new TBar(time, 100, 105, 95, 100, 1000));
        vf.Update(new TBar(time.AddMinutes(1), 100, 110, 98, 105, 2000));
        var originalValue = vf.Update(new TBar(time.AddMinutes(2), 105, 115, 103, 110, 1500));

        // Make correction
        vf.Update(new TBar(time.AddMinutes(2), 105, 120, 100, 115, 3000), isNew: false);

        // Restore original
        var restoredValue = vf.Update(new TBar(time.AddMinutes(2), 105, 115, 103, 110, 1500), isNew: false);

        Assert.Equal(originalValue.Value, restoredValue.Value, Tolerance);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsState()
    {
        var vf = new Vf();
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            vf.Update(new TBar(time.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i, 1000));
        }

        vf.Reset();

        Assert.False(vf.IsHot);
        Assert.Equal(default, vf.Last);
    }

    [Fact]
    public void Reset_AllowsReuse()
    {
        var vf = new Vf();
        var time = DateTime.UtcNow;

        // First use
        vf.Update(new TBar(time, 100, 105, 95, 100, 1000));
        var firstResult = vf.Update(new TBar(time.AddMinutes(1), 100, 110, 98, 105, 2000));

        vf.Reset();

        // Second use with same data
        vf.Update(new TBar(time, 100, 105, 95, 100, 1000));
        var secondResult = vf.Update(new TBar(time.AddMinutes(1), 100, 110, 98, 105, 2000));

        Assert.Equal(firstResult.Value, secondResult.Value, Tolerance);
    }

    #endregion

    #region NaN/Infinity Handling Tests

    [Fact]
    public void Update_NaNClose_UsesLastValidValue()
    {
        var vf = new Vf();
        var time = DateTime.UtcNow;

        vf.Update(new TBar(time, 100, 105, 95, 100, 1000));
        _ = vf.Update(new TBar(time.AddMinutes(1), 100, 110, 98, 105, 2000));

        // Update with NaN close
        var nanResult = vf.Update(new TBar(time.AddMinutes(2), 105, 115, 100, double.NaN, 1500));

        Assert.True(double.IsFinite(nanResult.Value), "VF should handle NaN close gracefully");
    }

    [Fact]
    public void Update_NaNVolume_UsesLastValidValue()
    {
        var vf = new Vf();
        var time = DateTime.UtcNow;

        vf.Update(new TBar(time, 100, 105, 95, 100, 1000));
        vf.Update(new TBar(time.AddMinutes(1), 100, 110, 98, 105, 2000));

        // Update with NaN volume
        var result = vf.Update(new TBar(time.AddMinutes(2), 105, 115, 100, 110, double.NaN));

        Assert.True(double.IsFinite(result.Value), "VF should handle NaN volume gracefully");
    }

    [Fact]
    public void Update_InfinityInput_UsesLastValidValue()
    {
        var vf = new Vf();
        var time = DateTime.UtcNow;

        vf.Update(new TBar(time, 100, 105, 95, 100, 1000));
        vf.Update(new TBar(time.AddMinutes(1), 100, 110, 98, 105, 2000));

        // Update with infinity
        var result = vf.Update(new TBar(time.AddMinutes(2), 105, 115, 100, double.PositiveInfinity, 1500));

        Assert.True(double.IsFinite(result.Value), "VF should handle infinity gracefully");
    }

    #endregion

    #region Event Tests

    [Fact]
    public void Update_PublishesEvent()
    {
        var vf = new Vf();
        TValue? receivedValue = null;
        bool? receivedIsNew = null;

        vf.Pub += (object? sender, in TValueEventArgs args) =>
        {
            receivedValue = args.Value;
            receivedIsNew = args.IsNew;
        };

        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        var result = vf.Update(bar);

        Assert.NotNull(receivedValue);
        Assert.Equal(result.Value, receivedValue.Value.Value);
        Assert.True(receivedIsNew);
    }

    [Fact]
    public void Update_IsNewFalse_PublishesEventWithIsNewFalse()
    {
        var vf = new Vf();
        var time = DateTime.UtcNow;

        vf.Update(new TBar(time, 100, 105, 95, 100, 1000));

        bool? receivedIsNew = null;
        vf.Pub += (object? sender, in TValueEventArgs args) => receivedIsNew = args.IsNew;

        vf.Update(new TBar(time.AddMinutes(1), 100, 110, 98, 105, 2000), isNew: false);

        Assert.False(receivedIsNew);
    }

    #endregion

    #region Batch Mode Tests

    [Fact]
    public void Update_TBarSeries_ReturnsCorrectLength()
    {
        var vf = new Vf();
        var series = GenerateTestBarSeries(100);

        var result = vf.Update(series);

        Assert.Equal(100, result.Count);
    }

    [Fact]
    public void Calculate_TBarSeries_ReturnsCorrectLength()
    {
        var series = GenerateTestBarSeries(100);

        var result = Vf.Calculate(series, DefaultPeriod);

        Assert.Equal(100, result.Count);
    }

    [Fact]
    public void Calculate_EmptySeries_ReturnsEmpty()
    {
        var series = new TBarSeries();

        var result = Vf.Calculate(series, DefaultPeriod);

        Assert.Empty(result);
    }

    #endregion

    #region Span Mode Tests

    [Fact]
    public void Calculate_Span_MatchesStreamingMode()
    {
        var series = GenerateTestBarSeries(50);
        var close = new double[50];
        var volume = new double[50];
        var output = new double[50];

        // Extract values from series
        for (int i = 0; i < 50; i++)
        {
            close[i] = series[i].Close;
            volume[i] = series[i].Volume;
        }

        // Span calculation
        Vf.Calculate(close, volume, output, DefaultPeriod);

        // Streaming calculation
        var vf = new Vf(DefaultPeriod);
        var streamingResult = vf.Update(series);

        // Compare last 30 values (after warmup)
        for (int i = 20; i < 50; i++)
        {
            Assert.Equal(streamingResult[i].Value, output[i], Tolerance);
        }
    }

    [Fact]
    public void Calculate_Span_MismatchedLengths_ThrowsArgumentException()
    {
        var close = new double[100];
        var volume = new double[50]; // Different length
        var output = new double[100];

        var ex = Assert.Throws<ArgumentException>(() => Vf.Calculate(close, volume, output, DefaultPeriod));
        Assert.Equal("volume", ex.ParamName);
    }

    [Fact]
    public void Calculate_Span_OutputLengthMismatch_ThrowsArgumentException()
    {
        var close = new double[100];
        var volume = new double[100];
        var output = new double[50]; // Different length

        var ex = Assert.Throws<ArgumentException>(() => Vf.Calculate(close, volume, output, DefaultPeriod));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Calculate_Span_InvalidPeriod_ThrowsArgumentException()
    {
        var close = new double[100];
        var volume = new double[100];
        var output = new double[100];

        var ex = Assert.Throws<ArgumentException>(() => Vf.Calculate(close, volume, output, period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Calculate_Span_EmptyInput_ReturnsWithoutError()
    {
        var close = Array.Empty<double>();
        var volume = Array.Empty<double>();
        var output = Array.Empty<double>();

        // Should not throw
        Vf.Calculate(close, volume, output, DefaultPeriod);
        Assert.True(true); // Test passes if no exception
    }

    [Fact]
    public void Calculate_Span_FirstValueIsZero()
    {
        var close = new double[] { 100, 105, 110, 108, 112 };
        var volume = new double[] { 1000, 2000, 1500, 1800, 2200 };
        var output = new double[5];

        Vf.Calculate(close, volume, output, period: 3);

        Assert.Equal(0, output[0]);
    }

    #endregion

    #region TValue Update Tests

    [Fact]
    public void Update_TValue_ThrowsNotSupportedException()
    {
        var vf = new Vf();
        var time = DateTime.UtcNow;

        // Build up state with bars
        vf.Update(new TBar(time, 100, 105, 95, 100, 1000));
        vf.Update(new TBar(time.AddMinutes(1), 100, 110, 98, 105, 2000));

        // Update with TValue should throw NotSupportedException (VF requires volume)
        var ex = Assert.Throws<NotSupportedException>(() => vf.Update(new TValue(time.AddMinutes(2), 110)));
        Assert.Contains("volume", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Mode Consistency Tests

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        var series = GenerateTestBarSeries(100);
        var close = new double[100];
        var volume = new double[100];

        // Extract values from series
        for (int i = 0; i < 100; i++)
        {
            close[i] = series[i].Close;
            volume[i] = series[i].Volume;
        }

        // Streaming mode
        var vf = new Vf(DefaultPeriod);
        var streamingResult = vf.Update(series);

        // Batch mode
        var batchResult = Vf.Calculate(series, DefaultPeriod);

        // Span mode
        var spanOutput = new double[100];
        Vf.Calculate(close, volume, spanOutput, DefaultPeriod);

        // Compare all modes (last 50 values to avoid warmup differences)
        for (int i = 50; i < 100; i++)
        {
            Assert.Equal(streamingResult[i].Value, batchResult[i].Value, Tolerance);
            Assert.Equal(streamingResult[i].Value, spanOutput[i], Tolerance);
        }
    }

    #endregion

    #region Helper Methods

    private static TBarSeries GenerateTestBarSeries(int count)
    {
        var bars = new TBarSeries();
        var gbm = new GBM(seed: 42);

        for (int i = 0; i < count; i++)
        {
            bars.Add(gbm.Next());
        }

        return bars;
    }

    #endregion
}