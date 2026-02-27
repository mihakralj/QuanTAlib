using Xunit;

namespace QuanTAlib.Tests;

// ═══════════════════════════════════════════════════════════════
// A) Constructor Validation
// ═══════════════════════════════════════════════════════════════
public class StderrConstructorTests
{
    [Fact]
    public void Constructor_PeriodLessThan3_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Stderr(2));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_PeriodZero_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Stderr(0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Stderr(-5));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_MinimumPeriod3_Works()
    {
        var se = new Stderr(3);
        Assert.Equal("Stderr(3)", se.Name);
    }

    [Fact]
    public void Constructor_ValidPeriod_SetsName()
    {
        var se = new Stderr(14);
        Assert.Equal("Stderr(14)", se.Name);
    }

    [Fact]
    public void Constructor_ValidPeriod_SetsWarmupPeriod()
    {
        var se = new Stderr(14);
        Assert.Equal(14, se.WarmupPeriod);
    }
}

// ═══════════════════════════════════════════════════════════════
// B) Basic Calculation
// ═══════════════════════════════════════════════════════════════
public class StderrBasicTests
{
    [Fact]
    public void Update_ReturnsTValue()
    {
        var se = new Stderr(5);
        var result = se.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_LastAccessible()
    {
        var se = new Stderr(5);
        se.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(se.Last.Value));
    }

    [Fact]
    public void Update_LinearSeries_StderrNearZero()
    {
        // Perfect linear series → residuals = 0 → SE = 0
        var se = new Stderr(10);
        for (int i = 0; i < 10; i++)
        {
            se.Update(new TValue(DateTime.UtcNow, i * 2.0 + 5.0));
        }
        Assert.Equal(0.0, se.Last.Value, precision: 8);
    }

    [Fact]
    public void Update_ConstantSeries_StderrIsZero()
    {
        // Constant data → horizontal line → all residuals = 0
        var se = new Stderr(10);
        for (int i = 0; i < 15; i++)
        {
            se.Update(new TValue(DateTime.UtcNow, 42.0));
        }
        Assert.Equal(0.0, se.Last.Value, precision: 8);
    }

    [Fact]
    public void Update_StderrAlwaysNonNegative()
    {
        var se = new Stderr(14);
        var gbm = new GBM();
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next();
            se.Update(new TValue(bar.Time, bar.Close));
            Assert.True(se.Last.Value >= 0.0, $"Stderr was negative at bar {i}: {se.Last.Value}");
        }
    }

    [Fact]
    public void Update_KnownData_Manual()
    {
        // x=0,1,2; y=2,4,5
        // slope = (3*14 - 3*11) / (3*5 - 9) = (42-33)/(15-9) = 9/6 = 1.5
        // intercept = (11 - 1.5*3)/3 = (11-4.5)/3 = 6.5/3 ≈ 2.1667
        // residuals: y0=2, yhat0=2.1667 → -0.1667
        //            y1=4, yhat1=3.6667  → 0.3333
        //            y2=5, yhat2=5.1667  → -0.1667
        // SSR = 0.02778 + 0.11111 + 0.02778 = 0.16667
        // SE = sqrt(0.16667 / 1) = 0.4082...
        var se = new Stderr(3);
        se.Update(new TValue(DateTime.UtcNow, 2.0));
        se.Update(new TValue(DateTime.UtcNow, 4.0));
        se.Update(new TValue(DateTime.UtcNow, 5.0));
        Assert.Equal(Math.Sqrt(1.0 / 6.0), se.Last.Value, precision: 8);
    }
}

// ═══════════════════════════════════════════════════════════════
// C) State + Bar Correction
// ═══════════════════════════════════════════════════════════════
public class StderrStateTests
{
    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var se = new Stderr(5);
        for (int i = 0; i < 5; i++)
        {
            se.Update(new TValue(DateTime.UtcNow, i * 10.0 + 10.0));
        }
        double after5 = se.Last.Value;

        se.Update(new TValue(DateTime.UtcNow, 999.0), isNew: true);
        Assert.NotEqual(after5, se.Last.Value);
    }

    [Fact]
    public void IsNew_False_UpdatesWithoutAdvancing()
    {
        var se = new Stderr(5);
        for (int i = 0; i < 5; i++)
        {
            se.Update(new TValue(DateTime.UtcNow, i * 10.0 + 10.0));
        }

        se.Update(new TValue(DateTime.UtcNow, 50.0), isNew: true);
        double afterNew = se.Last.Value;

        se.Update(new TValue(DateTime.UtcNow, 60.0), isNew: false);
        Assert.NotEqual(afterNew, se.Last.Value);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var se = new Stderr(5);
        for (int i = 0; i < 15; i++)
        {
            se.Update(new TValue(DateTime.UtcNow, i * 5.0));
        }

        se.Reset();

        Assert.False(se.IsHot);
        Assert.Equal(default, se.Last);
    }
}

// ═══════════════════════════════════════════════════════════════
// D) Warmup / IsHot
// ═══════════════════════════════════════════════════════════════
public class StderrWarmupTests
{
    [Fact]
    public void IsHot_FalseBeforePeriodBars()
    {
        var se = new Stderr(10);
        for (int i = 0; i < 9; i++)
        {
            se.Update(new TValue(DateTime.UtcNow, i + 1.0));
            Assert.False(se.IsHot, $"IsHot should be false at bar {i + 1}");
        }
    }

    [Fact]
    public void IsHot_TrueAfterPeriodBars()
    {
        var se = new Stderr(10);
        for (int i = 0; i < 10; i++)
        {
            se.Update(new TValue(DateTime.UtcNow, i + 1.0));
        }
        Assert.True(se.IsHot);
    }
}

// ═══════════════════════════════════════════════════════════════
// E) Robustness (NaN / Infinity)
// ═══════════════════════════════════════════════════════════════
public class StderrRobustnessTests
{
    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var se = new Stderr(5);
        for (int i = 0; i < 5; i++)
        {
            se.Update(new TValue(DateTime.UtcNow, 10.0 + i));
        }

        se.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(se.Last.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValid()
    {
        var se = new Stderr(5);
        for (int i = 0; i < 5; i++)
        {
            se.Update(new TValue(DateTime.UtcNow, 10.0 + i));
        }

        se.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(se.Last.Value));
    }

    [Fact]
    public void MultipleNaN_ContinuesWithLastValid()
    {
        var se = new Stderr(5);
        for (int i = 0; i < 5; i++)
        {
            se.Update(new TValue(DateTime.UtcNow, 10.0 + i));
        }

        for (int i = 0; i < 5; i++)
        {
            se.Update(new TValue(DateTime.UtcNow, double.NaN));
            Assert.True(double.IsFinite(se.Last.Value));
        }
    }
}

// ═══════════════════════════════════════════════════════════════
// F) Consistency — all 4 API modes must agree
// ═══════════════════════════════════════════════════════════════
public class StderrConsistencyTests
{
    [Fact]
    public void AllModes_ProduceSameResult()
    {
        const int period = 14;
        const int count = 200;
        var gbm = new GBM(seed: 42);
        var series = new TSeries();
        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next();
            series.Add(new TValue(bar.Time, bar.Close));
        }

        // 1. Batch (TSeries)
        var batchResult = Stderr.Batch(series, period);
        double expected = batchResult.Last.Value;

        // 2. Span
        var values = series.Values.ToArray();
        var spanOutput = new double[values.Length];
        Stderr.Batch(values.AsSpan(), spanOutput.AsSpan(), period);
        double spanResult = spanOutput[^1];

        // 3. Streaming
        var streaming = new Stderr(period);
        foreach (var tv in series)
        {
            streaming.Update(tv);
        }
        double streamingResult = streaming.Last.Value;

        // 4. Eventing
        var pubSource = new TSeries();
        var eventing = new Stderr(pubSource, period);
        foreach (var tv in series)
        {
            pubSource.Add(tv);
        }
        double eventingResult = eventing.Last.Value;

        Assert.Equal(expected, spanResult, precision: 9);
        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, eventingResult, precision: 9);
    }

    [Fact]
    public void BatchTSeries_MatchesIterativeUpdate()
    {
        const int period = 10;
        var gbm = new GBM(seed: 7);
        var series = new TSeries();
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next();
            series.Add(new TValue(bar.Time, bar.Close));
        }

        var batchSeries = Stderr.Batch(series, period);

        var streaming = new Stderr(period);
        TSeries streamingSeries = streaming.Update(series);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchSeries[i].Value, streamingSeries[i].Value, precision: 9);
        }
    }
}

// ═══════════════════════════════════════════════════════════════
// G) Span API Tests
// ═══════════════════════════════════════════════════════════════
public class StderrSpanTests
{
    [Fact]
    public void Span_LengthMismatch_ThrowsArgumentException()
    {
        var src = new double[10];
        var dst = new double[9];
        var ex = Assert.Throws<ArgumentException>(() => Stderr.Batch(src.AsSpan(), dst.AsSpan(), 5));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Span_PeriodLessThan3_ThrowsArgumentException()
    {
        var src = new double[10];
        var dst = new double[10];
        var ex = Assert.Throws<ArgumentException>(() => Stderr.Batch(src.AsSpan(), dst.AsSpan(), 2));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Span_EmptyInput_NoThrow()
    {
        var src = Array.Empty<double>();
        var dst = Array.Empty<double>();
        Stderr.Batch(src.AsSpan(), dst.AsSpan(), 5);
        Assert.True(dst.Length == 0); // no throw; destination remains empty
    }

    [Fact]
    public void Span_MatchesTSeriesResult()
    {
        const int period = 7;
        var gbm = new GBM(seed: 99);
        var series = new TSeries();
        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next();
            series.Add(new TValue(bar.Time, bar.Close));
        }

        var batchSeries = Stderr.Batch(series, period);
        var values = series.Values.ToArray();
        var output = new double[values.Length];
        Stderr.Batch(values.AsSpan(), output.AsSpan(), period);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchSeries[i].Value, output[i], precision: 9);
        }
    }

    [Fact]
    public void Span_HandlesNaN()
    {
        var src = new double[] { 1, 2, double.NaN, 4, 5, 6, 7, 8, 9 };
        var dst = new double[src.Length];
        Stderr.Batch(src.AsSpan(), dst.AsSpan(), 4);
        Assert.True(dst.All(double.IsFinite));
    }

    [Fact]
    public void Span_LargeInput_NoStackOverflow()
    {
        const int size = 10_000;
        var src = new double[size];
        var dst = new double[size];
        for (int i = 0; i < size; i++)
        {
            src[i] = i;
        }
        Stderr.Batch(src.AsSpan(), dst.AsSpan(), 20);
        Assert.True(double.IsFinite(dst[^1]));
    }
}

// ═══════════════════════════════════════════════════════════════
// H) Chainability
// ═══════════════════════════════════════════════════════════════
public class StderrChainabilityTests
{
    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var se = new Stderr(5);
        int fired = 0;
        se.Pub += (object? _, in TValueEventArgs _) => fired++;

        for (int i = 0; i < 10; i++)
        {
            se.Update(new TValue(DateTime.UtcNow, i + 1.0));
        }

        Assert.Equal(10, fired);
    }

    [Fact]
    public void EventBasedChaining_Works()
    {
        var source = new TSeries();
        var se = new Stderr(source, 5);

        for (int i = 0; i < 10; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddMinutes(i), (i + 1) * 10.0));
        }

        Assert.True(se.IsHot);
        Assert.True(double.IsFinite(se.Last.Value));
        Assert.True(se.Last.Value >= 0.0);
    }
}
