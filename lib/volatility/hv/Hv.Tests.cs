namespace QuanTAlib.Tests;
using Xunit;

public class HvTests
{
    private const double Tolerance = 1e-9;

    private static TBarSeries GenerateTestData(int count = 100)
    {
        var gbm = new GBM(seed: 42);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    private static TSeries GeneratePriceSeries(int count = 100)
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var t = new List<long>(count);
        var v = new List<double>(count);
        for (int i = 0; i < count; i++)
        {
            t.Add(bars[i].Time);
            v.Add(bars[i].Close);
        }
        return new TSeries(t, v);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_DefaultParameters_SetsCorrectValues()
    {
        var hv = new Hv();
        Assert.Equal(20, hv.Period);
        Assert.True(hv.Annualize);
        Assert.Equal(252, hv.AnnualPeriods);
        Assert.Equal("Hv(20)", hv.Name);
        Assert.Equal(21, hv.WarmupPeriod); // period + 1
    }

    [Fact]
    public void Constructor_CustomParameters_SetsCorrectValues()
    {
        var hv = new Hv(period: 10, annualize: false, annualPeriods: 365);
        Assert.Equal(10, hv.Period);
        Assert.False(hv.Annualize);
        Assert.Equal(365, hv.AnnualPeriods);
        Assert.Equal("Hv(10)", hv.Name);
    }

    [Fact]
    public void Constructor_PeriodOne_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Hv(period: 1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Hv(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Hv(period: -1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroAnnualPeriodsWhenAnnualizing_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Hv(period: 10, annualize: true, annualPeriods: 0));
        Assert.Equal("annualPeriods", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroAnnualPeriodsWhenNotAnnualizing_DoesNotThrow()
    {
        var hv = new Hv(period: 10, annualize: false, annualPeriods: 0);
        Assert.Equal(0, hv.AnnualPeriods);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_SinglePrice_ReturnsZero()
    {
        var hv = new Hv(period: 5);
        var price = new TValue(DateTime.UtcNow, 100.0);
        var result = hv.Update(price);

        // First price cannot produce a return, so volatility is 0
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void Update_TwoPrices_ReturnsZero()
    {
        var hv = new Hv(period: 5);
        hv.Update(new TValue(DateTime.UtcNow, 100.0));
        var result = hv.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 101.0));

        // Second price gives first return, but std dev of 1 value is 0
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void Update_MultiplePrices_ReturnsPositiveVolatility()
    {
        var hv = new Hv(period: 5);
        var prices = GeneratePriceSeries(10);

        double lastValue = 0;
        for (int i = 0; i < prices.Count; i++)
        {
            lastValue = hv.Update(prices[i]).Value;
        }

        Assert.True(lastValue > 0, "HV should return positive volatility after warmup");
    }

    [Fact]
    public void Update_ReturnsLastValue()
    {
        var hv = new Hv(period: 5);
        var price = new TValue(DateTime.UtcNow, 100.0);
        var result = hv.Update(price);

        Assert.Equal(result.Value, hv.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_WithoutAnnualization_ReturnsSmallerValues()
    {
        var hvAnnual = new Hv(period: 10, annualize: true, annualPeriods: 252);
        var hvNoAnnual = new Hv(period: 10, annualize: false);
        var prices = GeneratePriceSeries(20);

        double lastAnnual = 0;
        double lastNoAnnual = 0;
        for (int i = 0; i < prices.Count; i++)
        {
            lastAnnual = hvAnnual.Update(prices[i]).Value;
            lastNoAnnual = hvNoAnnual.Update(prices[i]).Value;
        }

        // Annualized values should be larger by factor of sqrt(252)
        Assert.True(lastAnnual > lastNoAnnual, "Annualized values should be larger");
    }

    [Fact]
    public void Update_AnnualizationFactor_Correct()
    {
        var hvAnnual = new Hv(period: 10, annualize: true, annualPeriods: 252);
        var hvNoAnnual = new Hv(period: 10, annualize: false);
        var prices = GeneratePriceSeries(30);

        for (int i = 0; i < prices.Count; i++)
        {
            hvAnnual.Update(prices[i]);
            hvNoAnnual.Update(prices[i]);
        }

        double factor = hvAnnual.Last.Value / hvNoAnnual.Last.Value;
        double expectedFactor = Math.Sqrt(252);

        Assert.Equal(expectedFactor, factor, 1e-6);
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var hv = new Hv(period: 5);
        var prices = GeneratePriceSeries(10);

        // Feed enough prices to get non-zero volatility (need at least 3 returns for variance)
        for (int i = 0; i < 5; i++)
        {
            hv.Update(prices[i], isNew: true);
        }
        var result1 = hv.Last.Value;

        // Add one more price - state should advance
        hv.Update(prices[5], isNew: true);
        var result2 = hv.Last.Value;

        // Both values should be positive (after warmup) and different
        Assert.True(result1 > 0, "First result should be positive after warmup");
        Assert.True(result2 > 0, "Second result should be positive");
        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public void Update_IsNewFalse_UpdatesCurrentBar()
    {
        var hv = new Hv(period: 5);
        var prices = GeneratePriceSeries(6);

        // Process first 5 prices
        for (int i = 0; i < 5; i++)
        {
            hv.Update(prices[i], isNew: true);
        }

        // Add 6th price
        hv.Update(prices[5], isNew: true);
        var firstValue = hv.Last.Value;

        // Update the 6th price with different value
        var updatedPrice = new TValue(prices[5].Time, prices[5].Value * 1.05);
        hv.Update(updatedPrice, isNew: false);
        var updatedValue = hv.Last.Value;

        Assert.NotEqual(firstValue, updatedValue);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoresState()
    {
        var hv = new Hv(period: 5);
        var prices = GeneratePriceSeries(10);

        // Process first 5 prices
        for (int i = 0; i < 5; i++)
        {
            hv.Update(prices[i], isNew: true);
        }

        // Add price 6 and correct multiple times
        hv.Update(prices[5], isNew: true);
        hv.Update(prices[5], isNew: false);
        hv.Update(prices[5], isNew: false);
        hv.Update(prices[5], isNew: false);

        // Now continue with price 7
        hv.Update(prices[6], isNew: true);

        // Create new instance and process same data
        var hv2 = new Hv(period: 5);
        for (int i = 0; i < 7; i++)
        {
            hv2.Update(prices[i], isNew: true);
        }

        Assert.Equal(hv.Last.Value, hv2.Last.Value, Tolerance);
    }

    #endregion

    #region IsHot and Warmup Tests

    [Fact]
    public void IsHot_BeforeWarmup_ReturnsFalse()
    {
        var hv = new Hv(period: 10);
        var prices = GeneratePriceSeries(5);

        for (int i = 0; i < prices.Count; i++)
        {
            hv.Update(prices[i]);
        }

        Assert.False(hv.IsHot);
    }

    [Fact]
    public void IsHot_AfterWarmup_ReturnsTrue()
    {
        var hv = new Hv(period: 10);
        var prices = GeneratePriceSeries(15);

        for (int i = 0; i < prices.Count; i++)
        {
            hv.Update(prices[i]);
        }

        Assert.True(hv.IsHot);
    }

    [Fact]
    public void IsHot_ExactlyAtWarmup_ReturnsTrue()
    {
        // Need period+1 prices to get period returns
        var hv = new Hv(period: 10);
        var prices = GeneratePriceSeries(11); // 11 prices = 10 returns

        for (int i = 0; i < prices.Count; i++)
        {
            hv.Update(prices[i]);
        }

        Assert.True(hv.IsHot);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsState()
    {
        var hv = new Hv(period: 5);
        var prices = GeneratePriceSeries(10);

        for (int i = 0; i < prices.Count; i++)
        {
            hv.Update(prices[i]);
        }

        hv.Reset();

        Assert.False(hv.IsHot);
        Assert.Equal(0, hv.Last.Value);
    }

    [Fact]
    public void Reset_AllowsReprocessing()
    {
        var hv = new Hv(period: 5);
        var prices = GeneratePriceSeries(10);

        // First pass
        for (int i = 0; i < prices.Count; i++)
        {
            hv.Update(prices[i]);
        }
        var firstResult = hv.Last.Value;

        // Reset and second pass
        hv.Reset();
        for (int i = 0; i < prices.Count; i++)
        {
            hv.Update(prices[i]);
        }
        var secondResult = hv.Last.Value;

        Assert.Equal(firstResult, secondResult, Tolerance);
    }

    #endregion

    #region Robustness Tests

    [Fact]
    public void Update_WithNaNValues_UsesLastValidValue()
    {
        var hv = new Hv(period: 5);
        var prices = GeneratePriceSeries(10);

        for (int i = 0; i < prices.Count; i++)
        {
            hv.Update(prices[i]);
        }
        var valueBeforeInvalid = hv.Last.Value;

        // Price with NaN - should use last valid value
        var nanPrice = new TValue(DateTime.UtcNow, double.NaN);
        var result = hv.Update(nanPrice);

        Assert.True(double.IsFinite(result.Value), "Result should be finite when using last valid value");
        Assert.Equal(valueBeforeInvalid, result.Value, Tolerance);
    }

    [Fact]
    public void Update_WithInfinityValues_UsesLastValidValue()
    {
        var hv = new Hv(period: 5);
        var prices = GeneratePriceSeries(10);

        for (int i = 0; i < prices.Count; i++)
        {
            hv.Update(prices[i]);
        }
        var valueBeforeInvalid = hv.Last.Value;

        // Price with infinity - should use last valid value
        var infPrice = new TValue(DateTime.UtcNow, double.PositiveInfinity);
        var result = hv.Update(infPrice);

        Assert.True(double.IsFinite(result.Value), "Result should be finite when using last valid value");
        Assert.Equal(valueBeforeInvalid, result.Value, Tolerance);
    }

    [Fact]
    public void Update_WithZeroPrice_UsesLastValidValue()
    {
        var hv = new Hv(period: 5);
        var prices = GeneratePriceSeries(10);

        for (int i = 0; i < prices.Count; i++)
        {
            hv.Update(prices[i]);
        }
        var valueBeforeInvalid = hv.Last.Value;

        // Zero price - invalid for log return
        var zeroPrice = new TValue(DateTime.UtcNow, 0.0);
        var result = hv.Update(zeroPrice);

        Assert.True(double.IsFinite(result.Value), "Result should be finite when using last valid value");
        Assert.Equal(valueBeforeInvalid, result.Value, Tolerance);
    }

    [Fact]
    public void Update_WithNegativePrice_UsesLastValidValue()
    {
        var hv = new Hv(period: 5);
        var prices = GeneratePriceSeries(10);

        for (int i = 0; i < prices.Count; i++)
        {
            hv.Update(prices[i]);
        }
        var valueBeforeInvalid = hv.Last.Value;

        // Negative price - invalid for log return
        var negPrice = new TValue(DateTime.UtcNow, -100.0);
        var result = hv.Update(negPrice);

        Assert.True(double.IsFinite(result.Value), "Result should be finite when using last valid value");
        Assert.Equal(valueBeforeInvalid, result.Value, Tolerance);
    }

    #endregion

    #region Batch and Series Tests

    [Fact]
    public void Batch_MatchesStreamingResults()
    {
        const int dataCount = 100;
        var prices = GeneratePriceSeries(dataCount);

        // Streaming
        var hvStreaming = new Hv(period: 10);
        var streamingResults = new double[dataCount];
        for (int i = 0; i < dataCount; i++)
        {
            streamingResults[i] = hvStreaming.Update(prices[i]).Value;
        }

        // Batch
        var batchResults = new double[dataCount];
        Hv.Batch(prices.Values, batchResults, period: 10);

        // Compare last 50 values (after warmup)
        for (int i = 50; i < dataCount; i++)
        {
            Assert.Equal(streamingResults[i], batchResults[i], Tolerance);
        }
    }

    [Fact]
    public void Calculate_TSeries_ReturnsCorrectLength()
    {
        const int dataCount = 50;
        var priceSeries = GeneratePriceSeries(dataCount);

        var result = Hv.Batch(priceSeries, period: 10);

        Assert.Equal(dataCount, result.Count);
    }

    [Fact]
    public void Update_TSeries_MatchesStreamingResults()
    {
        const int dataCount = 50;
        var priceSeries = GeneratePriceSeries(dataCount);

        // Series update
        var hvSeries = new Hv(period: 10);
        var seriesResult = hvSeries.Update(priceSeries);

        // Streaming
        var hvStreaming = new Hv(period: 10);
        var streamingResults = new double[dataCount];
        for (int i = 0; i < dataCount; i++)
        {
            streamingResults[i] = hvStreaming.Update(priceSeries[i]).Value;
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
        var prices = Array.Empty<double>();
        var output = Array.Empty<double>();

        // Should not throw
        Hv.Batch(prices, output, period: 10);
        Assert.Empty(output);
    }

    [Fact]
    public void Batch_OutputTooShort_ThrowsArgumentException()
    {
        var prices = new double[10];
        var output = new double[5]; // Too short

        var ex = Assert.Throws<ArgumentException>(() =>
            Hv.Batch(prices, output, period: 10));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_InvalidPeriod_ThrowsArgumentException()
    {
        var prices = new double[10];
        var output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() =>
            Hv.Batch(prices, output, period: 1));
        Assert.Equal("period", ex.ParamName);
    }

    #endregion

    #region Event Publishing Tests

    [Fact]
    public void Update_PublishesEvent()
    {
        var hv = new Hv(period: 5);
        bool eventFired = false;
        hv.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        var price = new TValue(DateTime.UtcNow, 100.0);
        hv.Update(price);

        Assert.True(eventFired);
    }

    [Fact]
    public void ChainedIndicator_ReceivesValues()
    {
        var source = new Hv(period: 5);
        var downstream = new Sma(source, period: 3);

        var prices = GeneratePriceSeries(15);
        for (int i = 0; i < prices.Count; i++)
        {
            source.Update(prices[i]);
        }

        Assert.True(downstream.Last.Value > 0, "Downstream indicator should receive values");
    }

    #endregion

    #region TBar Update Tests

    [Fact]
    public void Update_TBar_UsesClosePrice()
    {
        var hv1 = new Hv(period: 5);
        var hv2 = new Hv(period: 5);

        // Use TBar for hv1
        var bar = new TBar(DateTime.UtcNow, 100.0, 105.0, 98.0, 102.0, 1000);
        hv1.Update(bar);

        // Use TValue with close price for hv2
        var tvalue = new TValue(bar.Time, bar.Close);
        hv2.Update(tvalue);

        Assert.Equal(hv1.Last.Value, hv2.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_TBarSeries_ReturnsCorrectLength()
    {
        const int dataCount = 50;
        var barSeries = GenerateTestData(dataCount);

        var hv = new Hv(period: 10);
        var result = hv.Update(barSeries);

        Assert.Equal(dataCount, result.Count);
    }

    [Fact]
    public void Hv_IgnoresHighLow_UsesOnlyClose()
    {
        // HV uses close prices only, so changing High-Low shouldn't affect result
        var hv1 = new Hv(period: 5);
        var hv2 = new Hv(period: 5);

        // Bar with same Close but different High-Low
        var bar1 = new TBar(DateTime.UtcNow, 100.0, 105.0, 98.0, 102.0, 1000);
        var bar2 = new TBar(DateTime.UtcNow, 99.0, 200.0, 50.0, 102.0, 1000); // Different H-L, same Close

        var result1 = hv1.Update(bar1).Value;
        var result2 = hv2.Update(bar2).Value;

        // Results should be identical since only Close matters
        Assert.Equal(result1, result2, Tolerance);
    }

    #endregion

    #region Additional Tests

    [Fact]
    public void LargeDataset_Performance()
    {
        var hv = new Hv(period: 20);
        var prices = GeneratePriceSeries(5000);

        for (int i = 0; i < prices.Count; i++)
        {
            var result = hv.Update(prices[i]);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void DifferentParameters_ProduceDistinctValues()
    {
        var prices = GeneratePriceSeries(50);

        var hv1 = new Hv(period: 10);
        var hv2 = new Hv(period: 20);
        var hv3 = new Hv(period: 10, annualize: false);

        for (int i = 0; i < prices.Count; i++)
        {
            hv1.Update(prices[i]);
            hv2.Update(prices[i]);
            hv3.Update(prices[i]);
        }

        Assert.True(double.IsFinite(hv1.Last.Value));
        Assert.True(double.IsFinite(hv2.Last.Value));
        Assert.True(double.IsFinite(hv3.Last.Value));
        // Different parameters should produce different values
        Assert.NotEqual(hv1.Last.Value, hv2.Last.Value);
        Assert.NotEqual(hv1.Last.Value, hv3.Last.Value);
    }

    [Fact]
    public void StaticCalculate_TSeries_Works()
    {
        var prices = GeneratePriceSeries(100);

        var result = Hv.Batch(prices, period: 14);

        Assert.Equal(100, result.Count);
        Assert.True(double.IsFinite(result[result.Count - 1].Value));
    }

    [Fact]
    public void StaticCalculate_TBarSeries_Works()
    {
        var bars = GenerateTestData(100);

        var result = Hv.Batch(bars, period: 14);

        Assert.Equal(100, result.Count);
        Assert.True(double.IsFinite(result[result.Count - 1].Value));
    }

    [Fact]
    public void StaticCalculate_ValidatesInput()
    {
        var prices = GeneratePriceSeries(10);

        Assert.Throws<ArgumentException>(() => Hv.Batch(prices, period: 1));
        Assert.Throws<ArgumentException>(() => Hv.Batch(prices, period: 0));
        Assert.Throws<ArgumentException>(() => Hv.Batch(prices, period: -1));
        Assert.Throws<ArgumentException>(() => Hv.Batch(prices, period: 10, annualize: true, annualPeriods: 0));
    }

    [Fact]
    public void Prime_Works()
    {
        var hv = new Hv(period: 5);
        var values = new double[] { 100.0, 101.0, 99.5, 102.0, 100.5, 103.0, 101.0 };

        hv.Prime(values);

        Assert.True(hv.IsHot);
        Assert.True(double.IsFinite(hv.Last.Value));
    }

    [Fact]
    public void KnownValue_ManualCalculation()
    {
        // Test with known values to verify calculation
        // Prices: 100, 102, 101, 103, 102 (5 prices = 4 returns)
        // Log returns: ln(102/100), ln(101/102), ln(103/101), ln(102/103)
        // = 0.01980263, -0.00985222, 0.01961015, -0.00975899

        var hv = new Hv(period: 4, annualize: false);
        var prices = new double[] { 100.0, 102.0, 101.0, 103.0, 102.0 };

        for (int i = 0; i < prices.Length; i++)
        {
            hv.Update(new TValue(DateTime.UtcNow.AddMinutes(i), prices[i]));
        }

        // Calculate expected population std dev manually
        double[] returns = new double[4];
        for (int i = 1; i < prices.Length; i++)
        {
            returns[i - 1] = Math.Log(prices[i] / prices[i - 1]);
        }

        double sum = 0, sumSq = 0;
        for (int i = 0; i < returns.Length; i++)
        {
            sum += returns[i];
            sumSq += returns[i] * returns[i];
        }
        double mean = sum / returns.Length;
        double variance = (sumSq / returns.Length) - (mean * mean);
        double expected = Math.Sqrt(variance);

        Assert.Equal(expected, hv.Last.Value, 1e-9);
    }

    #endregion
}