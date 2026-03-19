namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for the Ehlers Truncated Bandpass Filter.
/// Since TBF is a proprietary Ehlers indicator, no external library implementations exist.
/// Validation uses self-consistency: bandpass behavior, DC rejection, truncation properties,
/// mode consistency, and determinism.
/// </summary>
public class TbfValidationTests
{
    [Fact]
    public void Validate_BandpassBehavior_InBandPassesOutBandAttenuated()
    {
        // TBF with period=20, bandwidth=0.3 should pass ~20-bar cycles and attenuate others
        const int T = 1000;
        double[] sine5 = new double[T];    // Period 5: too fast, should be attenuated
        double[] sine20 = new double[T];   // Period 20: in-band, should pass
        double[] sine200 = new double[T];  // Period 200: too slow, should be attenuated

        for (int i = 0; i < T; i++)
        {
            sine5[i] = Math.Sin(2.0 * Math.PI * i / 5.0);
            sine20[i] = Math.Sin(2.0 * Math.PI * i / 20.0);
            sine200[i] = Math.Sin(2.0 * Math.PI * i / 200.0);
        }

        double[] out5 = new double[T];
        double[] out20 = new double[T];
        double[] out200 = new double[T];
        double[] bp5 = new double[T];
        double[] bp20 = new double[T];
        double[] bp200 = new double[T];

        Tbf.Batch(sine5, out5, bp5, 20, 0.3, 25);
        Tbf.Batch(sine20, out20, bp20, 20, 0.3, 25);
        Tbf.Batch(sine200, out200, bp200, 20, 0.3, 25);

        double amp5 = GetAmplitude(out5);
        double amp20 = GetAmplitude(out20);
        double amp200 = GetAmplitude(out200);

        // In-band signal should have larger amplitude than out-of-band
        Assert.True(amp20 > amp200, $"In-band (P=20, amp={amp20:E3}) should exceed trend (P=200, amp={amp200:E3})");
        Assert.True(amp20 > amp5, $"In-band (P=20, amp={amp20:E3}) should exceed noise (P=5, amp={amp5:E3})");
    }

    [Fact]
    public void Validate_StreamingMatchesSpan()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var data = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();

        // Span path
        double[] spanTbf = new double[input.Length];
        double[] spanBp = new double[input.Length];
        Tbf.Batch(input, spanTbf, spanBp, 20, 0.1, 10);

        // Streaming path
        var ind = new Tbf(20, 0.1, 10);
        double[] streamTbf = new double[input.Length];
        double[] streamBp = new double[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, input[i]));
            streamTbf[i] = ind.Last.Value;
            streamBp[i] = ind.Bp.Value;
        }

        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(spanTbf[i], streamTbf[i], 1e-9);
            Assert.Equal(spanBp[i], streamBp[i], 1e-9);
        }
    }

    [Fact]
    public void Validate_ConstantInput_OutputZero()
    {
        double[] input = Enumerable.Repeat(50.0, 1000).ToArray();
        double[] tbfOut = new double[1000];
        double[] bpOut = new double[1000];

        Tbf.Batch(input, tbfOut, bpOut, 20, 0.1, 10);

        // Bandpass on constant → zero (DC rejection)
        Assert.True(Math.Abs(tbfOut[^1]) < 1e-10, $"Expected TBF 0 for constant, got {tbfOut[^1]}");
        Assert.True(Math.Abs(bpOut[^1]) < 1e-10, $"Expected BP 0 for constant, got {bpOut[^1]}");
    }

    [Fact]
    public void Validate_Deterministic()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 99);
        var data = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();

        double[] out1 = new double[input.Length];
        double[] out2 = new double[input.Length];
        double[] bp1 = new double[input.Length];
        double[] bp2 = new double[input.Length];

        Tbf.Batch(input, out1, bp1, 20, 0.1, 10);
        Tbf.Batch(input, out2, bp2, 20, 0.1, 10);

        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(out1[i], out2[i], 15);
            Assert.Equal(bp1[i], bp2[i], 15);
        }
    }

    [Fact]
    public void Validate_OutputOscillatesAroundZero()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 77);
        var data = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();
        double[] output = new double[input.Length];
        double[] bp = new double[input.Length];

        Tbf.Batch(input, output, bp, 20, 0.1, 10);

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

        Assert.True(hasPositive, "TBF output should have positive values");
        Assert.True(hasNegative, "TBF output should have negative values");
    }

    [Fact]
    public void Validate_LargeDataset_Stable()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 55);
        var data = gbm.Fetch(10000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();
        double[] tbfOut = new double[input.Length];
        double[] bpOut = new double[input.Length];

        Tbf.Batch(input, tbfOut, bpOut, 20, 0.1, 10);

        for (int i = 0; i < tbfOut.Length; i++)
        {
            Assert.True(double.IsFinite(tbfOut[i]), $"TBF[{i}] is not finite: {tbfOut[i]}");
            Assert.True(double.IsFinite(bpOut[i]), $"BP[{i}] is not finite: {bpOut[i]}");
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
        double[] tbfOut = new double[100];
        double[] bpOut = new double[100];

        Tbf.Batch(input, tbfOut, bpOut, 20, 0.1, 10);

        for (int i = 0; i < tbfOut.Length; i++)
        {
            Assert.True(double.IsFinite(tbfOut[i]), $"TBF[{i}] should be finite with NaN input");
            Assert.True(double.IsFinite(bpOut[i]), $"BP[{i}] should be finite with NaN input");
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
        double[] bp1 = new double[input.Length];
        double[] bp2 = new double[input.Length];

        Tbf.Batch(input, out1, bp1, 20, 0.1, 10);
        Tbf.Batch(input, out2, bp2, 40, 0.2, 15);

        bool anyDifferent = false;
        for (int i = 20; i < input.Length; i++)
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
    public void Validate_TruncatedHasSmallerTransientThanStandard()
    {
        // After a price shock, the truncated filter should recover faster
        // because it has finite memory (limited to Length bars)
        const int total = 200;
        double[] input = new double[total];

        // Flat → shock → flat pattern
        for (int i = 0; i < total; i++)
        {
            input[i] = (i == 50) ? 200.0 : 100.0;
        }

        double[] tbfOut = new double[total];
        double[] bpOut = new double[total];
        Tbf.Batch(input, tbfOut, bpOut, 20, 0.1, 10);

        // After Length+2 bars past the shock (i > 62), TBF should be back to ~0
        // but standard BP still has residual transient
        double tbfPostShock = 0;
        double bpPostShock = 0;
        int startCheck = 50 + 10 + 5; // shock bar + length + margin
        for (int i = startCheck; i < Math.Min(startCheck + 20, total); i++)
        {
            tbfPostShock += Math.Abs(tbfOut[i]);
            bpPostShock += Math.Abs(bpOut[i]);
        }

        // TBF should have less accumulated transient energy after truncation window passes
        Assert.True(tbfPostShock <= bpPostShock + 1e-6,
            $"Truncated ({tbfPostShock:E3}) should have ≤ transient energy than standard ({bpPostShock:E3})");
    }

    [Fact]
    public void Validate_LongerTruncation_ApproachesStandardBP()
    {
        // As length increases, the truncated version should approach the standard BP
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 44);
        var data = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();

        double[] tbfShort = new double[input.Length];
        double[] tbfLong = new double[input.Length];
        double[] bpShort = new double[input.Length];
        double[] bpLong = new double[input.Length];

        Tbf.Batch(input, tbfShort, bpShort, 20, 0.1, 5);
        Tbf.Batch(input, tbfLong, bpLong, 20, 0.1, 50);

        // The longer truncation should be closer to its matching standard BP
        double diffShort = 0, diffLong = 0;
        int start = 60; // after both warmup
        for (int i = start; i < input.Length; i++)
        {
            diffShort += Math.Abs(tbfShort[i] - bpShort[i]);
            diffLong += Math.Abs(tbfLong[i] - bpLong[i]);
        }

        Assert.True(diffLong < diffShort,
            $"Longer truncation ({diffLong:E3}) should be closer to standard BP than shorter ({diffShort:E3})");
    }

    [Fact]
    public void Validate_EmptySpan_NoOp()
    {
        double[] empty = Array.Empty<double>();
        double[] tbfOut = Array.Empty<double>();
        double[] bpOut = Array.Empty<double>();

        // Should not throw and produce no output
        Tbf.Batch(empty, tbfOut, bpOut, 20, 0.1, 10);
        Assert.Empty(tbfOut);
    }

    [Fact]
    public void Validate_ResetAndReplay_Identical()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 66);
        var data = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();

        var ind = new Tbf(20, 0.1, 10);
        double[] run1 = new double[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            run1[i] = ind.Update(new TValue(DateTime.UtcNow, input[i])).Value;
        }

        ind.Reset();
        double[] run2 = new double[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            run2[i] = ind.Update(new TValue(DateTime.UtcNow, input[i])).Value;
        }

        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(run1[i], run2[i], 12);
        }
    }

    [Fact]
    public void Validate_StandardBP_MatchesDirect2PoleRecurrence()
    {
        // Verify the standard BP output matches a direct implementation
        // of Ehlers 2-pole bandpass: BP = a0*(Close - Close[2]) + a1*BP[1] - s1*BP[2]
        int period = 20;
        double bandwidth = 0.1;
        int length = 10;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 11);
        var data = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();

        // Compute using TBF class (get BP output)
        double[] tbfOut = new double[input.Length];
        double[] bpOut = new double[input.Length];
        Tbf.Batch(input, tbfOut, bpOut, period, bandwidth, length);

        // Compute direct 2-pole bandpass
        double twoPi = 2.0 * Math.PI;
        double l1 = Math.Cos(twoPi / period);
        double g1 = Math.Cos(bandwidth * twoPi / period);
        double s1 = (1.0 / g1) - Math.Sqrt((1.0 / (g1 * g1)) - 1.0);
        double a0 = 0.5 * (1.0 - s1);
        double a1 = l1 * (1.0 + s1);

        double[] directBp = new double[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            if (i <= 2)
            {
                directBp[i] = 0.0;
            }
            else
            {
                directBp[i] = a0 * (input[i] - input[i - 2])
                    + a1 * directBp[i - 1]
                    - s1 * directBp[i - 2];
            }
        }

        // The standard BP from TBF should match the direct computation
        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(directBp[i], bpOut[i], 1e-9);
        }
    }

    [Theory]
    [InlineData(10, 0.1, 5)]
    [InlineData(20, 0.1, 10)]
    [InlineData(30, 0.2, 15)]
    [InlineData(50, 0.3, 25)]
    public void Validate_AllParameterCombos_ProduceFiniteOutput(int period, double bandwidth, int length)
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 22);
        var data = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();

        double[] tbfOut = new double[input.Length];
        double[] bpOut = new double[input.Length];
        Tbf.Batch(input, tbfOut, bpOut, period, bandwidth, length);

        for (int i = 0; i < input.Length; i++)
        {
            Assert.True(double.IsFinite(tbfOut[i]), $"TBF[{i}] not finite for p={period},bw={bandwidth},len={length}");
        }
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
