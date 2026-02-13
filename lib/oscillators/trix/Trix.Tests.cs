using Xunit;

namespace QuanTAlib.Tests;

// ── A) Constructor Validation ───────────────────────────────────────────────
public sealed class TrixConstructorTests
{
    [Fact]
    public void Constructor_ZeroPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Trix(0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Trix(-5));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_DefaultPeriod_Creates()
    {
        var trix = new Trix();
        Assert.NotNull(trix);
        Assert.Equal(14, trix.Period);
        Assert.Equal("Trix(14)", trix.Name);
    }

    [Fact]
    public void Constructor_CustomPeriod_Creates()
    {
        var trix = new Trix(5);
        Assert.Equal(5, trix.Period);
        Assert.Equal("Trix(5)", trix.Name);
    }

    [Fact]
    public void Constructor_WarmupPeriod_IsTriplePeriod()
    {
        var trix = new Trix(10);
        Assert.Equal(30, trix.WarmupPeriod);
    }

    [Fact]
    public void Constructor_PeriodOne_IsValid()
    {
        var trix = new Trix(1);
        Assert.NotNull(trix);
        Assert.Equal(1, trix.Period);
    }
}

// ── B) Basic Calculation ────────────────────────────────────────────────────
public sealed class TrixBasicTests
{
    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var trix = new Trix(10);
        Assert.Equal(0, trix.Last.Value);

        TValue result = trix.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(result.Value, trix.Last.Value);
    }

    [Fact]
    public void FirstBar_OutputIsZero()
    {
        var trix = new Trix(5);
        var result = trix.Update(new TValue(DateTime.UtcNow, 100));
        // First bar: no previous EMA3 to compare against, output = 0
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void SecondBar_ProducesNonZeroValue()
    {
        var trix = new Trix(5);
        trix.Update(new TValue(DateTime.UtcNow, 100));
        var result = trix.Update(new TValue(DateTime.UtcNow, 110));
        // EMA3 changes vs first bar → non-zero TRIX
        Assert.NotEqual(0.0, result.Value);
    }

    [Fact]
    public void Name_Available()
    {
        var trix = new Trix(7);
        Assert.Equal("Trix(7)", trix.Name);
    }

    [Fact]
    public void Last_IsAccessible()
    {
        var trix = new Trix(5);
        trix.Update(new TValue(DateTime.UtcNow, 100));
        trix.Update(new TValue(DateTime.UtcNow, 110));
        Assert.True(double.IsFinite(trix.Last.Value));
    }
}

// ── C) State + Bar Correction ───────────────────────────────────────────────
public sealed class TrixBarCorrectionTests
{
    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var trix = new Trix(5);
        trix.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double val1 = trix.Last.Value;

        trix.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        double val2 = trix.Last.Value;

        // Different values should produce different states
        Assert.NotEqual(val1, val2);
    }

    [Fact]
    public void IsNew_False_Rollback()
    {
        var trix = new Trix(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        // Feed enough bars to get past trivial state
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            trix.Update(new TValue(bar.Time, bar.Close), isNew: true);
        }

        // Feed one more bar with isNew=true and remember value
        var nextBar = gbm.Next(isNew: true);
        var originalInput = new TValue(nextBar.Time, nextBar.Close);
        var val1 = trix.Update(originalInput, isNew: true);

        // Correct with isNew=false (different value)
        trix.Update(new TValue(nextBar.Time, nextBar.Close + 50), isNew: false);

        // Re-apply original value with isNew=false → should match val1
        var restored = trix.Update(originalInput, isNew: false);

        Assert.Equal(val1.Value, restored.Value, 1e-10);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var trix = new Trix(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 20 new values
        TValue twentiethInput = default;
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            twentiethInput = new TValue(bar.Time, bar.Close);
            trix.Update(twentiethInput, isNew: true);
        }

        double stateAfterTwenty = trix.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            trix.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 20th input again with isNew=false
        TValue finalResult = trix.Update(twentiethInput, isNew: false);

        Assert.Equal(stateAfterTwenty, finalResult.Value, 1e-10);
    }
}

// ── D) Warmup / Convergence ─────────────────────────────────────────────────
public sealed class TrixWarmupTests
{
    [Fact]
    public void IsHot_InitiallyFalse()
    {
        var trix = new Trix(5);
        Assert.False(trix.IsHot);
    }

    [Fact]
    public void IsHot_BecomesTrueAfterWarmupPeriodBars()
    {
        const int period = 5;
        var trix = new Trix(period);
        int warmup = trix.WarmupPeriod; // period * 3 = 15

        // Feed warmup-1 bars → still cold (Count < WarmupPeriod)
        for (int i = 1; i < warmup; i++)
        {
            trix.Update(new TValue(DateTime.UtcNow, i * 10));
            Assert.False(trix.IsHot, $"Should not be hot at bar {i} (need {warmup})");
        }

        // Bar at warmup count → hot (Count == WarmupPeriod)
        trix.Update(new TValue(DateTime.UtcNow, warmup * 10));
        Assert.True(trix.IsHot);
    }

    [Fact]
    public void WarmupPeriod_IsTriplePeriod()
    {
        var trix = new Trix(10);
        Assert.Equal(30, trix.WarmupPeriod);
    }

    [Fact]
    public void IsHot_StaysTrue()
    {
        var trix = new Trix(3);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            trix.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(trix.IsHot);
    }
}

// ── E) Robustness (NaN / Infinity) ─────────────────────────────────────────
public sealed class TrixRobustnessTests
{
    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var trix = new Trix(5);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 15; i++)
        {
            trix.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        var result = trix.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var trix = new Trix(5);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 15; i++)
        {
            trix.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        var resultPos = trix.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultPos.Value));

        var resultNeg = trix.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultNeg.Value));
    }

    [Fact]
    public void BatchNaN_DoesNotCrash()
    {
        double[] source = [100, 110, double.NaN, 130, 140, double.NaN, 160];
        double[] output = new double[source.Length];

        Trix.Batch(source.AsSpan(), output.AsSpan(), 3);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output at index {i} is not finite");
        }
    }
}

// ── F) Consistency (All 4 Modes Match) ──────────────────────────────────────
public sealed class TrixConsistencyTests
{
    private static TSeries GenerateCloseSeries(int count, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: seed);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        return bars.Close;
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        const int period = 10;
        var series = GenerateCloseSeries(100);

        // 1. Batch Mode
        var batchSeries = Trix.Batch(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var spanInput = series.Values.ToArray();
        var spanOutput = new double[spanInput.Length];
        Trix.Batch(spanInput.AsSpan(), spanOutput.AsSpan(), period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Trix(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Trix(pubSource, period);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        Assert.Equal(expected, spanResult, precision: 9);
        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, eventingResult, precision: 9);
    }

    [Fact]
    public void BatchVsStreaming_AllPoints()
    {
        const int period = 5;
        var series = GenerateCloseSeries(50);

        // Batch
        var batchSeries = Trix.Batch(series, period);

        // Streaming
        var streamingInd = new Trix(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
            Assert.Equal(batchSeries[i].Value, streamingInd.Last.Value, 1e-10);
        }
    }

    [Fact]
    public void SpanVsBatch_AllPoints()
    {
        const int period = 7;
        var series = GenerateCloseSeries(80);

        var batchSeries = Trix.Batch(series, period);

        var spanInput = series.Values.ToArray();
        var spanOutput = new double[spanInput.Length];
        Trix.Batch(spanInput.AsSpan(), spanOutput.AsSpan(), period);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchSeries[i].Value, spanOutput[i], 1e-10);
        }
    }
}

// ── G) Span API Tests ───────────────────────────────────────────────────────
public sealed class TrixSpanTests
{
    [Fact]
    public void Batch_Span_MismatchedLengths_Throws()
    {
        double[] source = new double[10];
        double[] output = new double[5];

        var ex = Assert.Throws<ArgumentException>(
            () => Trix.Batch(source.AsSpan(), output.AsSpan(), 3));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_ZeroPeriod_Throws()
    {
        double[] source = new double[10];
        double[] output = new double[10];

        var ex = Assert.Throws<ArgumentException>(
            () => Trix.Batch(source.AsSpan(), output.AsSpan(), 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_EmptyArrays_DoesNotThrow()
    {
        double[] source = [];
        double[] output = [];

        Trix.Batch(source.AsSpan(), output.AsSpan(), 3);

        Assert.True(output.Length == 0);
    }

    [Fact]
    public void Batch_Span_SingleElement()
    {
        double[] source = [100.0];
        double[] output = new double[1];

        Trix.Batch(source.AsSpan(), output.AsSpan(), 5);

        // First element output = 0 (no previous EMA3)
        Assert.Equal(0.0, output[0]);
    }

    [Fact]
    public void Batch_Span_LargeData_DoesNotStackOverflow()
    {
        const int count = 10_000;
        double[] source = new double[count];
        double[] output = new double[count];
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 42);

        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next(isNew: true);
            source[i] = bar.Close;
        }

        Trix.Batch(source.AsSpan(), output.AsSpan(), 14);

        // Should produce finite results
        Assert.True(double.IsFinite(output[^1]));
    }

    [Fact]
    public void Batch_Span_NaN_HandlesGracefully()
    {
        double[] source = new double[20];
        double[] output = new double[20];
        var gbm = new GBM(startPrice: 100, seed: 42);

        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            source[i] = bar.Close;
        }

        // Inject NaN at indices 5, 10, 15
        source[5] = double.NaN;
        source[10] = double.NaN;
        source[15] = double.NaN;

        Trix.Batch(source.AsSpan(), output.AsSpan(), 3);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output[{i}] is not finite");
        }
    }
}

// ── H) Chainability ────────────────────────────────────────────────────────
public sealed class TrixEventTests
{
    [Fact]
    public void Chainability_Works()
    {
        var trix1 = new Trix(10);
        var trix2 = new Trix(trix1, 5);

        trix1.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(double.IsFinite(trix2.Last.Value));
    }

    [Fact]
    public void EventChaining_ProducesResults()
    {
        var source = new TSeries();
        var trix = new Trix(source, 5);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 42);

        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            source.Add(bar.Time, bar.Close);
        }

        Assert.True(double.IsFinite(trix.Last.Value));
        Assert.True(trix.IsHot);
    }

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var trix = new Trix(5);
        int eventCount = 0;

        trix.Pub += HandleEvent;

        for (int i = 0; i < 10; i++)
        {
            trix.Update(new TValue(DateTime.UtcNow, 100 + i));
        }

        Assert.Equal(10, eventCount);

        trix.Pub -= HandleEvent;

        void HandleEvent(object? sender, in TValueEventArgs e)
        {
            eventCount++;
        }
    }
}

// ── Extra: Batch Tests ──────────────────────────────────────────────────────
public sealed class TrixBatchTests
{
    private static TSeries GenerateCloseSeries(int count, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: seed);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        return bars.Close;
    }

    [Fact]
    public void Batch_TSeries_ReturnsCorrectCount()
    {
        var series = GenerateCloseSeries(50);
        var result = Trix.Batch(series, 10);
        Assert.Equal(50, result.Count);
    }

    [Fact]
    public void Batch_TSeries_PreservesTimestamps()
    {
        var series = GenerateCloseSeries(20);
        var result = Trix.Batch(series, 5);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(series[i].Time, result[i].Time);
        }
    }

    [Fact]
    public void Calculate_ReturnsIndicatorAndResults()
    {
        var series = GenerateCloseSeries(30);
        var (results, indicator) = Trix.Calculate(series, 5);

        Assert.NotNull(indicator);
        Assert.Equal(30, results.Count);
        Assert.Equal(5, indicator.Period);
        Assert.True(indicator.IsHot);
    }
}

// ── Extra: Reset Tests ──────────────────────────────────────────────────────
public sealed class TrixResetTests
{
    [Fact]
    public void Reset_ClearsState()
    {
        var trix = new Trix(5);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 42);

        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            trix.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(trix.IsHot);

        trix.Reset();

        Assert.False(trix.IsHot);
        Assert.Equal(0, trix.Last.Value);
    }

    [Fact]
    public void Reset_AcceptsNewValues()
    {
        var trix = new Trix(5);
        var gbm = new GBM(startPrice: 100, seed: 42);

        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            trix.Update(new TValue(bar.Time, bar.Close));
        }

        double valueBefore = trix.Last.Value;
        trix.Reset();

        trix.Update(new TValue(DateTime.UtcNow, 50));
        Assert.Equal(0, trix.Last.Value); // First bar after reset = 0

        trix.Update(new TValue(DateTime.UtcNow, 60));
        Assert.NotEqual(0, trix.Last.Value);
        Assert.NotEqual(valueBefore, trix.Last.Value);
    }
}

// ── Extra: Prime Tests ──────────────────────────────────────────────────────
public sealed class TrixPrimeTests
{
    [Fact]
    public void Prime_SetsUpState()
    {
        var trix = new Trix(5);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 42);
        double[] data = new double[20];

        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            data[i] = bar.Close;
        }

        trix.Prime(data.AsSpan());

        Assert.True(trix.IsHot);
        Assert.True(double.IsFinite(trix.Last.Value));
    }

    [Fact]
    public void Prime_ThenUpdate_ProducesValidResults()
    {
        var trix = new Trix(5);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 42);
        double[] data = new double[20];

        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            data[i] = bar.Close;
        }

        trix.Prime(data.AsSpan());

        // Post-prime updates should work normally
        var result = trix.Update(new TValue(DateTime.UtcNow, 110));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_TSeries_RestoresStreamingState()
    {
        var trix = new Trix(5);
        var series = new TSeries();
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 42);

        for (int i = 0; i < 30; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        var batchResult = trix.Update(series);

        // After Update(TSeries), indicator should be hot with correct last value
        Assert.True(trix.IsHot);
        Assert.Equal(batchResult.Last.Value, trix.Last.Value, 1e-10);

        // Subsequent streaming updates should work
        var nextBar = gbm.Next(isNew: true);
        var nextResult = trix.Update(new TValue(nextBar.Time, nextBar.Close));
        Assert.True(double.IsFinite(nextResult.Value));
    }
}
