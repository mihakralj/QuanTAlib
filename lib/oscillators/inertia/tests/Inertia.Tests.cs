using Xunit;

namespace QuanTAlib.Tests;

public sealed class InertiaTests
{
    private const int DefaultPeriod = 20;

    // === A) Constructor validation ===

    [Fact]
    public void Constructor_DefaultPeriod_Is20()
    {
        var inertia = new Inertia();
        Assert.Equal(DefaultPeriod, inertia.Period);
    }

    [Fact]
    public void Constructor_CustomPeriod_IsStored()
    {
        var inertia = new Inertia(period: 10);
        Assert.Equal(10, inertia.Period);
    }

    [Fact]
    public void Constructor_ZeroPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Inertia(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Inertia(period: -1));
        Assert.Equal("period", ex.ParamName);
    }

    // === B) Basic calculation ===

    [Fact]
    public void Update_ReturnsFiniteValue()
    {
        var inertia = new Inertia(period: 5);
        for (int i = 0; i < 10; i++)
        {
            inertia.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        Assert.True(double.IsFinite(inertia.Last.Value));
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var inertia = new Inertia(period: 5);
        inertia.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(inertia.Last.Value));
    }

    [Fact]
    public void Name_ContainsPeriod()
    {
        var inertia = new Inertia(period: 14);
        Assert.Contains("14", inertia.Name, StringComparison.Ordinal);
        Assert.Contains("Inertia", inertia.Name, StringComparison.Ordinal);
    }

    // === C) State + bar correction ===

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var inertia = new Inertia(period: 5);
        for (int i = 0; i < 5; i++)
        {
            inertia.Update(new TValue(DateTime.UtcNow, 100.0 + i), isNew: true);
        }
        double first = inertia.Last.Value;
        inertia.Update(new TValue(DateTime.UtcNow, 110.0), isNew: true);
        Assert.NotEqual(first, inertia.Last.Value);
    }

    [Fact]
    public void Update_IsNewFalse_CorrectsSameBar()
    {
        var inertia = new Inertia(period: 5);
        for (int i = 0; i < 6; i++)
        {
            inertia.Update(new TValue(DateTime.UtcNow, 100.0 + i), isNew: true);
        }

        double before = inertia.Last.Value;
        inertia.Update(new TValue(DateTime.UtcNow, 200.0), isNew: false);
        double corrected = inertia.Last.Value;
        Assert.NotEqual(before, corrected);

        // Restore original
        inertia.Update(new TValue(DateTime.UtcNow, 105.0), isNew: false);
        Assert.Equal(before, inertia.Last.Value, precision: 10);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoreState()
    {
        var inertia = new Inertia(period: 5);
        for (int i = 0; i < 10; i++)
        {
            inertia.Update(new TValue(DateTime.UtcNow, 100.0 + i), isNew: true);
        }

        double baseline = inertia.Last.Value;

        // Multiple corrections on same bar
        for (int c = 0; c < 5; c++)
        {
            inertia.Update(new TValue(DateTime.UtcNow, 150.0 + c), isNew: false);
        }

        // Restore original value
        inertia.Update(new TValue(DateTime.UtcNow, 109.0), isNew: false);
        Assert.Equal(baseline, inertia.Last.Value, precision: 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var inertia = new Inertia(period: 5);
        for (int i = 0; i < 10; i++)
        {
            inertia.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        Assert.True(inertia.IsHot);

        inertia.Reset();
        Assert.False(inertia.IsHot);
        Assert.Equal(0.0, inertia.Last.Value);
    }

    // === D) Warmup/convergence ===

    [Fact]
    public void IsHot_FlipsAtPeriod()
    {
        var inertia = new Inertia(period: 5);
        for (int i = 0; i < 4; i++)
        {
            inertia.Update(new TValue(DateTime.UtcNow, 100.0 + i));
            Assert.False(inertia.IsHot);
        }
        inertia.Update(new TValue(DateTime.UtcNow, 104.0));
        Assert.True(inertia.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesPeriod()
    {
        var inertia = new Inertia(period: 10);
        Assert.Equal(10, inertia.WarmupPeriod);
    }

    // === E) Robustness ===

    [Fact]
    public void Update_NaN_UsesLastValid()
    {
        var inertia = new Inertia(period: 5);
        for (int i = 0; i < 10; i++)
        {
            inertia.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        _ = inertia.Last.Value;
        inertia.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(inertia.Last.Value));
    }

    [Fact]
    public void Update_Infinity_UsesLastValid()
    {
        var inertia = new Inertia(period: 5);
        for (int i = 0; i < 10; i++)
        {
            inertia.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        inertia.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(inertia.Last.Value));
    }

    [Fact]
    public void Update_NegativeInfinity_UsesLastValid()
    {
        var inertia = new Inertia(period: 5);
        for (int i = 0; i < 10; i++)
        {
            inertia.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        inertia.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(inertia.Last.Value));
    }

    [Fact]
    public void Update_BatchNaN_StaysFinite()
    {
        var inertia = new Inertia(period: 5);
        for (int i = 0; i < 10; i++)
        {
            inertia.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        for (int i = 0; i < 5; i++)
        {
            inertia.Update(new TValue(DateTime.UtcNow, double.NaN));
            Assert.True(double.IsFinite(inertia.Last.Value));
        }
    }

    // === F) Consistency (4 modes) ===

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        int period = 10;
        int count = 50;
        var gbm = new GBM(startPrice: 100.0, seed: 42);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = new TSeries();
        for (int i = 0; i < bars.Count; i++)
        {
            source.Add(new TValue(bars[i].Time, bars[i].Close));
        }

        // Mode 1: Streaming
        var streaming = new Inertia(period);
        var streamResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            streaming.Update(new TValue(source.Times[i], source.Values[i]));
            streamResults[i] = streaming.Last.Value;
        }

        // Mode 2: Batch (TSeries)
        var batchResults = Inertia.Batch(source, period);
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamResults[i], batchResults.Values[i], precision: 4);
        }

        // Mode 3: Span
        Span<double> spanOutput = new double[count];
        Inertia.Batch(source.Values, spanOutput, period);
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamResults[i], spanOutput[i], precision: 4);
        }

        // Mode 4: Event-based
        var eventInertia = new Inertia(period);
        var eventResults = new double[count];
        int idx = 0;
        eventInertia.Pub += (object? _, in TValueEventArgs e) =>
        {
            if (idx < count)
            {
                eventResults[idx++] = e.Value.Value;
            }
        };
        for (int i = 0; i < count; i++)
        {
            eventInertia.Update(new TValue(source.Times[i], source.Values[i]));
        }
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamResults[i], eventResults[i], precision: 10);
        }
    }

    // === G) Span API tests ===

    [Fact]
    public void Batch_Span_MismatchedLength_Throws()
    {
        var src = new double[] { 1, 2, 3 };
        var output = new double[5];
        var ex = Assert.Throws<ArgumentException>(() =>
            Inertia.Batch(src.AsSpan(), output.AsSpan(), 3));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_ZeroPeriod_Throws()
    {
        var src = new double[] { 1, 2, 3 };
        var output = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Inertia.Batch(src.AsSpan(), output.AsSpan(), 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_Empty_NoException()
    {
        var src = ReadOnlySpan<double>.Empty;
        var output = Span<double>.Empty;
        Inertia.Batch(src, output, 5);
        Assert.True(true);
    }

    [Fact]
    public void Batch_Span_MatchesTSeries()
    {
        int period = 5;
        int count = 30;
        var gbm = new GBM(startPrice: 100.0, seed: 42);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = new TSeries();
        for (int i = 0; i < bars.Count; i++)
        {
            source.Add(new TValue(bars[i].Time, bars[i].Close));
        }

        var batchTs = Inertia.Batch(source, period);

        Span<double> spanOutput = new double[count];
        Inertia.Batch(source.Values, spanOutput, period);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(batchTs.Values[i], spanOutput[i], precision: 12);
        }
    }

    // === H) Chainability ===

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var inertia = new Inertia(period: 5);
        int count = 0;
        inertia.Pub += (object? _, in TValueEventArgs _) => count++;
        for (int i = 0; i < 10; i++)
        {
            inertia.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        Assert.Equal(10, count);
    }

    [Fact]
    public void Chaining_EventBased_Works()
    {
        var source = new TSeries();
        var inertia = new Inertia(source, period: 5);

        for (int i = 0; i < 10; i++)
        {
            source.Add(new TValue(DateTime.UtcNow, 100.0 + i), isNew: true);
        }

        Assert.True(inertia.IsHot);
        Assert.True(double.IsFinite(inertia.Last.Value));
    }

    // === Mathematical behavior ===

    [Fact]
    public void ConstantPrice_InertiaIsZero()
    {
        var inertia = new Inertia(period: 5);
        for (int i = 0; i < 10; i++)
        {
            inertia.Update(new TValue(DateTime.UtcNow, 100.0));
        }
        // Constant price → perfect regression → residual = 0
        Assert.Equal(0.0, inertia.Last.Value, precision: 10);
    }

    [Fact]
    public void LinearTrend_InertiaIsZero()
    {
        var inertia = new Inertia(period: 5);
        for (int i = 0; i < 10; i++)
        {
            inertia.Update(new TValue(DateTime.UtcNow, 100.0 + i * 2.0));
        }
        // Perfect linear trend → regression fits perfectly → residual = 0
        Assert.Equal(0.0, inertia.Last.Value, precision: 10);
    }

    [Fact]
    public void RisingAboveTrend_InertiaPositive()
    {
        var inertia = new Inertia(period: 5);
        // Feed a trend, then spike up
        for (int i = 0; i < 5; i++)
        {
            inertia.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        // Spike above the trend line
        inertia.Update(new TValue(DateTime.UtcNow, 200.0));
        Assert.True(inertia.Last.Value > 0);
    }

    [Fact]
    public void FallingBelowTrend_InertiaNegative()
    {
        var inertia = new Inertia(period: 5);
        // Feed a trend, then drop
        for (int i = 0; i < 5; i++)
        {
            inertia.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        // Drop below the trend line
        inertia.Update(new TValue(DateTime.UtcNow, 90.0));
        Assert.True(inertia.Last.Value < 0);
    }

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var source = new TSeries();
        for (int i = 0; i < 30; i++)
        {
            source.Add(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        var (results, indicator) = Inertia.Calculate(source, period: 10);
        Assert.Equal(30, results.Count);
        Assert.True(indicator.IsHot);
    }
}
