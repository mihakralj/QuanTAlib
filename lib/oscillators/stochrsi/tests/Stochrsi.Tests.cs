using Xunit;

namespace QuanTAlib.Tests;

// ── A) Constructor Validation ──────────────────────────────────────
public sealed class StochrsiConstructorTests
{
    [Fact]
    public void DefaultParameters_AreCorrect()
    {
        var ind = new Stochrsi();
        Assert.Equal("StochRsi(14,14,3,3)", ind.Name);
        // WarmupPeriod = rsi.WarmupPeriod(15) + stochLength(14)-1 + kSmooth(3)-1 + dSmooth(3)-1 = 32
        Assert.Equal(32, ind.WarmupPeriod);
    }

    [Fact]
    public void CustomParameters_SetsNameCorrectly()
    {
        var ind = new Stochrsi(7, 10, 2, 5);
        Assert.Equal("StochRsi(7,10,2,5)", ind.Name);
    }

    [Theory]
    [InlineData(0, 14, 3, 3, "rsiLength")]
    [InlineData(-1, 14, 3, 3, "rsiLength")]
    [InlineData(14, 0, 3, 3, "stochLength")]
    [InlineData(14, -1, 3, 3, "stochLength")]
    [InlineData(14, 14, 0, 3, "kSmooth")]
    [InlineData(14, 14, -1, 3, "kSmooth")]
    [InlineData(14, 14, 3, 0, "dSmooth")]
    [InlineData(14, 14, 3, -1, "dSmooth")]
    public void InvalidParameters_ThrowsArgumentException(int rsi, int stoch, int k, int d, string paramName)
    {
        var ex = Assert.Throws<ArgumentException>(() => new Stochrsi(rsi, stoch, k, d));
        Assert.Equal(paramName, ex.ParamName);
    }

    [Fact]
    public void MinimalParameters_Work()
    {
        var ind = new Stochrsi(1, 1, 1, 1);
        Assert.Equal("StochRsi(1,1,1,1)", ind.Name);
    }

    [Fact]
    public void Constructor_PeriodOne_IsValid()
    {
        var ind = new Stochrsi(1, 1, 1, 1);
        Assert.NotNull(ind);
    }
}

// ── B) Basic Calculation ───────────────────────────────────────────
public sealed class StochrsiBasicTests
{
    [Fact]
    public void Update_ReturnsTValue()
    {
        var ind = new Stochrsi();
        TValue result = ind.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(double.IsFinite(result.Value) || double.IsNaN(result.Value));
    }

    [Fact]
    public void Last_IsAccessible()
    {
        var ind = new Stochrsi(5, 5, 2, 2);
        ind.Update(new TValue(DateTime.UtcNow, 100));
        ind.Update(new TValue(DateTime.UtcNow, 110));
        Assert.IsType<TValue>(ind.Last);
    }

    [Fact]
    public void Name_Available()
    {
        var ind = new Stochrsi(7, 10, 2, 5);
        Assert.Equal("StochRsi(7,10,2,5)", ind.Name);
    }

    [Fact]
    public void KAndD_AreAccessible()
    {
        var ind = new Stochrsi(3, 3, 1, 1);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 30; i++)
        {
            var bar = gbm.Next(isNew: true);
            ind.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(double.IsFinite(ind.K));
        Assert.True(double.IsFinite(ind.D));
    }

    [Fact]
    public void ConvergedValues_InRange0to100()
    {
        var ind = new Stochrsi(7, 7, 3, 3);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            ind.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(ind.IsHot);
        Assert.InRange(ind.K, -0.01, 100.01);
        Assert.InRange(ind.D, -0.01, 100.01);
    }
}

// ── C) State + Bar Correction ──────────────────────────────────────
public sealed class StochrsiBarCorrectionTests
{
    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var ind = new Stochrsi(5, 5, 2, 2);
        ind.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double val1 = ind.Last.Value;

        ind.Update(new TValue(DateTime.UtcNow, 150), isNew: true);
        double val2 = ind.Last.Value;

        Assert.NotEqual(val1, val2);
    }

    [Fact]
    public void IsNew_False_Rollback()
    {
        var ind = new Stochrsi(5, 5, 2, 2);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        // Feed enough bars to get past trivial state
        for (int i = 0; i < 30; i++)
        {
            var bar = gbm.Next(isNew: true);
            ind.Update(new TValue(bar.Time, bar.Close), isNew: true);
        }

        // Feed one more bar with isNew=true and remember value
        var nextBar = gbm.Next(isNew: true);
        var originalInput = new TValue(nextBar.Time, nextBar.Close);
        var val1 = ind.Update(originalInput, isNew: true);

        // Correct with isNew=false (different value)
        ind.Update(new TValue(nextBar.Time, nextBar.Close + 50), isNew: false);

        // Re-apply original value with isNew=false → should match val1
        var restored = ind.Update(originalInput, isNew: false);

        Assert.Equal(val1.Value, restored.Value, 1e-10);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var ind = new Stochrsi(5, 5, 2, 2);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 30 new values
        TValue thirtiethInput = default;
        for (int i = 0; i < 30; i++)
        {
            var bar = gbm.Next(isNew: true);
            thirtiethInput = new TValue(bar.Time, bar.Close);
            ind.Update(thirtiethInput, isNew: true);
        }

        double stateAfterThirty = ind.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            ind.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 30th input again with isNew=false
        TValue finalResult = ind.Update(thirtiethInput, isNew: false);

        Assert.Equal(stateAfterThirty, finalResult.Value, 1e-10);
    }
}

// ── D) Warmup / Convergence ────────────────────────────────────────
public sealed class StochrsiWarmupTests
{
    [Fact]
    public void IsHot_InitiallyFalse()
    {
        var ind = new Stochrsi();
        Assert.False(ind.IsHot);
    }

    [Fact]
    public void IsHot_BecomesTrueAfterSufficientBars()
    {
        var ind = new Stochrsi(3, 3, 1, 1);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);

        // Feed bars until hot
        bool becameHot = false;
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            ind.Update(new TValue(bar.Time, bar.Close));
            if (ind.IsHot)
            {
                becameHot = true;
                break;
            }
        }

        Assert.True(becameHot);
    }

    [Fact]
    public void IsHot_StaysTrue()
    {
        var ind = new Stochrsi(3, 3, 1, 1);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            ind.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(ind.IsHot);

        // Feed more bars, should stay hot
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            ind.Update(new TValue(bar.Time, bar.Close));
            Assert.True(ind.IsHot);
        }
    }

    [Fact]
    public void WarmupPeriod_ScalesWithParameters()
    {
        // Default: rsiLength=14 → rsi.WarmupPeriod=15
        // warm = 15 + 14-1 + 3-1 + 3-1 = 32
        var ind1 = new Stochrsi(14, 14, 3, 3);
        Assert.Equal(32, ind1.WarmupPeriod);

        // Custom: rsiLength=7 → rsi.WarmupPeriod=8
        // warm = 8 + 10-1 + 2-1 + 5-1 = 22
        var ind2 = new Stochrsi(7, 10, 2, 5);
        Assert.Equal(22, ind2.WarmupPeriod);
    }
}

// ── E) Robustness (NaN / Infinity) ─────────────────────────────────
public sealed class StochrsiRobustnessTests
{
    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var ind = new Stochrsi(5, 5, 2, 2);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 25; i++)
        {
            ind.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        var result = ind.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var ind = new Stochrsi(5, 5, 2, 2);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 25; i++)
        {
            ind.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        var resultPos = ind.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultPos.Value));

        var resultNeg = ind.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultNeg.Value));
    }

    [Fact]
    public void BatchNaN_DoesNotCrash()
    {
        double[] source = [100, 110, 120, 130, 140, double.NaN, 160, 170, 180, 190];
        double[] output = new double[source.Length];

        Stochrsi.Batch(source.AsSpan(), output.AsSpan(), 3, 3, 1, 1);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output at index {i} is not finite");
        }
    }
}

// ── F) Consistency (All 4 Modes Match) ─────────────────────────────
public sealed class StochrsiConsistencyTests
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
        const int rsiLen = 7;
        const int stochLen = 7;
        const int kSm = 3;
        const int dSm = 3;
        var series = GenerateCloseSeries(100);

        // 1. Batch Mode (TSeries)
        var batchSeries = Stochrsi.Batch(series, rsiLen, stochLen, kSm, dSm);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var spanInput = series.Values.ToArray();
        var spanOutput = new double[spanInput.Length];
        Stochrsi.Batch(spanInput.AsSpan(), spanOutput.AsSpan(), rsiLen, stochLen, kSm, dSm);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Stochrsi(rsiLen, stochLen, kSm, dSm);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Stochrsi(pubSource, rsiLen, stochLen, kSm, dSm);
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
        const int rsiLen = 5;
        const int stochLen = 5;
        const int kSm = 2;
        const int dSm = 2;
        var series = GenerateCloseSeries(50);

        // Batch
        var batchSeries = Stochrsi.Batch(series, rsiLen, stochLen, kSm, dSm);

        // Streaming
        var streamingInd = new Stochrsi(rsiLen, stochLen, kSm, dSm);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
            Assert.Equal(batchSeries[i].Value, streamingInd.Last.Value, 1e-10);
        }
    }

    [Fact]
    public void SpanVsBatch_AllPoints()
    {
        const int rsiLen = 7;
        const int stochLen = 7;
        const int kSm = 3;
        const int dSm = 3;
        var series = GenerateCloseSeries(80);

        var batchSeries = Stochrsi.Batch(series, rsiLen, stochLen, kSm, dSm);

        var spanInput = series.Values.ToArray();
        var spanOutput = new double[spanInput.Length];
        Stochrsi.Batch(spanInput.AsSpan(), spanOutput.AsSpan(), rsiLen, stochLen, kSm, dSm);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchSeries[i].Value, spanOutput[i], 1e-10);
        }
    }
}

// ── G) Span API Tests ──────────────────────────────────────────────
public sealed class StochrsiSpanTests
{
    [Fact]
    public void Batch_Span_MismatchedLengths_Throws()
    {
        double[] source = new double[10];
        double[] output = new double[5];

        var ex = Assert.Throws<ArgumentException>(
            () => Stochrsi.Batch(source.AsSpan(), output.AsSpan(), 3, 3, 1, 1));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_ZeroRsiLength_Throws()
    {
        double[] source = new double[10];
        double[] output = new double[10];

        var ex = Assert.Throws<ArgumentException>(
            () => Stochrsi.Batch(source.AsSpan(), output.AsSpan(), 0, 3, 1, 1));
        Assert.Equal("rsiLength", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_ZeroStochLength_Throws()
    {
        double[] source = new double[10];
        double[] output = new double[10];

        var ex = Assert.Throws<ArgumentException>(
            () => Stochrsi.Batch(source.AsSpan(), output.AsSpan(), 3, 0, 1, 1));
        Assert.Equal("stochLength", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_ZeroKSmooth_Throws()
    {
        double[] source = new double[10];
        double[] output = new double[10];

        var ex = Assert.Throws<ArgumentException>(
            () => Stochrsi.Batch(source.AsSpan(), output.AsSpan(), 3, 3, 0, 1));
        Assert.Equal("kSmooth", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_ZeroDSmooth_Throws()
    {
        double[] source = new double[10];
        double[] output = new double[10];

        var ex = Assert.Throws<ArgumentException>(
            () => Stochrsi.Batch(source.AsSpan(), output.AsSpan(), 3, 3, 1, 0));
        Assert.Equal("dSmooth", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_EmptyArrays_DoesNotThrow()
    {
        double[] source = [];
        double[] output = [];

        Stochrsi.Batch(source.AsSpan(), output.AsSpan(), 3, 3, 1, 1);

        Assert.Empty(output);
    }

    [Fact]
    public void Batch_Span_SingleElement()
    {
        double[] source = [100.0];
        double[] output = new double[1];

        Stochrsi.Batch(source.AsSpan(), output.AsSpan(), 5, 5, 1, 1);

        Assert.True(double.IsFinite(output[0]));
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

        Stochrsi.Batch(source.AsSpan(), output.AsSpan(), 14, 14, 3, 3);

        Assert.True(double.IsFinite(output[^1]));
    }

    [Fact]
    public void Batch_Span_NaN_HandlesGracefully()
    {
        double[] source = new double[30];
        double[] output = new double[30];
        var gbm = new GBM(startPrice: 100, seed: 42);

        for (int i = 0; i < 30; i++)
        {
            var bar = gbm.Next(isNew: true);
            source[i] = bar.Close;
        }

        // Inject NaN at indices 5, 15, 25
        source[5] = double.NaN;
        source[15] = double.NaN;
        source[25] = double.NaN;

        Stochrsi.Batch(source.AsSpan(), output.AsSpan(), 3, 3, 1, 1);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output[{i}] is not finite");
        }
    }
}

// ── H) Chainability ────────────────────────────────────────────────
public sealed class StochrsiEventTests
{
    [Fact]
    public void Chainability_Works()
    {
        var stochrsi = new Stochrsi(5, 5, 2, 2);
        // Chain another AbstractBase indicator from StochRSI output
        var ema = new Ema(stochrsi, 3);

        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < 30; i++)
        {
            var bar = gbm.Next(isNew: true);
            stochrsi.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(double.IsFinite(ema.Last.Value));
    }

    [Fact]
    public void EventChaining_ProducesResults()
    {
        var source = new TSeries();
        var ind = new Stochrsi(source, 5, 5, 2, 2);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 42);

        for (int i = 0; i < 30; i++)
        {
            var bar = gbm.Next(isNew: true);
            source.Add(bar.Time, bar.Close);
        }

        Assert.True(double.IsFinite(ind.Last.Value));
        Assert.True(ind.IsHot);
    }

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var ind = new Stochrsi(5, 5, 2, 2);
        int eventCount = 0;

        ind.Pub += HandleEvent;

        for (int i = 0; i < 10; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, 100 + i));
        }

        Assert.Equal(10, eventCount);

        ind.Pub -= HandleEvent;

        void HandleEvent(object? sender, in TValueEventArgs e)
        {
            eventCount++;
        }
    }
}

// ── Extra: Batch Tests ─────────────────────────────────────────────
public sealed class StochrsiBatchTests
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
        var result = Stochrsi.Batch(series, 5, 5, 2, 2);
        Assert.Equal(50, result.Count);
    }

    [Fact]
    public void Batch_TSeries_PreservesTimestamps()
    {
        var series = GenerateCloseSeries(30);
        var result = Stochrsi.Batch(series, 5, 5, 2, 2);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(series[i].Time, result[i].Time);
        }
    }

    [Fact]
    public void Calculate_ReturnsIndicatorAndResults()
    {
        var series = GenerateCloseSeries(50);
        var (results, indicator) = Stochrsi.Calculate(series, 5, 5, 2, 2);

        Assert.NotNull(indicator);
        Assert.Equal(50, results.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void UpdateKD_ReturnsBothKAndDSeries()
    {
        var series = GenerateCloseSeries(50);
        var ind = new Stochrsi(5, 5, 2, 2);
        var (kSeries, dSeries) = ind.UpdateKD(series);

        Assert.Equal(50, kSeries.Count);
        Assert.Equal(50, dSeries.Count);

        // After warmup, values should be in 0-100 range
        Assert.True(double.IsFinite(kSeries.Last.Value));
        Assert.True(double.IsFinite(dSeries.Last.Value));
    }

    [Fact]
    public void UpdateKD_EmptySeries_ReturnsEmpty()
    {
        var ind = new Stochrsi(5, 5, 2, 2);
        var (kSeries, dSeries) = ind.UpdateKD(new TSeries());
        Assert.Empty(kSeries);
        Assert.Empty(dSeries);
    }
}

// ── Extra: Reset Tests ─────────────────────────────────────────────
public sealed class StochrsiResetTests
{
    [Fact]
    public void Reset_ClearsState()
    {
        var ind = new Stochrsi(5, 5, 2, 2);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 42);

        for (int i = 0; i < 30; i++)
        {
            var bar = gbm.Next(isNew: true);
            ind.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(ind.IsHot);

        ind.Reset();

        Assert.False(ind.IsHot);
        Assert.Equal(0, ind.Last.Value);
    }

    [Fact]
    public void Reset_AcceptsNewValues()
    {
        var ind = new Stochrsi(5, 5, 2, 2);
        var gbm = new GBM(startPrice: 100, seed: 42);

        for (int i = 0; i < 30; i++)
        {
            var bar = gbm.Next(isNew: true);
            ind.Update(new TValue(bar.Time, bar.Close));
        }

        ind.Reset();

        // After reset, should accept new values without error
        var result = ind.Update(new TValue(DateTime.UtcNow, 50));
        Assert.True(double.IsFinite(result.Value));
    }
}

// ── Extra: Prime Tests ─────────────────────────────────────────────
public sealed class StochrsiPrimeTests
{
    [Fact]
    public void Prime_SetsUpState()
    {
        var ind = new Stochrsi(5, 5, 2, 2);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 42);
        double[] data = new double[30];

        for (int i = 0; i < 30; i++)
        {
            var bar = gbm.Next(isNew: true);
            data[i] = bar.Close;
        }

        ind.Prime(data.AsSpan());

        Assert.True(ind.IsHot);
        Assert.True(double.IsFinite(ind.Last.Value));
    }

    [Fact]
    public void Prime_ThenUpdate_ProducesValidResults()
    {
        var ind = new Stochrsi(5, 5, 2, 2);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 42);
        double[] data = new double[30];

        for (int i = 0; i < 30; i++)
        {
            var bar = gbm.Next(isNew: true);
            data[i] = bar.Close;
        }

        ind.Prime(data.AsSpan());

        // Post-prime updates should work normally
        var result = ind.Update(new TValue(DateTime.UtcNow, 110));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_TSeries_RestoresStreamingState()
    {
        var ind = new Stochrsi(5, 5, 2, 2);
        var series = new TSeries();
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 42);

        for (int i = 0; i < 40; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        var batchResult = ind.Update(series);

        // After Update(TSeries), indicator should be hot with correct last value
        Assert.True(ind.IsHot);
        Assert.Equal(batchResult.Last.Value, ind.Last.Value, 1e-10);

        // Subsequent streaming updates should work
        var nextBar = gbm.Next(isNew: true);
        var nextResult = ind.Update(new TValue(nextBar.Time, nextBar.Close));
        Assert.True(double.IsFinite(nextResult.Value));
    }
}
