using System;
using System.Linq;
using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for the AGC (Automatic Gain Control) filter.
/// Since AGC is a proprietary Ehlers normalizer, no external library implementations exist.
/// Validation uses self-consistency: bounded output, normalization behavior, mode consistency, and determinism.
/// </summary>
public class AgcValidationTests
{
    [Fact]
    public void Validate_SineWave_NormalizesToUnitAmplitude()
    {
        // A pure sine wave (amplitude=1) should normalize to ~1 peak after warmup
        const int T = 1000;
        double[] sine = new double[T];
        for (int i = 0; i < T; i++)
        {
            sine[i] = Math.Sin(2.0 * Math.PI * i / 20.0);
        }

        double[] output = new double[T];
        Agc.Batch(sine, output, 0.991);

        // After warmup, output peaks should be close to ±1
        double maxAbs = 0;
        for (int i = T - 100; i < T; i++)
        {
            maxAbs = Math.Max(maxAbs, Math.Abs(output[i]));
        }
        Assert.True(maxAbs >= 0.95 && maxAbs <= 1.0001,
            $"Normalized sine should peak near ±1, got max |output| = {maxAbs}");
    }

    [Fact]
    public void Validate_GrowingAmplitude_TracksWithinBounds()
    {
        // Sine wave with growing amplitude — AGC should keep output bounded
        const int T = 1000;
        double[] input = new double[T];
        for (int i = 0; i < T; i++)
        {
            double amplitude = 1.0 + i * 0.01; // grows from 1 to 11
            input[i] = amplitude * Math.Sin(2.0 * Math.PI * i / 20.0);
        }

        double[] output = new double[T];
        Agc.Batch(input, output, 0.991);

        for (int i = 0; i < T; i++)
        {
            Assert.True(output[i] >= -1.0001 && output[i] <= 1.0001,
                $"Output[{i}] = {output[i]} exceeds [-1, +1] bounds");
        }
    }

    [Fact]
    public void Validate_StreamingMatchesSpan()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var data = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Use roofing to create oscillating input
        double[] prices = data.Close.Values.ToArray();
        double[] filtered = new double[prices.Length];
        Roofing.Batch(prices, filtered, 48, 10);

        // Span mode
        double[] spanOut = new double[filtered.Length];
        Agc.Batch(filtered, spanOut, 0.991);

        // Streaming mode
        var ind = new Agc(0.991);
        double[] streamOut = new double[filtered.Length];
        for (int i = 0; i < filtered.Length; i++)
        {
            streamOut[i] = ind.Update(new TValue(DateTime.UtcNow, filtered[i])).Value;
        }

        for (int i = 0; i < filtered.Length; i++)
        {
            Assert.Equal(spanOut[i], streamOut[i], 1e-9);
        }
    }

    [Fact]
    public void Validate_Deterministic()
    {
        double[] input = new double[500];
        for (int i = 0; i < input.Length; i++)
        {
            input[i] = Math.Sin(2.0 * Math.PI * i / 25.0) * (1.0 + 0.3 * Math.Sin(2.0 * Math.PI * i / 100.0));
        }

        double[] out1 = new double[input.Length];
        double[] out2 = new double[input.Length];

        Agc.Batch(input, out1, 0.991);
        Agc.Batch(input, out2, 0.991);

        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(out1[i], out2[i], 15);
        }
    }

    [Fact]
    public void Validate_DecayingAmplitude_OutputGrows()
    {
        // When amplitude decays, AGC peak decays too, so normalized output stays near ±1
        const int T = 1000;
        double[] input = new double[T];
        for (int i = 0; i < T; i++)
        {
            double amplitude = 10.0 * Math.Exp(-i * 0.005); // exponentially decaying
            input[i] = amplitude * Math.Sin(2.0 * Math.PI * i / 20.0);
        }

        double[] output = new double[T];
        Agc.Batch(input, output, 0.991);

        // Output should still oscillate near ±1 in the tail (AGC adapts)
        double maxTail = 0;
        for (int i = T - 100; i < T; i++)
        {
            maxTail = Math.Max(maxTail, Math.Abs(output[i]));
        }
        Assert.True(maxTail > 0.5, $"Decaying amplitude should still produce sizable normalized output, got max = {maxTail}");
    }

    [Fact]
    public void Validate_LargeDataset_Stable()
    {
        double[] input = new double[10000];
        for (int i = 0; i < input.Length; i++)
        {
            input[i] = Math.Sin(2.0 * Math.PI * i / 20.0);
        }
        double[] output = new double[input.Length];

        Agc.Batch(input, output, 0.991);

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
            input[i] = i % 7 == 0 ? double.NaN : Math.Sin(2.0 * Math.PI * i / 20.0);
        }
        double[] output = new double[100];

        Agc.Batch(input, output, 0.991);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output[{i}] should be finite with NaN input");
        }
    }

    [Fact]
    public void Validate_DifferentDecays_ProduceDifferentOutput()
    {
        double[] input = new double[500];
        for (int i = 0; i < input.Length; i++)
        {
            input[i] = Math.Sin(2.0 * Math.PI * i / 20.0);
        }

        double[] out1 = new double[input.Length];
        double[] out2 = new double[input.Length];

        Agc.Batch(input, out1, 0.991);
        Agc.Batch(input, out2, 0.95);

        bool anyDifferent = false;
        for (int i = 50; i < input.Length; i++)
        {
            if (Math.Abs(out1[i] - out2[i]) > 1e-10)
            {
                anyDifferent = true;
                break;
            }
        }
        Assert.True(anyDifferent, "Different decay parameters should produce different output");
    }
}
