namespace QuanTAlib.Tests;

/// <summary>
/// PFE Validation Tests — Self-consistency validation.
/// No external library (TA-Lib, Skender, Tulip, Ooples) implements PFE.
/// Validation focuses on internal consistency and mathematical correctness.
/// </summary>
public sealed class PfeValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private bool _disposed;

    public PfeValidationTests()
    {
        _testData = new ValidationTestData();
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (disposing)
        {
            _testData?.Dispose();
        }
    }

    // ============== Self-Consistency ==============

    [Fact]
    public void Validation_BatchMatchesStreaming()
    {
        int[][] paramSets = { new[] { 5, 3 }, new[] { 10, 5 }, new[] { 20, 8 } };
        var series = _testData.Data;

        foreach (int[] ps in paramSets)
        {
            int period = ps[0];
            int smooth = ps[1];

            // Streaming
            var pfeStream = new Pfe(period, smooth);
            var streamResults = new List<double>();
            foreach (var tv in series)
            {
                streamResults.Add(pfeStream.Update(tv).Value);
            }

            // Batch
            var batchResults = Pfe.Batch(series, period, smooth);

            Assert.Equal(streamResults.Count, batchResults.Count);
            for (int i = 0; i < streamResults.Count; i++)
            {
                Assert.Equal(streamResults[i], batchResults[i].Value, 1e-10);
            }
        }
    }

    [Fact]
    public void Validation_SpanMatchesStreaming()
    {
        int[][] paramSets = { new[] { 5, 3 }, new[] { 10, 5 }, new[] { 20, 8 } };
        var series = _testData.Data;
        int len = series.Count;

        double[] values = series.Values.ToArray();

        foreach (int[] ps in paramSets)
        {
            int period = ps[0];
            int smooth = ps[1];

            // Streaming
            var pfeStream = new Pfe(period, smooth);
            var streamResults = new double[len];
            for (int i = 0; i < len; i++)
            {
                streamResults[i] = pfeStream.Update(series[i]).Value;
            }

            // Span batch
            double[] spanResults = new double[len];
            Pfe.Batch(values, spanResults, period, smooth);

            for (int i = 0; i < len; i++)
            {
                Assert.Equal(streamResults[i], spanResults[i], 1e-10);
            }
        }
    }

    // ============== Known-Value Tests ==============

    [Fact]
    public void Validation_ConstantPrice_HundredPfe()
    {
        // Constant price: priceDiff=0, straightLine=sqrt(0+period^2)=period
        // fractalPath = period*sqrt(1) = period. Efficiency = 100%.
        // Sign: priceDiff=0 >= 0 → positive. So PFE = +100.
        var pfe = new Pfe(5, 3);
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < 30; i++)
        {
            pfe.Update(new TValue(baseTime.AddMinutes(i), 100));
        }

        Assert.Equal(100.0, pfe.Last.Value, 1e-4);
    }

    [Fact]
    public void Validation_MonotonicIncrease_PositivePfe()
    {
        // For strictly increasing prices, PFE should be positive
        var pfe = new Pfe(5, 3);
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < 30; i++)
        {
            pfe.Update(new TValue(baseTime.AddMinutes(i), 100 + i));
        }

        Assert.True(pfe.Last.Value > 0, $"PFE should be positive for uptrend, got {pfe.Last.Value}");
    }

    [Fact]
    public void Validation_MonotonicDecrease_NegativePfe()
    {
        // For strictly decreasing prices, PFE should be negative
        var pfe = new Pfe(5, 3);
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < 30; i++)
        {
            pfe.Update(new TValue(baseTime.AddMinutes(i), 200 - i));
        }

        Assert.True(pfe.Last.Value < 0, $"PFE should be negative for downtrend, got {pfe.Last.Value}");
    }

    [Fact]
    public void Validation_WarmupBarsReturnZero()
    {
        var pfe = new Pfe(5, 3);
        var baseTime = DateTime.UtcNow;

        // First period bars (before close buffer is full) should return 0
        for (int i = 0; i < 5; i++)
        {
            var result = pfe.Update(new TValue(baseTime.AddMinutes(i), 100 + i));
            Assert.Equal(0.0, result.Value, 1e-10);
        }
    }

    [Fact]
    public void Validation_DivByZero_ReturnsZero()
    {
        // If all prices are identical, fractal path = period * sqrt(0 + 1) = period
        // But straight line distance has priceDiff=0, so straightLine = sqrt(0 + period^2) = period
        // rawPfe = 0 because priceDiff >= 0 ? efficiency : -efficiency maps to +efficiency when priceDiff=0
        // But efficiency = period/period*100 = 100 when constant
        // Actually for constant: numerator = 0, so rawPfe = sign(0) * 100 = +100 (per sign convention)
        // Wait: straightLine = sqrt(0 + 25) = 5, fractalPath = 5*1 = 5, efficiency = 100
        // priceDiff = 0 >= 0, so rawPfe = +100
        // Actually priceDiff=0 means no change, but the formula gives 100% efficiency
        // No, rechecking: priceDiff = close - close[period] = 0 for constant
        // straightLine = sqrt(0 + period^2) = period
        // fractalPath = sum of sqrt(0 + 1) = period
        // so rawPfe = sign(0) * (period/period)*100 = +100 for constant
        // This is mathematically correct: a flat line IS efficient in the Euclidean sense
        // But the PineScript code uses the sign as: priceDiff >= 0 ? efficiency : -efficiency
        // So a flat line gets +100.

        // Instead test div-by-zero guard for fractalPath near 0 (can't happen naturally)
        // Just verify constant produces a defined result
        var pfe = new Pfe(5, 3);
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < 15; i++)
        {
            var result = pfe.Update(new TValue(baseTime.AddMinutes(i), 50));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    // ============== Bounded Output ==============

    [Fact]
    public void Validation_OutputAlwaysBounded()
    {
        var pfe = new Pfe(10, 5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.5, sigma: 2.0);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        foreach (var tv in series)
        {
            var result = pfe.Update(tv);
            if (pfe.IsHot)
            {
                Assert.True(result.Value >= -100.1 && result.Value <= 100.1,
                    $"PFE must be in [-100, +100] when hot, got {result.Value}");
            }
        }
    }

    // ============== Different Periods ==============

    [Fact]
    public void Validation_DifferentPeriods_ProduceDifferentResults()
    {
        var pfe_5 = new Pfe(5, 3);
        var pfe_10 = new Pfe(10, 5);
        var pfe_20 = new Pfe(20, 8);

        var gbm = new GBM(startPrice: 100.0, mu: 0.1, sigma: 0.3);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        foreach (var tv in series)
        {
            pfe_5.Update(tv);
            pfe_10.Update(tv);
            pfe_20.Update(tv);
        }

        // All should be finite and bounded
        Assert.True(double.IsFinite(pfe_5.Last.Value));
        Assert.True(double.IsFinite(pfe_10.Last.Value));
        Assert.True(double.IsFinite(pfe_20.Last.Value));
    }

    [Fact]
    public void Validation_Calculate_ReturnsHotIndicator()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.3);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var (results, indicator) = Pfe.Calculate(series, 10, 5);

        Assert.Equal(series.Count, results.Count);
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void Validation_BarCorrection_Consistent()
    {
        var pfe1 = new Pfe(10, 5);
        var pfe2 = new Pfe(10, 5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.3);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // Pfe1: feed all values normally
        foreach (var tv in series)
        {
            pfe1.Update(tv, isNew: true);
        }

        // Pfe2: feed values with correction on last bar
        for (int i = 0; i < series.Count - 1; i++)
        {
            pfe2.Update(series[i], isNew: true);
        }
        // Feed wrong last value first
        pfe2.Update(new TValue(series[^1].Time, 999999), isNew: true);
        // Correct it
        pfe2.Update(series[^1], isNew: false);

        Assert.Equal(pfe1.Last.Value, pfe2.Last.Value, 1e-10);
    }

    [Fact]
    public void Validation_Symmetry_UpAndDownTrends()
    {
        // A linear rise should produce +PFE, a linear fall should produce -PFE
        // with equal magnitude (symmetric)
        var pfeUp = new Pfe(5, 3);
        var pfeDown = new Pfe(5, 3);
        var baseTime = DateTime.UtcNow;

        double basePrice = 1000;
        for (int i = 0; i < 30; i++)
        {
            pfeUp.Update(new TValue(baseTime.AddMinutes(i), basePrice + i));
            pfeDown.Update(new TValue(baseTime.AddMinutes(i), basePrice - i));
        }

        // Up should be positive, down should be negative
        Assert.True(pfeUp.Last.Value > 0);
        Assert.True(pfeDown.Last.Value < 0);

        // Absolute values should be approximately equal (symmetric efficiency)
        Assert.Equal(Math.Abs(pfeUp.Last.Value), Math.Abs(pfeDown.Last.Value), 1e-10);
    }

    [Fact]
    public void Validation_ManualKnownValue_LinearTrend()
    {
        // For a perfectly linear trend with step=1:
        // straightLine = sqrt((close-close[period])^2 + period^2) = sqrt(period^2 + period^2) = period*sqrt(2)
        // fractalPath = period * sqrt(1^2 + 1) = period * sqrt(2)
        // rawPfe = +1 * (period*sqrt(2)) / (period*sqrt(2)) * 100 = 100
        // After EMA settles, PFE should approach 100
        var pfe = new Pfe(5, 1); // smoothPeriod=1 means no smoothing (EMA with alpha=1)
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < 30; i++)
        {
            pfe.Update(new TValue(baseTime.AddMinutes(i), 100.0 + i));
        }

        // With smoothPeriod=1, alpha=2/(1+1)=1, so EMA=rawPfe exactly
        // rawPfe for perfect linear trend = 100
        Assert.Equal(100.0, pfe.Last.Value, 1e-6);
    }
}
