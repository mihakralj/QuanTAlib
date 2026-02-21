namespace QuanTAlib.Tests;

public class FiTests
{
    // ── A) Constructor validation ──────────────────────────────────────

    [Fact]
    public void Fi_Constructor_DefaultPeriod_Is13()
    {
        var fi = new Fi();
        Assert.Equal("Fi(13)", fi.Name);
        Assert.Equal(13, fi.WarmupPeriod);
    }

    [Fact]
    public void Fi_Constructor_CustomPeriod_SetsCorrectly()
    {
        var fi = new Fi(20);
        Assert.Equal("Fi(20)", fi.Name);
        Assert.Equal(20, fi.WarmupPeriod);
    }

    [Fact]
    public void Fi_Constructor_InvalidPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Fi(0));
        Assert.Equal("period", ex.ParamName);

        ex = Assert.Throws<ArgumentException>(() => new Fi(-1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Fi_Constructor_Period1_IsValid()
    {
        var fi = new Fi(1);
        Assert.Equal("Fi(1)", fi.Name);
    }

    // ── B) Basic calculation ───────────────────────────────────────────

    [Fact]
    public void Fi_BasicCalculation_FirstBar_ReturnsInput()
    {
        var fi = new Fi(3);
        var time = DateTime.UtcNow;

        // First bar: EMA initialized to input
        var val = fi.Update(new TValue(time, 400.0));
        Assert.Equal(400.0, val.Value, 10);
    }

    [Fact]
    public void Fi_BasicCalculation_EmaSmoothing()
    {
        var fi = new Fi(3);
        var time = DateTime.UtcNow;

        // alpha = 2/(3+1) = 0.5, decay = 0.5
        // Bar 0: ema = 400, result = 400
        _ = fi.Update(new TValue(time, 400.0));

        // Bar 1: ema = 0.5*(-400) + 0.5*400 = 0, e = 0.5, c = 1/(1-0.5) = 2
        // result = 2 * 0 = 0
        var val2 = fi.Update(new TValue(time.AddMinutes(1), -400.0));
        Assert.Equal(0.0, val2.Value, 10);

        Assert.Equal(val2.Value, fi.Last.Value);
        Assert.Equal("Fi(3)", fi.Name);
    }

    [Fact]
    public void Fi_BasicCalculation_PositiveInput_PositiveOutput()
    {
        var fi = new Fi(5);
        var time = DateTime.UtcNow;

        // Feed constant positive raw force
        for (int i = 0; i < 20; i++)
        {
            fi.Update(new TValue(time.AddMinutes(i), 100.0));
        }

        // After convergence, EMA of constant should equal constant
        Assert.True(fi.Last.Value > 0);
    }

    // ── C) State + bar correction ──────────────────────────────────────

    [Fact]
    public void Fi_IsNew_True_AdvancesState()
    {
        var fi = new Fi(3);
        var time = DateTime.UtcNow;

        var val1 = fi.Update(new TValue(time, 100.0), isNew: true);
        var val2 = fi.Update(new TValue(time.AddMinutes(1), 200.0), isNew: true);

        // Two distinct updates should give different values (EMA blending)
        Assert.NotEqual(val1.Value, val2.Value);
    }

    [Fact]
    public void Fi_IsNew_False_RollsBackState()
    {
        var fi = new Fi(3);
        var time = DateTime.UtcNow;

        _ = fi.Update(new TValue(time, 100.0), isNew: true);
        var val2 = fi.Update(new TValue(time.AddMinutes(1), 200.0), isNew: true);

        // Correction: isNew=false rolls back to state after bar 1
        var val2Corrected = fi.Update(new TValue(time.AddMinutes(1), 300.0), isNew: false);

        // Different input => different result
        Assert.NotEqual(val2.Value, val2Corrected.Value);
    }

    [Fact]
    public void Fi_IterativeCorrections_RestoreState()
    {
        var fi = new Fi(5);
        var time = DateTime.UtcNow;

        // Build up state
        _ = fi.Update(new TValue(time, 100.0), isNew: true);
        _ = fi.Update(new TValue(time.AddMinutes(1), 200.0), isNew: true);

        // Multiple corrections to bar 3
        _ = fi.Update(new TValue(time.AddMinutes(2), 50.0), isNew: true);
        _ = fi.Update(new TValue(time.AddMinutes(2), 80.0), isNew: false);
        _ = fi.Update(new TValue(time.AddMinutes(2), 120.0), isNew: false);
        var finalVal = fi.Update(new TValue(time.AddMinutes(2), 200.0), isNew: false);

        // Should match a fresh computation with the final corrected value
        var fi2 = new Fi(5);
        _ = fi2.Update(new TValue(time, 100.0), isNew: true);
        _ = fi2.Update(new TValue(time.AddMinutes(1), 200.0), isNew: true);
        var expected = fi2.Update(new TValue(time.AddMinutes(2), 200.0), isNew: true);

        Assert.Equal(expected.Value, finalVal.Value, 10);
    }

    [Fact]
    public void Fi_Reset_ClearsState()
    {
        var fi = new Fi(3);
        var time = DateTime.UtcNow;

        fi.Update(new TValue(time, 100.0));
        fi.Update(new TValue(time.AddMinutes(1), 200.0));

        Assert.NotEqual(0, fi.Last.Value);

        fi.Reset();
        Assert.False(fi.IsHot);
        Assert.Equal(0, fi.Last.Value);
    }

    // ── D) Warmup/convergence ──────────────────────────────────────────

    [Fact]
    public void Fi_IsHot_FlipsWhenWarmupComplete()
    {
        var fi = new Fi(3);
        var time = DateTime.UtcNow;

        Assert.False(fi.IsHot);

        // Feed enough data — warmup ends when e <= 1e-10
        // For period=3, alpha=0.5, decay=0.5, need ~34 bars (0.5^34 ≈ 5.8e-11)
        for (int i = 0; i < 50; i++)
        {
            fi.Update(new TValue(time.AddMinutes(i), 100.0 + i));
        }

        Assert.True(fi.IsHot);
    }

    [Fact]
    public void Fi_WarmupPeriod_EqualsPeriod()
    {
        var fi = new Fi(7);
        Assert.Equal(7, fi.WarmupPeriod);
    }

    // ── E) Robustness ──────────────────────────────────────────────────

    [Fact]
    public void Fi_NaN_Input_UsesLastValid()
    {
        var fi = new Fi(3);
        var time = DateTime.UtcNow;

        fi.Update(new TValue(time, 100.0));
        fi.Update(new TValue(time.AddMinutes(1), 200.0));

        // NaN should use last valid value
        var val = fi.Update(new TValue(time.AddMinutes(2), double.NaN));
        Assert.True(double.IsFinite(val.Value));
    }

    [Fact]
    public void Fi_Infinity_Input_UsesLastValid()
    {
        var fi = new Fi(3);
        var time = DateTime.UtcNow;

        fi.Update(new TValue(time, 100.0));
        var val = fi.Update(new TValue(time.AddMinutes(1), double.PositiveInfinity));
        Assert.True(double.IsFinite(val.Value));

        val = fi.Update(new TValue(time.AddMinutes(2), double.NegativeInfinity));
        Assert.True(double.IsFinite(val.Value));
    }

    [Fact]
    public void Fi_BatchNaN_Safe()
    {
        var fi = new Fi(5);
        var time = DateTime.UtcNow;

        // Interleave NaNs with valid values
        for (int i = 0; i < 30; i++)
        {
            double value = (i % 7 == 3) ? double.NaN : (100.0 * Math.Sin(i));
            fi.Update(new TValue(time.AddMinutes(i), value));
        }

        Assert.True(double.IsFinite(fi.Last.Value));
    }

    // ── F) Consistency ─────────────────────────────────────────────────

    [Fact]
    public void Fi_Streaming_Matches_Batch()
    {
        int period = 5;
        int count = 50;
        var time = DateTime.UtcNow;

        var source = new TSeries();
        for (int i = 0; i < count; i++)
        {
            source.Add(new TValue(time.AddMinutes(i), 100.0 * Math.Sin(i * 0.2)));
        }

        // Streaming
        var fi = new Fi(period);
        var streamResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            var val = fi.Update(source[i], isNew: true);
            streamResults[i] = val.Value;
        }

        // Batch
        var batchSeries = Fi.Batch(source, period);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(batchSeries[i].Value, streamResults[i], 10);
        }
    }

    [Fact]
    public void Fi_Streaming_Matches_SpanCalculate()
    {
        int period = 5;
        int count = 50;
        var time = DateTime.UtcNow;

        var source = new TSeries();
        for (int i = 0; i < count; i++)
        {
            source.Add(new TValue(time.AddMinutes(i), 100.0 * Math.Sin(i * 0.2)));
        }

        // Streaming
        var fi = new Fi(period);
        var streamResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            var val = fi.Update(source[i], isNew: true);
            streamResults[i] = val.Value;
        }

        // Span calculate
        var spanOutput = new double[count];
        Fi.Calculate(source.Values, spanOutput, period);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(spanOutput[i], streamResults[i], 10);
        }
    }

    [Fact]
    public void Fi_Eventing_Matches_Streaming()
    {
        int period = 5;
        int count = 50;
        var time = DateTime.UtcNow;

        var source = new TSeries();
        for (int i = 0; i < count; i++)
        {
            source.Add(new TValue(time.AddMinutes(i), 100.0 * Math.Sin(i * 0.2)));
        }

        // Streaming
        var fi1 = new Fi(period);
        var streamResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            var val = fi1.Update(source[i], isNew: true);
            streamResults[i] = val.Value;
        }

        // Eventing via Update(TSeries) which resets and streams
        var fi2 = new Fi(period);
        var eventResults = fi2.Update(source);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(eventResults[i].Value, streamResults[i], 10);
        }
    }

    // ── G) Span API tests ──────────────────────────────────────────────

    [Fact]
    public void Fi_Calculate_MismatchedLengths_ThrowsArgumentException()
    {
        var src = new double[10];
        var output = new double[5];

        var ex = Assert.Throws<ArgumentException>(() => Fi.Calculate(src, output));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Fi_Calculate_InvalidPeriod_ThrowsArgumentException()
    {
        var src = new double[10];
        var output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() => Fi.Calculate(src, output, 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Fi_Calculate_EmptyInput_NoOp()
    {
        ReadOnlySpan<double> src = [];
        Span<double> output = [];
        Fi.Calculate(src, output); // Should not throw
        Assert.True(true); // S2699: assertion confirms no-exception completion
    }

    [Fact]
    public void Fi_Calculate_NaN_HandledGracefully()
    {
        var src = new double[] { 100, double.NaN, 200, 300, double.NaN, 400 };
        var output = new double[6];

        Fi.Calculate(src, output, 3);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"output[{i}] = {output[i]} should be finite");
        }
    }

    [Fact]
    public void Fi_Calculate_LargeData_NoStackOverflow()
    {
        int size = 10_000;
        var src = new double[size];
        var output = new double[size];

        for (int i = 0; i < size; i++)
        {
            src[i] = Math.Sin(i * 0.1) * 100;
        }

        Fi.Calculate(src, output, 13);

        Assert.True(double.IsFinite(output[size - 1]));
    }

    // ── H) Chainability ────────────────────────────────────────────────

    [Fact]
    public void Fi_PubEvent_FiresOnUpdate()
    {
        var fi = new Fi();
        bool eventFired = false;
        fi.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        fi.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(eventFired);
    }

    [Fact]
    public void Fi_Chaining_EventBased()
    {
        var fi1 = new Fi(3);
        var fi2 = new Fi(fi1, 5);

        var time = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            fi1.Update(new TValue(time.AddMinutes(i), 100.0 * Math.Sin(i * 0.3)));
        }

        // fi2 should have received updates from fi1's Pub events
        Assert.True(double.IsFinite(fi2.Last.Value));
    }

    [Fact]
    public void Fi_Calculate_StaticFactory_ReturnsResultsAndIndicator()
    {
        var time = DateTime.UtcNow;
        var source = new TSeries();
        for (int i = 0; i < 100; i++)
        {
            source.Add(new TValue(time.AddMinutes(i), 100.0 + i));
        }

        var (results, indicator) = Fi.Calculate(source, 5);

        Assert.Equal(100, results.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Fi_Prime_InitializesState()
    {
        var fi = new Fi(5);
        var source = new double[30];
        for (int i = 0; i < 30; i++)
        {
            source[i] = 100.0 + i;
        }

        fi.Prime(source);
        Assert.True(double.IsFinite(fi.Last.Value));
    }

    [Fact]
    public void Fi_ConstantInput_ConvergesToConstant()
    {
        var fi = new Fi(5);
        var time = DateTime.UtcNow;

        // EMA of constant should converge to the constant
        for (int i = 0; i < 200; i++)
        {
            fi.Update(new TValue(time.AddMinutes(i), 42.0));
        }

        Assert.Equal(42.0, fi.Last.Value, 6);
    }
}
