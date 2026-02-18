namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for the LMS Adaptive Filter.
/// Since LMS is a custom adaptive filter with no direct external library equivalent,
/// validation uses self-consistency: adaptive convergence, streaming/span parity,
/// determinism, stability, and mathematical properties of the NLMS algorithm.
/// </summary>
public class LmsValidationTests
{
    [Fact]
    public void Validate_AdaptiveConvergence_SineWave()
    {
        // LMS should learn to predict a periodic signal with decreasing error
        const int T = 500;
        double[] sine = new double[T];
        for (int i = 0; i < T; i++)
        {
            sine[i] = 100.0 + 10.0 * Math.Sin(2 * Math.PI * i / 40.0);
        }

        double[] output = new double[T];
        Lms.Batch(sine, output, 8, 0.5);

        // Compute mean squared error in first quarter vs last quarter
        double mseFirst = 0, mseLast = 0;
        int q = T / 4;
        for (int i = 0; i < q; i++)
        {
            double e = sine[i] - output[i];
            mseFirst += e * e;
        }
        for (int i = T - q; i < T; i++)
        {
            double e = sine[i] - output[i];
            mseLast += e * e;
        }
        mseFirst /= q;
        mseLast /= q;

        Assert.True(mseLast < mseFirst, $"Error should decrease: first quarter MSE={mseFirst:F4}, last quarter MSE={mseLast:F4}");
    }

    [Fact]
    public void Validate_StreamingMatchesSpan()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var data = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();

        // Span path
        double[] spanOut = new double[input.Length];
        Lms.Batch(input, spanOut, 8, 0.5);

        // Streaming path
        var ind = new Lms(8, 0.5);
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
    public void Validate_ConstantInput_ConvergesToConstant()
    {
        // Constant input → filter should predict constant → output ≈ input after warmup
        double[] input = Enumerable.Repeat(50.0, 500).ToArray();
        double[] output = new double[500];

        Lms.Batch(input, output, 8, 0.5);

        // After warmup, output should converge close to input
        Assert.True(Math.Abs(output[^1] - 50.0) < 1.0,
            $"Constant input should yield ~50, got {output[^1]}");
    }

    [Fact]
    public void Validate_Deterministic()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 99);
        var data = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();

        double[] out1 = new double[input.Length];
        double[] out2 = new double[input.Length];

        Lms.Batch(input, out1, 8, 0.5);
        Lms.Batch(input, out2, 8, 0.5);

        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(out1[i], out2[i], 15);
        }
    }

    [Fact]
    public void Validate_OutputFollowsInput()
    {
        // LMS is an overlay filter — output should track input direction
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 77);
        var data = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();
        double[] output = new double[input.Length];

        Lms.Batch(input, output, 8, 0.5);

        // Correlation between input and output should be positive and high
        double meanIn = 0, meanOut = 0;
        int start = 50; // skip warmup
        int n = input.Length - start;
        for (int i = start; i < input.Length; i++)
        {
            meanIn += input[i];
            meanOut += output[i];
        }
        meanIn /= n;
        meanOut /= n;

        double cov = 0, varIn = 0, varOut = 0;
        for (int i = start; i < input.Length; i++)
        {
            double dIn = input[i] - meanIn;
            double dOut = output[i] - meanOut;
            cov += dIn * dOut;
            varIn += dIn * dIn;
            varOut += dOut * dOut;
        }

        double corr = cov / Math.Sqrt(varIn * varOut);
        Assert.True(corr > 0.5, $"Output should track input, correlation = {corr:F4}");
    }

    [Fact]
    public void Validate_LargeDataset_Stable()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 55);
        var data = gbm.Fetch(10000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();
        double[] output = new double[input.Length];

        Lms.Batch(input, output, 16, 0.5);

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

        Lms.Batch(input, output, 4, 0.5);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output[{i}] should be finite with NaN input");
        }
    }

    [Fact]
    public void Validate_DifferentOrders_ProduceDifferentOutput()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 33);
        var data = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();

        double[] out1 = new double[input.Length];
        double[] out2 = new double[input.Length];

        Lms.Batch(input, out1, 4, 0.5);
        Lms.Batch(input, out2, 16, 0.5);

        bool anyDifferent = false;
        for (int i = 20; i < input.Length; i++)
        {
            if (Math.Abs(out1[i] - out2[i]) > 1e-12)
            {
                anyDifferent = true;
                break;
            }
        }

        Assert.True(anyDifferent, "Different orders should produce different output");
    }

    [Fact]
    public void Validate_HigherMu_FasterAdaptation()
    {
        // Higher mu → faster adaptation → lower initial error (but potentially noisier)
        const int T = 200;
        double[] input = new double[T];
        // Step function: sudden level shift
        for (int i = 0; i < T; i++)
        {
            input[i] = i < 50 ? 100.0 : 120.0;
        }

        double[] outLow = new double[T];
        double[] outHigh = new double[T];

        Lms.Batch(input, outLow, 4, 0.1);
        Lms.Batch(input, outHigh, 4, 1.0);

        // After step (bars 60-80), high-mu should be closer to 120 than low-mu
        double errLow = 0, errHigh = 0;
        for (int i = 60; i < 80; i++)
        {
            errLow += Math.Abs(outLow[i] - 120.0);
            errHigh += Math.Abs(outHigh[i] - 120.0);
        }

        Assert.True(errHigh < errLow, $"Higher mu should adapt faster: errHigh={errHigh:F4}, errLow={errLow:F4}");
    }
}
