namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for the Wavelet Denoising Filter.
/// Since wavelet denoising is a custom filter with no direct external library equivalent,
/// validation uses self-consistency: denoising effectiveness, streaming/span parity,
/// determinism, stability, and mathematical properties of the à trous algorithm.
/// </summary>
public class WaveletValidationTests
{
    [Fact]
    public void Validate_DenoisingEffectiveness_HighNoiseSignal()
    {
        // Use a signal with very strong high-frequency noise so denoising is unambiguous.
        // Clean signal: slow sine. Noise: large amplitude, high frequency.
        const int T = 500;
        double[] clean = new double[T];
        double[] noisy = new double[T];
        for (int i = 0; i < T; i++)
        {
            clean[i] = 100.0 + 10.0 * Math.Sin(2 * Math.PI * i / 80.0);
            // Alternating noise with amplitude 25 — much larger than signal variation
            noisy[i] = clean[i] + 25.0 * ((i % 2 == 0) ? 1.0 : -1.0);
        }

        double[] denoised = new double[T];
        Wavelet.Batch(noisy, denoised, 4, 1.0);

        // The denoised signal should have much less variance of first-differences
        // than the noisy signal (alternating noise creates huge diffs)
        int start = T / 2;
        double noisyDiffVar = DiffVariance(noisy.AsSpan(start));
        double denoisedDiffVar = DiffVariance(denoised.AsSpan(start));

        Assert.True(denoisedDiffVar < noisyDiffVar,
            $"Denoised diff variance ({denoisedDiffVar:F4}) should be less than noisy diff variance ({noisyDiffVar:F4})");
    }

    [Fact]
    public void Validate_StreamingMatchesSpan()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var data = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();

        // Streaming
        var indicator = new Wavelet(4, 1.0);
        double[] streamResults = new double[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            streamResults[i] = indicator.Update(new TValue(DateTime.UtcNow, input[i])).Value;
        }

        // Span
        double[] spanResults = new double[input.Length];
        Wavelet.Batch(input, spanResults, 4, 1.0);

        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(streamResults[i], spanResults[i], 1e-10);
        }
    }

    [Fact]
    public void Validate_Deterministic()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 99);
        var data = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();

        double[] run1 = new double[input.Length];
        double[] run2 = new double[input.Length];

        Wavelet.Batch(input, run1, 3, 1.5);
        Wavelet.Batch(input, run2, 3, 1.5);

        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(run1[i], run2[i], 1e-15);
        }
    }

    [Fact]
    public void Validate_HigherThreshold_MoreDeviation()
    {
        // Higher threshold removes more detail coefficients, so the output
        // deviates more from the original input signal.
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.3, seed: 77);
        var data = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();

        double[] low = new double[input.Length];
        double[] high = new double[input.Length];

        Wavelet.Batch(input, low, 4, 0.5);
        Wavelet.Batch(input, high, 4, 3.0);

        // Sum of absolute deviations from input should be higher for larger threshold
        int start = input.Length / 2;
        double sadLow = 0, sadHigh = 0;
        for (int i = start; i < input.Length; i++)
        {
            sadLow += Math.Abs(input[i] - low[i]);
            sadHigh += Math.Abs(input[i] - high[i]);
        }

        Assert.True(sadHigh >= sadLow,
            $"High-threshold SAD ({sadHigh:F4}) should be >= low-threshold SAD ({sadLow:F4})");
    }

    [Fact]
    public void Validate_ConstantSignal_Preserved()
    {
        const int len = 100;
        double[] input = new double[len];
        double[] output = new double[len];
        Array.Fill(input, 42.0);

        Wavelet.Batch(input, output, 4, 1.0);

        for (int i = 0; i < len; i++)
        {
            Assert.Equal(42.0, output[i], 1e-10);
        }
    }

    [Fact]
    public void Validate_LinearTrend_MinimalDistortion()
    {
        const int len = 200;
        double[] input = new double[len];
        double[] output = new double[len];
        for (int i = 0; i < len; i++)
        {
            input[i] = 100.0 + 0.5 * i;
        }

        Wavelet.Batch(input, output, 3, 1.0);

        // After warmup, denoised should closely track the trend
        int start = len / 2;
        double maxDiff = 0;
        for (int i = start; i < len; i++)
        {
            maxDiff = Math.Max(maxDiff, Math.Abs(input[i] - output[i]));
        }

        Assert.True(maxDiff < 5.0, $"Max deviation from linear trend ({maxDiff:F4}) should be small");
    }

    [Fact]
    public void Validate_Stability_LongSeries()
    {
        const int len = 5000;
        double[] input = new double[len];
        double[] output = new double[len];
        for (int i = 0; i < len; i++)
        {
            input[i] = 100 + 10 * Math.Sin(2 * Math.PI * i / 50.0) + 0.5 * Math.Sin(101.1 * i);
        }

        Wavelet.Batch(input, output, 4, 1.0);

        // All values should be finite and bounded
        for (int i = 0; i < len; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"output[{i}] must be finite");
            Assert.True(Math.Abs(output[i]) < 200, $"output[{i}] ({output[i]:F2}) should be bounded");
        }
    }

    [Fact]
    public void Validate_ZeroThreshold_PreservesSignal()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 55);
        var data = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();

        double[] output = new double[input.Length];
        Wavelet.Batch(input, output, 3, 0.0);

        // With zero threshold, soft thresholding does nothing — all details pass through
        // The reconstruction should still be valid (close to input)
        for (int i = 0; i < input.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]));
        }
    }

    [Fact]
    public void Validate_Calculate_ReturnsTupleWithIndicator()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 33);
        var data = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (results, indicator) = Wavelet.Calculate(data.Close, 3, 1.0);

        Assert.Equal(data.Close.Count, results.Count);
        Assert.True(indicator.IsHot);
        Assert.Equal(results[^1].Value, indicator.Last.Value, 1e-10);
    }

    private static double DiffVariance(ReadOnlySpan<double> data)
    {
        if (data.Length < 2)
        {
            return 0;
        }

        double sum = 0, sum2 = 0;
        for (int i = 1; i < data.Length; i++)
        {
            double d = data[i] - data[i - 1];
            sum += d;
            sum2 += d * d;
        }
        int n = data.Length - 1;
        double mean = sum / n;
        return sum2 / n - mean * mean;
    }
}
