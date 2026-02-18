namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for OneEuro Filter: verifying mathematical properties of the
/// speed-adaptive low-pass filter algorithm.
/// </summary>
public class OneEuroValidationTests
{
    // ═══════════════════════════════════════════════════════
    // 1. Constant Input → Exact Passthrough
    // ═══════════════════════════════════════════════════════
    [Fact]
    public void ConstantInput_ReturnsConstant()
    {
        int N = 200;
        double[] src = new double[N];
        double[] output = new double[N];
        Array.Fill(src, 100.0);
        OneEuro.Batch(src, output);

        for (int i = 0; i < N; i++)
        {
            Assert.Equal(100.0, output[i], 1e-10);
        }
    }

    // ═══════════════════════════════════════════════════════
    // 2. Step Response — Filter Converges to New Level
    // ═══════════════════════════════════════════════════════
    [Fact]
    public void StepInput_ConvergesToNewLevel()
    {
        int N = 500;
        double[] src = new double[N];
        double[] output = new double[N];
        for (int i = 0; i < N; i++)
        {
            src[i] = i < 100 ? 100.0 : 200.0;
        }

        OneEuro.Batch(src, output);

        // After enough bars, should converge close to 200
        Assert.True(Math.Abs(output[^1] - 200.0) < 0.01,
            $"Should converge to 200, got {output[^1]:F6}");
    }

    // ═══════════════════════════════════════════════════════
    // 3. Zero Beta → No Speed Adaptation (constant alpha)
    // ═══════════════════════════════════════════════════════
    [Fact]
    public void ZeroBeta_FixedCutoff()
    {
        int N = 100;
        double[] src = new double[N];
        double[] output = new double[N];
        for (int i = 0; i < N; i++)
        {
            src[i] = 100.0 + 10.0 * Math.Sin(2.0 * Math.PI * i / 20.0);
        }

        // With beta=0, cutoff is always minCutoff regardless of speed
        OneEuro.Batch(src, output, minCutoff: 0.5, beta: 0.0);

        // The filter should smooth equally regardless of speed changes
        // Verify it produces finite values and is smoother than input
        double inputVariation = 0, outputVariation = 0;
        for (int i = 1; i < N; i++)
        {
            inputVariation += Math.Abs(src[i] - src[i - 1]);
            outputVariation += Math.Abs(output[i] - output[i - 1]);
        }
        Assert.True(outputVariation < inputVariation);
    }

    // ═══════════════════════════════════════════════════════
    // 4. Higher MinCutoff → Less Smoothing
    // ═══════════════════════════════════════════════════════
    [Fact]
    public void HigherMinCutoff_LessSmoothing()
    {
        int N = 200;
        double[] src = new double[N];
        for (int i = 0; i < N; i++)
        {
            src[i] = 100.0 + 5.0 * Math.Sin(2.0 * Math.PI * i / 10.0);
        }

        double[] smoothOut = new double[N];
        double[] roughOut = new double[N];
        OneEuro.Batch(src, smoothOut, minCutoff: 0.1, beta: 0.0);
        OneEuro.Batch(src, roughOut, minCutoff: 10.0, beta: 0.0);

        double smoothRange = GetAmplitude(smoothOut);
        double roughRange = GetAmplitude(roughOut);

        // Higher cutoff should preserve more amplitude (less smoothing)
        Assert.True(roughRange > smoothRange,
            $"Higher cutoff ({roughRange:F4}) should have more amplitude than lower ({smoothRange:F4})");
    }

    // ═══════════════════════════════════════════════════════
    // 5. Higher Beta → Less Lag on Step Change
    // ═══════════════════════════════════════════════════════
    [Fact]
    public void HigherBeta_ReducesLag()
    {
        int N = 200;
        double[] src = new double[N];
        for (int i = 0; i < N; i++)
        {
            src[i] = i < 50 ? 100.0 : 200.0;
        }

        double[] slowOut = new double[N];
        double[] fastOut = new double[N];
        OneEuro.Batch(src, slowOut, minCutoff: 0.5, beta: 0.0);
        OneEuro.Batch(src, fastOut, minCutoff: 0.5, beta: 0.5);

        // After step (at bar 55), fast should be closer to 200
        double slowDist = Math.Abs(200.0 - slowOut[55]);
        double fastDist = Math.Abs(200.0 - fastOut[55]);

        Assert.True(fastDist < slowDist,
            $"High-beta distance ({fastDist:F4}) should be less than zero-beta ({slowDist:F4})");
    }

    // ═══════════════════════════════════════════════════════
    // 6. Noise Reduction — Jitter Removal
    // ═══════════════════════════════════════════════════════
    [Fact]
    public void NoiseReduction_SmoothsJitter()
    {
        int N = 500;
        double[] src = new double[N];
        var rng = new Random(42);
        for (int i = 0; i < N; i++)
        {
            src[i] = 100.0 + rng.NextDouble() * 2.0 - 1.0; // ±1 jitter around 100
        }

        double[] output = new double[N];
        OneEuro.Batch(src, output, minCutoff: 0.1, beta: 0.0);

        // Compute variance of output vs input (skip first 50 warmup)
        double inVar = Variance(src.AsSpan(50));
        double outVar = Variance(output.AsSpan(50));

        Assert.True(outVar < inVar * 0.5,
            $"Output variance ({outVar:F6}) should be much less than input variance ({inVar:F6})");
    }

    // ═══════════════════════════════════════════════════════
    // 7. Output Bounded by Input Range
    // ═══════════════════════════════════════════════════════
    [Fact]
    public void OutputBoundedByInputRange()
    {
        int N = 200;
        double[] src = new double[N];
        for (int i = 0; i < N; i++)
        {
            src[i] = 100.0 + 20.0 * Math.Sin(2.0 * Math.PI * i / 30.0);
        }

        double[] output = new double[N];
        OneEuro.Batch(src, output);

        double srcMin = src.Min();
        double srcMax = src.Max();

        for (int i = 0; i < N; i++)
        {
            Assert.True(output[i] >= srcMin - 1.0 && output[i] <= srcMax + 1.0,
                $"Output[{i}]={output[i]:F4} should be within input range [{srcMin:F4}, {srcMax:F4}]");
        }
    }

    // ═══════════════════════════════════════════════════════
    // 8. Streaming Matches Batch
    // ═══════════════════════════════════════════════════════
    [Fact]
    public void StreamingMatchesBatch()
    {
        int N = 200;
        double[] src = new double[N];
        for (int i = 0; i < N; i++)
        {
            src[i] = 100.0 + 5.0 * Math.Sin(2.0 * Math.PI * i / 20.0) + (i % 3 == 0 ? 1.0 : -1.0);
        }

        // Batch
        double[] batchOut = new double[N];
        OneEuro.Batch(src, batchOut);

        // Streaming
        var ind = new OneEuro();
        double[] streamOut = new double[N];
        for (int i = 0; i < N; i++)
        {
            streamOut[i] = ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), src[i])).Value;
        }

        for (int i = 0; i < N; i++)
        {
            Assert.Equal(batchOut[i], streamOut[i], 1e-10);
        }
    }

    // ═══════════════════════════════════════════════════════
    // 9. GBM Feed Stability
    // ═══════════════════════════════════════════════════════
    [Fact]
    public void GbmFeed_ProducesFiniteResults()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.5, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ind = new OneEuro();
        int finiteCount = 0;
        foreach (var bar in bars.Close)
        {
            var result = ind.Update(bar);
            if (double.IsFinite(result.Value))
            {
                finiteCount++;
            }
        }
        Assert.Equal(500, finiteCount);
    }

    // ═══════════════════════════════════════════════════════
    // Helper Methods
    // ═══════════════════════════════════════════════════════
    private static double GetAmplitude(double[] data)
    {
        double max = double.MinValue, min = double.MaxValue;
        foreach (double v in data)
        {
            if (v > max) { max = v; }
            if (v < min) { min = v; }
        }
        return (max - min) / 2.0;
    }

    private static double Variance(ReadOnlySpan<double> data)
    {
        double sum = 0, sumSq = 0;
        foreach (double v in data)
        {
            sum += v;
            sumSq += v * v;
        }
        double mean = sum / data.Length;
        return sumSq / data.Length - mean * mean;
    }
}
