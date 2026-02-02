namespace QuanTAlib.Tests;
using Xunit;

public class HlvTests
{
    private const double Tolerance = 1e-9;

    private static TBarSeries GenerateTestData(int count = 100)
    {
        var gbm = new GBM(seed: 42);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_DefaultParameters_SetsCorrectValues()
    {
        var hlv = new Hlv();
        Assert.Equal(20, hlv.Period);
        Assert.True(hlv.Annualize);
        Assert.Equal(252, hlv.AnnualPeriods);
        Assert.Equal("Hlv(20)", hlv.Name);
        Assert.Equal(20, hlv.WarmupPeriod);
    }

    [Fact]
    public void Constructor_CustomParameters_SetsCorrectValues()
    {
        var hlv = new Hlv(period: 10, annualize: false, annualPeriods: 365);
        Assert.Equal(10, hlv.Period);
        Assert.False(hlv.Annualize);
        Assert.Equal(365, hlv.AnnualPeriods);
        Assert.Equal("Hlv(10)", hlv.Name);
    }

    [Fact]
    public void Constructor_ZeroPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Hlv(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Hlv(period: -1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroAnnualPeriodsWhenAnnualizing_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Hlv(period: 10, annualize: true, annualPeriods: 0));
        Assert.Equal("annualPeriods", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroAnnualPeriodsWhenNotAnnualizing_DoesNotThrow()
    {
        var hlv = new Hlv(period: 10, annualize: false, annualPeriods: 0);
        Assert.Equal(0, hlv.AnnualPeriods);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_SingleBar_ReturnsNonNegativeValue()
    {
        var hlv = new Hlv(period: 5);
        var bar = new TBar(DateTime.UtcNow, 100.0, 105.0, 98.0, 102.0, 1000);
        var result = hlv.Update(bar);

        Assert.True(result.Value >= 0, "HLV should return non-negative values");
    }

    [Fact]
    public void Update_MultipleBars_ReturnsCorrectCount()
    {
        var hlv = new Hlv(period: 5);
        var bars = GenerateTestData(10);

        for (int i = 0; i < bars.Count; i++)
        {
            hlv.Update(bars[i]);
        }

        Assert.True(hlv.IsHot, "Indicator should be hot after warmup period");
    }

    [Fact]
    public void Update_ReturnsLastValue()
    {
        var hlv = new Hlv(period: 5);
        var bar = new TBar(DateTime.UtcNow, 100.0, 105.0, 98.0, 102.0, 1000);
        var result = hlv.Update(bar);

        Assert.Equal(result.Value, hlv.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_WithoutAnnualization_ReturnsSmallerValues()
    {
        var hlvAnnual = new Hlv(period: 10, annualize: true, annualPeriods: 252);
        var hlvNoAnnual = new Hlv(period: 10, annualize: false);
        var bars = GenerateTestData(20);

        double lastAnnual = 0;
        double lastNoAnnual = 0;
        for (int i = 0; i < bars.Count; i++)
        {
            lastAnnual = hlvAnnual.Update(bars[i]).Value;
            lastNoAnnual = hlvNoAnnual.Update(bars[i]).Value;
        }

        // Annualized values should be larger by factor of sqrt(252)
        Assert.True(lastAnnual > lastNoAnnual, "Annualized values should be larger");
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var hlv = new Hlv(period: 5);
        var bar1 = new TBar(DateTime.UtcNow, 100.0, 105.0, 98.0, 102.0, 1000);
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 102.0, 107.0, 100.0, 105.0, 1000);

        hlv.Update(bar1, isNew: true);
        var result1 = hlv.Last.Value;

        hlv.Update(bar2, isNew: true);
        var result2 = hlv.Last.Value;

        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public void Update_IsNewFalse_UpdatesCurrentBar()
    {
        var hlv = new Hlv(period: 5);
        var bar1 = new TBar(DateTime.UtcNow, 100.0, 105.0, 98.0, 102.0, 1000);

        hlv.Update(bar1, isNew: true);
        var firstValue = hlv.Last.Value;

        // Update the same bar with different high-low values
        var bar1Updated = new TBar(DateTime.UtcNow, 100.0, 110.0, 95.0, 108.0, 1000);
        hlv.Update(bar1Updated, isNew: false);
        var updatedValue = hlv.Last.Value;

        Assert.NotEqual(firstValue, updatedValue);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoresState()
    {
        var hlv = new Hlv(period: 5);
        var bars = GenerateTestData(10);

        // Process first 5 bars
        for (int i = 0; i < 5; i++)
        {
            hlv.Update(bars[i], isNew: true);
        }

        // Add bar 6 and correct multiple times
        hlv.Update(bars[5], isNew: true);
        hlv.Update(bars[5], isNew: false);
        hlv.Update(bars[5], isNew: false);
        hlv.Update(bars[5], isNew: false);

        // Now continue with bar 7
        hlv.Update(bars[6], isNew: true);

        // Create new instance and process same data
        var hlv2 = new Hlv(period: 5);
        for (int i = 0; i < 7; i++)
        {
            hlv2.Update(bars[i], isNew: true);
        }

        Assert.Equal(hlv.Last.Value, hlv2.Last.Value, Tolerance);
    }

    #endregion

    #region IsHot and Warmup Tests

    [Fact]
    public void IsHot_BeforeWarmup_ReturnsFalse()
    {
        var hlv = new Hlv(period: 10);
        var bars = GenerateTestData(5);

        for (int i = 0; i < bars.Count; i++)
        {
            hlv.Update(bars[i]);
        }

        Assert.False(hlv.IsHot);
    }

    [Fact]
    public void IsHot_AfterWarmup_ReturnsTrue()
    {
        var hlv = new Hlv(period: 10);
        var bars = GenerateTestData(15);

        for (int i = 0; i < bars.Count; i++)
        {
            hlv.Update(bars[i]);
        }

        Assert.True(hlv.IsHot);
    }

    [Fact]
    public void IsHot_ExactlyAtWarmup_ReturnsTrue()
    {
        var hlv = new Hlv(period: 10);
        var bars = GenerateTestData(10);

        for (int i = 0; i < bars.Count; i++)
        {
            hlv.Update(bars[i]);
        }

        Assert.True(hlv.IsHot);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsState()
    {
        var hlv = new Hlv(period: 5);
        var bars = GenerateTestData(10);

        for (int i = 0; i < bars.Count; i++)
        {
            hlv.Update(bars[i]);
        }

        hlv.Reset();

        Assert.False(hlv.IsHot);
        Assert.Equal(0, hlv.Last.Value);
    }

    [Fact]
    public void Reset_AllowsReprocessing()
    {
        var hlv = new Hlv(period: 5);
        var bars = GenerateTestData(10);

        // First pass
        for (int i = 0; i < bars.Count; i++)
        {
            hlv.Update(bars[i]);
        }
        var firstResult = hlv.Last.Value;

        // Reset and second pass
        hlv.Reset();
        for (int i = 0; i < bars.Count; i++)
        {
            hlv.Update(bars[i]);
        }
        var secondResult = hlv.Last.Value;

        Assert.Equal(firstResult, secondResult, Tolerance);
    }

    #endregion

    #region Robustness Tests

    [Fact]
    public void Update_WithNaNValues_UsesLastValidEstimator()
    {
        var hlv = new Hlv(period: 5);
        var bars = GenerateTestData(10);

        for (int i = 0; i < bars.Count; i++)
        {
            hlv.Update(bars[i]);
        }
        var valueBeforeInvalid = hlv.Last.Value;

        // Bar with NaN high - should use last valid Parkinson estimator
        var nanBar = new TBar(DateTime.UtcNow, 100.0, double.NaN, 98.0, 102.0, 1000);
        var result = hlv.Update(nanBar);

        // Result should be finite and close to previous (RMA smoothed)
        Assert.True(double.IsFinite(result.Value), "Result should be finite when using last valid estimator");
        Assert.True(result.Value >= 0, "Volatility should be non-negative");
        double relativeDiff = Math.Abs(result.Value - valueBeforeInvalid) / valueBeforeInvalid;
        Assert.True(relativeDiff < 0.2, $"Value should be similar to previous: {valueBeforeInvalid} vs {result.Value}");
    }

    [Fact]
    public void Update_WithInfinityValues_UsesLastValidEstimator()
    {
        var hlv = new Hlv(period: 5);
        var bars = GenerateTestData(10);

        for (int i = 0; i < bars.Count; i++)
        {
            hlv.Update(bars[i]);
        }
        var valueBeforeInvalid = hlv.Last.Value;

        // Bar with infinity - should use last valid Parkinson estimator
        var infBar = new TBar(DateTime.UtcNow, 100.0, double.PositiveInfinity, 98.0, 102.0, 1000);
        var result = hlv.Update(infBar);

        Assert.True(double.IsFinite(result.Value), "Result should be finite when using last valid estimator");
        Assert.True(result.Value >= 0, "Volatility should be non-negative");
        double relativeDiff = Math.Abs(result.Value - valueBeforeInvalid) / valueBeforeInvalid;
        Assert.True(relativeDiff < 0.2, $"Value should be similar to previous: {valueBeforeInvalid} vs {result.Value}");
    }

    [Fact]
    public void Update_WithZeroPrices_UsesLastValidEstimator()
    {
        var hlv = new Hlv(period: 5);
        var bars = GenerateTestData(10);

        for (int i = 0; i < bars.Count; i++)
        {
            hlv.Update(bars[i]);
        }
        var valueBeforeInvalid = hlv.Last.Value;

        // Bar with zero low (invalid for log) - should use last valid Parkinson estimator
        var zeroBar = new TBar(DateTime.UtcNow, 100.0, 105.0, 0.0, 102.0, 1000);
        var result = hlv.Update(zeroBar);

        Assert.True(double.IsFinite(result.Value), "Result should be finite when using last valid estimator");
        Assert.True(result.Value >= 0, "Volatility should be non-negative");
        double relativeDiff = Math.Abs(result.Value - valueBeforeInvalid) / valueBeforeInvalid;
        Assert.True(relativeDiff < 0.2, $"Value should be similar to previous: {valueBeforeInvalid} vs {result.Value}");
    }

    [Fact]
    public void Update_WithNegativePrices_UsesLastValidEstimator()
    {
        var hlv = new Hlv(period: 5);
        var bars = GenerateTestData(10);

        for (int i = 0; i < bars.Count; i++)
        {
            hlv.Update(bars[i]);
        }
        var valueBeforeInvalid = hlv.Last.Value;

        // Bar with negative price - should use last valid Parkinson estimator
        var negBar = new TBar(DateTime.UtcNow, 100.0, 105.0, -98.0, 102.0, 1000);
        var result = hlv.Update(negBar);

        Assert.True(double.IsFinite(result.Value), "Result should be finite when using last valid estimator");
        Assert.True(result.Value >= 0, "Volatility should be non-negative");
        double relativeDiff = Math.Abs(result.Value - valueBeforeInvalid) / valueBeforeInvalid;
        Assert.True(relativeDiff < 0.2, $"Value should be similar to previous: {valueBeforeInvalid} vs {result.Value}");
    }

    #endregion

    #region Batch and Series Tests

    [Fact]
    public void Batch_MatchesStreamingResults()
    {
        const int dataCount = 100;
        var bars = GenerateTestData(dataCount);

        // Streaming
        var hlvStreaming = new Hlv(period: 10);
        var streamingResults = new double[dataCount];
        for (int i = 0; i < dataCount; i++)
        {
            streamingResults[i] = hlvStreaming.Update(bars[i]).Value;
        }

        // Batch (HLV only uses high-low)
        var highs = new double[dataCount];
        var lows = new double[dataCount];
        var batchResults = new double[dataCount];

        for (int i = 0; i < dataCount; i++)
        {
            highs[i] = bars[i].High;
            lows[i] = bars[i].Low;
        }

        Hlv.Batch(highs, lows, batchResults, period: 10);

        // Compare last 50 values (after warmup)
        for (int i = 50; i < dataCount; i++)
        {
            Assert.Equal(streamingResults[i], batchResults[i], Tolerance);
        }
    }

    [Fact]
    public void Calculate_TBarSeries_ReturnsCorrectLength()
    {
        const int dataCount = 50;
        var barSeries = GenerateTestData(dataCount);

        var result = Hlv.Calculate(barSeries, period: 10);

        Assert.Equal(dataCount, result.Count);
    }

    [Fact]
    public void Update_TBarSeries_MatchesStreamingResults()
    {
        const int dataCount = 50;
        var barSeries = GenerateTestData(dataCount);

        // Series update
        var hlvSeries = new Hlv(period: 10);
        var seriesResult = hlvSeries.Update(barSeries);

        // Streaming
        var hlvStreaming = new Hlv(period: 10);
        var streamingResults = new double[dataCount];
        for (int i = 0; i < dataCount; i++)
        {
            streamingResults[i] = hlvStreaming.Update(barSeries[i]).Value;
        }

        // Compare last 30 values
        for (int i = 20; i < dataCount; i++)
        {
            Assert.Equal(streamingResults[i], seriesResult.Values[i], Tolerance);
        }
    }

    [Fact]
    public void Batch_EmptyInput_DoesNotThrow()
    {
        var highs = Array.Empty<double>();
        var lows = Array.Empty<double>();
        var output = Array.Empty<double>();

        // Should not throw
        Hlv.Batch(highs, lows, output, period: 10);
        Assert.Empty(output);
    }

    [Fact]
    public void Batch_MismatchedLengths_ThrowsArgumentException()
    {
        var highs = new double[10];
        var lows = new double[5]; // Mismatched
        var output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() =>
            Hlv.Batch(highs, lows, output, period: 10));
        Assert.Equal("low", ex.ParamName);
    }

    [Fact]
    public void Batch_OutputTooShort_ThrowsArgumentException()
    {
        var highs = new double[10];
        var lows = new double[10];
        var output = new double[5]; // Too short

        var ex = Assert.Throws<ArgumentException>(() =>
            Hlv.Batch(highs, lows, output, period: 10));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_InvalidPeriod_ThrowsArgumentException()
    {
        var highs = new double[10];
        var lows = new double[10];
        var output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() =>
            Hlv.Batch(highs, lows, output, period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    #endregion

    #region Event Publishing Tests

    [Fact]
    public void Update_PublishesEvent()
    {
        var hlv = new Hlv(period: 5);
        bool eventFired = false;
        hlv.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        var bar = new TBar(DateTime.UtcNow, 100.0, 105.0, 98.0, 102.0, 1000);
        hlv.Update(bar);

        Assert.True(eventFired);
    }

    [Fact]
    public void ChainedIndicator_ReceivesValues()
    {
        var source = new Hlv(period: 5);
        var downstream = new Sma(source, period: 3);

        var bars = GenerateTestData(10);
        for (int i = 0; i < bars.Count; i++)
        {
            source.Update(bars[i]);
        }

        Assert.True(downstream.Last.Value > 0, "Downstream indicator should receive values");
    }

    #endregion

    #region TValue Update Tests

    [Fact]
    public void Update_TValue_TreatsAsPrecomputedEstimator()
    {
        var hlv1 = new Hlv(period: 5);
        var hlv2 = new Hlv(period: 5);

        // For hlv1, use bar data
        var bar = new TBar(DateTime.UtcNow, 100.0, 105.0, 98.0, 102.0, 1000);
        hlv1.Update(bar);

        // For hlv2, use pre-computed Parkinson estimator value
        // Compute manually: (1/(4*ln(2))) * (ln(105)-ln(98))^2
        double lnH = Math.Log(105.0);
        double lnL = Math.Log(98.0);
        double hlRange = lnH - lnL;
        double C_4LN2_INV = 0.36067376022224085; // 1 / (4 * ln(2))
        double pkEstimator = C_4LN2_INV * hlRange * hlRange;

        var tvalue = new TValue(bar.Time, pkEstimator);
        hlv2.Update(tvalue);

        Assert.Equal(hlv1.Last.Value, hlv2.Last.Value, Tolerance);
    }

    #endregion

    #region Additional Tests

    [Fact]
    public void LargeDataset_Performance()
    {
        var hlv = new Hlv(period: 20);
        var bars = GenerateTestData(5000);

        for (int i = 0; i < bars.Count; i++)
        {
            var result = hlv.Update(bars[i]);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void DifferentParameters_ProduceDistinctValues()
    {
        var bars = GenerateTestData(50);

        var hlv1 = new Hlv(period: 10);
        var hlv2 = new Hlv(period: 20);
        var hlv3 = new Hlv(period: 10, annualize: false);

        for (int i = 0; i < bars.Count; i++)
        {
            hlv1.Update(bars[i]);
            hlv2.Update(bars[i]);
            hlv3.Update(bars[i]);
        }

        Assert.True(double.IsFinite(hlv1.Last.Value));
        Assert.True(double.IsFinite(hlv2.Last.Value));
        Assert.True(double.IsFinite(hlv3.Last.Value));
        // Different parameters should produce different values
        Assert.NotEqual(hlv1.Last.Value, hlv2.Last.Value);
        Assert.NotEqual(hlv1.Last.Value, hlv3.Last.Value);
    }

    [Fact]
    public void StaticCalculate_Works()
    {
        var bars = GenerateTestData(100);

        var result = Hlv.Calculate(bars, period: 14);

        Assert.Equal(100, result.Count);
        Assert.True(double.IsFinite(result[result.Count - 1].Value));
    }

    [Fact]
    public void StaticCalculate_ValidatesInput()
    {
        var bars = GenerateTestData(10);

        Assert.Throws<ArgumentException>(() => Hlv.Calculate(bars, period: 0));
        Assert.Throws<ArgumentException>(() => Hlv.Calculate(bars, period: -1));
        Assert.Throws<ArgumentException>(() => Hlv.Calculate(bars, period: 10, annualize: true, annualPeriods: 0));
    }

    [Fact]
    public void Prime_Works()
    {
        var hlv = new Hlv(period: 5);
        var values = new double[] { 0.001, 0.002, 0.0015, 0.0018, 0.0012, 0.0022 };

        hlv.Prime(values);

        Assert.True(hlv.IsHot);
        Assert.True(double.IsFinite(hlv.Last.Value));
    }

    [Fact]
    public void Hlv_OnlyUsesHighLow_NotOpenClose()
    {
        // HLV (Parkinson) only uses High-Low, so changing Open/Close shouldn't affect result
        var hlv1 = new Hlv(period: 5);
        var hlv2 = new Hlv(period: 5);

        // Bar with same High-Low but different Open-Close
        var bar1 = new TBar(DateTime.UtcNow, 100.0, 105.0, 98.0, 102.0, 1000);
        var bar2 = new TBar(DateTime.UtcNow, 99.0, 105.0, 98.0, 104.0, 1000); // Different O/C

        var result1 = hlv1.Update(bar1).Value;
        var result2 = hlv2.Update(bar2).Value;

        // Results should be identical since only H-L matters
        Assert.Equal(result1, result2, Tolerance);
    }

    #endregion
}