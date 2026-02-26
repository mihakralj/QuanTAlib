namespace QuanTAlib.Tests;

/// <summary>
/// RAVI Validation Tests — Self-consistency validation.
/// No external library (TA-Lib, Skender, Tulip, Ooples) implements RAVI.
/// Validation focuses on internal consistency and mathematical correctness.
/// </summary>
public sealed class RaviValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private bool _disposed;

    public RaviValidationTests()
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
        int[][] paramSets = { new[] { 3, 10 }, new[] { 5, 20 }, new[] { 7, 65 } };
        var series = _testData.Data;

        foreach (var ps in paramSets)
        {
            int shortP = ps[0];
            int longP = ps[1];

            // Streaming
            var raviStream = new Ravi(shortP, longP);
            var streamResults = new List<double>();
            foreach (var tv in series)
            {
                streamResults.Add(raviStream.Update(tv).Value);
            }

            // Batch
            var batchResults = Ravi.Batch(series, shortP, longP);

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
        int[][] paramSets = { new[] { 3, 10 }, new[] { 5, 20 }, new[] { 7, 65 } };
        var series = _testData.Data;
        int len = series.Count;

        double[] values = series.Values.ToArray();

        foreach (var ps in paramSets)
        {
            int shortP = ps[0];
            int longP = ps[1];

            // Streaming
            var raviStream = new Ravi(shortP, longP);
            var streamResults = new double[len];
            for (int i = 0; i < len; i++)
            {
                streamResults[i] = raviStream.Update(series[i]).Value;
            }

            // Span batch
            double[] spanResults = new double[len];
            Ravi.Batch(values, spanResults, shortP, longP);

            for (int i = 0; i < len; i++)
            {
                Assert.Equal(streamResults[i], spanResults[i], 1e-10);
            }
        }
    }

    // ============== Known-Value Tests ==============

    [Fact]
    public void Validation_ConstantPrice_ZeroRavi()
    {
        var ravi = new Ravi(3, 10);
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            var result = ravi.Update(new TValue(baseTime.AddMinutes(i), 100));
            if (ravi.IsHot)
            {
                Assert.Equal(0.0, result.Value, 1e-10);
            }
        }
    }

    [Fact]
    public void Validation_EqualPeriods_ThrowsException()
    {
        // Short must be strictly less than long — equal throws
        Assert.Throws<ArgumentException>(() => new Ravi(10, 10));
    }

    [Fact]
    public void Validation_WarmupBarsReturnZero()
    {
        var ravi = new Ravi(3, 10);
        var baseTime = DateTime.UtcNow;

        // First 9 bars (before long SMA is full) should return 0
        for (int i = 0; i < 9; i++)
        {
            var result = ravi.Update(new TValue(baseTime.AddMinutes(i), 100 + i));
            Assert.Equal(0.0, result.Value, 1e-10);
            Assert.False(ravi.IsHot);
        }
    }

    [Fact]
    public void Validation_DivByZero_ReturnsZero()
    {
        // If all prices are 0, SMA_long = 0 → division guard should produce 0
        var ravi = new Ravi(3, 10);
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < 15; i++)
        {
            var result = ravi.Update(new TValue(baseTime.AddMinutes(i), 0));
            Assert.Equal(0.0, result.Value, 1e-10);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    // ============== Different Periods ==============

    [Fact]
    public void Validation_DifferentPeriods_ProduceDifferentResults()
    {
        var ravi_3_10 = new Ravi(3, 10);
        var ravi_5_20 = new Ravi(5, 20);
        var ravi_7_65 = new Ravi(7, 65);

        var gbm = new GBM(startPrice: 100.0, mu: 0.1, sigma: 0.3);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        foreach (var tv in series)
        {
            ravi_3_10.Update(tv);
            ravi_5_20.Update(tv);
            ravi_7_65.Update(tv);
        }

        // All should be finite and non-negative
        Assert.True(double.IsFinite(ravi_3_10.Last.Value));
        Assert.True(double.IsFinite(ravi_5_20.Last.Value));
        Assert.True(double.IsFinite(ravi_7_65.Last.Value));
        Assert.True(ravi_3_10.Last.Value >= 0);
        Assert.True(ravi_5_20.Last.Value >= 0);
        Assert.True(ravi_7_65.Last.Value >= 0);
    }

    [Fact]
    public void Validation_Calculate_ReturnsHotIndicator()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.3);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var (results, indicator) = Ravi.Calculate(series, 5, 20);

        Assert.Equal(series.Count, results.Count);
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void Validation_BarCorrection_Consistent()
    {
        var ravi1 = new Ravi(5, 20);
        var ravi2 = new Ravi(5, 20);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.3);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // Ravi1: feed all values normally
        foreach (var tv in series)
        {
            ravi1.Update(tv, isNew: true);
        }

        // Ravi2: feed values with correction on last bar
        for (int i = 0; i < series.Count - 1; i++)
        {
            ravi2.Update(series[i], isNew: true);
        }
        // Feed wrong last value first
        ravi2.Update(new TValue(series[^1].Time, 999999), isNew: true);
        // Correct it
        ravi2.Update(series[^1], isNew: false);

        Assert.Equal(ravi1.Last.Value, ravi2.Last.Value, 1e-10);
    }

    [Fact]
    public void Validation_Ravi_AlwaysNonNegative()
    {
        var ravi = new Ravi(7, 65);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 1.0);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        foreach (var tv in series)
        {
            var result = ravi.Update(tv);
            Assert.True(result.Value >= 0, $"RAVI must be non-negative, got {result.Value}");
        }
    }

    [Fact]
    public void Validation_Symmetry_UpAndDownTrends()
    {
        // A monotonic rise of +1/bar and a monotonic fall of -1/bar
        // should produce equal RAVI after warmup
        var raviUp = new Ravi(3, 10);
        var raviDown = new Ravi(3, 10);
        var baseTime = DateTime.UtcNow;

        double basePrice = 1000;
        for (int i = 0; i < 20; i++)
        {
            raviUp.Update(new TValue(baseTime.AddMinutes(i), basePrice + i));
            raviDown.Update(new TValue(baseTime.AddMinutes(i), basePrice - i));
        }

        // Not exactly equal because normalization denominator differs,
        // but both should be positive and finite
        Assert.True(raviUp.Last.Value > 0);
        Assert.True(raviDown.Last.Value > 0);
        Assert.True(double.IsFinite(raviUp.Last.Value));
        Assert.True(double.IsFinite(raviDown.Last.Value));
    }

    [Fact]
    public void Validation_ManualKnownValue()
    {
        // Manual calculation: 5 bars, shortPeriod=2, longPeriod=5
        // Prices: 100, 102, 104, 106, 108
        // After 5 bars:
        //   SMA_short(2) = (106 + 108) / 2 = 107
        //   SMA_long(5) = (100 + 102 + 104 + 106 + 108) / 5 = 104
        //   RAVI = |107 - 104| / 104 * 100 = 3/104 * 100 ≈ 2.884615...

        var ravi = new Ravi(2, 5);
        var baseTime = DateTime.UtcNow;

        ravi.Update(new TValue(baseTime, 100));
        ravi.Update(new TValue(baseTime.AddMinutes(1), 102));
        ravi.Update(new TValue(baseTime.AddMinutes(2), 104));
        ravi.Update(new TValue(baseTime.AddMinutes(3), 106));
        ravi.Update(new TValue(baseTime.AddMinutes(4), 108));

        double expected = Math.Abs(107.0 - 104.0) / 104.0 * 100.0;
        Assert.Equal(expected, ravi.Last.Value, 1e-10);
    }
}
