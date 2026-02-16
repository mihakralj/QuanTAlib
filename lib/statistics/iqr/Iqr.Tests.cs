using Xunit;

namespace QuanTAlib.Tests;

// ═══════════════════════════════════════════════════════════════
// A) Constructor Validation
// ═══════════════════════════════════════════════════════════════
public class IqrConstructorTests
{
    [Fact]
    public void Constructor_PeriodLessThan2_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Iqr(1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_PeriodZero_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Iqr(0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Iqr(-5));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidPeriod_SetsName()
    {
        var iqr = new Iqr(20);
        Assert.Equal("Iqr(20)", iqr.Name);
    }

    [Fact]
    public void Constructor_ValidPeriod_SetsWarmupPeriod()
    {
        var iqr = new Iqr(20);
        Assert.Equal(20, iqr.WarmupPeriod);
    }

    [Fact]
    public void Constructor_MinimumPeriod2_Works()
    {
        var iqr = new Iqr(2);
        Assert.Equal("Iqr(2)", iqr.Name);
    }
}

// ═══════════════════════════════════════════════════════════════
// B) Basic Calculation
// ═══════════════════════════════════════════════════════════════
public class IqrBasicTests
{
    [Fact]
    public void Update_ReturnsTValue()
    {
        var iqr = new Iqr(5);
        var result = iqr.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_LastAccessible()
    {
        var iqr = new Iqr(5);
        iqr.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(0.0, iqr.Last.Value, 0); // single value → IQR = 0
    }

    [Fact]
    public void Update_ConstantSeries_IqrIsZero()
    {
        var iqr = new Iqr(10);
        for (int i = 0; i < 20; i++)
        {
            iqr.Update(new TValue(DateTime.UtcNow, 42.0));
        }
        Assert.Equal(0.0, iqr.Last.Value, 10);
    }

    [Fact]
    public void Update_KnownValues_CorrectIqr()
    {
        // Window: {1, 2, 3, 4, 5} → sorted: [1,2,3,4,5]
        // Q1: rank = 0.25*4 = 1.0 → value[1] = 2.0
        // Q3: rank = 0.75*4 = 3.0 → value[3] = 4.0
        // IQR = 4.0 - 2.0 = 2.0
        var iqr = new Iqr(5);
        for (int i = 1; i <= 5; i++)
        {
            iqr.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.Equal(2.0, iqr.Last.Value, 10);
    }

    [Fact]
    public void Update_KnownValues_Interpolation()
    {
        // Window: {1, 2, 3, 4} → sorted: [1,2,3,4]
        // Q1: rank = 0.25*3 = 0.75 → 1 + 0.75*(2-1) = 1.75
        // Q3: rank = 0.75*3 = 2.25 → 3 + 0.25*(4-3) = 3.25
        // IQR = 3.25 - 1.75 = 1.5
        var iqr = new Iqr(4);
        for (int i = 1; i <= 4; i++)
        {
            iqr.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.Equal(1.5, iqr.Last.Value, 10);
    }

    [Fact]
    public void Update_TwoValues_CorrectIqr()
    {
        // Window: {10, 20} → sorted: [10,20]
        // Q1: rank = 0.25*1 = 0.25 → 10 + 0.25*(20-10) = 12.5
        // Q3: rank = 0.75*1 = 0.75 → 10 + 0.75*(20-10) = 17.5
        // IQR = 17.5 - 12.5 = 5.0
        var iqr = new Iqr(2);
        iqr.Update(new TValue(DateTime.UtcNow, 10.0));
        iqr.Update(new TValue(DateTime.UtcNow, 20.0));
        Assert.Equal(5.0, iqr.Last.Value, 10);
    }
}

// ═══════════════════════════════════════════════════════════════
// C) State + Bar Correction (critical)
// ═══════════════════════════════════════════════════════════════
public class IqrStateCorrectionTests
{
    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var iqr = new Iqr(5);
        iqr.Update(new TValue(DateTime.UtcNow, 10.0), isNew: true);
        iqr.Update(new TValue(DateTime.UtcNow, 20.0), isNew: true);
        double afterTwo = iqr.Last.Value;

        iqr.Update(new TValue(DateTime.UtcNow, 30.0), isNew: true);
        double afterThree = iqr.Last.Value;

        // Three values should produce different IQR than two
        Assert.NotEqual(afterTwo, afterThree);
    }

    [Fact]
    public void IsNew_False_Rewrites()
    {
        var iqr = new Iqr(5);
        for (int i = 1; i <= 5; i++)
        {
            iqr.Update(new TValue(DateTime.UtcNow, i));
        }
        double before = iqr.Last.Value;

        // Correct last bar with same value
        iqr.Update(new TValue(DateTime.UtcNow, 5.0), isNew: false);
        Assert.Equal(before, iqr.Last.Value, 10);
    }

    [Fact]
    public void IsNew_False_DifferentValue_ChangesResult()
    {
        var iqr = new Iqr(5);
        // Feed values where Q1/Q3 region includes the last bar
        double[] vals = [10, 20, 30, 40, 50];
        for (int i = 0; i < vals.Length; i++)
        {
            iqr.Update(new TValue(DateTime.UtcNow, vals[i]));
        }
        double before = iqr.Last.Value; // sorted [10,20,30,40,50] → Q1=20, Q3=40, IQR=20

        // Correct last bar (50) with value that shifts Q3 → should change IQR
        iqr.Update(new TValue(DateTime.UtcNow, 25.0), isNew: false);
        // sorted [10,20,25,30,40] → Q1=15 or 20, Q3=35 or 30, IQR differs
        Assert.NotEqual(before, iqr.Last.Value);
    }

    [Fact]
    public void IterativeCorrections_RestoreState()
    {
        var iqr = new Iqr(5);
        for (int i = 1; i <= 5; i++)
        {
            iqr.Update(new TValue(DateTime.UtcNow, i));
        }
        double original = iqr.Last.Value;

        // Multiple corrections
        iqr.Update(new TValue(DateTime.UtcNow, 99.0), isNew: false);
        iqr.Update(new TValue(DateTime.UtcNow, 5.0), isNew: false);
        Assert.Equal(original, iqr.Last.Value, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var iqr = new Iqr(5);
        for (int i = 1; i <= 10; i++)
        {
            iqr.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.True(iqr.IsHot);

        iqr.Reset();
        Assert.False(iqr.IsHot);
        Assert.Equal(default, iqr.Last);
    }
}

// ═══════════════════════════════════════════════════════════════
// D) Warmup/Convergence
// ═══════════════════════════════════════════════════════════════
public class IqrWarmupTests
{
    [Fact]
    public void IsHot_FlipsWhenBufferFull()
    {
        var iqr = new Iqr(5);
        for (int i = 0; i < 4; i++)
        {
            iqr.Update(new TValue(DateTime.UtcNow, i));
            Assert.False(iqr.IsHot);
        }
        iqr.Update(new TValue(DateTime.UtcNow, 4));
        Assert.True(iqr.IsHot);
    }

    [Fact]
    public void WarmupPeriod_EqualsToPeriod()
    {
        var iqr = new Iqr(20);
        Assert.Equal(20, iqr.WarmupPeriod);
    }

    [Fact]
    public void SingleValue_IqrIsZero()
    {
        var iqr = new Iqr(5);
        iqr.Update(new TValue(DateTime.UtcNow, 42.0));
        Assert.Equal(0.0, iqr.Last.Value, 10);
    }
}

// ═══════════════════════════════════════════════════════════════
// E) Robustness (critical)
// ═══════════════════════════════════════════════════════════════
public class IqrRobustnessTests
{
    [Fact]
    public void NaN_UsesLastValid()
    {
        var iqr = new Iqr(5);
        for (int i = 1; i <= 5; i++)
        {
            iqr.Update(new TValue(DateTime.UtcNow, i));
        }
        _ = iqr.Last.Value;

        // Feed NaN — should substitute last valid, IQR remains stable
        iqr.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(iqr.Last.Value));
    }

    [Fact]
    public void Infinity_UsesLastValid()
    {
        var iqr = new Iqr(5);
        for (int i = 1; i <= 5; i++)
        {
            iqr.Update(new TValue(DateTime.UtcNow, i));
        }

        iqr.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(iqr.Last.Value));
    }

    [Fact]
    public void NegativeInfinity_UsesLastValid()
    {
        var iqr = new Iqr(5);
        for (int i = 1; i <= 5; i++)
        {
            iqr.Update(new TValue(DateTime.UtcNow, i));
        }

        iqr.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(iqr.Last.Value));
    }

    [Fact]
    public void BatchNaN_NoPropagation()
    {
        var iqr = new Iqr(5);
        for (int i = 0; i < 10; i++)
        {
            iqr.Update(new TValue(DateTime.UtcNow, i % 2 == 0 ? double.NaN : (double)i));
        }
        Assert.True(double.IsFinite(iqr.Last.Value));
    }

    [Fact]
    public void IqrAlwaysNonNegative()
    {
        var iqr = new Iqr(10);
        var rng = new GBM();
        for (int i = 0; i < 100; i++)
        {
            var bar = rng.Next();
            iqr.Update(new TValue(bar.Time, bar.Close));
            Assert.True(iqr.Last.Value >= 0.0, $"IQR was negative at bar {i}: {iqr.Last.Value}");
        }
    }
}

// ═══════════════════════════════════════════════════════════════
// F) Consistency (critical)
// ═══════════════════════════════════════════════════════════════
public class IqrConsistencyTests
{
    private const double Tolerance = 1e-10;

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
        var streaming = new Iqr(period);
        var streamResults = new double[bars];
        for (int i = 0; i < bars; i++)
        {
            streaming.Update(source[i]);
            streamResults[i] = streaming.Last.Value;
        }

        // Batch
        var batchSeries = Iqr.Batch(source, period);

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
        var streaming = new Iqr(period);
        var streamResults = new double[bars];
        for (int i = 0; i < bars; i++)
        {
            streaming.Update(source[i]);
            streamResults[i] = streaming.Last.Value;
        }

        // Span
        var spanOutput = new double[bars];
        Iqr.Batch(source.Values, spanOutput.AsSpan(), period);

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

        // Event-based: subscribe to source
        var eventIqr = new Iqr(source, period);

        // Streaming manual
        var manualIqr = new Iqr(period);

        for (int i = 0; i < bars; i++)
        {
            var bar = rng.Next();
            var tv = new TValue(bar.Time, bar.Close);
            manualIqr.Update(tv);
            source.Add(tv);
        }

        Assert.Equal(manualIqr.Last.Value, eventIqr.Last.Value, Tolerance);
    }
}

// ═══════════════════════════════════════════════════════════════
// G) Span API Tests
// ═══════════════════════════════════════════════════════════════
public class IqrSpanTests
{
    [Fact]
    public void Span_MismatchedLengths_ThrowsArgumentException()
    {
        var source = new double[10];
        var output = new double[5];
        var ex = Assert.Throws<ArgumentException>(() =>
            Iqr.Batch(source.AsSpan(), output.AsSpan(), 5));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Span_InvalidPeriod_ThrowsArgumentException()
    {
        var source = new double[10];
        var output = new double[10];
        var ex = Assert.Throws<ArgumentException>(() =>
            Iqr.Batch(source.AsSpan(), output.AsSpan(), 1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Span_EmptyInput_NoException()
    {
        var source = ReadOnlySpan<double>.Empty;
        var output = Span<double>.Empty;
        Iqr.Batch(source, output, 5);
        Assert.True(true); // S2699 — confirms no exception was thrown
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
        Iqr.Batch(source.AsSpan(), output.AsSpan(), 50);
        Assert.True(double.IsFinite(output[len - 1]));
    }

    [Fact]
    public void Span_HandlesNaN()
    {
        var source = new double[] { 1, 2, double.NaN, 4, 5, 6, 7, 8, 9, 10 };
        var output = new double[10];
        Iqr.Batch(source.AsSpan(), output.AsSpan(), 5);
        // NaN is stored as-is in span batch (no last-valid substitution in static batch)
        // but output should still be finite for most values
        Assert.True(double.IsFinite(output[9]));
    }
}

// ═══════════════════════════════════════════════════════════════
// H) Chainability
// ═══════════════════════════════════════════════════════════════
public class IqrEventTests
{
    [Fact]
    public void Pub_Fires()
    {
        var iqr = new Iqr(5);
        bool fired = false;
        iqr.Pub += (object? _, in TValueEventArgs _) => fired = true;
        iqr.Update(new TValue(DateTime.UtcNow, 42.0));
        Assert.True(fired);
    }

    [Fact]
    public void EventChaining_Works()
    {
        var source = new TSeries();
        var iqr = new Iqr(source, 5);

        source.Add(new TValue(DateTime.UtcNow, 10.0));
        source.Add(new TValue(DateTime.UtcNow, 20.0));
        source.Add(new TValue(DateTime.UtcNow, 30.0));

        Assert.True(double.IsFinite(iqr.Last.Value));
    }
}
