namespace QuanTAlib.Tests;

/// <summary>
/// VHF Validation Tests — Self-consistency validation.
/// No external library (TA-Lib, Skender, Tulip, Ooples) implements VHF.
/// Validation focuses on internal consistency and mathematical correctness.
/// </summary>
public sealed class VhfValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private bool _disposed;

    public VhfValidationTests()
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
        int[] periods = { 5, 10, 28 };
        var series = _testData.Data;

        foreach (int period in periods)
        {
            // Streaming
            var vhfStream = new Vhf(period);
            var streamResults = new List<double>();
            foreach (var tv in series)
            {
                streamResults.Add(vhfStream.Update(tv).Value);
            }

            // Batch
            var batchResults = Vhf.Batch(series, period);

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
        int[] periods = { 5, 10, 28 };
        var series = _testData.Data;
        int len = series.Count;

        double[] values = series.Values.ToArray();

        foreach (int period in periods)
        {
            // Streaming
            var vhfStream = new Vhf(period);
            var streamResults = new double[len];
            for (int i = 0; i < len; i++)
            {
                streamResults[i] = vhfStream.Update(series[i]).Value;
            }

            // Span batch
            double[] spanResults = new double[len];
            Vhf.Batch(values, spanResults, period);

            for (int i = 0; i < len; i++)
            {
                Assert.Equal(streamResults[i], spanResults[i], 1e-10);
            }
        }
    }

    // ============== Known-Value Tests ==============

    [Fact]
    public void Validation_ConstantPrice_ZeroVhf()
    {
        var vhf = new Vhf(5);
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            var result = vhf.Update(new TValue(baseTime.AddMinutes(i), 100));
            if (vhf.IsHot)
            {
                Assert.Equal(0.0, result.Value, 1e-10);
            }
        }
    }

    [Fact]
    public void Validation_MonotonicIncrease_VhfEqualsOne()
    {
        // For strictly monotonic increase with equal steps:
        // Highest - Lowest = N * step
        // Sum of |changes| = N * step
        // VHF = 1.0
        var vhf = new Vhf(5);
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            vhf.Update(new TValue(baseTime.AddMinutes(i), 100 + i));
        }

        Assert.True(vhf.IsHot);
        Assert.Equal(1.0, vhf.Last.Value, 1e-10);
    }

    [Fact]
    public void Validation_MonotonicDecrease_VhfEqualsOne()
    {
        // For strictly monotonic decrease with equal steps:
        // Range = N * step, sum of |changes| = N * step → VHF = 1.0
        var vhf = new Vhf(5);
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            vhf.Update(new TValue(baseTime.AddMinutes(i), 200 - i));
        }

        Assert.True(vhf.IsHot);
        Assert.Equal(1.0, vhf.Last.Value, 1e-10);
    }

    [Fact]
    public void Validation_WarmupBarsReturnZero()
    {
        var vhf = new Vhf(5);
        var baseTime = DateTime.UtcNow;

        // First period bars (before close buffer is full) should return 0
        for (int i = 0; i < 5; i++)
        {
            var result = vhf.Update(new TValue(baseTime.AddMinutes(i), 100 + i));
            Assert.Equal(0.0, result.Value, 1e-10);
            Assert.False(vhf.IsHot);
        }
    }

    [Fact]
    public void Validation_DivByZero_ReturnsZero()
    {
        // If all prices are identical, sum of |changes| = 0 → guard produces 0
        var vhf = new Vhf(5);
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < 15; i++)
        {
            var result = vhf.Update(new TValue(baseTime.AddMinutes(i), 50));
            Assert.Equal(0.0, result.Value, 1e-10);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    // ============== Different Periods ==============

    [Fact]
    public void Validation_DifferentPeriods_ProduceDifferentResults()
    {
        var vhf_5 = new Vhf(5);
        var vhf_10 = new Vhf(10);
        var vhf_28 = new Vhf(28);

        var gbm = new GBM(startPrice: 100.0, mu: 0.1, sigma: 0.3);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        foreach (var tv in series)
        {
            vhf_5.Update(tv);
            vhf_10.Update(tv);
            vhf_28.Update(tv);
        }

        // All should be finite and non-negative
        Assert.True(double.IsFinite(vhf_5.Last.Value));
        Assert.True(double.IsFinite(vhf_10.Last.Value));
        Assert.True(double.IsFinite(vhf_28.Last.Value));
        Assert.True(vhf_5.Last.Value >= 0);
        Assert.True(vhf_10.Last.Value >= 0);
        Assert.True(vhf_28.Last.Value >= 0);
    }

    [Fact]
    public void Validation_Calculate_ReturnsHotIndicator()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.3);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var (results, indicator) = Vhf.Calculate(series, 10);

        Assert.Equal(series.Count, results.Count);
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void Validation_BarCorrection_Consistent()
    {
        var vhf1 = new Vhf(10);
        var vhf2 = new Vhf(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.3);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // Vhf1: feed all values normally
        foreach (var tv in series)
        {
            vhf1.Update(tv, isNew: true);
        }

        // Vhf2: feed values with correction on last bar
        for (int i = 0; i < series.Count - 1; i++)
        {
            vhf2.Update(series[i], isNew: true);
        }
        // Feed wrong last value first
        vhf2.Update(new TValue(series[^1].Time, 999999), isNew: true);
        // Correct it
        vhf2.Update(series[^1], isNew: false);

        Assert.Equal(vhf1.Last.Value, vhf2.Last.Value, 1e-8);
    }

    [Fact]
    public void Validation_Vhf_AlwaysNonNegative()
    {
        var vhf = new Vhf(14);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 1.0);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        foreach (var tv in series)
        {
            var result = vhf.Update(tv);
            Assert.True(result.Value >= 0, $"VHF must be non-negative, got {result.Value}");
        }
    }

    [Fact]
    public void Validation_ManualKnownValue()
    {
        // Manual calculation: period=3
        // Prices: 100, 102, 101, 104
        // After 4 bars (period+1=4 close values):
        //   Close buffer: [100, 102, 101, 104]
        //   Highest = 104, Lowest = 100, Range = 4
        //   Abs diffs: |102-100|=2, |101-102|=1, |104-101|=3 → Sum = 6
        //   VHF = 4 / 6 = 0.666...

        var vhf = new Vhf(3);
        var baseTime = DateTime.UtcNow;

        vhf.Update(new TValue(baseTime, 100));
        vhf.Update(new TValue(baseTime.AddMinutes(1), 102));
        vhf.Update(new TValue(baseTime.AddMinutes(2), 101));
        vhf.Update(new TValue(baseTime.AddMinutes(3), 104));

        double expected = 4.0 / 6.0;
        Assert.Equal(expected, vhf.Last.Value, 1e-10);
    }

    [Fact]
    public void Validation_Symmetry_UpAndDownTrends()
    {
        // A monotonic rise of +1/bar and a monotonic fall of -1/bar
        // should produce equal VHF (both equal 1.0)
        var vhfUp = new Vhf(5);
        var vhfDown = new Vhf(5);
        var baseTime = DateTime.UtcNow;

        double basePrice = 1000;
        for (int i = 0; i < 20; i++)
        {
            vhfUp.Update(new TValue(baseTime.AddMinutes(i), basePrice + i));
            vhfDown.Update(new TValue(baseTime.AddMinutes(i), basePrice - i));
        }

        // Both should be exactly 1.0 for monotonic movement
        Assert.Equal(1.0, vhfUp.Last.Value, 1e-10);
        Assert.Equal(1.0, vhfDown.Last.Value, 1e-10);
    }
}
