namespace QuanTAlib;

public class WaveletTests
{
    private readonly GBM _gbm;

    public WaveletTests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
    }

    // --- A) Constructor Validation ---

    [Fact]
    public void Constructor_ValidatesLevels_TooSmall()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Wavelet(levels: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Wavelet(levels: -1));
    }

    [Fact]
    public void Constructor_ValidatesLevels_TooLarge()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Wavelet(levels: 9));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Wavelet(levels: 100));
    }

    [Fact]
    public void Constructor_ValidatesThreshMult_Negative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Wavelet(levels: 4, threshMult: -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Wavelet(levels: 4, threshMult: -1.0));
    }

    [Fact]
    public void Constructor_AcceptsZeroThreshMult()
    {
        var ind = new Wavelet(levels: 4, threshMult: 0.0);
        Assert.Equal(0.0, ind.ThreshMult);
    }

    [Fact]
    public void Constructor_AcceptsEdgeLevels()
    {
        var ind1 = new Wavelet(levels: 1);
        Assert.Equal(1, ind1.Levels);

        var ind8 = new Wavelet(levels: 8);
        Assert.Equal(8, ind8.Levels);
    }

    [Fact]
    public void Constructor_SetsName()
    {
        var ind = new Wavelet(4, 1.0);
        Assert.Equal("Wavelet(4,1.0)", ind.Name);
    }

    [Fact]
    public void Constructor_SetsWarmupPeriod()
    {
        var ind = new Wavelet(4, 1.0);
        Assert.Equal(16, ind.WarmupPeriod); // 2^4
    }

    [Fact]
    public void Constructor_DefaultParameters()
    {
        var ind = new Wavelet();
        Assert.Equal(4, ind.Levels);
        Assert.Equal(1.0, ind.ThreshMult);
    }

    [Fact]
    public void Constructor_ExposesProperties()
    {
        var ind = new Wavelet(3, 2.5);
        Assert.Equal(3, ind.Levels);
        Assert.Equal(2.5, ind.ThreshMult, 1e-15);
    }

    // --- B) Basic Calculation ---

    [Fact]
    public void Calc_ReturnsValue()
    {
        var ind = new Wavelet(4, 1.0);
        var result = ind.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Calc_LastUpdated()
    {
        var ind = new Wavelet(4, 1.0);
        var tv = new TValue(DateTime.UtcNow, 100);
        ind.Update(tv);
        Assert.Equal(ind.Last.Value, tv.Value);
    }

    [Fact]
    public void Calc_IsHot_Eventually()
    {
        var ind = new Wavelet(2, 1.0); // warmup = 4 bars
        for (int i = 0; i < 10; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        Assert.True(ind.IsHot);
    }

    [Fact]
    public void Calc_Name_Accessible()
    {
        var ind = new Wavelet(3, 0.5);
        Assert.Contains("Wavelet", ind.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Calc_KnownValue_ConstantInput()
    {
        // Constant input should pass through with minimal distortion
        var ind = new Wavelet(2, 1.0);
        double constant = 50.0;
        for (int i = 0; i < 20; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, constant));
        }
        // All detail coefficients should be zero for constant input
        Assert.Equal(constant, ind.Last.Value, 1e-10);
    }

    // --- C) State + Bar Correction (critical) ---

    [Fact]
    public void State_IsNew_True_Advances()
    {
        var ind = new Wavelet(2, 1.0);
        ind.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        ind.Update(new TValue(DateTime.UtcNow, 101), isNew: true);
        // Two new bars should produce a finite denoised value (wavelet smooths, not passthrough)
        Assert.True(double.IsFinite(ind.Last.Value), "Second bar should produce finite result");
    }

    [Fact]
    public void State_IsNew_False_Rewrites()
    {
        var ind = new Wavelet(2, 1.0);
        ind.Update(new TValue(DateTime.UtcNow, 100), isNew: true);

        ind.Update(new TValue(DateTime.UtcNow, 105), isNew: true);
        ind.Update(new TValue(DateTime.UtcNow, 103), isNew: false);
        double afterCorrection = ind.Last.Value;

        ind.Update(new TValue(DateTime.UtcNow, 103), isNew: false);
        double afterSecondCorrection = ind.Last.Value;

        // Multiple corrections with same value should be idempotent
        Assert.Equal(afterCorrection, afterSecondCorrection, 1e-15);
    }

    [Fact]
    public void State_IterativeCorrections_Restore()
    {
        var ind = new Wavelet(2, 1.0);

        // Feed some bars
        for (int i = 0; i < 10; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, 100 + i), isNew: true);
        }

        // Correct last bar multiple times
        ind.Update(new TValue(DateTime.UtcNow, 200), isNew: false);
        double v1 = ind.Last.Value;
        ind.Update(new TValue(DateTime.UtcNow, 200), isNew: false);
        double v2 = ind.Last.Value;

        Assert.Equal(v1, v2, 1e-15);
    }

    [Fact]
    public void State_Reset_ClearsState()
    {
        var ind = new Wavelet(2, 1.0);

        for (int i = 0; i < 20; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        Assert.True(ind.IsHot);

        ind.Reset();
        Assert.False(ind.IsHot);
        Assert.Equal(default, ind.Last);
    }

    // --- D) Warmup / Convergence ---

    [Fact]
    public void Warmup_IsHot_FlipsWhenBufferFull()
    {
        var ind = new Wavelet(2, 1.0); // madLen = 4
        Assert.False(ind.IsHot);

        for (int i = 0; i < 3; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, 100 + i));
            Assert.False(ind.IsHot, $"Should not be hot after {i + 1} bars");
        }

        ind.Update(new TValue(DateTime.UtcNow, 103));
        Assert.True(ind.IsHot, "Should be hot after 4 bars (2^2)");
    }

    [Fact]
    public void Warmup_WarmupPeriod_DependsOnLevels()
    {
        Assert.Equal(2, new Wavelet(1, 1.0).WarmupPeriod);   // 2^1
        Assert.Equal(4, new Wavelet(2, 1.0).WarmupPeriod);   // 2^2
        Assert.Equal(8, new Wavelet(3, 1.0).WarmupPeriod);   // 2^3
        Assert.Equal(16, new Wavelet(4, 1.0).WarmupPeriod);  // 2^4
        Assert.Equal(32, new Wavelet(5, 1.0).WarmupPeriod);  // 2^5
    }

    // --- E) Robustness (critical) ---

    [Fact]
    public void Robust_NaN_UsesLastValid()
    {
        var ind = new Wavelet(2, 1.0);

        ind.Update(new TValue(DateTime.UtcNow, 100));
        ind.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(ind.Last.Value));
    }

    [Fact]
    public void Robust_Infinity_UsesLastValid()
    {
        var ind = new Wavelet(2, 1.0);

        ind.Update(new TValue(DateTime.UtcNow, 100));
        ind.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));

        Assert.True(double.IsFinite(ind.Last.Value));
    }

    [Fact]
    public void Robust_BatchNaN_Safe()
    {
        double[] input = [100, 101, double.NaN, 103, 104, double.NaN, 106, 107];
        double[] output = new double[input.Length];

        Wavelet.Batch(input, output, 2, 1.0);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"output[{i}] should be finite but was {output[i]}");
        }
    }

    [Fact]
    public void Robust_NegativeInfinity_UsesLastValid()
    {
        var ind = new Wavelet(2, 1.0);

        ind.Update(new TValue(DateTime.UtcNow, 100));
        ind.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));

        Assert.True(double.IsFinite(ind.Last.Value));
    }

    // --- F) Consistency (critical) ---

    [Fact]
    public void Consistency_BatchCalc_MatchesStreaming()
    {
        var data = _gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();

        // Streaming
        var streaming = new Wavelet(3, 1.0);
        double[] streamResults = new double[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            streamResults[i] = streaming.Update(new TValue(DateTime.UtcNow, input[i])).Value;
        }

        // Batch TSeries
        var batchResults = Wavelet.Batch(data.Close, 3, 1.0);

        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(batchResults[i].Value, streamResults[i], 1e-10);
        }
    }

    [Fact]
    public void Consistency_SpanCalc_MatchesStreaming()
    {
        var data = _gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();

        // Streaming
        var streaming = new Wavelet(3, 1.0);
        double[] streamResults = new double[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            streamResults[i] = streaming.Update(new TValue(DateTime.UtcNow, input[i])).Value;
        }

        // Span batch
        double[] spanOutput = new double[input.Length];
        Wavelet.Batch(input, spanOutput, 3, 1.0);

        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(spanOutput[i], streamResults[i], 1e-10);
        }
    }

    [Fact]
    public void Consistency_EventDriven_MatchesStreaming()
    {
        var data = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Direct streaming
        var direct = new Wavelet(3, 1.0);
        double[] directResults = new double[data.Close.Count];
        for (int i = 0; i < data.Close.Count; i++)
        {
            directResults[i] = direct.Update(data.Close[i]).Value;
        }

        // Event-driven
        var source = new TSeries();
        var eventDriven = new Wavelet(source, 3, 1.0);
        for (int i = 0; i < data.Close.Count; i++)
        {
            source.Add(data.Close[i]);
        }

        Assert.Equal(directResults[^1], eventDriven.Last.Value, 1e-10);
    }

    [Fact]
    public void Consistency_AllFourModes_Match()
    {
        var data = _gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();

        // 1. Streaming
        var streaming = new Wavelet(3, 1.0);
        double[] streamResults = new double[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            streamResults[i] = streaming.Update(new TValue(DateTime.UtcNow, input[i])).Value;
        }

        // 2. Batch TSeries
        var batchResults = Wavelet.Batch(data.Close, 3, 1.0);

        // 3. Span batch
        double[] spanOutput = new double[input.Length];
        Wavelet.Batch(input, spanOutput, 3, 1.0);

        // 4. Event-driven (check last value)
        var source = new TSeries();
        var eventDriven = new Wavelet(source, 3, 1.0);
        for (int i = 0; i < input.Length; i++)
        {
            source.Add(data.Close[i]);
        }

        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(streamResults[i], batchResults[i].Value, 1e-10);
            Assert.Equal(streamResults[i], spanOutput[i], 1e-10);
        }
        Assert.Equal(streamResults[^1], eventDriven.Last.Value, 1e-10);
    }

    // --- G) Span API Tests ---

    [Fact]
    public void Span_ValidatesLengths()
    {
        double[] input = [1, 2, 3, 4, 5];
        double[] shortOutput = new double[3];

        var ex = Assert.Throws<ArgumentException>(() => Wavelet.Batch(input, shortOutput, 2, 1.0));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Span_ValidatesLevels()
    {
        double[] input = [1, 2, 3, 4, 5];
        double[] output = new double[5];

        Assert.Throws<ArgumentOutOfRangeException>(() => Wavelet.Batch(input, output, 0, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Wavelet.Batch(input, output, 9, 1.0));
    }

    [Fact]
    public void Span_ValidatesThreshMult()
    {
        double[] input = [1, 2, 3, 4, 5];
        double[] output = new double[5];

        Assert.Throws<ArgumentOutOfRangeException>(() => Wavelet.Batch(input, output, 2, -1.0));
    }

    [Fact]
    public void Span_MatchesTSeries()
    {
        var data = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();
        double[] spanOutput = new double[input.Length];

        Wavelet.Batch(input, spanOutput, 3, 1.0);
        var tseriesOutput = Wavelet.Batch(data.Close, 3, 1.0);

        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(tseriesOutput[i].Value, spanOutput[i], 1e-10);
        }
    }

    [Fact]
    public void Span_HandlesNaN()
    {
        double[] input = [100, double.NaN, 102, 103, 104];
        double[] output = new double[5];

        Wavelet.Batch(input, output, 2, 1.0);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"output[{i}] should be finite");
        }
    }

    [Fact]
    public void Span_LargeData_NoStackOverflow()
    {
        int len = 10_000;
        double[] input = new double[len];
        double[] output = new double[len];
        for (int i = 0; i < len; i++)
        {
            input[i] = 100 + Math.Sin(i * 0.1);
        }

        Wavelet.Batch(input, output, 4, 1.0);

        for (int i = 0; i < len; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"output[{i}] should be finite");
        }
    }

    // --- H) Chainability ---

    [Fact]
    public void Chain_PubFires()
    {
        var ind = new Wavelet(2, 1.0);
        bool fired = false;
        ind.Pub += (object? _, in TValueEventArgs _) => fired = true;

        ind.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(fired);
    }

    [Fact]
    public void Chain_EventBasedChaining()
    {
        var source = new TSeries();
        var ind = new Wavelet(source, 2, 1.0);

        source.Add(DateTime.UtcNow, 100);
        source.Add(DateTime.UtcNow, 101);
        source.Add(DateTime.UtcNow, 102);

        Assert.True(double.IsFinite(ind.Last.Value));
    }

    [Fact]
    public void Chain_Dispose_UnsubscribesEvent()
    {
        var source = new TSeries();
        var ind = new Wavelet(source, 2, 1.0);

        source.Add(DateTime.UtcNow, 100);
        double beforeDispose = ind.Last.Value;

        ind.Dispose();

        source.Add(DateTime.UtcNow, 200);
        // After dispose, ind should not receive updates
        Assert.Equal(beforeDispose, ind.Last.Value, 1e-15);
    }

    // --- Additional: Denoising Behavior ---

    [Fact]
    public void Denoise_ReducesVariance()
    {
        // Noisy signal: sine wave + noise
        const int len = 200;
        double[] input = new double[len];
        double[] output = new double[len];

        for (int i = 0; i < len; i++)
        {
            double signal = 100 + 10 * Math.Sin(2 * Math.PI * i / 40.0);
            double noise = 2.0 * Math.Sin(17.3 * i) + 1.5 * Math.Cos(31.7 * i);
            input[i] = signal + noise;
        }

        Wavelet.Batch(input, output, 3, 1.0);

        // Compute variance of input vs output (last half, after warmup)
        int start = len / 2;
        double inputVar = Variance(input.AsSpan(start));
        double outputVar = Variance(output.AsSpan(start));

        Assert.True(outputVar < inputVar, $"Output variance ({outputVar:F4}) should be less than input variance ({inputVar:F4})");
    }

    [Fact]
    public void Denoise_ZeroThreshold_LessSmoothing()
    {
        var data = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();

        double[] zeroThresh = new double[input.Length];
        double[] normalThresh = new double[input.Length];

        Wavelet.Batch(input, zeroThresh, 3, 0.0);
        Wavelet.Batch(input, normalThresh, 3, 1.0);

        // Zero threshold should be closer to original signal
        double diffZero = 0, diffNormal = 0;
        for (int i = 0; i < input.Length; i++)
        {
            diffZero += Math.Abs(input[i] - zeroThresh[i]);
            diffNormal += Math.Abs(input[i] - normalThresh[i]);
        }

        Assert.True(diffZero <= diffNormal + 1e-10,
            $"Zero threshold diff ({diffZero:F4}) should be <= normal threshold diff ({diffNormal:F4})");
    }

    [Fact]
    public void Denoise_HigherThreshold_MoreDeviation()
    {
        // Higher threshold removes more detail coefficients, so output deviates more from input
        const int len = 300;
        double[] input = new double[len];
        for (int i = 0; i < len; i++)
        {
            input[i] = 100 + 10 * Math.Sin(2 * Math.PI * i / 40.0) + 5 * Math.Sin(73.1 * i);
        }

        double[] lowThresh = new double[len];
        double[] highThresh = new double[len];

        Wavelet.Batch(input, lowThresh, 3, 0.5);
        Wavelet.Batch(input, highThresh, 3, 3.0);

        // Higher threshold should deviate more from original (more aggressive denoising)
        double diffLow = 0, diffHigh = 0;
        int start = len / 2;
        for (int i = start; i < len; i++)
        {
            diffLow += Math.Abs(input[i] - lowThresh[i]);
            diffHigh += Math.Abs(input[i] - highThresh[i]);
        }

        Assert.True(diffHigh >= diffLow - 1e-10,
            $"High threshold deviation ({diffHigh:F4}) should be >= low threshold deviation ({diffLow:F4})");
    }

    // --- Helpers ---

    private static double Variance(ReadOnlySpan<double> data)
    {
        double sum = 0, sum2 = 0;
        for (int i = 0; i < data.Length; i++)
        {
            sum += data[i];
            sum2 += data[i] * data[i];
        }
        double mean = sum / data.Length;
        return sum2 / data.Length - mean * mean;
    }
}
