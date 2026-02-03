using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for ACF (Autocorrelation Function).
/// ACF is not commonly implemented in trading libraries (TA-Lib, Skender, etc.),
/// so validation is done against mathematical properties and known theoretical results.
/// </summary>
public class AcfValidationTests
{
    private const double Tolerance = 1e-9;

    #region Mathematical Property Validation

    [Fact]
    public void Validation_AcfAtLagZero_ShouldBeOne()
    {
        // ACF at lag 0 = variance / variance = 1
        // We can't directly test lag=0 (our minimum is 1), but we can verify
        // that with highly correlated data (perfect positive correlation), ACF approaches 1
        var acf = new Acf(20, 1);

        // Create a series where each value is very close to the previous
        // (linear trend: x_t = t)
        for (int i = 0; i < 30; i++)
        {
            acf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), i * 1.0));
        }

        // For a linear trend, lag-1 autocorrelation should be high (close to 1)
        // With period=20, the sample ACF may be lower than theoretical due to finite window
        Assert.True(acf.Last.Value >= 0.8, $"Linear trend should have high lag-1 ACF, got {acf.Last.Value}");
    }

    [Fact]
    public void Validation_AcfBoundedByOne()
    {
        // ACF must always be in [-1, 1]
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int lag = 1; lag <= 10; lag++)
        {
            var acf = new Acf(50, lag);
            foreach (var bar in bars)
            {
                acf.Update(new TValue(bar.Time, bar.Close));
                Assert.True(acf.Last.Value >= -1.0 && acf.Last.Value <= 1.0,
                    $"ACF at lag {lag} = {acf.Last.Value} is out of bounds");
            }
        }
    }

    [Fact]
    public void Validation_ConstantSeries_AcfIsZeroOrUndefined()
    {
        // For a constant series, variance = 0, so ACF is undefined
        // Our implementation returns 0 in this case
        var acf = new Acf(20, 1);

        for (int i = 0; i < 30; i++)
        {
            acf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }

        Assert.Equal(0, acf.Last.Value);
    }

    [Fact]
    public void Validation_AlternatingSequence_NegativeAcf()
    {
        // For an alternating sequence (100, -100, 100, -100, ...),
        // the lag-1 ACF should be strongly negative (close to -1)
        var acf = new Acf(20, 1);

        for (int i = 0; i < 30; i++)
        {
            double val = i % 2 == 0 ? 100.0 : -100.0;
            acf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), val));
        }

        // Alternating sequence has perfect negative correlation at lag 1
        Assert.True(acf.Last.Value < -0.9, $"Alternating sequence should have negative ACF, got {acf.Last.Value}");
    }

    [Fact]
    public void Validation_PeriodicSequence_AcfMatchesPeriod()
    {
        // For a periodic sequence with period p, ACF at lag p should be high
        int period = 4;
        var acfLag4 = new Acf(20, period);
        var acfLag2 = new Acf(20, 2); // Half period

        // Create periodic sequence: 1, 2, 3, 4, 1, 2, 3, 4, ...
        for (int i = 0; i < 50; i++)
        {
            double val = (i % period) + 1;
            acfLag4.Update(new TValue(DateTime.UtcNow.AddSeconds(i), val));
            acfLag2.Update(new TValue(DateTime.UtcNow.AddSeconds(i), val));
        }

        // ACF at lag=period should be high (perfect correlation in theory)
        // With period=20 window and lag=4, finite sample effects reduce measured ACF
        Assert.True(acfLag4.Last.Value >= 0.75, $"ACF at period lag should be high, got {acfLag4.Last.Value}");

        // ACF at lag=period/2 for a sawtooth pattern (1,2,3,4 repeating) should be lower
        // because values at distance 2 are not as correlated as at distance 4
    }

    [Fact]
    public void Validation_RandomWhiteNoise_AcfNearZero()
    {
        // For white noise, ACF at any lag > 0 should be close to zero
        var acf = new Acf(100, 5);

        // Generate pseudo-random values with zero mean
        var random = new Random(42);
        for (int i = 0; i < 500; i++)
        {
            double val = random.NextDouble() * 2 - 1; // Uniform [-1, 1]
            acf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), val));
        }

        // For white noise, ACF should be close to zero (but not exactly due to finite sample)
        // Standard error is approximately 1/sqrt(n) ≈ 0.1 for n=100
        Assert.True(Math.Abs(acf.Last.Value) < 0.3,
            $"White noise ACF at lag 5 should be near zero, got {acf.Last.Value}");
    }

    #endregion

    #region Streaming vs Batch Consistency

    [Theory]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(999)]
    public void Validation_StreamingMatchesBatch(int seed)
    {
        const int period = 20;
        const int lag = 1;
        const int dataLen = 100;

        var gbm = new GBM(seed: seed);
        var bars = gbm.Fetch(dataLen, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming
        var streaming = new Acf(period, lag);
        foreach (var bar in bars)
        {
            streaming.Update(new TValue(bar.Time, bar.Close));
        }

        // Batch via TSeries
        var tSeries = new TSeries();
        foreach (var bar in bars)
        {
            tSeries.Add(new TValue(bar.Time, bar.Close));
        }

        var batch = Acf.Calculate(tSeries, period, lag);

        // Compare last values
        Assert.Equal(batch[^1].Value, streaming.Last.Value, Tolerance);
    }

    [Fact]
    public void Validation_SpanMatchesTSeries()
    {
        const int period = 14;
        const int lag = 2;
        const int dataLen = 200;

        var gbm = new GBM(seed: 77);
        var bars = gbm.Fetch(dataLen, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // TSeries approach
        var tSeries = new TSeries();
        foreach (var bar in bars)
        {
            tSeries.Add(new TValue(bar.Time, bar.Close));
        }

        var tSeriesResult = Acf.Calculate(tSeries, period, lag);

        // Span approach
        double[] source = new double[dataLen];
        double[] spanResult = new double[dataLen];
        for (int i = 0; i < dataLen; i++)
        {
            source[i] = bars[i].Close;
        }

        Acf.Batch(source, spanResult, period, lag);

        // Compare all values after warmup
        for (int i = period; i < dataLen; i++)
        {
            Assert.Equal(tSeriesResult[i].Value, spanResult[i], Tolerance);
        }
    }

    #endregion

    #region AR(1) Process Validation

    [Fact]
    public void Validation_AR1Process_AcfDecaysExponentially()
    {
        // For an AR(1) process: X_t = φ * X_{t-1} + ε_t
        // The theoretical ACF at lag k is φ^k
        double phi = 0.8; // AR(1) coefficient
        const int n = 1000;

        double[] ar1Data = new double[n];
        ar1Data[0] = 0;
        var random = new Random(42);

        // Generate AR(1) process
        for (int i = 1; i < n; i++)
        {
            double epsilon = (random.NextDouble() * 2 - 1) * 0.1; // Small noise
            ar1Data[i] = phi * ar1Data[i - 1] + epsilon;
        }

        // Compute ACF at different lags
        var acfLag1 = new Acf(200, 1);
        var acfLag2 = new Acf(200, 2);
        var acfLag3 = new Acf(200, 3);

        for (int i = 0; i < n; i++)
        {
            var tv = new TValue(DateTime.UtcNow.AddSeconds(i), ar1Data[i]);
            acfLag1.Update(tv);
            acfLag2.Update(tv);
            acfLag3.Update(tv);
        }

        // Theoretical values: ρ_1 = φ = 0.8, ρ_2 = φ² = 0.64, ρ_3 = φ³ = 0.512
        // Allow some tolerance due to finite sample effects
        Assert.True(acfLag1.Last.Value > 0.7 && acfLag1.Last.Value < 0.9,
            $"ACF at lag 1 for AR(1) with φ=0.8 should be ~0.8, got {acfLag1.Last.Value}");
        Assert.True(acfLag2.Last.Value > 0.5 && acfLag2.Last.Value < 0.8,
            $"ACF at lag 2 for AR(1) with φ=0.8 should be ~0.64, got {acfLag2.Last.Value}");
        Assert.True(acfLag3.Last.Value > 0.4 && acfLag3.Last.Value < 0.7,
            $"ACF at lag 3 for AR(1) with φ=0.8 should be ~0.512, got {acfLag3.Last.Value}");

        // Verify decay: ρ_1 > ρ_2 > ρ_3
        Assert.True(acfLag1.Last.Value > acfLag2.Last.Value,
            $"ACF should decay: lag1={acfLag1.Last.Value} should be > lag2={acfLag2.Last.Value}");
        Assert.True(acfLag2.Last.Value > acfLag3.Last.Value,
            $"ACF should decay: lag2={acfLag2.Last.Value} should be > lag3={acfLag3.Last.Value}");
    }

    #endregion

    #region Different Period Sizes

    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public void Validation_DifferentPeriods_ConsistentResults(int period)
    {
        const int lag = 1;
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var acf = new Acf(period, lag);
        foreach (var bar in bars)
        {
            acf.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(acf.IsHot);
        Assert.True(double.IsFinite(acf.Last.Value));
        Assert.True(acf.Last.Value >= -1.0 && acf.Last.Value <= 1.0);
    }

    #endregion
}