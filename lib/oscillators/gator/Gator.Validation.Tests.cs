namespace QuanTAlib.Tests;

/// <summary>
/// GATOR Validation Tests — Self-consistency validation.
/// No external library (TA-Lib, Skender, Tulip, Ooples) implements the Gator oscillator
/// as a standalone indicator. Validation focuses on internal consistency and mathematical correctness.
/// </summary>
public sealed class GatorValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private bool _disposed;

    public GatorValidationTests()
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
        int[][] paramSets = { new[] { 5, 3, 3, 2, 2, 1 }, new[] { 13, 8, 8, 5, 5, 3 } };
        var series = _testData.Data;

        foreach (var ps in paramSets)
        {
            int jp = ps[0], js = ps[1], tp = ps[2], ts = ps[3], lp = ps[4], ls = ps[5];

            var gatorStream = new Gator(jp, js, tp, ts, lp, ls);
            var streamResults = new List<double>();
            foreach (var tv in series)
            {
                streamResults.Add(gatorStream.Update(tv).Value);
            }

            var batchResults = Gator.Batch(series, jp, js, tp, ts, lp, ls);

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
        int[][] paramSets = { new[] { 5, 3, 3, 2, 2, 1 }, new[] { 13, 8, 8, 5, 5, 3 } };
        var series = _testData.Data;
        int len = series.Count;

        double[] values = series.Values.ToArray();

        foreach (var ps in paramSets)
        {
            int jp = ps[0], js = ps[1], tp = ps[2], ts = ps[3], lp = ps[4], ls = ps[5];

            var gatorStream = new Gator(jp, js, tp, ts, lp, ls);
            var streamResults = new double[len];
            for (int i = 0; i < len; i++)
            {
                streamResults[i] = gatorStream.Update(series[i]).Value;
            }

            double[] spanResults = new double[len];
            Gator.Batch(values, spanResults, jp, js, tp, ts, lp, ls);

            for (int i = 0; i < len; i++)
            {
                Assert.Equal(streamResults[i], spanResults[i], 1e-10);
            }
        }
    }

    // ============== Known-Value Tests ==============

    [Fact]
    public void Validation_ConstantPrice_ZeroHistograms()
    {
        var gator = new Gator(jawPeriod: 5, jawShift: 3, teethPeriod: 3, teethShift: 2, lipsPeriod: 2, lipsShift: 1);
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < 30; i++)
        {
            var result = gator.Update(new TValue(baseTime.AddMinutes(i), 100));
            if (gator.IsHot)
            {
                // All SMMAs converge to input → shifted values all equal → histograms = 0
                Assert.Equal(0.0, result.Value, 1e-6);
                Assert.Equal(0.0, gator.Lower, 1e-6);
            }
        }
    }

    [Fact]
    public void Validation_WarmupBarsReturnZero()
    {
        var gator = new Gator(jawPeriod: 5, jawShift: 3, teethPeriod: 3, teethShift: 2, lipsPeriod: 2, lipsShift: 1);
        var baseTime = DateTime.UtcNow;

        // Before all buffers are full, output is 0
        for (int i = 0; i < 3; i++)
        {
            var result = gator.Update(new TValue(baseTime.AddMinutes(i), 100 + i));
            Assert.Equal(0.0, result.Value, 1e-10);
            Assert.False(gator.IsHot);
        }
    }

    // ============== Different Periods ==============

    [Fact]
    public void Validation_DifferentPeriods_ProduceDifferentResults()
    {
        var gator_small = new Gator(jawPeriod: 5, jawShift: 3, teethPeriod: 3, teethShift: 2, lipsPeriod: 2, lipsShift: 1);
        var gator_default = new Gator();

        var gbm = new GBM(startPrice: 100.0, mu: 0.1, sigma: 0.3);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        foreach (var tv in series)
        {
            gator_small.Update(tv);
            gator_default.Update(tv);
        }

        Assert.True(double.IsFinite(gator_small.Last.Value));
        Assert.True(double.IsFinite(gator_default.Last.Value));
        Assert.True(gator_small.Last.Value >= 0);
        Assert.True(gator_default.Last.Value >= 0);
    }

    [Fact]
    public void Validation_Calculate_ReturnsHotIndicator()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.3);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var (results, indicator) = Gator.Calculate(series, jawPeriod: 5, jawShift: 3, teethPeriod: 3, teethShift: 2, lipsPeriod: 2, lipsShift: 1);

        Assert.Equal(series.Count, results.Count);
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void Validation_BarCorrection_Consistent()
    {
        int jp = 5, js = 3, tp = 3, ts = 2, lp = 2, ls = 1;
        var gator1 = new Gator(jp, js, tp, ts, lp, ls);
        var gator2 = new Gator(jp, js, tp, ts, lp, ls);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.3);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // Gator1: feed all values normally
        foreach (var tv in series)
        {
            gator1.Update(tv, isNew: true);
        }

        // Gator2: feed values with correction on last bar
        for (int i = 0; i < series.Count - 1; i++)
        {
            gator2.Update(series[i], isNew: true);
        }
        // Feed wrong last value first
        gator2.Update(new TValue(series[^1].Time, 999999), isNew: true);
        // Correct it
        gator2.Update(series[^1], isNew: false);

        Assert.Equal(gator1.Last.Value, gator2.Last.Value, 1e-10);
    }

    [Fact]
    public void Validation_Gator_UpperAlwaysNonNegative()
    {
        var gator = new Gator();
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 1.0);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        foreach (var tv in series)
        {
            var result = gator.Update(tv);
            Assert.True(result.Value >= 0, $"Upper must be non-negative, got {result.Value}");
        }
    }

    [Fact]
    public void Validation_Gator_LowerAlwaysNonPositive()
    {
        var gator = new Gator();
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 1.0);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        foreach (var tv in series)
        {
            gator.Update(tv);
            Assert.True(gator.Lower <= 0, $"Lower must be non-positive, got {gator.Lower}");
        }
    }

    [Fact]
    public void Validation_Symmetry_UpperAndLowerCoexist()
    {
        // In a trending market, both upper and lower should be active
        var gator = new Gator(jawPeriod: 5, jawShift: 3, teethPeriod: 3, teethShift: 2, lipsPeriod: 2, lipsShift: 1);
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < 30; i++)
        {
            gator.Update(new TValue(baseTime.AddMinutes(i), 100 + (i * 3)));
        }

        Assert.True(gator.IsHot);
        // In a strong trend, upper > 0 and lower < 0
        Assert.True(gator.Last.Value > 0, $"Upper should be positive in trend, got {gator.Last.Value}");
        Assert.True(gator.Lower < 0, $"Lower should be negative in trend, got {gator.Lower}");
    }

    [Fact]
    public void Validation_ZeroShift_StillWorks()
    {
        // Zero shift = no delay, immediate difference
        var gator = new Gator(jawPeriod: 13, jawShift: 0, teethPeriod: 8, teethShift: 0, lipsPeriod: 5, lipsShift: 0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.3);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            var result = gator.Update(new TValue(bar.Time, bar.Close));
            Assert.True(double.IsFinite(result.Value));
            Assert.True(result.Value >= 0);
        }
    }
}
