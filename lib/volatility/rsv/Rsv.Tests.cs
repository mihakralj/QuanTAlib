namespace QuanTAlib.Tests;
using Xunit;

public class RsvTests
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
        var rsv = new Rsv();
        Assert.Equal(20, rsv.Period);
        Assert.True(rsv.Annualize);
        Assert.Equal(252, rsv.AnnualPeriods);
        Assert.Equal("Rsv(20)", rsv.Name);
        Assert.Equal(20, rsv.WarmupPeriod);
    }

    [Fact]
    public void Constructor_CustomParameters_SetsCorrectValues()
    {
        var rsv = new Rsv(period: 10, annualize: false, annualPeriods: 365);
        Assert.Equal(10, rsv.Period);
        Assert.False(rsv.Annualize);
        Assert.Equal(365, rsv.AnnualPeriods);
        Assert.Equal("Rsv(10)", rsv.Name);
    }

    [Fact]
    public void Constructor_ZeroPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Rsv(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Rsv(period: -1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroAnnualPeriodsWhenAnnualizing_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Rsv(period: 10, annualize: true, annualPeriods: 0));
        Assert.Equal("annualPeriods", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroAnnualPeriodsWhenNotAnnualizing_DoesNotThrow()
    {
        var rsv = new Rsv(period: 10, annualize: false, annualPeriods: 0);
        Assert.Equal(0, rsv.AnnualPeriods);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_SingleBar_ReturnsNonNegativeValue()
    {
        var rsv = new Rsv(period: 5);
        var bar = new TBar(DateTime.UtcNow, 100.0, 105.0, 98.0, 102.0, 1000);
        var result = rsv.Update(bar);

        Assert.True(result.Value >= 0, "RSV should return non-negative values");
    }

    [Fact]
    public void Update_MultipleBars_ReturnsCorrectCount()
    {
        var rsv = new Rsv(period: 5);
        var bars = GenerateTestData(10);

        for (int i = 0; i < bars.Count; i++)
        {
            rsv.Update(bars[i]);
        }

        Assert.True(rsv.IsHot, "Indicator should be hot after warmup period");
    }

    [Fact]
    public void Update_ReturnsLastValue()
    {
        var rsv = new Rsv(period: 5);
        var bar = new TBar(DateTime.UtcNow, 100.0, 105.0, 98.0, 102.0, 1000);
        var result = rsv.Update(bar);

        Assert.Equal(result.Value, rsv.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_WithoutAnnualization_ReturnsSmallerValues()
    {
        var rsvAnnual = new Rsv(period: 10, annualize: true, annualPeriods: 252);
        var rsvNoAnnual = new Rsv(period: 10, annualize: false);
        var bars = GenerateTestData(20);

        double lastAnnual = 0;
        double lastNoAnnual = 0;
        for (int i = 0; i < bars.Count; i++)
        {
            lastAnnual = rsvAnnual.Update(bars[i]).Value;
            lastNoAnnual = rsvNoAnnual.Update(bars[i]).Value;
        }

        // Annualized values should be larger by factor of sqrt(252)
        Assert.True(lastAnnual > lastNoAnnual, "Annualized values should be larger");
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var rsv = new Rsv(period: 5);
        var bar1 = new TBar(DateTime.UtcNow, 100.0, 105.0, 98.0, 102.0, 1000);
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 102.0, 107.0, 100.0, 105.0, 1000);

        rsv.Update(bar1, isNew: true);
        var result1 = rsv.Last.Value;

        rsv.Update(bar2, isNew: true);
        var result2 = rsv.Last.Value;

        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public void Update_IsNewFalse_UpdatesCurrentBar()
    {
        var rsv = new Rsv(period: 5);
        var bar1 = new TBar(DateTime.UtcNow, 100.0, 105.0, 98.0, 102.0, 1000);

        rsv.Update(bar1, isNew: true);
        var firstValue = rsv.Last.Value;

        // Update the same bar with different OHLC values
        var bar1Updated = new TBar(DateTime.UtcNow, 99.0, 110.0, 95.0, 108.0, 1000);
        rsv.Update(bar1Updated, isNew: false);
        var updatedValue = rsv.Last.Value;

        Assert.NotEqual(firstValue, updatedValue);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoresState()
    {
        var rsv = new Rsv(period: 5);
        var bars = GenerateTestData(10);

        // Process first 5 bars
        for (int i = 0; i < 5; i++)
        {
            rsv.Update(bars[i], isNew: true);
        }

        // Add bar 6 and correct multiple times
        rsv.Update(bars[5], isNew: true);
        rsv.Update(bars[5], isNew: false);
        rsv.Update(bars[5], isNew: false);
        rsv.Update(bars[5], isNew: false);

        // Now continue with bar 7
        rsv.Update(bars[6], isNew: true);

        // Create new instance and process same data
        var rsv2 = new Rsv(period: 5);
        for (int i = 0; i < 7; i++)
        {
            rsv2.Update(bars[i], isNew: true);
        }

        Assert.Equal(rsv.Last.Value, rsv2.Last.Value, Tolerance);
    }

    #endregion

    #region IsHot and Warmup Tests

    [Fact]
    public void IsHot_BeforeWarmup_ReturnsFalse()
    {
        var rsv = new Rsv(period: 10);
        var bars = GenerateTestData(5);

        for (int i = 0; i < bars.Count; i++)
        {
            rsv.Update(bars[i]);
        }

        Assert.False(rsv.IsHot);
    }

    [Fact]
    public void IsHot_AfterWarmup_ReturnsTrue()
    {
        var rsv = new Rsv(period: 10);
        var bars = GenerateTestData(15);

        for (int i = 0; i < bars.Count; i++)
        {
            rsv.Update(bars[i]);
        }

        Assert.True(rsv.IsHot);
    }

    [Fact]
    public void IsHot_ExactlyAtWarmup_ReturnsTrue()
    {
        var rsv = new Rsv(period: 10);
        var bars = GenerateTestData(10);

        for (int i = 0; i < bars.Count; i++)
        {
            rsv.Update(bars[i]);
        }

        Assert.True(rsv.IsHot);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsState()
    {
        var rsv = new Rsv(period: 5);
        var bars = GenerateTestData(10);

        for (int i = 0; i < bars.Count; i++)
        {
            rsv.Update(bars[i]);
        }

        rsv.Reset();

        Assert.False(rsv.IsHot);
        Assert.Equal(0, rsv.Last.Value);
    }

    [Fact]
    public void Reset_AllowsReprocessing()
    {
        var rsv = new Rsv(period: 5);
        var bars = GenerateTestData(10);

        // First pass
        for (int i = 0; i < bars.Count; i++)
        {
            rsv.Update(bars[i]);
        }
        var firstResult = rsv.Last.Value;

        // Reset and second pass
        rsv.Reset();
        for (int i = 0; i < bars.Count; i++)
        {
            rsv.Update(bars[i]);
        }
        var secondResult = rsv.Last.Value;

        Assert.Equal(firstResult, secondResult, Tolerance);
    }

    #endregion

    #region Robustness Tests

    [Fact]
    public void Update_WithNaNValues_UsesLastValidVariance()
    {
        var rsv = new Rsv(period: 5);
        var bars = GenerateTestData(10);

        for (int i = 0; i < bars.Count; i++)
        {
            rsv.Update(bars[i]);
        }
        var valueBeforeInvalid = rsv.Last.Value;

        // Bar with NaN high - should use last valid RS variance
        var nanBar = new TBar(DateTime.UtcNow, 100.0, double.NaN, 98.0, 102.0, 1000);
        var result = rsv.Update(nanBar);

        // Result should be finite and close to previous (SMA smoothed)
        Assert.True(double.IsFinite(result.Value), "Result should be finite when using last valid variance");
        Assert.True(result.Value >= 0, "Volatility should be non-negative");
        double relativeDiff = Math.Abs(result.Value - valueBeforeInvalid) / valueBeforeInvalid;
        Assert.True(relativeDiff < 0.2, $"Value should be similar to previous: {valueBeforeInvalid} vs {result.Value}");
    }

    [Fact]
    public void Update_WithInfinityValues_UsesLastValidVariance()
    {
        var rsv = new Rsv(period: 5);
        var bars = GenerateTestData(10);

        for (int i = 0; i < bars.Count; i++)
        {
            rsv.Update(bars[i]);
        }
        var valueBeforeInvalid = rsv.Last.Value;

        // Bar with infinity - should use last valid RS variance
        var infBar = new TBar(DateTime.UtcNow, 100.0, double.PositiveInfinity, 98.0, 102.0, 1000);
        var result = rsv.Update(infBar);

        Assert.True(double.IsFinite(result.Value), "Result should be finite when using last valid variance");
        Assert.True(result.Value >= 0, "Volatility should be non-negative");
        double relativeDiff = Math.Abs(result.Value - valueBeforeInvalid) / valueBeforeInvalid;
        Assert.True(relativeDiff < 0.2, $"Value should be similar to previous: {valueBeforeInvalid} vs {result.Value}");
    }

    [Fact]
    public void Update_WithZeroPrices_UsesLastValidVariance()
    {
        var rsv = new Rsv(period: 5);
        var bars = GenerateTestData(10);

        for (int i = 0; i < bars.Count; i++)
        {
            rsv.Update(bars[i]);
        }
        var valueBeforeInvalid = rsv.Last.Value;

        // Bar with zero low (invalid for log) - should use last valid RS variance
        var zeroBar = new TBar(DateTime.UtcNow, 100.0, 105.0, 0.0, 102.0, 1000);
        var result = rsv.Update(zeroBar);

        Assert.True(double.IsFinite(result.Value), "Result should be finite when using last valid variance");
        Assert.True(result.Value >= 0, "Volatility should be non-negative");
        double relativeDiff = Math.Abs(result.Value - valueBeforeInvalid) / valueBeforeInvalid;
        Assert.True(relativeDiff < 0.2, $"Value should be similar to previous: {valueBeforeInvalid} vs {result.Value}");
    }

    [Fact]
    public void Update_WithNegativePrices_UsesLastValidVariance()
    {
        var rsv = new Rsv(period: 5);
        var bars = GenerateTestData(10);

        for (int i = 0; i < bars.Count; i++)
        {
            rsv.Update(bars[i]);
        }
        var valueBeforeInvalid = rsv.Last.Value;

        // Bar with negative price - should use last valid RS variance
        var negBar = new TBar(DateTime.UtcNow, 100.0, 105.0, -98.0, 102.0, 1000);
        var result = rsv.Update(negBar);

        Assert.True(double.IsFinite(result.Value), "Result should be finite when using last valid variance");
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
        var rsvStreaming = new Rsv(period: 10);
        var streamingResults = new double[dataCount];
        for (int i = 0; i < dataCount; i++)
        {
            streamingResults[i] = rsvStreaming.Update(bars[i]).Value;
        }

        // Batch (RSV uses all OHLC)
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

        Rsv.Batch(opens, highs, lows, closes, batchResults, period: 10);

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

        var result = Rsv.Calculate(barSeries, period: 10);

        Assert.Equal(dataCount, result.Count);
    }

    [Fact]
    public void Update_TBarSeries_MatchesStreamingResults()
    {
        const int dataCount = 50;
        var barSeries = GenerateTestData(dataCount);

        // Series update
        var rsvSeries = new Rsv(period: 10);
        var seriesResult = rsvSeries.Update(barSeries);

        // Streaming
        var rsvStreaming = new Rsv(period: 10);
        var streamingResults = new double[dataCount];
        for (int i = 0; i < dataCount; i++)
        {
            streamingResults[i] = rsvStreaming.Update(barSeries[i]).Value;
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
        Rsv.Batch(opens, highs, lows, closes, output, period: 10);
        Assert.Empty(output);
    }

    [Fact]
    public void Batch_MismatchedLengths_ThrowsArgumentException()
    {
        var opens = new double[10];
        var highs = new double[10];
        var lows = new double[5]; // Mismatched
        var closes = new double[10];
        var output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() =>
            Rsv.Batch(opens, highs, lows, closes, output, period: 10));
        Assert.Equal("close", ex.ParamName);
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
            Rsv.Batch(opens, highs, lows, closes, output, period: 10));
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
            Rsv.Batch(opens, highs, lows, closes, output, period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    #endregion

    #region Event Publishing Tests

    [Fact]
    public void Update_PublishesEvent()
    {
        var rsv = new Rsv(period: 5);
        bool eventFired = false;
        rsv.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        var bar = new TBar(DateTime.UtcNow, 100.0, 105.0, 98.0, 102.0, 1000);
        rsv.Update(bar);

        Assert.True(eventFired);
    }

    [Fact]
    public void ChainedIndicator_ReceivesValues()
    {
        var source = new Rsv(period: 5);
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
    public void Update_TValue_TreatsAsPrecomputedVariance()
    {
        var rsv1 = new Rsv(period: 5);
        var rsv2 = new Rsv(period: 5);

        // For rsv1, use bar data
        var bar = new TBar(DateTime.UtcNow, 100.0, 105.0, 98.0, 102.0, 1000);
        rsv1.Update(bar);

        // For rsv2, use pre-computed RS variance value
        // Compute manually following the formula
        double o = 100.0, h = 105.0, l = 98.0, c = 102.0;
        double term1 = Math.Log(h / o);
        double term2 = Math.Log(h / c);
        double term3 = Math.Log(l / o);
        double term4 = Math.Log(l / c);
        double rsVariance = (term1 * term2) + (term3 * term4);

        var tvalue = new TValue(bar.Time, rsVariance);
        rsv2.Update(tvalue);

        Assert.Equal(rsv1.Last.Value, rsv2.Last.Value, Tolerance);
    }

    #endregion

    #region RSV-Specific Tests

    [Fact]
    public void Rsv_UsesAllOhlcPrices()
    {
        // RSV uses all OHLC, so changing Open should affect result (unlike HLV)
        var rsv1 = new Rsv(period: 5);
        var rsv2 = new Rsv(period: 5);

        // Bars with same H-L-C but different Open
        var bar1 = new TBar(DateTime.UtcNow, 100.0, 105.0, 98.0, 102.0, 1000);
        var bar2 = new TBar(DateTime.UtcNow, 99.0, 105.0, 98.0, 102.0, 1000); // Different Open

        var result1 = rsv1.Update(bar1).Value;
        var result2 = rsv2.Update(bar2).Value;

        // Results should be different since Open matters for RSV
        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public void Rsv_UsesSmaMakesSmoothTransitions()
    {
        // SMA should produce smoother transitions than RMA
        var rsv = new Rsv(period: 10);
        var bars = GenerateTestData(50);

        var results = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            results[i] = rsv.Update(bars[i]).Value;
        }

        // Check that results don't have extreme jumps after warmup
        for (int i = 11; i < bars.Count; i++)
        {
            double change = Math.Abs(results[i] - results[i - 1]);
            double avg = (results[i] + results[i - 1]) / 2;
            if (avg > 0.001) // Avoid division by very small numbers
            {
                double relativeChange = change / avg;
                Assert.True(relativeChange < 0.5, $"SMA should produce smooth transitions: change={relativeChange:P} at index {i}");
            }
        }
    }

    [Fact]
    public void Rsv_DriftAdjusted_HandlesTrendingMarket()
    {
        // RSV is drift-adjusted, so trending markets should still produce reasonable volatility
        var rsv = new Rsv(period: 10, annualize: false);

        // Create trending bars (each bar higher than previous)
        var bars = new TBarSeries();
        double basePrice = 100.0;
        for (int i = 0; i < 20; i++)
        {
            double trend = i * 0.5; // Upward trend
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i),
                basePrice + trend,
                basePrice + trend + 2.0,
                basePrice + trend - 1.0,
                basePrice + trend + 1.5,
                1000
            );
            bars.Add(bar);
            rsv.Update(bar);
        }

        // RSV should still produce reasonable (non-inflated) volatility despite drift
        Assert.True(rsv.Last.Value > 0, "RSV should be positive");
        Assert.True(rsv.Last.Value < 1.0, "RSV (non-annualized) should be reasonable despite trending market");
    }

    [Fact]
    public void LargeDataset_Performance()
    {
        var rsv = new Rsv(period: 20);
        var bars = GenerateTestData(5000);

        for (int i = 0; i < bars.Count; i++)
        {
            var result = rsv.Update(bars[i]);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void DifferentParameters_ProduceDistinctValues()
    {
        var bars = GenerateTestData(50);

        var rsv1 = new Rsv(period: 10);
        var rsv2 = new Rsv(period: 20);
        var rsv3 = new Rsv(period: 10, annualize: false);

        for (int i = 0; i < bars.Count; i++)
        {
            rsv1.Update(bars[i]);
            rsv2.Update(bars[i]);
            rsv3.Update(bars[i]);
        }

        Assert.True(double.IsFinite(rsv1.Last.Value));
        Assert.True(double.IsFinite(rsv2.Last.Value));
        Assert.True(double.IsFinite(rsv3.Last.Value));
        // Different parameters should produce different values
        Assert.NotEqual(rsv1.Last.Value, rsv2.Last.Value);
        Assert.NotEqual(rsv1.Last.Value, rsv3.Last.Value);
    }

    [Fact]
    public void StaticCalculate_Works()
    {
        var bars = GenerateTestData(100);

        var result = Rsv.Calculate(bars, period: 14);

        Assert.Equal(100, result.Count);
        Assert.True(double.IsFinite(result[result.Count - 1].Value));
    }

    [Fact]
    public void StaticCalculate_ValidatesInput()
    {
        var bars = GenerateTestData(10);

        Assert.Throws<ArgumentException>(() => Rsv.Calculate(bars, period: 0));
        Assert.Throws<ArgumentException>(() => Rsv.Calculate(bars, period: -1));
        Assert.Throws<ArgumentException>(() => Rsv.Calculate(bars, period: 10, annualize: true, annualPeriods: 0));
    }

    [Fact]
    public void Prime_Works()
    {
        var rsv = new Rsv(period: 5);
        var values = new double[] { 0.001, 0.002, 0.0015, 0.0018, 0.0012, 0.0022 };

        rsv.Prime(values);

        Assert.True(rsv.IsHot);
        Assert.True(double.IsFinite(rsv.Last.Value));
    }

    #endregion
}