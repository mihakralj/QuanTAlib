namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for the Super Passband Filter.
/// Since SPBF is a proprietary Ehlers indicator, no external library implementations exist.
/// Validation uses self-consistency: bandpass behavior, DC rejection, mode consistency, and determinism.
/// </summary>
public class SpbfValidationTests
{
    [Fact]
    public void Validate_BandpassBehavior_Synthetic()
    {
        // SPBF with shortPeriod=20, longPeriod=80 should pass cycles between ~20 and ~80 bars
        // Ehlers alpha = 5/N, so shorter period = faster EMA, longer = slower EMA
        const int T = 1000;
        double[] sine10 = new double[T];   // Period 10: too fast, should be attenuated
        double[] sine40 = new double[T];   // Period 40: in-band, should pass
        double[] sine200 = new double[T];  // Period 200: too slow (trend), should be attenuated

        for (int i = 0; i < T; i++)
        {
            sine10[i] = Math.Sin(2 * Math.PI * i / 10.0);
            sine40[i] = Math.Sin(2 * Math.PI * i / 40.0);
            sine200[i] = Math.Sin(2 * Math.PI * i / 200.0);
        }

        double[] out10 = new double[T];
        double[] out40 = new double[T];
        double[] out200 = new double[T];

        Spbf.Batch(sine10, out10, 20, 80, 50);
        Spbf.Batch(sine40, out40, 20, 80, 50);
        Spbf.Batch(sine200, out200, 20, 80, 50);

        double amp10 = GetAmplitude(out10);
        double amp40 = GetAmplitude(out40);
        double amp200 = GetAmplitude(out200);

        // In-band signal should have larger amplitude than out-of-band
        Assert.True(amp40 > amp200, $"In-band (P=40, amp={amp40}) should exceed trend (P=200, amp={amp200})");
        Assert.True(amp40 > amp10, $"In-band (P=40, amp={amp40}) should exceed noise (P=10, amp={amp10})");
    }

    [Fact]
    public void Validate_StreamingMatchesSpan()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var data = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();

        // Span path
        double[] spanOut = new double[input.Length];
        Spbf.Batch(input, spanOut, 40, 60, 50);

        // Streaming path
        var ind = new Spbf(40, 60, 50);
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
        double[] input = Enumerable.Repeat(50.0, 1000).ToArray();
        double[] output = new double[1000];

        Spbf.Batch(input, output, 40, 60, 50);

        // Bandpass on constant → zero (DC rejection)
        Assert.True(Math.Abs(output[^1]) < 1e-10, $"Expected 0 for constant, got {output[^1]}");
    }

    [Fact]
    public void Validate_Deterministic()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 99);
        var data = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();

        double[] out1 = new double[input.Length];
        double[] out2 = new double[input.Length];

        Spbf.Batch(input, out1, 40, 60, 50);
        Spbf.Batch(input, out2, 40, 60, 50);

        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(out1[i], out2[i], 15);
        }
    }

    [Fact]
    public void Validate_OutputOscillatesAroundZero()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 77);
        var data = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();
        double[] output = new double[input.Length];

        Spbf.Batch(input, output, 40, 60, 50);

        bool hasPositive = false, hasNegative = false;
        for (int i = 100; i < output.Length; i++)
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

        Spbf.Batch(input, output, 40, 60, 50);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output[{i}] is not finite: {output[i]}");
        }
    }

    [Fact]
    public void Validate_NaN_Batch_Safe()
    {
        double[] input = new double[100];
        for (int i = 0; i < 100; i++)
        {
            input[i] = i % 7 == 0 ? double.NaN : 100.0 + Math.Sin(i * 0.1);
        }
        double[] output = new double[100];

        Spbf.Batch(input, output, 20, 30, 10);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output[{i}] should be finite with NaN input");
        }
    }

    [Fact]
    public void Validate_DifferentPeriods_ProduceDifferentOutput()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 33);
        var data = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();

        double[] out1 = new double[input.Length];
        double[] out2 = new double[input.Length];

        Spbf.Batch(input, out1, 40, 60, 50);
        Spbf.Batch(input, out2, 20, 80, 30);

        bool anyDifferent = false;
        for (int i = 10; i < input.Length; i++)
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
    public void Validate_RmsEnvelope_BoundsPassband()
    {
        // After warmup, RMS should approximate the amplitude envelope of the passband
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 88);
        var data = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();

        double[] pb = new double[input.Length];
        double[] rms = new double[input.Length];

        Spbf.BatchWithRms(input, pb, rms, 40, 60, 50);

        // After warmup, most passband values should be within ±2*RMS
        int inBound = 0, total = 0;
        for (int i = 100; i < input.Length; i++)
        {
            total++;
            if (Math.Abs(pb[i]) <= 2.0 * rms[i])
            {
                inBound++;
            }
        }

        double ratio = (double)inBound / total;
        Assert.True(ratio > 0.80, $"Expected >80% of PB within ±2*RMS, got {ratio:P1}");
    }

    private static double GetAmplitude(double[] data)
    {
        // Measure peak-to-peak amplitude in last half (after warmup)
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
