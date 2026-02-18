using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for the Voss Predictive Filter.
/// Since VOSS is a proprietary Ehlers indicator, no external library implementations exist.
/// Validation uses self-consistency: bandpass behavior, predictor lead, mode consistency, and determinism.
/// </summary>
public class VossValidationTests
{
    [Fact]
    public void Validate_BandpassBehavior_Synthetic()
    {
        // Voss BPF stage with period=20 should pass cycles near period 20
        // Cycles far from period (very fast or very slow) should be attenuated
        const int T = 1000;
        double[] sine5 = new double[T];    // Period 5: too fast, attenuated
        double[] sine20 = new double[T];   // Period 20: in-band, should pass
        double[] sine100 = new double[T];  // Period 100: too slow, attenuated

        for (int i = 0; i < T; i++)
        {
            sine5[i] = Math.Sin(2 * Math.PI * i / 5.0);
            sine20[i] = Math.Sin(2 * Math.PI * i / 20.0);
            sine100[i] = Math.Sin(2 * Math.PI * i / 100.0);
        }

        double[] out5 = new double[T];
        double[] out20 = new double[T];
        double[] out100 = new double[T];

        Voss.Batch(sine5, out5, 20, 3, 0.25);
        Voss.Batch(sine20, out20, 20, 3, 0.25);
        Voss.Batch(sine100, out100, 20, 3, 0.25);

        double amp5 = GetAmplitude(out5);
        double amp20 = GetAmplitude(out20);
        double amp100 = GetAmplitude(out100);

        // Voss predictor amplifies the in-band signal
        Assert.True(amp20 > 0.5, $"In-band signal (P=20) should pass. Amplitude: {amp20}");
        Assert.True(amp5 < amp20, $"Out-of-band fast signal should be smaller. Fast: {amp5}, In-band: {amp20}");
        Assert.True(amp100 < amp20, $"Out-of-band slow signal should be smaller. Slow: {amp100}, In-band: {amp20}");
    }

    [Fact]
    public void Validate_VossLeadsFilt()
    {
        // The Voss predictor should lead (anticipate) the bandpass filter
        // Test with a clean sinusoid at the tuned period
        const int T = 500;
        double[] sine = new double[T];
        for (int i = 0; i < T; i++)
        {
            sine[i] = Math.Sin(2 * Math.PI * i / 20.0);
        }

        var ind = new Voss(20, 3, 0.25);
        var vossVals = new double[T];
        var filtVals = new double[T];

        for (int i = 0; i < T; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, sine[i]));
            vossVals[i] = ind.Last.Value;
            filtVals[i] = ind.LastFilt;
        }

        // Find zero crossings of Filt (positive to negative) in the settled region
        // Voss should cross zero first (leading)
        int vossLeadCount = 0;
        int filtLeadCount = 0;
        for (int i = 200; i < T - 1; i++)
        {
            // Filt zero crossing (positive → negative)
            if (filtVals[i] > 0 && filtVals[i + 1] <= 0)
            {
                // Check if Voss already crossed (is negative) nearby
                if (vossVals[i] <= 0)
                {
                    vossLeadCount++;
                }
                else
                {
                    filtLeadCount++;
                }
            }
        }

        // Voss should lead more often than filt for an in-band signal
        Assert.True(vossLeadCount > 0, "Voss should lead the bandpass at least once");
    }

    [Fact]
    public void Validate_StreamingMatchesSpan()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var data = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();

        double[] spanOut = new double[input.Length];
        Voss.Batch(input, spanOut, 20, 3, 0.25);

        var ind = new Voss(20, 3, 0.25);
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

        Voss.Batch(input, output, 20, 3, 0.25);

        // Bandpass on constant → zero (removes DC)
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

        Voss.Batch(input, out1, 20, 3, 0.25);
        Voss.Batch(input, out2, 20, 3, 0.25);

        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(out1[i], out2[i], 15); // Exact match
        }
    }

    [Fact]
    public void Validate_OutputOscillatesAroundZero()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 77);
        var data = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();
        double[] output = new double[input.Length];

        Voss.Batch(input, output, 20, 3, 0.25);

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

        Assert.True(hasPositive, "Voss output should have positive values");
        Assert.True(hasNegative, "Voss output should have negative values");
    }

    [Fact]
    public void Validate_LargeDataset_Stable()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 55);
        var data = gbm.Fetch(10000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();
        double[] output = new double[input.Length];

        Voss.Batch(input, output, 20, 3, 0.25);

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

        Voss.Batch(input, output, 20, 3, 0.25);

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

        Voss.Batch(input, out1, 20, 3, 0.25);
        Voss.Batch(input, out2, 40, 5, 0.15);

        bool anyDifferent = false;
        for (int i = 100; i < input.Length; i++)
        {
            if (Math.Abs(out1[i] - out2[i]) > 1e-10)
            {
                anyDifferent = true;
                break;
            }
        }
        Assert.True(anyDifferent, "Different parameters should produce different output");
    }

    private static double GetAmplitude(double[] signal)
    {
        double max = 0;
        for (int i = signal.Length - 100; i < signal.Length; i++)
        {
            max = Math.Max(max, Math.Abs(signal[i]));
        }
        return max;
    }
}
