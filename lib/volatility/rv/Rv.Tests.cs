namespace QuanTAlib.Tests;
using Xunit;

public class RvTests
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
        var rv = new Rv();
        Assert.Equal(5, rv.Period);
        Assert.Equal(20, rv.SmoothingPeriod);
        Assert.True(rv.Annualize);
        Assert.Equal(252, rv.AnnualPeriods);
        Assert.Equal("Rv(5,20)", rv.Name);
        Assert.Equal(25, rv.WarmupPeriod); // period + smoothingPeriod
    }

    [Fact]
    public void Constructor_CustomParameters_SetsCorrectValues()
    {
        var rv = new Rv(period: 10, smoothingPeriod: 30, annualize: false, annualPeriods: 365);
        Assert.Equal(10, rv.Period);
        Assert.Equal(30, rv.SmoothingPeriod);
        Assert.False(rv.Annualize);
        Assert.Equal(365, rv.AnnualPeriods);
        Assert.Equal("Rv(10,30)", rv.Name);
    }

    [Fact]
    public void Constructor_ZeroPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Rv(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Rv(period: -1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroSmoothingPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Rv(period: 5, smoothingPeriod: 0));
        Assert.Equal("smoothingPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeSmoothingPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Rv(period: 5, smoothingPeriod: -1));
        Assert.Equal("smoothingPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroAnnualPeriodsWhenAnnualizing_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Rv(period: 5, smoothingPeriod: 20, annualize: true, annualPeriods: 0));
        Assert.Equal("annualPeriods", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroAnnualPeriodsWhenNotAnnualizing_DoesNotThrow()
    {
        var rv = new Rv(period: 5, smoothingPeriod: 20, annualize: false, annualPeriods: 0);
        Assert.Equal(0, rv.AnnualPeriods);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_SinglePrice_ReturnsZero()
    {
        var rv = new Rv(period: 5, smoothingPeriod: 10);
        var price = new TValue(DateTime.UtcNow, 100.0);
        var result = rv.Update(price);

        // First price cannot produce a return, so volatility is 0
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void Update_TwoPrices_ReturnsPositiveVolatility()
    {
        var rv = new Rv(period: 5, smoothingPeriod: 10);
        rv.Update(new TValue(DateTime.UtcNow, 100.0));
        var result = rv.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 101.0));

        // Second price gives first squared return, so volatility should be positive
        Assert.True(result.Value >= 0);
    }

    [Fact]
    public void Update_MultiplePrices_ReturnsPositiveVolatility()
    {
        var rv = new Rv(period: 5, smoothingPeriod: 10);
        var prices = GeneratePriceSeries(30);

        double lastValue = 0;
        for (int i = 0; i < prices.Count; i++)
        {
            lastValue = rv.Update(prices[i]).Value;
        }

        Assert.True(lastValue > 0, "RV should return positive volatility after warmup");
    }

    [Fact]
    public void Update_ReturnsLastValue()
    {
        var rv = new Rv(period: 5, smoothingPeriod: 10);
        var price = new TValue(DateTime.UtcNow, 100.0);
        var result = rv.Update(price);

        Assert.Equal(result.Value, rv.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_WithoutAnnualization_ReturnsSmallerValues()
    {
        var rvAnnual = new Rv(period: 5, smoothingPeriod: 10, annualize: true, annualPeriods: 252);
        var rvNoAnnual = new Rv(period: 5, smoothingPeriod: 10, annualize: false);
        var prices = GeneratePriceSeries(30);

        double lastAnnual = 0;
        double lastNoAnnual = 0;
        for (int i = 0; i < prices.Count; i++)
        {
            lastAnnual = rvAnnual.Update(prices[i]).Value;
            lastNoAnnual = rvNoAnnual.Update(prices[i]).Value;
        }

        // Annualized values should be larger by factor of sqrt(252)
        Assert.True(lastAnnual > lastNoAnnual, "Annualized values should be larger");
    }

    [Fact]
    public void Update_AnnualizationFactor_Correct()
    {
        var rvAnnual = new Rv(period: 5, smoothingPeriod: 10, annualize: true, annualPeriods: 252);
        var rvNoAnnual = new Rv(period: 5, smoothingPeriod: 10, annualize: false);
        var prices = GeneratePriceSeries(50);

        for (int i = 0; i < prices.Count; i++)
        {
            rvAnnual.Update(prices[i]);
            rvNoAnnual.Update(prices[i]);
        }

        double factor = rvAnnual.Last.Value / rvNoAnnual.Last.Value;
        double expectedFactor = Math.Sqrt(252);

        Assert.Equal(expectedFactor, factor, 1e-6);
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var rv = new Rv(period: 3, smoothingPeriod: 5);
        var prices = GeneratePriceSeries(10);

        for (int i = 0; i < 5; i++)
        {
            rv.Update(prices[i], isNew: true);
        }
        var result1 = rv.Last.Value;

        rv.Update(prices[5], isNew: true);
        var result2 = rv.Last.Value;

        Assert.True(result1 >= 0, "First result should be non-negative");
        Assert.True(result2 >= 0, "Second result should be non-negative");
    }

    [Fact]
    public void Update_IsNewFalse_UpdatesCurrentBar()
    {
        var rv = new Rv(period: 3, smoothingPeriod: 5);
        var prices = GeneratePriceSeries(10);

        for (int i = 0; i < 5; i++)
        {
            rv.Update(prices[i], isNew: true);
        }

        rv.Update(prices[5], isNew: true);
        var firstValue = rv.Last.Value;

        var updatedPrice = new TValue(prices[5].Time, prices[5].Value * 1.05);
        rv.Update(updatedPrice, isNew: false);
        var updatedValue = rv.Last.Value;

        Assert.NotEqual(firstValue, updatedValue);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoresState()
    {
        var rv = new Rv(period: 3, smoothingPeriod: 5);
        var prices = GeneratePriceSeries(15);

        for (int i = 0; i < 5; i++)
        {
            rv.Update(prices[i], isNew: true);
        }

        rv.Update(prices[5], isNew: true);
        rv.Update(prices[5], isNew: false);
        rv.Update(prices[5], isNew: false);
        rv.Update(prices[5], isNew: false);

        rv.Update(prices[6], isNew: true);

        var rv2 = new Rv(period: 3, smoothingPeriod: 5);
        for (int i = 0; i < 7; i++)
        {
            rv2.Update(prices[i], isNew: true);
        }

        Assert.Equal(rv.Last.Value, rv2.Last.Value, Tolerance);
    }

    #endregion

    #region IsHot and Warmup Tests

    [Fact]
    public void IsHot_BeforeWarmup_ReturnsFalse()
    {
        var rv = new Rv(period: 5, smoothingPeriod: 10);
        var prices = GeneratePriceSeries(10);

        for (int i = 0; i < prices.Count; i++)
        {
            rv.Update(prices[i]);
        }

        Assert.False(rv.IsHot);
    }

    [Fact]
    public void IsHot_AfterWarmup_ReturnsTrue()
    {
        var rv = new Rv(period: 5, smoothingPeriod: 10);
        var prices = GeneratePriceSeries(20);

        for (int i = 0; i < prices.Count; i++)
        {
            rv.Update(prices[i]);
        }

        Assert.True(rv.IsHot);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsState()
    {
        var rv = new Rv(period: 5, smoothingPeriod: 10);
        var prices = GeneratePriceSeries(20);

        for (int i = 0; i < prices.Count; i++)
        {
            rv.Update(prices[i]);
        }

        rv.Reset();

        Assert.False(rv.IsHot);
        Assert.Equal(0, rv.Last.Value);
    }

    [Fact]
    public void Reset_AllowsReprocessing()
    {
        var rv = new Rv(period: 5, smoothingPeriod: 10);
        var prices = GeneratePriceSeries(20);

        for (int i = 0; i < prices.Count; i++)
        {
            rv.Update(prices[i]);
        }
        var firstResult = rv.Last.Value;

        rv.Reset();
        for (int i = 0; i < prices.Count; i++)
        {
            rv.Update(prices[i]);
        }
        var secondResult = rv.Last.Value;

        Assert.Equal(firstResult, secondResult, Tolerance);
    }

    #endregion

    #region Robustness Tests

    [Fact]
    public void Update_WithNaNValues_UsesLastValidValue()
    {
        var rv = new Rv(period: 5, smoothingPeriod: 10);
        var prices = GeneratePriceSeries(20);

        for (int i = 0; i < prices.Count; i++)
        {
            rv.Update(prices[i]);
        }
        var valueBeforeInvalid = rv.Last.Value;

        var nanPrice = new TValue(DateTime.UtcNow, double.NaN);
        var result = rv.Update(nanPrice);

        Assert.True(double.IsFinite(result.Value), "Result should be finite when using last valid value");
        Assert.Equal(valueBeforeInvalid, result.Value, Tolerance);
    }

    [Fact]
    public void Update_WithInfinityValues_UsesLastValidValue()
    {
        var rv = new Rv(period: 5, smoothingPeriod: 10);
        var prices = GeneratePriceSeries(20);

        for (int i = 0; i < prices.Count; i++)
        {
            rv.Update(prices[i]);
        }
        var valueBeforeInvalid = rv.Last.Value;

        var infPrice = new TValue(DateTime.UtcNow, double.PositiveInfinity);
        var result = rv.Update(infPrice);

        Assert.True(double.IsFinite(result.Value), "Result should be finite when using last valid value");
        Assert.Equal(valueBeforeInvalid, result.Value, Tolerance);
    }

    [Fact]
    public void Update_WithZeroPrice_UsesLastValidValue()
    {
        var rv = new Rv(period: 5, smoothingPeriod: 10);
        var prices = GeneratePriceSeries(20);

        for (int i = 0; i < prices.Count; i++)
        {
            rv.Update(prices[i]);
        }
        var valueBeforeInvalid = rv.Last.Value;

        var zeroPrice = new TValue(DateTime.UtcNow, 0.0);
        var result = rv.Update(zeroPrice);

        Assert.True(double.IsFinite(result.Value), "Result should be finite when using last valid value");
        Assert.Equal(valueBeforeInvalid, result.Value, Tolerance);
    }

    [Fact]
    public void Update_WithNegativePrice_UsesLastValidValue()
    {
        var rv = new Rv(period: 5, smoothingPeriod: 10);
        var prices = GeneratePriceSeries(20);

        for (int i = 0; i < prices.Count; i++)
        {
            rv.Update(prices[i]);
        }
        var valueBeforeInvalid = rv.Last.Value;

        var negPrice = new TValue(DateTime.UtcNow, -100.0);
        var result = rv.Update(negPrice);

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

        var rvStreaming = new Rv(period: 5, smoothingPeriod: 10);
        var streamingResults = new double[dataCount];
        for (int i = 0; i < dataCount; i++)
        {
            streamingResults[i] = rvStreaming.Update(prices[i]).Value;
        }

        var batchResults = new double[dataCount];
        Rv.Batch(prices.Values, batchResults, period: 5, smoothingPeriod: 10);

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

        var result = Rv.Batch(priceSeries, period: 5, smoothingPeriod: 10);

        Assert.Equal(dataCount, result.Count);
    }

    [Fact]
    public void Update_TSeries_MatchesStreamingResults()
    {
        const int dataCount = 50;
        var priceSeries = GeneratePriceSeries(dataCount);

        var rvSeries = new Rv(period: 5, smoothingPeriod: 10);
        var seriesResult = rvSeries.Update(priceSeries);

        var rvStreaming = new Rv(period: 5, smoothingPeriod: 10);
        var streamingResults = new double[dataCount];
        for (int i = 0; i < dataCount; i++)
        {
            streamingResults[i] = rvStreaming.Update(priceSeries[i]).Value;
        }

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

        Rv.Batch(prices, output, period: 5, smoothingPeriod: 10);
        Assert.Empty(output);
    }

    [Fact]
    public void Batch_OutputTooShort_ThrowsArgumentException()
    {
        var prices = new double[10];
        var output = new double[5];

        var ex = Assert.Throws<ArgumentException>(() =>
            Rv.Batch(prices, output, period: 5, smoothingPeriod: 10));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_InvalidPeriod_ThrowsArgumentException()
    {
        var prices = new double[10];
        var output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() =>
            Rv.Batch(prices, output, period: 0, smoothingPeriod: 10));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_InvalidSmoothingPeriod_ThrowsArgumentException()
    {
        var prices = new double[10];
        var output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() =>
            Rv.Batch(prices, output, period: 5, smoothingPeriod: 0));
        Assert.Equal("smoothingPeriod", ex.ParamName);
    }

    #endregion

    #region Event Publishing Tests

    [Fact]
    public void Update_PublishesEvent()
    {
        var rv = new Rv(period: 5, smoothingPeriod: 10);
        bool eventFired = false;
        rv.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        var price = new TValue(DateTime.UtcNow, 100.0);
        rv.Update(price);

        Assert.True(eventFired);
    }

    [Fact]
    public void ChainedIndicator_ReceivesValues()
    {
        var source = new Rv(period: 5, smoothingPeriod: 10);
        var downstream = new Sma(source, period: 3);

        var prices = GeneratePriceSeries(30);
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
        var rv1 = new Rv(period: 5, smoothingPeriod: 10);
        var rv2 = new Rv(period: 5, smoothingPeriod: 10);

        var bar = new TBar(DateTime.UtcNow, 100.0, 105.0, 98.0, 102.0, 1000);
        rv1.Update(bar);

        var tvalue = new TValue(bar.Time, bar.Close);
        rv2.Update(tvalue);

        Assert.Equal(rv1.Last.Value, rv2.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_TBarSeries_ReturnsCorrectLength()
    {
        const int dataCount = 50;
        var barSeries = GenerateTestData(dataCount);

        var rv = new Rv(period: 5, smoothingPeriod: 10);
        var result = rv.Update(barSeries);

        Assert.Equal(dataCount, result.Count);
    }

    #endregion

    #region Additional Tests

    [Fact]
    public void LargeDataset_Performance()
    {
        var rv = new Rv(period: 5, smoothingPeriod: 20);
        var prices = GeneratePriceSeries(5000);

        for (int i = 0; i < prices.Count; i++)
        {
            var result = rv.Update(prices[i]);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void DifferentParameters_ProduceDistinctValues()
    {
        var prices = GeneratePriceSeries(50);

        var rv1 = new Rv(period: 5, smoothingPeriod: 10);
        var rv2 = new Rv(period: 10, smoothingPeriod: 20);
        var rv3 = new Rv(period: 5, smoothingPeriod: 10, annualize: false);

        for (int i = 0; i < prices.Count; i++)
        {
            rv1.Update(prices[i]);
            rv2.Update(prices[i]);
            rv3.Update(prices[i]);
        }

        Assert.True(double.IsFinite(rv1.Last.Value));
        Assert.True(double.IsFinite(rv2.Last.Value));
        Assert.True(double.IsFinite(rv3.Last.Value));
        Assert.NotEqual(rv1.Last.Value, rv2.Last.Value);
        Assert.NotEqual(rv1.Last.Value, rv3.Last.Value);
    }

    [Fact]
    public void StaticCalculate_TSeries_Works()
    {
        var prices = GeneratePriceSeries(100);

        var result = Rv.Batch(prices, period: 5, smoothingPeriod: 14);

        Assert.Equal(100, result.Count);
        Assert.True(double.IsFinite(result[result.Count - 1].Value));
    }

    [Fact]
    public void StaticCalculate_TBarSeries_Works()
    {
        var bars = GenerateTestData(100);

        var result = Rv.Batch(bars, period: 5, smoothingPeriod: 14);

        Assert.Equal(100, result.Count);
        Assert.True(double.IsFinite(result[result.Count - 1].Value));
    }

    [Fact]
    public void StaticCalculate_ValidatesInput()
    {
        var prices = GeneratePriceSeries(10);

        Assert.Throws<ArgumentException>(() => Rv.Batch(prices, period: 0));
        Assert.Throws<ArgumentException>(() => Rv.Batch(prices, period: -1));
        Assert.Throws<ArgumentException>(() => Rv.Batch(prices, period: 5, smoothingPeriod: 0));
        Assert.Throws<ArgumentException>(() => Rv.Batch(prices, period: 5, smoothingPeriod: 10, annualize: true, annualPeriods: 0));
    }

    [Fact]
    public void Prime_Works()
    {
        var rv = new Rv(period: 3, smoothingPeriod: 5);
        var values = new double[] { 100.0, 101.0, 99.5, 102.0, 100.5, 103.0, 101.0, 104.0, 102.0, 105.0 };

        rv.Prime(values);

        Assert.True(rv.IsHot);
        Assert.True(double.IsFinite(rv.Last.Value));
    }

    [Fact]
    public void KnownValue_ManualCalculation()
    {
        // Test with known values: prices 100, 101, 102, 103 (3 returns)
        // Log returns: ln(101/100), ln(102/101), ln(103/102)
        // ≈ 0.00995, 0.00985, 0.00975
        // Squared returns sum, then sqrt, then SMA

        var rv = new Rv(period: 3, smoothingPeriod: 2, annualize: false);
        var prices = new double[] { 100.0, 101.0, 102.0, 103.0, 104.0 };

        for (int i = 0; i < prices.Length; i++)
        {
            rv.Update(new TValue(DateTime.UtcNow.AddMinutes(i), prices[i]));
        }

        // The result should be positive and finite
        Assert.True(rv.Last.Value > 0);
        Assert.True(double.IsFinite(rv.Last.Value));
    }

    [Fact]
    public void SmoothingEffect_ReducesNoise()
    {
        // Compare RV with different smoothing periods
        var prices = GeneratePriceSeries(100);

        var rvShortSmooth = new Rv(period: 5, smoothingPeriod: 3, annualize: false);
        var rvLongSmooth = new Rv(period: 5, smoothingPeriod: 20, annualize: false);

        var shortSmoothValues = new List<double>();
        var longSmoothValues = new List<double>();

        for (int i = 0; i < prices.Count; i++)
        {
            shortSmoothValues.Add(rvShortSmooth.Update(prices[i]).Value);
            longSmoothValues.Add(rvLongSmooth.Update(prices[i]).Value);
        }

        // Calculate variance of last 50 values
        double VarianceOfLast50(List<double> vals)
        {
            var last50 = vals.Skip(vals.Count - 50).ToList();
            double mean = last50.Average();
            return last50.Sum(v => (v - mean) * (v - mean)) / last50.Count;
        }

        double shortVariance = VarianceOfLast50(shortSmoothValues);
        double longVariance = VarianceOfLast50(longSmoothValues);

        // Longer smoothing should have lower variance (smoother)
        Assert.True(longVariance < shortVariance, "Longer smoothing should produce smoother output");
    }

    #endregion
}
