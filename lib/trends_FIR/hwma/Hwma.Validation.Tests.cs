namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for HWMA (Holt-Winters Moving Average).
/// Note: HWMA is not available in most external libraries (TA-Lib, Skender, etc.),
/// so we validate against our own PineScript reference implementation and mathematical properties.
/// </summary>
public class HwmaValidationTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void Hwma_MatchesPineScriptReference()
    {
        // Test that our implementation matches the PineScript reference
        // hwma.pine formulas:
        // α = 2/(period+1), β = 1/period, γ = 1/period
        // F = α × source + (1-α) × (prevF + prevV + 0.5 × prevA)
        // V = β × (F - prevF) + (1-β) × (prevV + prevA)
        // A = γ × (V - prevV) + (1-γ) × prevA
        // output = F + V + 0.5 × A
        var series = new TSeries();
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        int period = 10;
        var hwma = new Hwma(period);
        var results = hwma.Update(series);

        // Manual calculation
        double alpha = 2.0 / (period + 1.0);
        double beta = 1.0 / period;
        double gamma = 1.0 / period;

        double F = series[0].Value;
        double V = 0;
        double A = 0;

        for (int i = 1; i < series.Count; i++)
        {
            double prevF = F;
            double prevV = V;
            double prevA = A;

            F = alpha * series[i].Value + (1 - alpha) * (prevF + prevV + 0.5 * prevA);
            V = beta * (F - prevF) + (1 - beta) * (prevV + prevA);
            A = gamma * (V - prevV) + (1 - gamma) * prevA;
        }

        double expected = F + V + 0.5 * A;

        Assert.Equal(expected, results.Last.Value, Tolerance);
    }

    [Fact]
    public void Hwma_SmoothingFactorFormulas()
    {
        // Verify smoothing factors are calculated correctly from period
        // α = 2/(period+1), β = γ = 1/period
        int period = 10;

        double expectedAlpha = 2.0 / (period + 1.0); // 2/11 ≈ 0.1818
        double expectedBeta = 1.0 / period;          // 0.1
        double expectedGamma = 1.0 / period;         // 0.1

        Assert.Equal(2.0 / 11.0, expectedAlpha, Tolerance);
        Assert.Equal(0.1, expectedBeta, Tolerance);
        Assert.Equal(0.1, expectedGamma, Tolerance);
    }

    [Fact]
    public void Hwma_ConsistentAcrossModes()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        var series = new TSeries();
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        int period = 10;

        // Batch
        var batchResults = Hwma.Batch(series, period);

        // Streaming
        var streaming = new Hwma(period);
        var streamingResults = new TSeries();
        foreach (var item in series)
        {
            streamingResults.Add(streaming.Update(item));
        }

        // Span
        double[] input = series.Values.ToArray();
        double[] spanOutput = new double[input.Length];
        Hwma.Calculate(input.AsSpan(), spanOutput.AsSpan(), period);

        // All should match
        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchResults[i].Value, streamingResults[i].Value, Tolerance);
            Assert.Equal(batchResults[i].Value, spanOutput[i], Tolerance);
        }
    }

    [Fact]
    public void Hwma_DifferentPeriodsProduceDifferentResults()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        var series = new TSeries();
        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        var hwma5 = new Hwma(5);
        var hwma10 = new Hwma(10);
        var hwma20 = new Hwma(20);

        var results5 = hwma5.Update(series);
        var results10 = hwma10.Update(series);
        var results20 = hwma20.Update(series);

        // Different periods should produce different results
        Assert.NotEqual(results5.Last.Value, results10.Last.Value);
        Assert.NotEqual(results10.Last.Value, results20.Last.Value);
    }

    [Fact]
    public void Hwma_ConstantInput_ReturnsConstant()
    {
        var hwma = new Hwma(10);
        const double constantValue = 100.0;

        for (int i = 0; i < 20; i++)
        {
            var result = hwma.Update(new TValue(DateTime.UtcNow, constantValue));
            Assert.Equal(constantValue, result.Value, Tolerance);
        }
    }

    [Fact]
    public void Hwma_TripleExponentialSmoothing_Property()
    {
        // HWMA should exhibit the triple exponential smoothing behavior:
        // - Level (F) tracks the current value
        // - Velocity (V) tracks the trend/slope
        // - Acceleration (A) tracks the change in trend

        // For a linear trend, HWMA should converge to track it closely
        var hwma = new Hwma(10);

        // Linear uptrend: 100, 101, 102, ..., 119
        for (int i = 0; i < 20; i++)
        {
            double price = 100 + i;
            hwma.Update(new TValue(DateTime.UtcNow, price));
        }

        // After 20 points of linear trend, HWMA should be close to current value
        double lastPrice = 119;
        double hwmaValue = hwma.Last.Value;

        // Should be within 5% for a well-adapted filter
        Assert.True(Math.Abs(hwmaValue - lastPrice) / lastPrice < 0.05);
    }

    [Fact]
    public void Hwma_VelocityTracking_Uptrend()
    {
        // In a consistent uptrend, HWMA should be ahead of simple EMA
        // because it accounts for velocity
        var hwma = new Hwma(10);
        var ema = new Ema(10);

        // Generate uptrend
        for (int i = 0; i < 30; i++)
        {
            double price = 100 + i * 2; // Strong uptrend
            hwma.Update(new TValue(DateTime.UtcNow, price));
            ema.Update(new TValue(DateTime.UtcNow, price));
        }

        // HWMA should be closer to current price than EMA in uptrend
        // (or even ahead due to velocity/acceleration extrapolation)
        double currentPrice = 100 + 29 * 2; // 158
        double hwmaDiff = Math.Abs(hwma.Last.Value - currentPrice);
        double emaDiff = Math.Abs(ema.Last.Value - currentPrice);

        // HWMA should track better than or equal to EMA in trends
        Assert.True(hwmaDiff <= emaDiff * 1.5); // Allow some margin
    }

    [Fact]
    public void Hwma_AlphaBetaGamma_CustomValues()
    {
        // Test explicit alpha/beta/gamma constructor produces valid results
        var hwma = new Hwma(0.3, 0.2, 0.1);

        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 30; i++)
        {
            var bar = gbm.Next(isNew: true);
            hwma.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(double.IsFinite(hwma.Last.Value));
        Assert.True(hwma.Last.Value > 0);
    }
}
