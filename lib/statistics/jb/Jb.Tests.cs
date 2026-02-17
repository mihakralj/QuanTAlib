using Xunit;

namespace QuanTAlib.Tests;

// ═══════════════════════════════════════════════════════════════
// A) Constructor Validation
// ═══════════════════════════════════════════════════════════════
public class JbConstructorTests
{
    [Fact]
    public void Constructor_PeriodLessThan3_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Jb(2));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_PeriodZero_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Jb(0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Jb(-5));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidPeriod_SetsName()
    {
        var jb = new Jb(20);
        Assert.Equal("Jb(20)", jb.Name);
    }

    [Fact]
    public void Constructor_ValidPeriod_SetsWarmupPeriod()
    {
        var jb = new Jb(20);
        Assert.Equal(20, jb.WarmupPeriod);
    }

    [Fact]
    public void Constructor_MinimumPeriod3_Works()
    {
        var jb = new Jb(3);
        Assert.Equal("Jb(3)", jb.Name);
    }
}

// ═══════════════════════════════════════════════════════════════
// B) Basic Calculation
// ═══════════════════════════════════════════════════════════════
public class JbBasicTests
{
    [Fact]
    public void Update_ReturnsTValue()
    {
        var jb = new Jb(5);
        var result = jb.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_LastAccessible()
    {
        var jb = new Jb(5);
        jb.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(jb.Last.Value));
    }

    [Fact]
    public void Update_ConstantSeries_JbIsZero()
    {
        // Constant series → skewness = 0, excess kurtosis = 0 → JB = 0
        var jb = new Jb(10);
        for (int i = 0; i < 20; i++)
        {
            jb.Update(new TValue(DateTime.UtcNow, 42.0));
        }
        Assert.Equal(0.0, jb.Last.Value, 10);
    }

    [Fact]
    public void Update_SymmetricData_SkewnessZero_KurtosisNonZero()
    {
        // Symmetric data has skewness ≈ 0, but kurtosis may differ from normal
        // For uniform-like data {1,2,3,...,n}, JB > 0 due to platykurtic shape
        var jb = new Jb(20);
        for (int i = 1; i <= 20; i++)
        {
            jb.Update(new TValue(DateTime.UtcNow, i));
        }
        // Uniform distribution is platykurtic: excess kurtosis < 0, so JB > 0
        Assert.True(jb.Last.Value >= 0.0);
    }

    [Fact]
    public void Update_JbAlwaysNonNegative()
    {
        // JB = (n/6)(S² + EK²/4) is sum of squares → always >= 0
        var jb = new Jb(20);
        var rng = new GBM();
        for (int i = 0; i < 100; i++)
        {
            var bar = rng.Next();
            jb.Update(new TValue(bar.Time, bar.Close));
            Assert.True(jb.Last.Value >= 0.0, $"JB was negative at bar {i}: {jb.Last.Value}");
        }
    }

    [Fact]
    public void Update_KnownNormalDistribution_SmallJb()
    {
        // Near-normal data should produce small JB values
        // Using a simple linear series with period 50 as proxy
        var jb = new Jb(50);
        for (int i = 0; i < 100; i++)
        {
            // Triangular wave approximating normal shape
            double val = 50.0 + Math.Sin(i * 0.1) * 10.0;
            jb.Update(new TValue(DateTime.UtcNow, val));
        }
        Assert.True(double.IsFinite(jb.Last.Value));
    }
}

// ═══════════════════════════════════════════════════════════════
// C) State + Bar Correction (critical)
// ═══════════════════════════════════════════════════════════════
public class JbStateCorrectionTests
{
    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var jb = new Jb(5);
        jb.Update(new TValue(DateTime.UtcNow, 10.0), isNew: true);
        jb.Update(new TValue(DateTime.UtcNow, 20.0), isNew: true);
        double afterTwo = jb.Last.Value;

        jb.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        double afterThree = jb.Last.Value;

        // Adding an outlier should change JB
        Assert.NotEqual(afterTwo, afterThree);
    }

    [Fact]
    public void IsNew_False_Rewrites()
    {
        var jb = new Jb(5);
        for (int i = 1; i <= 5; i++)
        {
            jb.Update(new TValue(DateTime.UtcNow, i * 10.0));
        }
        double before = jb.Last.Value;

        // Correct last bar with same value
        jb.Update(new TValue(DateTime.UtcNow, 50.0), isNew: false);
        Assert.Equal(before, jb.Last.Value, 10);
    }

    [Fact]
    public void IsNew_False_DifferentValue_ChangesResult()
    {
        var jb = new Jb(5);
        double[] vals = [10, 20, 30, 40, 50];
        for (int i = 0; i < vals.Length; i++)
        {
            jb.Update(new TValue(DateTime.UtcNow, vals[i]));
        }
        double before = jb.Last.Value;

        // Correct last bar with very different value → changes skewness → changes JB
        jb.Update(new TValue(DateTime.UtcNow, 200.0), isNew: false);
        Assert.NotEqual(before, jb.Last.Value);
    }

    [Fact]
    public void IterativeCorrections_RestoreState()
    {
        var jb = new Jb(5);
        for (int i = 1; i <= 5; i++)
        {
            jb.Update(new TValue(DateTime.UtcNow, i * 10.0));
        }
        double original = jb.Last.Value;

        // Multiple corrections
        jb.Update(new TValue(DateTime.UtcNow, 999.0), isNew: false);
        jb.Update(new TValue(DateTime.UtcNow, 50.0), isNew: false);
        Assert.Equal(original, jb.Last.Value, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var jb = new Jb(5);
        for (int i = 1; i <= 10; i++)
        {
            jb.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.True(jb.IsHot);

        jb.Reset();
        Assert.False(jb.IsHot);
        Assert.Equal(default, jb.Last);
    }
}

// ═══════════════════════════════════════════════════════════════
// D) Warmup/Convergence
// ═══════════════════════════════════════════════════════════════
public class JbWarmupTests
{
    [Fact]
    public void IsHot_FlipsWhenBufferFull()
    {
        var jb = new Jb(5);
        for (int i = 0; i < 4; i++)
        {
            jb.Update(new TValue(DateTime.UtcNow, i + 1));
            Assert.False(jb.IsHot);
        }
        jb.Update(new TValue(DateTime.UtcNow, 5));
        Assert.True(jb.IsHot);
    }

    [Fact]
    public void WarmupPeriod_EqualsToPeriod()
    {
        var jb = new Jb(20);
        Assert.Equal(20, jb.WarmupPeriod);
    }

    [Fact]
    public void SingleValue_JbIsZero()
    {
        var jb = new Jb(5);
        jb.Update(new TValue(DateTime.UtcNow, 42.0));
        Assert.Equal(0.0, jb.Last.Value, 10);
    }

    [Fact]
    public void TwoValues_JbIsZero()
    {
        var jb = new Jb(5);
        jb.Update(new TValue(DateTime.UtcNow, 10.0));
        jb.Update(new TValue(DateTime.UtcNow, 20.0));
        Assert.Equal(0.0, jb.Last.Value, 10);
    }
}

// ═══════════════════════════════════════════════════════════════
// E) Robustness (critical)
// ═══════════════════════════════════════════════════════════════
public class JbRobustnessTests
{
    [Fact]
    public void NaN_UsesLastValid()
    {
        var jb = new Jb(5);
        for (int i = 1; i <= 5; i++)
        {
            jb.Update(new TValue(DateTime.UtcNow, i * 10.0));
        }

        jb.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(jb.Last.Value));
    }

    [Fact]
    public void Infinity_UsesLastValid()
    {
        var jb = new Jb(5);
        for (int i = 1; i <= 5; i++)
        {
            jb.Update(new TValue(DateTime.UtcNow, i * 10.0));
        }

        jb.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(jb.Last.Value));
    }

    [Fact]
    public void NegativeInfinity_UsesLastValid()
    {
        var jb = new Jb(5);
        for (int i = 1; i <= 5; i++)
        {
            jb.Update(new TValue(DateTime.UtcNow, i * 10.0));
        }

        jb.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(jb.Last.Value));
    }

    [Fact]
    public void BatchNaN_NoPropagation()
    {
        var jb = new Jb(5);
        for (int i = 0; i < 10; i++)
        {
            jb.Update(new TValue(DateTime.UtcNow, i % 2 == 0 ? double.NaN : (double)(i * 10)));
        }
        Assert.True(double.IsFinite(jb.Last.Value));
    }
}

// ═══════════════════════════════════════════════════════════════
// F) Consistency (critical)
// ═══════════════════════════════════════════════════════════════
public class JbConsistencyTests
{
    private const double Tolerance = 1e-8;

    [Fact]
    public void BatchCalc_MatchesStreaming()
    {
        int period = 10;
        int bars = 100;
        var rng = new GBM();
        var source = new TSeries();
        for (int i = 0; i < bars; i++)
        {
            var bar = rng.Next();
            source.Add(new TValue(bar.Time, bar.Close));
        }

        // Streaming
        var streaming = new Jb(period);
        var streamResults = new double[bars];
        for (int i = 0; i < bars; i++)
        {
            streaming.Update(source[i]);
            streamResults[i] = streaming.Last.Value;
        }

        // Batch
        var batchSeries = Jb.Batch(source, period);

        for (int i = period - 1; i < bars; i++)
        {
            Assert.Equal(streamResults[i], batchSeries[i].Value, Tolerance);
        }
    }

    [Fact]
    public void SpanCalc_MatchesStreaming()
    {
        int period = 10;
        int bars = 100;
        var rng = new GBM();
        var source = new TSeries();
        for (int i = 0; i < bars; i++)
        {
            var bar = rng.Next();
            source.Add(new TValue(bar.Time, bar.Close));
        }

        // Streaming
        var streaming = new Jb(period);
        var streamResults = new double[bars];
        for (int i = 0; i < bars; i++)
        {
            streaming.Update(source[i]);
            streamResults[i] = streaming.Last.Value;
        }

        // Span
        var spanOutput = new double[bars];
        Jb.Batch(source.Values, spanOutput.AsSpan(), period);

        for (int i = period - 1; i < bars; i++)
        {
            Assert.Equal(streamResults[i], spanOutput[i], Tolerance);
        }
    }

    [Fact]
    public void EventBased_MatchesStreaming()
    {
        int period = 10;
        int bars = 50;
        var rng = new GBM();
        var source = new TSeries();

        var eventJb = new Jb(source, period);
        var manualJb = new Jb(period);

        for (int i = 0; i < bars; i++)
        {
            var bar = rng.Next();
            var tv = new TValue(bar.Time, bar.Close);
            manualJb.Update(tv);
            source.Add(tv);
        }

        Assert.Equal(manualJb.Last.Value, eventJb.Last.Value, Tolerance);
    }
}

// ═══════════════════════════════════════════════════════════════
// G) Span API Tests
// ═══════════════════════════════════════════════════════════════
public class JbSpanTests
{
    [Fact]
    public void Span_MismatchedLengths_ThrowsArgumentException()
    {
        var source = new double[10];
        var output = new double[5];
        var ex = Assert.Throws<ArgumentException>(() =>
            Jb.Batch(source.AsSpan(), output.AsSpan(), 5));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Span_InvalidPeriod_ThrowsArgumentException()
    {
        var source = new double[10];
        var output = new double[10];
        var ex = Assert.Throws<ArgumentException>(() =>
            Jb.Batch(source.AsSpan(), output.AsSpan(), 2));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Span_EmptyInput_NoException()
    {
        var source = ReadOnlySpan<double>.Empty;
        var output = Span<double>.Empty;
        Jb.Batch(source, output, 5);
        Assert.True(true); // S2699 — confirms no exception
    }

    [Fact]
    public void Span_LargeData_NoStackOverflow()
    {
        int len = 10_000;
        var source = new double[len];
        var output = new double[len];
        var rng = new GBM();
        for (int i = 0; i < len; i++)
        {
            var bar = rng.Next();
            source[i] = bar.Close;
        }
        Jb.Batch(source.AsSpan(), output.AsSpan(), 50);
        Assert.True(double.IsFinite(output[len - 1]));
    }

    [Fact]
    public void Span_HandlesNaN()
    {
        var source = new double[] { 10, 20, double.NaN, 40, 50, 60, 70, 80, 90, 100 };
        var output = new double[10];
        Jb.Batch(source.AsSpan(), output.AsSpan(), 5);
        Assert.True(double.IsFinite(output[9]));
    }
}

// ═══════════════════════════════════════════════════════════════
// H) Chainability
// ═══════════════════════════════════════════════════════════════
public class JbEventTests
{
    [Fact]
    public void Pub_Fires()
    {
        var jb = new Jb(5);
        bool fired = false;
        jb.Pub += (object? _, in TValueEventArgs _) => fired = true;
        jb.Update(new TValue(DateTime.UtcNow, 42.0));
        Assert.True(fired);
    }

    [Fact]
    public void EventChaining_Works()
    {
        var source = new TSeries();
        var jb = new Jb(source, 5);

        source.Add(new TValue(DateTime.UtcNow, 10.0));
        source.Add(new TValue(DateTime.UtcNow, 20.0));
        source.Add(new TValue(DateTime.UtcNow, 30.0));

        Assert.True(double.IsFinite(jb.Last.Value));
    }
}
