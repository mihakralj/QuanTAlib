namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for the RLS Adaptive Filter.
/// Since RLS is a custom adaptive filter with no direct external library equivalent,
/// validation uses self-consistency: adaptive convergence, streaming/span parity,
/// determinism, stability, and mathematical properties of the RLS algorithm.
/// RLS should converge faster than LMS due to the inverse correlation matrix.
/// </summary>
public class RlsValidationTests
{
    [Fact]
    public void Validate_AdaptiveConvergence_SineWave()
    {
        // RLS should learn to predict a periodic signal with decreasing error
        const int T = 500;
        double[] sine = new double[T];
        for (int i = 0; i < T; i++)
        {
            sine[i] = 100.0 + 10.0 * Math.Sin(2 * Math.PI * i / 40.0);
        }

        double[] output = new double[T];
        Rls.Batch(sine, output, 8, 0.99);

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
        Rls.Batch(input, spanOut, 8, 0.99);

        // Streaming path
        var ind = new Rls(8, 0.99);
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
        // Constant input -> filter should predict constant -> output ~ input after warmup
        double[] input = Enumerable.Repeat(50.0, 500).ToArray();
        double[] output = new double[500];

        Rls.Batch(input, output, 8, 0.99);

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

        Rls.Batch(input, out1, 8, 0.99);
        Rls.Batch(input, out2, 8, 0.99);

        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(out1[i], out2[i], 15);
        }
    }

    [Fact]
    public void Validate_OutputFollowsInput()
    {
        // RLS is an overlay filter — output should track input direction
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 77);
        var data = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();
        double[] output = new double[input.Length];

        Rls.Batch(input, output, 8, 0.99);

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
        var data = gbm.Fetch(5000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();
        double[] output = new double[input.Length];

        Rls.Batch(input, output, 8, 0.99);

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

        Rls.Batch(input, output, 4, 0.99);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output[{i}] should be finite with NaN input");
        }
    }

    [Fact]
    public void Validate_FasterConvergenceThanLMS()
    {
        // RLS should converge faster than LMS on a step function
        const int T = 200;
        double[] input = new double[T];
        for (int i = 0; i < T; i++)
        {
            input[i] = i < 50 ? 100.0 : 120.0;
        }

        double[] rlsOut = new double[T];
        double[] lmsOut = new double[T];

        Rls.Batch(input, rlsOut, 4, 0.99);
        Lms.Batch(input, lmsOut, 4, 0.5);

        // Measure error in the adaptation window (bars 55-70 after step)
        double rlsErr = 0, lmsErr = 0;
        for (int i = 55; i < 70; i++)
        {
            rlsErr += Math.Abs(rlsOut[i] - 120.0);
            lmsErr += Math.Abs(lmsOut[i] - 120.0);
        }

        Assert.True(rlsErr < lmsErr,
            $"RLS should converge faster: RLS error={rlsErr:F4}, LMS error={lmsErr:F4}");
    }

    [Fact]
    public void Validate_DifferentLambda_ProduceDifferentOutput()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 33);
        var data = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();

        double[] out1 = new double[input.Length];
        double[] out2 = new double[input.Length];

        Rls.Batch(input, out1, 8, 0.99);
        Rls.Batch(input, out2, 8, 0.95);

        bool anyDifferent = false;
        for (int i = 20; i < input.Length; i++)
        {
            if (Math.Abs(out1[i] - out2[i]) > 1e-12)
            {
                anyDifferent = true;
                break;
            }
        }

        Assert.True(anyDifferent, "Different lambda values should produce different output");
    }
}
