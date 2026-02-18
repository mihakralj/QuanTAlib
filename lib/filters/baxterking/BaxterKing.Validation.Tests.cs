namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for the Baxter-King Band-Pass Filter.
/// BK is an academic econometric filter (Baxter &amp; King 1999); no external TA library
/// implements it. Validation uses self-consistency: DC rejection, bandpass behavior,
/// mode consistency, determinism, weight normalization, and numerical stability.
/// </summary>
public class BaxterKingValidationTests
{
    [Fact]
    public void Validate_BandpassBehavior_Synthetic()
    {
        // BK with pLow=6, pHigh=32 should pass cycles between 6 and 32 bars.
        // Cycle at period 16 (in-band) should have larger amplitude than
        // cycles at period 3 (too fast) and period 100 (too slow).
        const int T = 500;
        double[] sine3 = new double[T];    // Period 3: below pLow, should be rejected
        double[] sine16 = new double[T];   // Period 16: in-band, should pass
        double[] sine100 = new double[T];  // Period 100: above pHigh, should be rejected

        for (int i = 0; i < T; i++)
        {
            sine3[i] = Math.Sin(2 * Math.PI * i / 3.0);
            sine16[i] = Math.Sin(2 * Math.PI * i / 16.0);
            sine100[i] = Math.Sin(2 * Math.PI * i / 100.0);
        }

        double[] out3 = new double[T];
        double[] out16 = new double[T];
        double[] out100 = new double[T];

        BaxterKing.Batch(sine3, out3, 6, 32, 12);
        BaxterKing.Batch(sine16, out16, 6, 32, 12);
        BaxterKing.Batch(sine100, out100, 6, 32, 12);

        double amp3 = GetAmplitude(out3);
        double amp16 = GetAmplitude(out16);
        double amp100 = GetAmplitude(out100);

        Assert.True(amp16 > amp100, $"In-band (P=16, amp={amp16:E3}) should exceed trend (P=100, amp={amp100:E3})");
        Assert.True(amp16 > amp3, $"In-band (P=16, amp={amp16:E3}) should exceed noise (P=3, amp={amp3:E3})");
    }

    [Fact]
    public void Validate_StreamingMatchesSpan()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var data = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();

        // Span path
        double[] spanOut = new double[input.Length];
        BaxterKing.Batch(input, spanOut, 6, 32, 12);

        // Streaming path
        var ind = new BaxterKing(6, 32, 12);
        var streamResults = new double[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            streamResults[i] = ind.Update(new TValue(DateTime.UtcNow, input[i])).Value;
        }

        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(spanOut[i], streamResults[i], 1e-9);
        }
    }

    [Fact]
    public void Validate_ConstantInput_OutputZero()
    {
        double[] input = Enumerable.Repeat(50.0, 500).ToArray();
        double[] output = new double[500];

        BaxterKing.Batch(input, output, 6, 32, 12);

        // Band-pass on constant -> zero (DC rejection via weight normalization)
        for (int i = 25; i < output.Length; i++)
        {
            Assert.True(Math.Abs(output[i]) < 1e-12, $"Expected 0 for constant at [{i}], got {output[i]}");
        }
    }

    [Fact]
    public void Validate_Deterministic()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 99);
        var data = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();

        double[] out1 = new double[input.Length];
        double[] out2 = new double[input.Length];

        BaxterKing.Batch(input, out1, 6, 32, 12);
        BaxterKing.Batch(input, out2, 6, 32, 12);

        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(out1[i], out2[i], 15);
        }
    }

    [Fact]
    public void Validate_OutputOscillatesAroundZero()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 77);
        var data = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();
        double[] output = new double[input.Length];

        BaxterKing.Batch(input, output, 6, 32, 12);

        bool hasPositive = false, hasNegative = false;
        for (int i = 50; i < output.Length; i++)
        {
            if (output[i] > 0)
            {
                hasPositive = true;
            }
            if (output[i] < 0)
            {
                hasNegative = true;
            }
        }

        Assert.True(hasPositive, "Output should have positive values");
        Assert.True(hasNegative, "Output should have negative values");
    }

    [Fact]
    public void Validate_LargeDataset_Stable()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 55);
        var data = gbm.Fetch(10000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();
        double[] output = new double[input.Length];

        BaxterKing.Batch(input, output, 6, 32, 12);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output[{i}] is not finite: {output[i]}");
        }
    }

    [Fact]
    public void Validate_WeightsSumToZero()
    {
        // The BK normalization ensures weights sum exactly to zero.
        // Verify indirectly: linear ramp input produces zero output
        // (a linear function has zero band-pass content after DC + slope removal)
        double[] ramp = new double[200];
        for (int i = 0; i < ramp.Length; i++)
        {
            ramp[i] = i * 1.0;
        }
        double[] output = new double[200];

        BaxterKing.Batch(ramp, output, 6, 32, 12);

        // After warmup, linear ramp should produce ~0 because weights sum to 0
        for (int i = 25; i < output.Length; i++)
        {
            Assert.True(Math.Abs(output[i]) < 1e-8, $"Linear ramp output at [{i}] should be ~0, got {output[i]}");
        }
    }

    [Fact]
    public void Validate_DifferentPeriods_ProduceDifferentOutput()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 33);
        var data = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();

        double[] out1 = new double[input.Length];
        double[] out2 = new double[input.Length];

        BaxterKing.Batch(input, out1, 6, 32, 12);
        BaxterKing.Batch(input, out2, 10, 50, 20);

        bool anyDifferent = false;
        for (int i = 50; i < input.Length; i++)
        {
            if (Math.Abs(out1[i] - out2[i]) > 1e-12)
            {
                anyDifferent = true;
                break;
            }
        }

        Assert.True(anyDifferent, "Different parameters should produce different output");
    }

    [Fact]
    public void Validate_NearZeroMean()
    {
        // BK band-pass output should have near-zero mean over a long series
        // because the weights sum to zero (DC rejection).
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 44);
        var data = gbm.Fetch(2000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();
        double[] output = new double[input.Length];

        BaxterKing.Batch(input, output, 6, 32, 12);

        // Compute mean of output after warmup
        double sum = 0;
        int count = 0;
        for (int i = 25; i < output.Length; i++)
        {
            sum += output[i];
            count++;
        }
        double mean = sum / count;

        // Mean should be near zero (DC rejection)
        Assert.True(Math.Abs(mean) < 1.0, $"Mean of BK output should be near 0, got {mean:E3}");
    }

    private static double GetAmplitude(double[] data)
    {
        int start = data.Length / 2;
        double max = double.MinValue, min = double.MaxValue;
        for (int i = start; i < data.Length; i++)
        {
            if (data[i] > max)
            {
                max = data[i];
            }
            if (data[i] < min)
            {
                min = data[i];
            }
        }
        return (max - min) / 2.0;
    }
}
