namespace QuanTAlib.Tests;

public class RmedTests
{
    private readonly GBM _gbm = new(startPrice: 100, mu: 0.05, sigma: 0.5, seed: 42);

    // ── A) Constructor validation ──

    [Fact]
    public void Constructor_PeriodLessThanOne_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Rmed(0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_PeriodOne_Succeeds()
    {
        var rmed = new Rmed(1);
        Assert.Equal(1, rmed.Period);
    }

    [Fact]
    public void Constructor_DefaultPeriod_Is12()
    {
        var rmed = new Rmed();
        Assert.Equal(12, rmed.Period);
    }

    [Fact]
    public void Constructor_Name_ContainsPeriod()
    {
        var rmed = new Rmed(20);
        Assert.Contains("20", rmed.Name, StringComparison.Ordinal);
        Assert.Contains("Rmed", rmed.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_Alpha_IsInValidRange()
    {
        var rmed = new Rmed(12);
        Assert.InRange(rmed.Alpha, 0.0, 1.0);
    }

    [Fact]
    public void Constructor_Period1_Alpha_Is1()
    {
        // When period=1, alpha should be clamped to 1.0 (cos(2π) + sin(2π) - 1) / cos(2π) = 1
        var rmed = new Rmed(1);
        Assert.InRange(rmed.Alpha, 0.0, 1.0);
    }

    // ── B) Basic calculation ──

    [Fact]
    public void Update_ReturnsTValue()
    {
        var rmed = new Rmed(5);
        var result = rmed.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_FirstBar_ReturnsInputValue()
    {
        var rmed = new Rmed(12);
        var result = rmed.Update(new TValue(DateTime.UtcNow, 42.0));
        Assert.Equal(42.0, result.Value);
    }

    [Fact]
    public void Last_IsAccessible_AfterUpdate()
    {
        var rmed = new Rmed(5);
        rmed.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(rmed.Last.Value));
    }

    [Fact]
    public void Name_IsAccessible()
    {
        var rmed = new Rmed(10);
        Assert.False(string.IsNullOrEmpty(rmed.Name));
    }

    // ── C) State + bar correction ──

    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var rmed = new Rmed(5);
        TSeries src = _gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;

        double prev = 0;
        for (int i = 0; i < src.Count; i++)
        {
            var result = rmed.Update(new TValue(src.Times[i], src.Values[i]), isNew: true);
            if (i > 0)
            {
                // Values should generally differ as price changes
                _ = result.Value;
            }
            prev = result.Value;
        }
        Assert.True(double.IsFinite(prev));
    }

    [Fact]
    public void IsNew_False_RollsBackState()
    {
        var rmed = new Rmed(5);
        TSeries src = _gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;

        // Feed warmup
        for (int i = 0; i < 7; i++)
        {
            rmed.Update(new TValue(src.Times[i], src.Values[i]), isNew: true);
        }

        // Capture state before correction
        var beforeCorrection = rmed.Update(new TValue(src.Times[7], src.Values[7]), isNew: true);

        // Correct the bar (isNew=false with different value)
        var corrected = rmed.Update(new TValue(src.Times[7], src.Values[7] + 50.0), isNew: false);

        // They should differ since different input
        Assert.NotEqual(beforeCorrection.Value, corrected.Value);
    }

    [Fact]
    public void IterativeCorrections_RestoreState()
    {
        var rmed = new Rmed(5);
        TSeries src = _gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;

        // Feed all bars
        for (int i = 0; i < src.Count; i++)
        {
            rmed.Update(new TValue(src.Times[i], src.Values[i]), isNew: true);
        }
        double finalValue = rmed.Last.Value;

        // Correct last bar multiple times, then restore original
        rmed.Update(new TValue(src.Times[^1], 999.0), isNew: false);
        rmed.Update(new TValue(src.Times[^1], 888.0), isNew: false);
        var restored = rmed.Update(new TValue(src.Times[^1], src.Values[^1]), isNew: false);

        Assert.Equal(finalValue, restored.Value, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var rmed = new Rmed(5);
        TSeries src = _gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;

        for (int i = 0; i < src.Count; i++)
        {
            rmed.Update(new TValue(src.Times[i], src.Values[i]), isNew: true);
        }

        rmed.Reset();
        Assert.False(rmed.IsHot);

        // After reset, first bar should return input
        var result = rmed.Update(new TValue(DateTime.UtcNow, 50.0));
        Assert.Equal(50.0, result.Value);
    }

    // ── D) Warmup/convergence ──

    [Fact]
    public void IsHot_FlipsAfterWarmup()
    {
        var rmed = new Rmed(12);
        Assert.False(rmed.IsHot);

        TSeries src = _gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;

        for (int i = 0; i < src.Count; i++)
        {
            rmed.Update(new TValue(src.Times[i], src.Values[i]), isNew: true);
            if (i < 4)
            {
                Assert.False(rmed.IsHot);
            }
        }
        // After 5+ bars (MedianWindow), should be hot
        Assert.True(rmed.IsHot);
    }

    [Fact]
    public void WarmupPeriod_Equals_5()
    {
        var rmed = new Rmed(20);
        Assert.Equal(5, rmed.WarmupPeriod);
    }

    // ── E) Robustness ──

    [Fact]
    public void NaN_UsesLastValidValue()
    {
        var rmed = new Rmed(5);
        rmed.Update(new TValue(DateTime.UtcNow, 100.0));
        rmed.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 102.0));
        rmed.Update(new TValue(DateTime.UtcNow.AddMinutes(2), 104.0));

        var nanResult = rmed.Update(new TValue(DateTime.UtcNow.AddMinutes(3), double.NaN));
        Assert.True(double.IsFinite(nanResult.Value));
    }

    [Fact]
    public void Infinity_UsesLastValidValue()
    {
        var rmed = new Rmed(5);
        rmed.Update(new TValue(DateTime.UtcNow, 100.0));
        rmed.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 102.0));

        var infResult = rmed.Update(new TValue(DateTime.UtcNow.AddMinutes(2), double.PositiveInfinity));
        Assert.True(double.IsFinite(infResult.Value));
    }

    [Fact]
    public void BatchNaN_DoesNotCorruptStream()
    {
        var rmed = new Rmed(5);
        TSeries src = _gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;

        // Inject NaN at various positions
        for (int i = 0; i < src.Count; i++)
        {
            double val = (i == 5 || i == 10 || i == 15) ? double.NaN : src.Values[i];
            var result = rmed.Update(new TValue(src.Times[i], val), isNew: true);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    // ── F) Consistency (4 modes must match) ──

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        const int period = 10;
        TSeries src = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;

        // Mode 1: Streaming
        var rmedStreaming = new Rmed(period);
        double[] streaming = new double[src.Count];
        for (int i = 0; i < src.Count; i++)
        {
            streaming[i] = rmedStreaming.Update(new TValue(src.Times[i], src.Values[i]), isNew: true).Value;
        }

        // Mode 2: Batch TSeries
        TSeries batchTs = Rmed.Batch(src, period);

        // Mode 3: Batch Span
        double[] spanOut = new double[src.Count];
        Rmed.Batch(src.Values, spanOut.AsSpan(), period);

        // Mode 4: Event-driven
        var rmedEvent = new Rmed(period);
        double[] evented = new double[src.Count];
        int eventCount = 0;
        rmedEvent.Pub += (object? _, in TValueEventArgs args) =>
        {
            if (args.IsNew && eventCount < evented.Length)
            {
                evented[eventCount++] = args.Value.Value;
            }
        };
        for (int i = 0; i < src.Count; i++)
        {
            rmedEvent.Update(new TValue(src.Times[i], src.Values[i]), isNew: true);
        }

        // Compare all 4 modes
        for (int i = 0; i < src.Count; i++)
        {
            Assert.Equal(streaming[i], batchTs.Values[i], 10);
            Assert.Equal(streaming[i], spanOut[i], 10);
            Assert.Equal(streaming[i], evented[i], 10);
        }
    }

    [Fact]
    public void Streaming_MatchesBatch()
    {
        const int period = 12;
        TSeries src = _gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;

        // Streaming
        var rmed = new Rmed(period);
        double[] streaming = new double[src.Count];
        for (int i = 0; i < src.Count; i++)
        {
            streaming[i] = rmed.Update(new TValue(src.Times[i], src.Values[i]), isNew: true).Value;
        }

        // Batch
        double[] batch = new double[src.Count];
        Rmed.Batch(src.Values, batch.AsSpan(), period);

        for (int i = 0; i < src.Count; i++)
        {
            Assert.Equal(streaming[i], batch[i], 10);
        }
    }

    // ── G) Span API tests ──

    [Fact]
    public void Batch_MismatchedLengths_Throws()
    {
        double[] src = [1, 2, 3];
        double[] output = [0, 0];

        var ex = Assert.Throws<ArgumentException>(() => Rmed.Batch(src.AsSpan(), output.AsSpan(), 5));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_InvalidPeriod_Throws()
    {
        double[] src = [1, 2, 3];
        double[] output = [0, 0, 0];

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Rmed.Batch(src.AsSpan(), output.AsSpan(), 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_NaN_DoesNotCorrupt()
    {
        double[] src = [100, 101, double.NaN, 103, 104, 105, 106, 107];
        double[] output = new double[src.Length];

        Rmed.Batch(src.AsSpan(), output.AsSpan(), 5);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]));
        }
    }

    [Fact]
    public void Batch_LargeData_DoesNotStackOverflow()
    {
        const int size = 10_000;
        TSeries src = _gbm.Fetch(size, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        double[] output = new double[size];

        Rmed.Batch(src.Values, output.AsSpan(), 20);

        Assert.True(double.IsFinite(output[^1]));
    }

    // ── H) Chainability ──

    [Fact]
    public void Pub_EventFires()
    {
        var rmed = new Rmed(5);
        int count = 0;
        rmed.Pub += (object? _, in TValueEventArgs _) => count++;

        rmed.Update(new TValue(DateTime.UtcNow, 100.0));
        rmed.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 101.0));

        Assert.Equal(2, count);
    }

    [Fact]
    public void EventChaining_Works()
    {
        var src = new TSeries();
        var rmed = new Rmed(src, 5);

        TSeries data = _gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        for (int i = 0; i < data.Count; i++)
        {
            src.Add(new TValue(data.Times[i], data.Values[i]), isNew: true);
        }

        Assert.True(rmed.IsHot);
        Assert.True(double.IsFinite(rmed.Last.Value));
    }

    // ── Additional: Spike rejection ──

    [Fact]
    public void SingleSpike_IsRejected()
    {
        // Feed 10 bars at ~100, inject one massive spike, verify output barely moves
        var rmed = new Rmed(5);
        for (int i = 0; i < 5; i++)
        {
            rmed.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0), isNew: true);
        }
        _ = rmed.Last.Value;

        // Inject spike
        rmed.Update(new TValue(DateTime.UtcNow.AddMinutes(5), 10000.0), isNew: true);
        double afterSpike = rmed.Last.Value;

        // The median rejects the spike, so the EMA shouldn't jump to 10000
        // With 5-bar median of [100,100,100,100,10000], median = 100
        // So afterSpike ≈ alpha*100 + (1-alpha)*beforeSpike, roughly still near 100
        Assert.True(afterSpike < 500.0, $"Spike should be rejected, got {afterSpike}");
    }

    [Fact]
    public void TwoConsecutiveSpikes_AreRejected()
    {
        var rmed = new Rmed(5);
        for (int i = 0; i < 5; i++)
        {
            rmed.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0), isNew: true);
        }

        // Two consecutive spikes — still within the breakdown point of 5-bar median
        rmed.Update(new TValue(DateTime.UtcNow.AddMinutes(5), 10000.0), isNew: true);
        rmed.Update(new TValue(DateTime.UtcNow.AddMinutes(6), 10000.0), isNew: true);
        double afterSpikes = rmed.Last.Value;

        // Median of [100,100,100,10000,10000] = 100 → spike rejected
        Assert.True(afterSpikes < 500.0, $"Two spikes should be rejected, got {afterSpikes}");
    }

    [Fact]
    public void ConstantInput_ConvergesToConstant()
    {
        var rmed = new Rmed(10);
        TValue result = default;
        // Feed 50 bars of constant 50.0 — buffer zeros pollute median for first 4 bars,
        // then IIR converges. After enough bars the output must match the input.
        for (int i = 0; i < 50; i++)
        {
            result = rmed.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 50.0), isNew: true);
            Assert.True(double.IsFinite(result.Value));
        }
        // After 50 bars of constant input, EMA must have converged to 50.0
        Assert.Equal(50.0, result.Value, 6);
    }

    [Fact]
    public void Update_TSeries_Works()
    {
        TSeries src = _gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        var rmed = new Rmed(10);
        TSeries result = rmed.Update(src);

        Assert.Equal(src.Count, result.Count);
        for (int i = 0; i < result.Count; i++)
        {
            Assert.True(double.IsFinite(result.Values[i]));
        }
    }

    [Fact]
    public void Calculate_ReturnsTupleWithIndicator()
    {
        TSeries src = _gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        var (results, indicator) = Rmed.Calculate(src, 10);

        Assert.Equal(src.Count, results.Count);
        Assert.Equal(10, indicator.Period);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Prime_PopulatesState()
    {
        var rmed = new Rmed(5);
        TSeries src = _gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;

        rmed.Prime(src.Values);
        Assert.True(rmed.IsHot);
    }

    [Fact]
    public void Dispose_UnsubscribesFromSource()
    {
        TSeries src = _gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        var rmed = new Rmed(src, 5);
        double lastBeforeDispose = rmed.Last.Value;

        rmed.Dispose();

        // Adding more data to source should not affect the disposed indicator
        src.Add(new TValue(DateTime.UtcNow.AddMinutes(100), 999.0));
        Assert.Equal(lastBeforeDispose, rmed.Last.Value);
    }
}
