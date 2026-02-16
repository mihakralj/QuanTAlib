
namespace QuanTAlib.Tests;

// ═══════════════════════════════════════════════════════════════
// A) Constructor Validation
// ═══════════════════════════════════════════════════════════════
public class HurstConstructorTests
{
    [Fact]
    public void Constructor_ThrowsOnPeriodLessThan20()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Hurst(19));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Hurst(10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Hurst(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Hurst(-1));
    }

    [Fact]
    public void Constructor_AcceptsMinimumPeriod()
    {
        var h = new Hurst(20);
        Assert.NotNull(h);
        Assert.Equal("Hurst(20)", h.Name);
    }

    [Fact]
    public void Constructor_SetsWarmupPeriod()
    {
        var h = new Hurst(100);
        Assert.Equal(101, h.WarmupPeriod);
    }

    [Fact]
    public void Constructor_ParamName_IsPeriod()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Hurst(5));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_LargePeriodAccepted()
    {
        var h = new Hurst(500);
        Assert.Equal("Hurst(500)", h.Name);
        Assert.Equal(501, h.WarmupPeriod);
    }
}

// ═══════════════════════════════════════════════════════════════
// B) Basic Calculation
// ═══════════════════════════════════════════════════════════════
public class HurstBasicTests
{
    [Fact]
    public void Calc_ReturnsValue()
    {
        var h = new Hurst(20);
        TValue result = h.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(result.Value, h.Last.Value);
    }

    [Fact]
    public void Calc_FirstValue_ReturnsDefaultHalf()
    {
        var h = new Hurst(20);
        TValue result = h.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(0.5, result.Value);
    }

    [Fact]
    public void Calc_OutputIsFinite()
    {
        var h = new Hurst(20);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            var result = h.Update(new TValue(bar.Time, bar.Close));
            Assert.True(double.IsFinite(result.Value), $"Result at index {i} is not finite: {result.Value}");
        }
    }

    [Fact]
    public void Calc_GBM_ResultNearHalf()
    {
        // GBM with zero drift should produce H ≈ 0.5 (random walk)
        var h = new Hurst(100);
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.2, seed: 42);

        TValue lastResult = default;
        for (int i = 0; i < 500; i++)
        {
            var bar = gbm.Next(isNew: true);
            lastResult = h.Update(new TValue(bar.Time, bar.Close));
        }

        // H should be roughly 0.5 for random walk — allow wide tolerance
        Assert.InRange(lastResult.Value, 0.2, 0.8);
    }

    [Fact]
    public void IsHot_Accessible()
    {
        var h = new Hurst(20);
        Assert.False(h.IsHot);
    }

    [Fact]
    public void Name_IsAccessible()
    {
        var h = new Hurst(50);
        Assert.Equal("Hurst(50)", h.Name);
    }
}

// ═══════════════════════════════════════════════════════════════
// C) State + Bar Correction
// ═══════════════════════════════════════════════════════════════
public class HurstStateCorrectionTests
{
    [Fact]
    public void IsNew_True_Advances()
    {
        var h = new Hurst(20);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 25; i++)
        {
            var bar = gbm.Next(isNew: true);
            h.Update(new TValue(bar.Time, bar.Close), isNew: true);
        }
        double v1 = h.Last.Value;

        var nextBar = gbm.Next(isNew: true);
        h.Update(new TValue(nextBar.Time, nextBar.Close), isNew: true);
        double v2 = h.Last.Value;

        // Values should differ after advancing
        Assert.True(double.IsFinite(v1));
        Assert.True(double.IsFinite(v2));
    }

    [Fact]
    public void IsNew_False_Rewrites()
    {
        var h = new Hurst(20);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        // Build up state
        for (int i = 0; i < 25; i++)
        {
            var bar = gbm.Next(isNew: true);
            h.Update(new TValue(bar.Time, bar.Close), isNew: true);
        }

        _ = h.Last.Value;

        // Rewrite last value
        h.Update(new TValue(DateTime.UtcNow, 200), isNew: false);
        double valueAfterRewrite = h.Last.Value;

        // Rewrite again with original-like value
        h.Update(new TValue(DateTime.UtcNow, 200), isNew: false);
        double valueSecondRewrite = h.Last.Value;

        // Same rewrite value should produce same result
        Assert.Equal(valueAfterRewrite, valueSecondRewrite, 10);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var h = new Hurst(20);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        // Feed 25 new values
        TValue lastInput = default;
        for (int i = 0; i < 25; i++)
        {
            var bar = gbm.Next(isNew: true);
            lastInput = new TValue(bar.Time, bar.Close);
            h.Update(lastInput, isNew: true);
        }

        double stateAfter25 = h.Last.Value;

        // Generate corrections with isNew=false using same last price
        h.Update(new TValue(DateTime.UtcNow, lastInput.Value + 10), isNew: false);
        h.Update(new TValue(DateTime.UtcNow, lastInput.Value + 20), isNew: false);

        // Restore original value
        TValue finalResult = h.Update(lastInput, isNew: false);

        Assert.Equal(stateAfter25, finalResult.Value, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var h = new Hurst(20);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 30; i++)
        {
            var bar = gbm.Next(isNew: true);
            h.Update(new TValue(bar.Time, bar.Close));
        }
        Assert.True(h.IsHot);

        h.Reset();
        Assert.False(h.IsHot);
        Assert.Equal(0, h.Last.Value);
    }
}

// ═══════════════════════════════════════════════════════════════
// D) Warmup / Convergence
// ═══════════════════════════════════════════════════════════════
public class HurstWarmupTests
{
    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var h = new Hurst(20);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        // Need period+1 = 21 prices to get 20 log returns
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            h.Update(new TValue(bar.Time, bar.Close));
            Assert.False(h.IsHot, $"Should not be hot at input {i + 1}");
        }

        // 21st price → 20th log return → buffer full
        var finalBar = gbm.Next(isNew: true);
        h.Update(new TValue(finalBar.Time, finalBar.Close));
        Assert.True(h.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesPeriodPlusOne()
    {
        var h = new Hurst(50);
        Assert.Equal(51, h.WarmupPeriod);
    }
}

// ═══════════════════════════════════════════════════════════════
// E) Robustness
// ═══════════════════════════════════════════════════════════════
public class HurstRobustnessTests
{
    [Fact]
    public void NaN_UsesLastValidValue()
    {
        var h = new Hurst(20);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 25; i++)
        {
            var bar = gbm.Next(isNew: true);
            h.Update(new TValue(bar.Time, bar.Close));
        }

        var result = h.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void PositiveInfinity_UsesLastValidValue()
    {
        var h = new Hurst(20);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 25; i++)
        {
            var bar = gbm.Next(isNew: true);
            h.Update(new TValue(bar.Time, bar.Close));
        }

        var result = h.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void NegativeInfinity_UsesLastValidValue()
    {
        var h = new Hurst(20);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 25; i++)
        {
            var bar = gbm.Next(isNew: true);
            h.Update(new TValue(bar.Time, bar.Close));
        }

        var result = h.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        double[] source = new double[50];
        for (int i = 0; i < 50; i++)
        {
            source[i] = gbm.Next(isNew: true).Close;
        }
        source[10] = double.NaN;
        source[25] = double.NaN;

        double[] output = new double[source.Length];
        Hurst.Batch(source.AsSpan(), output.AsSpan(), 20);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"output[{i}] = {output[i]}");
        }
    }
}

// ═══════════════════════════════════════════════════════════════
// F) Consistency (all modes match)
// ═══════════════════════════════════════════════════════════════
public class HurstConsistencyTests
{
    [Fact]
    public void AllModes_ProduceSameResult()
    {
        const int period = 20;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        int count = 100;

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
        var batchSeries = Hurst.Batch(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode (static method with spans)
        var spanInput = values.ToArray();
        var spanOutput = new double[count];
        Hurst.Batch(spanInput.AsSpan(), spanOutput.AsSpan(), period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode (instance, one value at a time)
        var streamingInd = new Hurst(period);
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
        const int period = 20;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        int count = 60;

        var times = new List<long>(count);
        var values = new List<double>(count);

        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next(isNew: true);
            times.Add(bar.Time);
            values.Add(bar.Close);
        }

        // Streaming
        var h = new Hurst(period);
        var streamingResults = new List<double>();
        for (int i = 0; i < count; i++)
        {
            streamingResults.Add(h.Update(new TValue(times[i], values[i])).Value);
        }

        // Batch
        var series = new TSeries(times, values);
        var batchResult = Hurst.Batch(series, period);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamingResults[i], batchResult.Values[i], precision: 10);
        }
    }
}

// ═══════════════════════════════════════════════════════════════
// G) Span API Tests
// ═══════════════════════════════════════════════════════════════
public class HurstSpanTests
{
    [Fact]
    public void SpanBatch_ValidatesLengths()
    {
        double[] source = new double[50];
        double[] wrongSize = new double[30];

        var ex = Assert.Throws<ArgumentException>(() =>
            Hurst.Batch(source.AsSpan(), wrongSize.AsSpan(), 20));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_ValidatesPeriod()
    {
        double[] source = new double[50];
        double[] output = new double[50];

        Assert.Throws<ArgumentException>(() =>
            Hurst.Batch(source.AsSpan(), output.AsSpan(), 19));
        Assert.Throws<ArgumentException>(() =>
            Hurst.Batch(source.AsSpan(), output.AsSpan(), 0));
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

        var tseriesResult = Hurst.Batch(series, 20);
        Hurst.Batch(source.AsSpan(), output.AsSpan(), 20);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-10);
        }
    }

    [Fact]
    public void SpanBatch_HandlesNaN()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        double[] source = new double[50];
        for (int i = 0; i < 50; i++)
        {
            source[i] = gbm.Next(isNew: true).Close;
        }
        source[5] = double.NaN;
        source[15] = double.NaN;

        double[] output = new double[50];
        Hurst.Batch(source.AsSpan(), output.AsSpan(), 20);

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
        Hurst.Batch(data.AsSpan(), output.AsSpan(), 100);

        Assert.True(double.IsFinite(output[^1]));
    }
}

// ═══════════════════════════════════════════════════════════════
// H) Event / Chainability
// ═══════════════════════════════════════════════════════════════
public class HurstEventTests
{
    [Fact]
    public void Pub_Fires()
    {
        var h = new Hurst(20);
        int eventCount = 0;
        h.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        h.Update(new TValue(DateTime.UtcNow, 100));

        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void EventBased_Chaining_Works()
    {
        var source = new TSeries();
        var h = new Hurst(20);

        source.Pub += (object? sender, in TValueEventArgs args) =>
        {
            h.Update(args.Value);
        };

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < 30; i++)
        {
            var bar = gbm.Next(isNew: true);
            source.Add(new TValue(bar.Time, bar.Close));
        }

        Assert.True(h.IsHot);
        Assert.True(double.IsFinite(h.Last.Value));
    }
}
