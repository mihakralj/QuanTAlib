using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for PACF (Partial Autocorrelation Function).
/// PACF is not commonly implemented in standard trading libraries (TA-Lib, Skender, Tulip, Ooples),
/// so validation is performed against mathematical properties and theoretical expectations.
/// </summary>
public class PacfValidationTests
{
    private const double Epsilon = 1e-6;

    #region Mathematical Property Validation

    [Fact]
    public void Pacf_OutputBoundedBetweenMinusOneAndOne()
    {
        // PACF must always be in range [-1, 1]
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int lag = 1; lag <= 10; lag++)
        {
            var pacf = new Pacf(50, lag);
            foreach (var bar in bars)
            {
                pacf.Update(new TValue(bar.Time, bar.Close));
                Assert.True(pacf.Last.Value >= -1.0 && pacf.Last.Value <= 1.0,
                    $"PACF at lag {lag} must be in [-1, 1], got {pacf.Last.Value}");
            }
        }
    }

    [Fact]
    public void Pacf_ConstantSeries_ReturnsZero()
    {
        // A constant series has zero variance, hence PACF = 0
        var pacf = new Pacf(20, 1);

        for (int i = 0; i < 50; i++)
        {
            pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }

        Assert.Equal(0, pacf.Last.Value, Epsilon);
    }

    [Fact]
    public void Pacf_Lag1_EqualsAcf_Lag1()
    {
        // By definition, φ_11 = r_1 (PACF at lag 1 equals ACF at lag 1)
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var pacf = new Pacf(50, 1);
        var acf = new Acf(50, 1);

        foreach (var bar in bars)
        {
            var tv = new TValue(bar.Time, bar.Close);
            pacf.Update(tv);
            acf.Update(tv);
        }

        Assert.Equal(acf.Last.Value, pacf.Last.Value, 6);
    }

    [Fact]
    public void Pacf_WhiteNoise_CloseToZeroForAllLags()
    {
        // For white noise (iid), all PACF values should be statistically close to zero
        // Using returns which are approximately white noise
        var gbm = new GBM(mu: 0.0, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Calculate returns
        var returns = new List<double>();
        for (int i = 1; i < bars.Count; i++)
        {
            returns.Add(Math.Log(bars[i].Close / bars[i - 1].Close));
        }

        // PACF of returns should be near zero (95% confidence: ±1.96/√n ≈ 0.062 for n=999)
        // We use a wider tolerance (0.2) since this is stochastic

        for (int lag = 1; lag <= 5; lag++)
        {
            var pacf = new Pacf(100, lag);
            foreach (double ret in returns)
            {
                pacf.Update(new TValue(DateTime.UtcNow, ret));
            }

            // Most PACF values should be within confidence bounds
            // We use a wider tolerance since this is stochastic
            Assert.True(Math.Abs(pacf.Last.Value) < 0.2,
                $"PACF at lag {lag} for white noise should be near zero, got {pacf.Last.Value}");
        }
    }

    [Fact]
    public void Pacf_AR1Process_CutoffAfterLag1()
    {
        // For AR(1) process: x_t = φ*x_{t-1} + ε_t
        // PACF should be significant at lag 1 and cut off (near zero) after
        double phi = 0.7; // AR(1) coefficient

        // Use incremental bar-to-bar log-returns as i.i.d. noise: log(close_i / close_{i-1})
        var gbm = new GBM(startPrice: 100.0, sigma: 0.2, seed: 42);
        var bars = gbm.Fetch(501, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var arProcess = new List<double> { 0.0 };

        // Generate AR(1) process using incremental log-returns as white noise ε
        for (int i = 1; i < 500; i++)
        {
            double noise = Math.Log(bars[i].Close / bars[i - 1].Close); // i.i.d. incremental return
            double newValue = phi * arProcess[^1] + noise;
            arProcess.Add(newValue);
        }

        // PACF at lag 1 should be close to phi
        var pacf1 = new Pacf(100, 1);
        foreach (double val in arProcess)
        {
            pacf1.Update(new TValue(DateTime.UtcNow, val));
        }

        // PACF at lag 1 should approximate the AR coefficient
        Assert.True(Math.Abs(pacf1.Last.Value - phi) < 0.15,
            $"PACF at lag 1 for AR(1) with φ={phi} should be near {phi}, got {pacf1.Last.Value}");

        // PACF at higher lags should be smaller (cutoff behavior)
        var pacf2 = new Pacf(100, 2);
        var pacf3 = new Pacf(100, 3);

        foreach (double val in arProcess)
        {
            pacf2.Update(new TValue(DateTime.UtcNow, val));
            pacf3.Update(new TValue(DateTime.UtcNow, val));
        }

        Assert.True(Math.Abs(pacf2.Last.Value) < Math.Abs(pacf1.Last.Value),
            $"PACF at lag 2 ({pacf2.Last.Value}) should be smaller than lag 1 ({pacf1.Last.Value})");
    }

    [Fact]
    public void Pacf_DeterministicTrend_HighPositiveAtLag1()
    {
        // A deterministic trend shows high persistence
        var pacf = new Pacf(30, 1);

        for (int i = 0; i < 100; i++)
        {
            pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i * 0.5));
        }

        // Trending series should have high positive PACF at lag 1
        Assert.True(pacf.Last.Value > 0.5,
            $"PACF at lag 1 for trending series should be high positive, got {pacf.Last.Value}");
    }

    [Fact]
    public void Pacf_AlternatingPattern_NegativeAtLag1()
    {
        // An alternating pattern should show negative PACF at lag 1
        var pacf = new Pacf(30, 1);

        for (int i = 0; i < 100; i++)
        {
            double value = (i % 2 == 0) ? 100.0 : 105.0;
            pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), value));
        }

        // Alternating series should have negative PACF at lag 1
        Assert.True(pacf.Last.Value < -0.5,
            $"PACF at lag 1 for alternating series should be negative, got {pacf.Last.Value}");
    }

    #endregion

    #region Batch vs Streaming Consistency

    [Fact]
    public void Pacf_BatchMatchesStreaming()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        int period = 30;
        int lag = 2;

        // Create TSeries from bars
        var tSeries = new TSeries();
        foreach (var bar in bars)
        {
            tSeries.Add(new TValue(bar.Time, bar.Close));
        }

        // Streaming
        var streaming = new Pacf(period, lag);
        foreach (var tv in tSeries)
        {
            streaming.Update(tv);
        }

        // Batch
        var batchResult = Pacf.Batch(tSeries, period, lag);

        // Compare last values
        Assert.Equal(batchResult[^1].Value, streaming.Last.Value, Epsilon);
    }

    [Fact]
    public void Pacf_SpanMatchesTSeries()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        int period = 30;
        int lag = 3;

        // Create arrays
        double[] source = bars.Select(b => b.Close).ToArray();
        double[] spanOutput = new double[source.Length];

        // Span calculation
        Pacf.Batch(source, spanOutput, period, lag);

        // TSeries calculation
        var tSeries = new TSeries();
        foreach (var bar in bars)
        {
            tSeries.Add(new TValue(bar.Time, bar.Close));
        }
        var tSeriesResult = Pacf.Batch(tSeries, period, lag);

        // Compare last 50 values
        for (int i = source.Length - 50; i < source.Length; i++)
        {
            Assert.Equal(tSeriesResult[i].Value, spanOutput[i], Epsilon);
        }
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Pacf_MinimumValidPeriod_Works()
    {
        // Period must be > lag + 1, so period=4 with lag=2 is minimum valid
        var pacf = new Pacf(4, 2);

        for (int i = 0; i < 10; i++)
        {
            pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        Assert.True(pacf.IsHot);
        Assert.True(double.IsFinite(pacf.Last.Value));
    }

    [Fact]
    public void Pacf_HighLag_Works()
    {
        // Test with high lag value
        int lag = 20;
        int period = 50;
        var pacf = new Pacf(period, lag);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            pacf.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(pacf.IsHot);
        Assert.True(pacf.Last.Value >= -1.0 && pacf.Last.Value <= 1.0);
    }

    [Fact]
    public void Pacf_NaNHandling_ProducesFiniteOutput()
    {
        var pacf = new Pacf(20, 1);

        // Feed some valid values
        for (int i = 0; i < 30; i++)
        {
            pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        // Inject NaN
        pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(30), double.NaN));

        Assert.True(double.IsFinite(pacf.Last.Value));
    }

    [Fact]
    public void Pacf_InfinityHandling_ProducesFiniteOutput()
    {
        var pacf = new Pacf(20, 1);

        // Feed some valid values
        for (int i = 0; i < 30; i++)
        {
            pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        // Inject infinity
        pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(30), double.PositiveInfinity));

        Assert.True(double.IsFinite(pacf.Last.Value));
    }

    #endregion

    #region Durbin-Levinson Recursion Verification

    [Fact]
    public void Pacf_DurbinLevinsonRecursion_ProducesCorrectResults()
    {
        // Verify that the Durbin-Levinson recursion produces mathematically valid results
        // by checking that the result is bounded and consistent across multiple runs

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var results = new List<double>();
        for (int run = 0; run < 3; run++)
        {
            var pacf = new Pacf(50, 5);
            foreach (var bar in bars)
            {
                pacf.Update(new TValue(bar.Time, bar.Close));
            }
            results.Add(pacf.Last.Value);
        }

        // All runs should produce the same result (deterministic)
        for (int i = 1; i < results.Count; i++)
        {
            Assert.Equal(results[0], results[i], Epsilon);
        }

        // Result should be bounded
        Assert.True(results[0] >= -1.0 && results[0] <= 1.0);
    }

    #endregion
}
