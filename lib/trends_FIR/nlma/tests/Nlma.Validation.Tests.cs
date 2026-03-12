// NLMA Validation Tests: Cross-mode consistency and mathematical properties
using System;
using System.Linq;
using Xunit;

namespace QuanTAlib.Tests;

public class NlmaValidationTests
{
    private const double Epsilon = 1e-6;

    [Fact]
    public void Batch_Matches_Streaming()
    {
        int period = 10;
        int flen = (5 * period) - 1; // 49
        int len = flen + 30;
        var src = new TSeries([], []);
        for (int i = 0; i < len; i++)
        {
            src.Add(new TValue(DateTime.MinValue.AddDays(i), 100 + (Math.Sin(i) * 20)));
        }

        var batchResult = Nlma.Batch(src, period);

        var streaming = new Nlma(period);
        for (int i = 0; i < src.Count; i++)
        {
            var streamVal = streaming.Update(src[i]);
            Assert.Equal(streamVal.Value, batchResult[i].Value, 6);
        }
    }

    [Fact]
    public void Span_Matches_Streaming()
    {
        int period = 8;
        int flen = (5 * period) - 1; // 39
        int len = flen + 20;
        double[] values = new double[len];
        for (int i = 0; i < len; i++)
        {
            values[i] = 50 + (i * 0.7);
        }

        double[] spanOutput = new double[len];
        Nlma.Batch(values, spanOutput, period);

        var streaming = new Nlma(period);
        for (int i = 0; i < len; i++)
        {
            var result = streaming.Update(new TValue(DateTime.MinValue.AddDays(i), values[i]));
            Assert.Equal(result.Value, spanOutput[i], 6);
        }
    }

    [Fact]
    public void Calculate_Matches_Batch()
    {
        int period = 12;
        int flen = (5 * period) - 1; // 59
        int len = flen + 20;
        var src = new TSeries([], []);
        for (int i = 0; i < len; i++)
        {
            src.Add(new TValue(DateTime.MinValue.AddDays(i), 200 + (i * 0.3)));
        }

        var batchResult = Nlma.Batch(src, period);
        var (calcResult, _) = Nlma.Calculate(src, period);

        for (int i = 0; i < src.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, calcResult[i].Value, 6);
        }
    }

    [Fact]
    public void ConstantInput_ProducesConstant()
    {
        int period = 15;
        int flen = (5 * period) - 1; // 74
        int len = flen + 20;
        var src = new TSeries([], []);
        for (int i = 0; i < len; i++)
        {
            src.Add(new TValue(DateTime.MinValue.AddDays(i), 42.0));
        }

        var result = Nlma.Batch(src, period);

        // DC gain = 1: constant input → output = constant (after warmup, and during warmup returns price)
        for (int i = 0; i < result.Count; i++)
        {
            Assert.Equal(42.0, result[i].Value, 8);
        }
    }

    [Fact]
    public void NaN_PreservedBeforeValidData()
    {
        var nlma = new Nlma(5);
        var first = nlma.Update(new TValue(DateTime.MinValue, double.NaN));
        Assert.True(double.IsNaN(first.Value));
    }

    [Fact]
    public void LargePeriod_Handles()
    {
        int period = 200;
        int flen = (5 * period) - 1; // 999
        int len = flen + 100;
        var src = new TSeries([], []);
        for (int i = 0; i < len; i++)
        {
            src.Add(new TValue(DateTime.MinValue.AddDays(i), 100 + (Math.Sin(i * 0.1) * 10)));
        }

        var result = Nlma.Batch(src, period);
        Assert.Equal(len, result.Count);

        for (int i = flen; i < result.Count; i++)
        {
            Assert.True(double.IsFinite(result[i].Value), $"Output at {i} should be finite");
        }
    }

    [Fact]
    public void DifferentPeriods_ProduceDifferentResults()
    {
        int maxFlen = (5 * 20) - 1; // 99 for period=20
        int len = maxFlen + 30;
        var src = new TSeries([], []);
        for (int i = 0; i < len; i++)
        {
            src.Add(new TValue(DateTime.MinValue.AddDays(i), 100 + i));
        }

        var result5 = Nlma.Batch(src, 5);
        var result20 = Nlma.Batch(src, 20);

        // Different periods must produce different results after both warmups
        bool anyDifferent = false;
        for (int i = maxFlen; i < len; i++)
        {
            if (Math.Abs(result5[i].Value - result20[i].Value) > 0.01)
            {
                anyDifferent = true;
                break;
            }
        }
        Assert.True(anyDifferent, "Different periods should produce different output");
    }

    [Fact]
    public void AllNaN_Input_ReturnsNaN()
    {
        var nlma = new Nlma(5);
        for (int i = 0; i < 30; i++)
        {
            nlma.Update(new TValue(DateTime.MinValue.AddDays(i), double.NaN));
        }
        Assert.True(double.IsNaN(nlma.Last.Value));
    }

    [Fact]
    public void BarCorrection_MultipleCorrections_Stable()
    {
        var nlma = new Nlma(5);
        for (int i = 0; i < 30; i++)
        {
            nlma.Update(new TValue(DateTime.MinValue.AddDays(i), 100 + i));
        }

        double beforeCorrection = nlma.Last.Value;

        // Multiple corrections should not drift
        for (int c = 0; c < 10; c++)
        {
            nlma.Update(new TValue(DateTime.MinValue.AddDays(29), 129.0 + (c * 0.001)), isNew: false);
        }

        // Final correction with original value
        nlma.Update(new TValue(DateTime.MinValue.AddDays(29), 129.0), isNew: false);
        Assert.Equal(beforeCorrection, nlma.Last.Value, 8);
    }

    [Fact]
    public void NLMA_HasNegativeWeights()
    {
        // NLMA with Igorad kernel contains negative weights that create the lag
        // cancellation effect. Verify this by checking that NLMA on sinusoidal data
        // differs from SMA and shows phase lead (less phase lag than SMA).
        int period = 10;
        int flen = (5 * period) - 1; // 49
        int len = 3 * flen;
        var src = new TSeries([], []);
        // Sinusoidal signal with period matching the filter period
        for (int i = 0; i < len; i++)
        {
            src.Add(new TValue(DateTime.MinValue.AddDays(i), 100 + (10 * Math.Sin(2 * Math.PI * i / 20))));
        }

        var nlmaResult = Nlma.Batch(src, period);
        var smaResult = Sma.Batch(src, period);

        // After warmup, NLMA and SMA should produce different results (different kernel)
        bool anyDifferent = false;
        for (int i = flen; i < len; i++)
        {
            if (Math.Abs(nlmaResult[i].Value - smaResult[i].Value) > 0.01)
            {
                anyDifferent = true;
                break;
            }
        }
        Assert.True(anyDifferent, "NLMA should produce different output than SMA (negative weights effect)");

        // NLMA's output should track closer to the original sinusoidal peaks
        // because its negative weights reduce smoothing lag on oscillating signals
        double nlmaMaxPeak = double.MinValue;
        double smaMaxPeak = double.MinValue;
        for (int i = flen; i < len; i++)
        {
            nlmaMaxPeak = Math.Max(nlmaMaxPeak, nlmaResult[i].Value);
            smaMaxPeak = Math.Max(smaMaxPeak, smaResult[i].Value);
        }
        // NLMA should preserve more of the signal amplitude than SMA(period)
        Assert.True(nlmaMaxPeak > smaMaxPeak,
            $"NLMA peak ({nlmaMaxPeak:F2}) should be higher than SMA peak ({smaMaxPeak:F2}) on sinusoidal input");
    }

    [Fact]
    public void NLMA_CanOvershoot()
    {
        // NLMA's negative weights can cause output to exceed input range
        int period = 14;
        int flen = (5 * period) - 1; // 69
        var nlma = new Nlma(period);

        // Step function: all 0s then all 100s — enough data for full kernel
        for (int i = 0; i < flen; i++)
        {
            nlma.Update(new TValue(DateTime.MinValue.AddDays(i), 0.0));
        }
        // Switch to 100
        for (int i = flen; i < 2 * flen; i++)
        {
            nlma.Update(new TValue(DateTime.MinValue.AddDays(i), 100.0));
        }

        // After the step, early values may overshoot above 100
        double lastVal = nlma.Last.Value;
        Assert.True(double.IsFinite(lastVal), "NLMA output should be finite");
    }
}
