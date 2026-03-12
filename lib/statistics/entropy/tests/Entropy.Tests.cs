
namespace QuanTAlib.Tests;

// ═══════════════════════════════════════════════════════════════
// A) Constructor Validation
// ═══════════════════════════════════════════════════════════════
public class EntropyConstructorTests
{
    [Fact]
    public void Constructor_ThrowsOnPeriodLessThan2()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Entropy(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Entropy(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Entropy(-1));
    }

    [Fact]
    public void Constructor_AcceptsMinimumPeriod()
    {
        var e = new Entropy(2);
        Assert.NotNull(e);
        Assert.Equal("Entropy(2)", e.Name);
    }

    [Fact]
    public void Constructor_SetsWarmupPeriod()
    {
        var e = new Entropy(14);
        Assert.Equal(14, e.WarmupPeriod);
    }

    [Fact]
    public void Constructor_ParamName_IsPeriod()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Entropy(1));
        Assert.Equal("period", ex.ParamName);
    }
}

// ═══════════════════════════════════════════════════════════════
// B) Basic Calculation
// ═══════════════════════════════════════════════════════════════
public class EntropyBasicTests
{
    [Fact]
    public void Calc_ReturnsValue()
    {
        var e = new Entropy(5);
        Assert.Equal(0, e.Last.Value);

        TValue result = e.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(result.Value, e.Last.Value);
    }

    [Fact]
    public void Calc_ConstantValues_ReturnsZero()
    {
        // All same values → zero entropy (perfectly predictable)
        var e = new Entropy(5);
        for (int i = 0; i < 5; i++)
        {
            e.Update(new TValue(DateTime.UtcNow, 42.0));
        }
        Assert.Equal(0, e.Last.Value, 10);
    }

    [Fact]
    public void Calc_OutputBetweenZeroAndOne()
    {
        var e = new Entropy(10);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            e.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.InRange(e.Last.Value, 0.0, 1.0);
    }

    [Fact]
    public void Calc_UniformSpread_HighEntropy()
    {
        // Evenly spaced distinct values → high entropy
        var e = new Entropy(10);
        for (int i = 1; i <= 10; i++)
        {
            e.Update(new TValue(DateTime.UtcNow, i * 10.0));
        }
        // With 10 distinct uniformly-spaced values in 10 bins, entropy should be close to 1
        Assert.True(e.Last.Value > 0.8, $"Expected high entropy, got {e.Last.Value}");
    }

    [Fact]
    public void IsHot_Accessible()
    {
        var e = new Entropy(5);
        Assert.False(e.IsHot);
    }

    [Fact]
    public void Name_IsAccessible()
    {
        var e = new Entropy(14);
        Assert.Equal("Entropy(14)", e.Name);
    }
}

// ═══════════════════════════════════════════════════════════════
// C) State + Bar Correction
// ═══════════════════════════════════════════════════════════════
public class EntropyStateCorrectionTests
{
    [Fact]
    public void IsNew_True_Advances()
    {
        var e = new Entropy(5);
        e.Update(new TValue(DateTime.UtcNow, 1), isNew: true);
        e.Update(new TValue(DateTime.UtcNow, 2), isNew: true);
        e.Update(new TValue(DateTime.UtcNow, 3), isNew: true);
        e.Update(new TValue(DateTime.UtcNow, 4), isNew: true);
        double v1 = e.Update(new TValue(DateTime.UtcNow, 5), isNew: true).Value;

        e.Update(new TValue(DateTime.UtcNow, 50), isNew: true);
        double v2 = e.Last.Value;

        Assert.NotEqual(v1, v2);
    }

    [Fact]
    public void IsNew_False_Rewrites()
    {
        var e = new Entropy(5);
        e.Update(new TValue(DateTime.UtcNow, 1));
        e.Update(new TValue(DateTime.UtcNow, 2));
        e.Update(new TValue(DateTime.UtcNow, 3));
        e.Update(new TValue(DateTime.UtcNow, 4));
        e.Update(new TValue(DateTime.UtcNow, 5), isNew: true);

        // Rewrite last value from 5 to 50
        var res = e.Update(new TValue(DateTime.UtcNow, 50), isNew: false);

        // Expected: entropy of {1, 2, 3, 4, 50}
        var expected = new Entropy(5);
        expected.Update(new TValue(DateTime.UtcNow, 1));
        expected.Update(new TValue(DateTime.UtcNow, 2));
        expected.Update(new TValue(DateTime.UtcNow, 3));
        expected.Update(new TValue(DateTime.UtcNow, 4));
        var expectedVal = expected.Update(new TValue(DateTime.UtcNow, 50));

        Assert.Equal(expectedVal.Value, res.Value, 10);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var e = new Entropy(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            e.Update(tenthInput, isNew: true);
        }

        double stateAfterTen = e.Last.Value;

        // Generate 9 corrections with isNew=false
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            e.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the original 10th input again with isNew=false
        TValue finalResult = e.Update(tenthInput, isNew: false);

        // Entropy rebuilds from buffer each update, so this should be exact
        Assert.Equal(stateAfterTen, finalResult.Value, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var e = new Entropy(5);
        for (int i = 0; i < 5; i++)
        {
            e.Update(new TValue(DateTime.UtcNow, i * 10.0));
        }
        Assert.True(e.IsHot);

        e.Reset();
        Assert.False(e.IsHot);
        Assert.Equal(0, e.Last.Value);
    }
}

// ═══════════════════════════════════════════════════════════════
// D) Warmup / Convergence
// ═══════════════════════════════════════════════════════════════
public class EntropyWarmupTests
{
    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var e = new Entropy(5);
        Assert.False(e.IsHot);

        for (int i = 1; i <= 4; i++)
        {
            e.Update(new TValue(DateTime.UtcNow, i * 10));
            Assert.False(e.IsHot);
        }

        e.Update(new TValue(DateTime.UtcNow, 50));
        Assert.True(e.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesPeriod()
    {
        var e = new Entropy(20);
        Assert.Equal(20, e.WarmupPeriod);
    }
}

// ═══════════════════════════════════════════════════════════════
// E) Robustness
// ═══════════════════════════════════════════════════════════════
public class EntropyRobustnessTests
{
    [Fact]
    public void NaN_UsesLastValidValue()
    {
        var e = new Entropy(5);
        e.Update(new TValue(DateTime.UtcNow, 10));
        e.Update(new TValue(DateTime.UtcNow, 20));
        e.Update(new TValue(DateTime.UtcNow, 30));
        var result = e.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void PositiveInfinity_UsesLastValidValue()
    {
        var e = new Entropy(5);
        e.Update(new TValue(DateTime.UtcNow, 10));
        e.Update(new TValue(DateTime.UtcNow, 20));
        e.Update(new TValue(DateTime.UtcNow, 30));
        var result = e.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void NegativeInfinity_UsesLastValidValue()
    {
        var e = new Entropy(5);
        e.Update(new TValue(DateTime.UtcNow, 10));
        e.Update(new TValue(DateTime.UtcNow, 20));
        e.Update(new TValue(DateTime.UtcNow, 30));
        var result = e.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        double[] source = [1, 2, double.NaN, 4, 5, 6, 7, 8, 9, 10];
        double[] output = new double[source.Length];

        Entropy.Batch(source.AsSpan(), output.AsSpan(), 5);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"output[{i}] = {output[i]}");
        }
    }
}

// ═══════════════════════════════════════════════════════════════
// F) Consistency (all 4 modes match)
// ═══════════════════════════════════════════════════════════════
public class EntropyConsistencyTests
{
    [Fact]
    public void AllModes_ProduceSameResult()
    {
        const int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        int count = 200;

        var times = new List<long>(count);
        var values = new List<double>(count);

        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next(isNew: true);
            times.Add(bar.Time);
            values.Add(bar.Close);
        }

        var series = new TSeries(times, values);

        // 1. Batch Mode (static method)
        var batchSeries = Entropy.Batch(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode (static method with spans)
        var spanInput = values.ToArray();
        var spanOutput = new double[count];
        Entropy.Batch(spanInput.AsSpan(), spanOutput.AsSpan(), period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode (instance, one value at a time)
        var streamingInd = new Entropy(period);
        for (int i = 0; i < count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // Assert all modes produce identical results
        Assert.Equal(expected, spanResult, precision: 9);
        Assert.Equal(expected, streamingResult, precision: 9);
    }

    [Fact]
    public void Batch_Matches_Streaming()
    {
        double[] data = [1, 2, 3, 4, 5, 10, 1, 2, 3, 4, 5, 20, 1, 3, 5, 7, 9, 11];
        int period = 5;

        // Streaming
        var e = new Entropy(period);
        var streamingResults = new List<double>();
        foreach (var val in data)
        {
            streamingResults.Add(e.Update(new TValue(DateTime.UtcNow, val)).Value);
        }

        // Batch
        var series = new TSeries(new List<long>(new long[data.Length]), new List<double>(data));
        var batchResult = Entropy.Batch(series, period);

        for (int i = 0; i < data.Length; i++)
        {
            Assert.Equal(streamingResults[i], batchResult.Values[i], precision: 10);
        }
    }
}

// ═══════════════════════════════════════════════════════════════
// G) Span API Tests
// ═══════════════════════════════════════════════════════════════
public class EntropySpanTests
{
    [Fact]
    public void SpanBatch_ValidatesLengths()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] wrongSize = new double[3];

        var ex = Assert.Throws<ArgumentException>(() =>
            Entropy.Batch(source.AsSpan(), wrongSize.AsSpan(), 3));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_ValidatesPeriod()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];

        Assert.Throws<ArgumentException>(() =>
            Entropy.Batch(source.AsSpan(), output.AsSpan(), 1));
        Assert.Throws<ArgumentException>(() =>
            Entropy.Batch(source.AsSpan(), output.AsSpan(), 0));
    }

    [Fact]
    public void SpanBatch_MatchesTSeriesBatch()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        int count = 100;

        var times = new List<long>(count);
        var values = new List<double>(count);
        double[] source = new double[count];
        double[] output = new double[count];

        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next(isNew: true);
            times.Add(bar.Time);
            values.Add(bar.Close);
            source[i] = bar.Close;
        }

        var series = new TSeries(times, values);

        var tseriesResult = Entropy.Batch(series, 10);
        Entropy.Batch(source.AsSpan(), output.AsSpan(), 10);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-10);
        }
    }

    [Fact]
    public void SpanBatch_HandlesNaN()
    {
        double[] source = [1, 2, double.NaN, 4, 5];
        double[] output = new double[5];

        Entropy.Batch(source.AsSpan(), output.AsSpan(), 3);

        for (int i = 0; i < source.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]));
        }
    }

    [Fact]
    public void SpanBatch_LargeData_NoStackOverflow()
    {
        int count = 5000;
        var data = new double[count];
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < count; i++)
        {
            data[i] = gbm.Next(isNew: true).Close;
        }

        var output = new double[count];

        // Should not throw StackOverflowException
        Entropy.Batch(data.AsSpan(), output.AsSpan(), 50);

        Assert.True(double.IsFinite(output[^1]));
    }
}

// ═══════════════════════════════════════════════════════════════
// H) Event / Chainability
// ═══════════════════════════════════════════════════════════════
public class EntropyEventTests
{
    [Fact]
    public void Pub_Fires()
    {
        var e = new Entropy(5);
        int eventCount = 0;
        e.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        e.Update(new TValue(DateTime.UtcNow, 100));

        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void EventBased_Chaining_Works()
    {
        var source = new TSeries();
        var e = new Entropy(5);

        // Subscribe to source
        source.Pub += (object? sender, in TValueEventArgs args) =>
        {
            e.Update(args.Value);
        };

        // Feed data through source
        for (int i = 1; i <= 10; i++)
        {
            source.Add(new TValue(DateTime.UtcNow, i * 10.0));
        }

        Assert.True(e.IsHot);
        Assert.True(double.IsFinite(e.Last.Value));
        // Allow tiny floating-point overshoot above 1.0
        Assert.True(e.Last.Value >= -1e-10 && e.Last.Value <= 1.0 + 1e-10,
            $"Expected entropy in [0, 1], got {e.Last.Value}");
    }
}
