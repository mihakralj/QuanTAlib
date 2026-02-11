namespace QuanTAlib.Tests;
using Xunit;

public class GkvTests
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
        var gkv = new Gkv();
        Assert.Equal(20, gkv.Period);
        Assert.True(gkv.Annualize);
        Assert.Equal(252, gkv.AnnualPeriods);
        Assert.Equal("Gkv(20)", gkv.Name);
        Assert.Equal(20, gkv.WarmupPeriod);
    }

    [Fact]
    public void Constructor_CustomParameters_SetsCorrectValues()
    {
        var gkv = new Gkv(period: 10, annualize: false, annualPeriods: 365);
        Assert.Equal(10, gkv.Period);
        Assert.False(gkv.Annualize);
        Assert.Equal(365, gkv.AnnualPeriods);
        Assert.Equal("Gkv(10)", gkv.Name);
    }

    [Fact]
    public void Constructor_ZeroPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Gkv(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Gkv(period: -1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroAnnualPeriodsWhenAnnualizing_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Gkv(period: 10, annualize: true, annualPeriods: 0));
        Assert.Equal("annualPeriods", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroAnnualPeriodsWhenNotAnnualizing_DoesNotThrow()
    {
        var gkv = new Gkv(period: 10, annualize: false, annualPeriods: 0);
        Assert.Equal(0, gkv.AnnualPeriods);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_SingleBar_ReturnsNonNegativeValue()
    {
        var gkv = new Gkv(period: 5);
        var bar = new TBar(DateTime.UtcNow, 100.0, 105.0, 98.0, 102.0, 1000);
        var result = gkv.Update(bar);

        Assert.True(result.Value >= 0, "GKV should return non-negative values");
    }

    [Fact]
    public void Update_MultipleBars_ReturnsCorrectCount()
    {
        var gkv = new Gkv(period: 5);
        var bars = GenerateTestData(10);

        for (int i = 0; i < bars.Count; i++)
        {
            gkv.Update(bars[i]);
        }

        Assert.True(gkv.IsHot, "Indicator should be hot after warmup period");
    }

    [Fact]
    public void Update_ReturnsLastValue()
    {
        var gkv = new Gkv(period: 5);
        var bar = new TBar(DateTime.UtcNow, 100.0, 105.0, 98.0, 102.0, 1000);
        var result = gkv.Update(bar);

        Assert.Equal(result.Value, gkv.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_WithoutAnnualization_ReturnsSmallerValues()
    {
        var gkvAnnual = new Gkv(period: 10, annualize: true, annualPeriods: 252);
        var gkvNoAnnual = new Gkv(period: 10, annualize: false);
        var bars = GenerateTestData(20);

        double lastAnnual = 0;
        double lastNoAnnual = 0;
        for (int i = 0; i < bars.Count; i++)
        {
            lastAnnual = gkvAnnual.Update(bars[i]).Value;
            lastNoAnnual = gkvNoAnnual.Update(bars[i]).Value;
        }

        // Annualized values should be larger by factor of sqrt(252)
        Assert.True(lastAnnual > lastNoAnnual, "Annualized values should be larger");
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var gkv = new Gkv(period: 5);
        var bar1 = new TBar(DateTime.UtcNow, 100.0, 105.0, 98.0, 102.0, 1000);
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 102.0, 107.0, 100.0, 105.0, 1000);

        gkv.Update(bar1, isNew: true);
        var result1 = gkv.Last.Value;

        gkv.Update(bar2, isNew: true);
        var result2 = gkv.Last.Value;

        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public void Update_IsNewFalse_UpdatesCurrentBar()
    {
        var gkv = new Gkv(period: 5);
        var bar1 = new TBar(DateTime.UtcNow, 100.0, 105.0, 98.0, 102.0, 1000);

        gkv.Update(bar1, isNew: true);
        var firstValue = gkv.Last.Value;

        // Update the same bar with different values
        var bar1Updated = new TBar(DateTime.UtcNow, 100.0, 110.0, 95.0, 108.0, 1000);
        gkv.Update(bar1Updated, isNew: false);
        var updatedValue = gkv.Last.Value;

        Assert.NotEqual(firstValue, updatedValue);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoresState()
    {
        var gkv = new Gkv(period: 5);
        var bars = GenerateTestData(10);

        // Process first 5 bars
        for (int i = 0; i < 5; i++)
        {
            gkv.Update(bars[i], isNew: true);
        }

        // Add bar 6 and correct multiple times
        gkv.Update(bars[5], isNew: true);
        gkv.Update(bars[5], isNew: false);
        gkv.Update(bars[5], isNew: false);
        gkv.Update(bars[5], isNew: false);

        // Now continue with bar 7
        gkv.Update(bars[6], isNew: true);

        // Create new instance and process same data
        var gkv2 = new Gkv(period: 5);
        for (int i = 0; i < 7; i++)
        {
            gkv2.Update(bars[i], isNew: true);
        }

        Assert.Equal(gkv.Last.Value, gkv2.Last.Value, Tolerance);
    }

    #endregion

    #region IsHot and Warmup Tests

    [Fact]
    public void IsHot_BeforeWarmup_ReturnsFalse()
    {
        var gkv = new Gkv(period: 10);
        var bars = GenerateTestData(5);

        for (int i = 0; i < bars.Count; i++)
        {
            gkv.Update(bars[i]);
        }

        Assert.False(gkv.IsHot);
    }

    [Fact]
    public void IsHot_AfterWarmup_ReturnsTrue()
    {
        var gkv = new Gkv(period: 10);
        var bars = GenerateTestData(15);

        for (int i = 0; i < bars.Count; i++)
        {
            gkv.Update(bars[i]);
        }

        Assert.True(gkv.IsHot);
    }

    [Fact]
    public void IsHot_ExactlyAtWarmup_ReturnsTrue()
    {
        var gkv = new Gkv(period: 10);
        var bars = GenerateTestData(10);

        for (int i = 0; i < bars.Count; i++)
        {
            gkv.Update(bars[i]);
        }

        Assert.True(gkv.IsHot);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsState()
    {
        var gkv = new Gkv(period: 5);
        var bars = GenerateTestData(10);

        for (int i = 0; i < bars.Count; i++)
        {
            gkv.Update(bars[i]);
        }

        gkv.Reset();

        Assert.False(gkv.IsHot);
        Assert.Equal(0, gkv.Last.Value);
    }

    [Fact]
    public void Reset_AllowsReprocessing()
    {
        var gkv = new Gkv(period: 5);
        var bars = GenerateTestData(10);

        // First pass
        for (int i = 0; i < bars.Count; i++)
        {
            gkv.Update(bars[i]);
        }
        var firstResult = gkv.Last.Value;

        // Reset and second pass
        gkv.Reset();
        for (int i = 0; i < bars.Count; i++)
        {
            gkv.Update(bars[i]);
        }
        var secondResult = gkv.Last.Value;

        Assert.Equal(firstResult, secondResult, Tolerance);
    }

    #endregion

    #region Robustness Tests

    [Fact]
    public void Update_WithNaNValues_UsesLastValidEstimator()
    {
        var gkv = new Gkv(period: 5);
        var bars = GenerateTestData(10);

        for (int i = 0; i < bars.Count; i++)
        {
            gkv.Update(bars[i]);
        }
        var valueBeforeInvalid = gkv.Last.Value;

        // Bar with NaN close - should use last valid GK estimator
        var nanBar = new TBar(DateTime.UtcNow, 100.0, 105.0, 98.0, double.NaN, 1000);
        var result = gkv.Update(nanBar);

        // Result should be finite and close to previous (RMA smoothed)
        Assert.True(double.IsFinite(result.Value), "Result should be finite when using last valid estimator");
        Assert.True(result.Value >= 0, "Volatility should be non-negative");
        // Value should be similar (within 20% relative) since same estimator is used
        double relativeDiff = Math.Abs(result.Value - valueBeforeInvalid) / valueBeforeInvalid;
        Assert.True(relativeDiff < 0.2, $"Value should be similar to previous: {valueBeforeInvalid} vs {result.Value}");
    }

    [Fact]
    public void Update_WithInfinityValues_UsesLastValidEstimator()
    {
        var gkv = new Gkv(period: 5);
        var bars = GenerateTestData(10);

        for (int i = 0; i < bars.Count; i++)
        {
            gkv.Update(bars[i]);
        }
        var valueBeforeInvalid = gkv.Last.Value;

        // Bar with infinity - should use last valid GK estimator
        var infBar = new TBar(DateTime.UtcNow, 100.0, double.PositiveInfinity, 98.0, 102.0, 1000);
        var result = gkv.Update(infBar);

        Assert.True(double.IsFinite(result.Value), "Result should be finite when using last valid estimator");
        Assert.True(result.Value >= 0, "Volatility should be non-negative");
        double relativeDiff = Math.Abs(result.Value - valueBeforeInvalid) / valueBeforeInvalid;
        Assert.True(relativeDiff < 0.2, $"Value should be similar to previous: {valueBeforeInvalid} vs {result.Value}");
    }

    [Fact]
    public void Update_WithZeroPrices_UsesLastValidEstimator()
    {
        var gkv = new Gkv(period: 5);
        var bars = GenerateTestData(10);

        for (int i = 0; i < bars.Count; i++)
        {
            gkv.Update(bars[i]);
        }
        var valueBeforeInvalid = gkv.Last.Value;

        // Bar with zero close (invalid for log) - should use last valid GK estimator
        var zeroBar = new TBar(DateTime.UtcNow, 100.0, 105.0, 98.0, 0.0, 1000);
        var result = gkv.Update(zeroBar);

        Assert.True(double.IsFinite(result.Value), "Result should be finite when using last valid estimator");
        Assert.True(result.Value >= 0, "Volatility should be non-negative");
        double relativeDiff = Math.Abs(result.Value - valueBeforeInvalid) / valueBeforeInvalid;
        Assert.True(relativeDiff < 0.2, $"Value should be similar to previous: {valueBeforeInvalid} vs {result.Value}");
    }

    [Fact]
    public void Update_WithNegativePrices_UsesLastValidEstimator()
    {
        var gkv = new Gkv(period: 5);
        var bars = GenerateTestData(10);

        for (int i = 0; i < bars.Count; i++)
        {
            gkv.Update(bars[i]);
        }
        var valueBeforeInvalid = gkv.Last.Value;

        // Bar with negative price - should use last valid GK estimator
        var negBar = new TBar(DateTime.UtcNow, 100.0, 105.0, -98.0, 102.0, 1000);
        var result = gkv.Update(negBar);

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
        var gkvStreaming = new Gkv(period: 10);
        var streamingResults = new double[dataCount];
        for (int i = 0; i < dataCount; i++)
        {
            streamingResults[i] = gkvStreaming.Update(bars[i]).Value;
        }

        // Batch
        var opens = new double[dataCount];
        var highs = new double[dataCount];
        var lows = new double[dataCount];
        var closes = new double[dataCount];
        var batchResults = new double[dataCount];

        for (int i = 0; i < dataCount; i++)
        {
            opens[i] = bars[i].Open;
            highs[i] = bars[i].High;
            lows[i] = bars[i].Low;
            closes[i] = bars[i].Close;
        }

        Gkv.Batch(opens, highs, lows, closes, batchResults, period: 10);

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

        var result = Gkv.Batch(barSeries, period: 10);

        Assert.Equal(dataCount, result.Count);
    }

    [Fact]
    public void Update_TBarSeries_MatchesStreamingResults()
    {
        const int dataCount = 50;
        var barSeries = GenerateTestData(dataCount);

        // Series update
        var gkvSeries = new Gkv(period: 10);
        var seriesResult = gkvSeries.Update(barSeries);

        // Streaming
        var gkvStreaming = new Gkv(period: 10);
        var streamingResults = new double[dataCount];
        for (int i = 0; i < dataCount; i++)
        {
            streamingResults[i] = gkvStreaming.Update(barSeries[i]).Value;
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
        var opens = Array.Empty<double>();
        var highs = Array.Empty<double>();
        var lows = Array.Empty<double>();
        var closes = Array.Empty<double>();
        var output = Array.Empty<double>();

        // Should not throw
        Gkv.Batch(opens, highs, lows, closes, output, period: 10);
        Assert.Empty(output);
    }

    [Fact]
    public void Batch_MismatchedLengths_ThrowsArgumentException()
    {
        var opens = new double[10];
        var highs = new double[5]; // Mismatched
        var lows = new double[10];
        var closes = new double[10];
        var output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() =>
            Gkv.Batch(opens, highs, lows, closes, output, period: 10));
        Assert.Equal("high", ex.ParamName);
    }

    [Fact]
    public void Batch_OutputTooShort_ThrowsArgumentException()
    {
        var opens = new double[10];
        var highs = new double[10];
        var lows = new double[10];
        var closes = new double[10];
        var output = new double[5]; // Too short

        var ex = Assert.Throws<ArgumentException>(() =>
            Gkv.Batch(opens, highs, lows, closes, output, period: 10));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_InvalidPeriod_ThrowsArgumentException()
    {
        var opens = new double[10];
        var highs = new double[10];
        var lows = new double[10];
        var closes = new double[10];
        var output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() =>
            Gkv.Batch(opens, highs, lows, closes, output, period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    #endregion

    #region Event Publishing Tests

    [Fact]
    public void Update_PublishesEvent()
    {
        var gkv = new Gkv(period: 5);
        bool eventFired = false;
        gkv.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        var bar = new TBar(DateTime.UtcNow, 100.0, 105.0, 98.0, 102.0, 1000);
        gkv.Update(bar);

        Assert.True(eventFired);
    }

    [Fact]
    public void ChainedIndicator_ReceivesValues()
    {
        var source = new Gkv(period: 5);
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
        var gkv1 = new Gkv(period: 5);
        var gkv2 = new Gkv(period: 5);

        // For gkv1, use bar data
        var bar = new TBar(DateTime.UtcNow, 100.0, 105.0, 98.0, 102.0, 1000);
        gkv1.Update(bar);

        // For gkv2, use pre-computed estimator value
        // Compute manually: 0.5*(ln(105)-ln(98))^2 - 0.386294*(ln(102)-ln(100))^2
        double lnH = Math.Log(105.0);
        double lnL = Math.Log(98.0);
        double lnO = Math.Log(100.0);
        double lnC = Math.Log(102.0);
        double term1 = 0.5 * Math.Pow(lnH - lnL, 2);
        double term2 = 0.38629436111989061883 * Math.Pow(lnC - lnO, 2);
        double gkEstimator = term1 - term2;

        var tvalue = new TValue(bar.Time, gkEstimator);
        gkv2.Update(tvalue);

        Assert.Equal(gkv1.Last.Value, gkv2.Last.Value, Tolerance);
    }

    #endregion

    #region Additional Tests

    [Fact]
    public void LargeDataset_Performance()
    {
        var gkv = new Gkv(period: 20);
        var bars = GenerateTestData(5000);

        for (int i = 0; i < bars.Count; i++)
        {
            var result = gkv.Update(bars[i]);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void DifferentParameters_ProduceDistinctValues()
    {
        var bars = GenerateTestData(50);

        var gkv1 = new Gkv(period: 10);
        var gkv2 = new Gkv(period: 20);
        var gkv3 = new Gkv(period: 10, annualize: false);

        for (int i = 0; i < bars.Count; i++)
        {
            gkv1.Update(bars[i]);
            gkv2.Update(bars[i]);
            gkv3.Update(bars[i]);
        }

        Assert.True(double.IsFinite(gkv1.Last.Value));
        Assert.True(double.IsFinite(gkv2.Last.Value));
        Assert.True(double.IsFinite(gkv3.Last.Value));
        // Different parameters should produce different values
        Assert.NotEqual(gkv1.Last.Value, gkv2.Last.Value);
        Assert.NotEqual(gkv1.Last.Value, gkv3.Last.Value);
    }

    [Fact]
    public void StaticCalculate_Works()
    {
        var bars = GenerateTestData(100);

        var result = Gkv.Batch(bars, period: 14);

        Assert.Equal(100, result.Count);
        Assert.True(double.IsFinite(result[result.Count - 1].Value));
    }

    [Fact]
    public void StaticCalculate_ValidatesInput()
    {
        var bars = GenerateTestData(10);

        Assert.Throws<ArgumentException>(() => Gkv.Batch(bars, period: 0));
        Assert.Throws<ArgumentException>(() => Gkv.Batch(bars, period: -1));
        Assert.Throws<ArgumentException>(() => Gkv.Batch(bars, period: 10, annualize: true, annualPeriods: 0));
    }

    [Fact]
    public void Prime_Works()
    {
        var gkv = new Gkv(period: 5);
        var values = new double[] { 0.001, 0.002, 0.0015, 0.0018, 0.0012, 0.0022 };

        gkv.Prime(values);

        Assert.True(gkv.IsHot);
        Assert.True(double.IsFinite(gkv.Last.Value));
    }

    #endregion
}