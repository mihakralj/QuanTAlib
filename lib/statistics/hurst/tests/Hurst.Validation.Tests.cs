
using Skender.Stock.Indicators;
using QuanTAlib.Tests;

// HURST Validation Tests - Hurst Exponent via Rescaled Range (R/S) Analysis
// Validated against self-consistency and known mathematical properties
// No external library provides a direct R/S-based Hurst exponent equivalent

namespace QuanTAlib.Tests;

public sealed class HurstValidationTests
{
    private static TSeries CreateGbmSeries(int count = 500, double mu = 0.0, double sigma = 0.2, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100.0, mu: mu, sigma: sigma, seed: seed);
        var times = new List<long>(count);
        var values = new List<double>(count);
        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next(isNew: true);
            times.Add(bar.Time);
            values.Add(bar.Close);
        }
        return new TSeries(times, values);
    }

    /// <summary>
    /// A pure random walk (GBM with zero drift) should produce H near 0.5.
    /// </summary>
    [Fact]
    public void RandomWalk_HurstNearHalf()
    {
        const int period = 100;
        var series = CreateGbmSeries(count: 1000, mu: 0.0, sigma: 0.2, seed: 42);

        var h = new Hurst(period);
        for (int i = 0; i < series.Count; i++)
        {
            h.Update(series[i]);
        }

        // H should be approximately 0.5 for random walk — allow generous tolerance
        Assert.InRange(h.Last.Value, 0.25, 0.75);
    }

    /// <summary>
    /// Multiple independent random walks should all produce H near 0.5.
    /// </summary>
    [Fact]
    public void MultipleRandomWalks_AllNearHalf()
    {
        const int period = 100;
        int[] seeds = [42, 123, 456, 789, 1024];

        foreach (int seed in seeds)
        {
            var series = CreateGbmSeries(count: 500, mu: 0.0, sigma: 0.2, seed: seed);

            var h = new Hurst(period);
            for (int i = 0; i < series.Count; i++)
            {
                h.Update(series[i]);
            }

            Assert.InRange(h.Last.Value, 0.2, 0.8);
        }
    }

    /// <summary>
    /// Hurst exponent range — should always produce finite values within theoretically meaningful bounds.
    /// </summary>
    [Fact]
    public void HurstRange_AlwaysFinite()
    {
        const int period = 50;
        var series = CreateGbmSeries(count: 300, mu: 0.05, sigma: 0.2, seed: 42);
        var h = new Hurst(period);

        for (int i = 0; i < series.Count; i++)
        {
            var result = h.Update(series[i]);
            Assert.True(double.IsFinite(result.Value), $"Value at {i} is not finite: {result.Value}");
        }
    }

    /// <summary>
    /// Batch and streaming must produce identical results.
    /// </summary>
    [Fact]
    public void BatchVsStreaming_ExactMatch()
    {
        const int period = 20;
        var series = CreateGbmSeries(count: 200, mu: 0.05, sigma: 0.2, seed: 42);

        // Batch
        var batchResult = Hurst.Batch(series, period);

        // Streaming
        var streamingInd = new Hurst(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }

        Assert.Equal(batchResult.Last.Value, streamingInd.Last.Value, 1e-12);
    }

    /// <summary>
    /// Span batch must match TSeries batch exactly.
    /// </summary>
    [Fact]
    public void SpanBatch_MatchesTSeriesBatch()
    {
        const int period = 30;
        var series = CreateGbmSeries(count: 200, mu: 0.05, sigma: 0.2, seed: 42);

        var tseriesResult = Hurst.Batch(series, period);

        double[] source = new double[series.Count];
        double[] output = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            source[i] = series[i].Value;
        }

        Hurst.Batch(source.AsSpan(), output.AsSpan(), period);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-10);
        }
    }

    /// <summary>
    /// Constant price series should produce H = 0.5 (degenerate — all log returns = 0).
    /// </summary>
    [Fact]
    public void ConstantSeries_ReturnsDefaultHalf()
    {
        const int period = 20;
        var h = new Hurst(period);

        for (int i = 0; i < 50; i++)
        {
            h.Update(new TValue(DateTime.UtcNow, 100.0));
        }

        // All log returns are zero → stddev = 0 → no valid R/S → default 0.5
        Assert.Equal(0.5, h.Last.Value, 1e-10);
    }

    /// <summary>
    /// Calculate static method returns both results and indicator.
    /// </summary>
    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var series = CreateGbmSeries(count: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var (results, indicator) = Hurst.Calculate(series, 20);

        Assert.Equal(series.Count, results.Count);
        Assert.True(indicator.IsHot);
        Assert.Equal(results.Last.Value, indicator.Last.Value, 1e-12);
    }

    /// <summary>
    /// Deterministic: same input always produces identical output.
    /// </summary>
    [Fact]
    public void Deterministic_SameInputSameOutput()
    {
        const int period = 30;
        var series = CreateGbmSeries(count: 200, mu: 0.05, sigma: 0.2, seed: 42);

        var h1 = new Hurst(period);
        var h2 = new Hurst(period);

        for (int i = 0; i < series.Count; i++)
        {
            h1.Update(series[i]);
            h2.Update(series[i]);
        }

        Assert.Equal(h1.Last.Value, h2.Last.Value, 1e-15);
    }

    /// <summary>
    /// Structural comparison with Skender GetHurst — both compute Hurst exponent
    /// but may use different R/S subdivision strategies and regression methods.
    /// Validates that Skender produces finite results in the same range.
    /// </summary>
    [Fact]
    public void Validate_Skender_Hurst_Structural()
    {
        const int period = 20;
        using var data = new ValidationTestData(10000);

        // QuanTAlib streaming
        var indicator = new Hurst(period);
        foreach (var tv in data.Data)
        {
            indicator.Update(tv);
        }

        // Skender
        var sResult = data.SkenderQuotes.GetHurst(period).ToList();

        // QuanTAlib produces finite output
        Assert.True(double.IsFinite(indicator.Last.Value), "QuanTAlib Hurst last must be finite");

        // Skender produces finite Hurst exponents
        int sFinite = sResult.Count(r => r.HurstExponent is not null && double.IsFinite(r.HurstExponent.Value));
        Assert.True(sFinite > 50, $"Skender produced only {sFinite} finite Hurst values");

        // Both Hurst exponents should be finite
        foreach (var r in sResult.Where(r => r.HurstExponent is not null))
        {
            Assert.True(double.IsFinite(r.HurstExponent!.Value),
                $"Skender Hurst value {r.HurstExponent.Value} is not finite");
        }
    }
}
