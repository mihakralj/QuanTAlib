namespace QuanTAlib.Tests;

public class EriTests
{
    // ── A) Constructor validation ──────────────────────────────────────

    [Fact]
    public void Eri_Constructor_DefaultPeriod_Is13()
    {
        var eri = new Eri();
        Assert.Equal("Eri(13)", eri.Name);
        Assert.Equal(13, eri.WarmupPeriod);
    }

    [Fact]
    public void Eri_Constructor_CustomPeriod_SetsCorrectly()
    {
        var eri = new Eri(20);
        Assert.Equal("Eri(20)", eri.Name);
        Assert.Equal(20, eri.WarmupPeriod);
    }

    [Fact]
    public void Eri_Constructor_InvalidPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Eri(0));
        Assert.Equal("period", ex.ParamName);

        ex = Assert.Throws<ArgumentException>(() => new Eri(-1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Eri_Constructor_Period1_IsValid()
    {
        var eri = new Eri(1);
        Assert.Equal("Eri(1)", eri.Name);
    }

    // ── B) Basic calculation ───────────────────────────────────────────

    [Fact]
    public void Eri_BasicCalculation_FirstBar_BullPowerIsHighMinusClose()
    {
        var eri = new Eri(3);
        var time = DateTime.UtcNow;

        // First bar: EMA = close = 100, Bull Power = high - EMA = 110 - 100 = 10
        var bar = new TBar(time, 100.0, 110.0, 90.0, 100.0, 1000.0);
        var val = eri.Update(bar);
        Assert.Equal(10.0, val.Value, 10);
    }

    [Fact]
    public void Eri_BasicCalculation_FirstBar_BearPowerIsLowMinusClose()
    {
        var eri = new Eri(3);
        var time = DateTime.UtcNow;

        // First bar: EMA = close = 100, Bear Power = low - EMA = 90 - 100 = -10
        var bar = new TBar(time, 100.0, 110.0, 90.0, 100.0, 1000.0);
        _ = eri.Update(bar);
        Assert.Equal(-10.0, eri.BearPower, 10);
    }

    [Fact]
    public void Eri_BasicCalculation_BullPowerPositive_InUptrend()
    {
        var eri = new Eri(3);
        var time = DateTime.UtcNow;

        // Feed rising prices — High should consistently be above EMA
        for (int i = 0; i < 30; i++)
        {
            double close = 100.0 + (i * 2);
            double high = close + 10;
            double low = close - 5;
            var bar = new TBar(time.AddMinutes(i), close, high, low, close, 1000.0);
            eri.Update(bar);
        }

        // Bull power should be positive (High > EMA in uptrend)
        Assert.True(eri.Last.Value > 0, $"Bull Power should be positive in uptrend, got {eri.Last.Value}");
    }

    [Fact]
    public void Eri_BasicCalculation_BearPowerNegative_InDowntrend()
    {
        var eri = new Eri(3);
        var time = DateTime.UtcNow;

        // Feed declining prices — Low should consistently be below EMA
        for (int i = 0; i < 30; i++)
        {
            double close = 200.0 - (i * 2);
            double high = close + 5;
            double low = close - 10;
            var bar = new TBar(time.AddMinutes(i), close, high, low, close, 1000.0);
            eri.Update(bar);
        }

        // Bear power should be negative (Low < EMA in downtrend)
        Assert.True(eri.BearPower < 0, $"Bear Power should be negative in downtrend, got {eri.BearPower}");
    }

    [Fact]
    public void Eri_BasicCalculation_AccessLast_Name_IsHot()
    {
        var eri = new Eri(3);
        var time = DateTime.UtcNow;

        var bar = new TBar(time, 100.0, 110.0, 90.0, 105.0, 1000.0);
        var val = eri.Update(bar);

        Assert.Equal(val.Value, eri.Last.Value);
        Assert.Equal("Eri(3)", eri.Name);
        Assert.False(eri.IsHot); // Only 1 bar, not yet warmed up for period=3
    }

    // ── C) State + bar correction ──────────────────────────────────────

    [Fact]
    public void Eri_IsNew_True_AdvancesState()
    {
        var eri = new Eri(3);
        var time = DateTime.UtcNow;

        var bar1 = new TBar(time, 100.0, 110.0, 90.0, 100.0, 1000.0);
        var bar2 = new TBar(time.AddMinutes(1), 105.0, 115.0, 95.0, 110.0, 1000.0);

        var val1 = eri.Update(bar1, isNew: true);
        var val2 = eri.Update(bar2, isNew: true);

        // Two distinct updates with different H/L/C should give different values
        Assert.NotEqual(val1.Value, val2.Value);
    }

    [Fact]
    public void Eri_IsNew_False_RollsBackState()
    {
        var eri = new Eri(3);
        var time = DateTime.UtcNow;

        var bar1 = new TBar(time, 100.0, 110.0, 90.0, 100.0, 1000.0);
        _ = eri.Update(bar1, isNew: true);

        var bar2 = new TBar(time.AddMinutes(1), 105.0, 115.0, 95.0, 110.0, 1000.0);
        var val2 = eri.Update(bar2, isNew: true);

        // Correction: isNew=false rolls back to state after bar 1
        // Use significantly different close to shift EMA and produce divergent bull power
        var bar2Corrected = new TBar(time.AddMinutes(1), 108.0, 150.0, 92.0, 140.0, 1000.0);
        var val2Corrected = eri.Update(bar2Corrected, isNew: false);

        // Different input => different result
        Assert.NotEqual(val2.Value, val2Corrected.Value);
    }

    [Fact]
    public void Eri_IterativeCorrections_RestoreState()
    {
        var eri = new Eri(5);
        var time = DateTime.UtcNow;

        // Build up state
        var bar1 = new TBar(time, 100.0, 110.0, 90.0, 100.0, 1000.0);
        _ = eri.Update(bar1, isNew: true);

        var bar2 = new TBar(time.AddMinutes(1), 105.0, 115.0, 95.0, 110.0, 1000.0);
        _ = eri.Update(bar2, isNew: true);

        // Multiple corrections to bar 3
        _ = eri.Update(new TBar(time.AddMinutes(2), 108.0, 118.0, 98.0, 105.0, 1000.0), isNew: true);
        _ = eri.Update(new TBar(time.AddMinutes(2), 112.0, 122.0, 102.0, 112.0, 1000.0), isNew: false);
        _ = eri.Update(new TBar(time.AddMinutes(2), 115.0, 125.0, 105.0, 118.0, 1000.0), isNew: false);
        var finalBar = new TBar(time.AddMinutes(2), 120.0, 130.0, 110.0, 120.0, 1000.0);
        var finalVal = eri.Update(finalBar, isNew: false);

        // Should match a fresh computation with the final corrected value
        var eri2 = new Eri(5);
        _ = eri2.Update(bar1, isNew: true);
        _ = eri2.Update(bar2, isNew: true);
        var expected = eri2.Update(finalBar, isNew: true);

        Assert.Equal(expected.Value, finalVal.Value, 10);
    }

    [Fact]
    public void Eri_Reset_ClearsState()
    {
        var eri = new Eri(3);
        var time = DateTime.UtcNow;

        eri.Update(new TBar(time, 100.0, 110.0, 90.0, 105.0, 1000.0));
        eri.Update(new TBar(time.AddMinutes(1), 110.0, 120.0, 100.0, 115.0, 1000.0));

        Assert.NotEqual(0, eri.Last.Value);

        eri.Reset();
        Assert.False(eri.IsHot);
        Assert.Equal(0, eri.Last.Value);
    }

    // ── D) Warmup/convergence ──────────────────────────────────────────

    [Fact]
    public void Eri_IsHot_FlipsWhenWarmupComplete()
    {
        var eri = new Eri(3);
        var time = DateTime.UtcNow;

        Assert.False(eri.IsHot);

        // Feed enough data — warmup ends after WarmupPeriod bars
        for (int i = 0; i < 50; i++)
        {
            double close = 100.0 + i;
            eri.Update(new TBar(time.AddMinutes(i), close, close + 10, close - 5, close, 1000.0));
        }

        Assert.True(eri.IsHot);
    }

    [Fact]
    public void Eri_WarmupPeriod_EqualsPeriod()
    {
        var eri = new Eri(7);
        Assert.Equal(7, eri.WarmupPeriod);
    }

    [Fact]
    public void Eri_ConvergesAfterManyBars_GBM()
    {
        var eri = new Eri(13);
        var gbm = new GBM();

        for (int i = 0; i < 200; i++)
        {
            var bar = gbm.Next(isNew: true);
            eri.Update(bar);
        }

        Assert.True(eri.IsHot);
        Assert.True(double.IsFinite(eri.Last.Value));
        Assert.True(double.IsFinite(eri.BearPower));
    }

    // ── E) Robustness ──────────────────────────────────────────────────

    [Fact]
    public void Eri_NaN_Input_UsesLastValid()
    {
        var eri = new Eri(3);
        var time = DateTime.UtcNow;

        eri.Update(new TBar(time, 100.0, 110.0, 90.0, 100.0, 1000.0));
        eri.Update(new TBar(time.AddMinutes(1), 105.0, 115.0, 95.0, 110.0, 1000.0));

        // NaN close should use last valid value
        var val = eri.Update(new TBar(time.AddMinutes(2), 108.0, double.NaN, 98.0, double.NaN, 1000.0));
        Assert.True(double.IsFinite(val.Value));
        Assert.True(double.IsFinite(eri.BearPower));
    }

    [Fact]
    public void Eri_Infinity_Input_UsesLastValid()
    {
        var eri = new Eri(3);
        var time = DateTime.UtcNow;

        eri.Update(new TBar(time, 100.0, 110.0, 90.0, 100.0, 1000.0));
        var val = eri.Update(new TBar(time.AddMinutes(1), 105.0, double.PositiveInfinity, 95.0, double.PositiveInfinity, 1000.0));
        Assert.True(double.IsFinite(val.Value));

        val = eri.Update(new TBar(time.AddMinutes(2), 108.0, 118.0, double.NegativeInfinity, double.NegativeInfinity, 1000.0));
        Assert.True(double.IsFinite(val.Value));
        Assert.True(double.IsFinite(eri.BearPower));
    }

    [Fact]
    public void Eri_BatchNaN_Safe_GBM()
    {
        var eri = new Eri(5);
        var gbm = new GBM();

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            double close = (i % 7 == 3) ? double.NaN : bar.Close;
            double high = (i % 11 == 5) ? double.NaN : bar.High;
            double low = bar.Low;
            eri.Update(new TBar(bar.Time, bar.Open, high, low, close, bar.Volume));
        }

        Assert.True(double.IsFinite(eri.Last.Value));
        Assert.True(double.IsFinite(eri.BearPower));
    }

    // ── F) Consistency ─────────────────────────────────────────────────

    [Fact]
    public void Eri_Streaming_Matches_Batch()
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
        var eri = new Eri(period);
        var streamResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            var val = eri.Update(source[i], isNew: true);
            streamResults[i] = val.Value;
        }

        // Batch
        var batchSeries = Eri.Batch(source, period);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(batchSeries[i].Value, streamResults[i], 10);
        }
    }

    [Fact]
    public void Eri_Streaming_Matches_SpanCalculate()
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
        var eri = new Eri(period);
        var streamResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            var val = eri.Update(source[i], isNew: true);
            streamResults[i] = val.Value;
        }

        // Span calculate
        var spanOutput = new double[count];
        Eri.Calculate(source.Values, spanOutput, period);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(spanOutput[i], streamResults[i], 10);
        }
    }

    [Fact]
    public void Eri_Eventing_Matches_Streaming()
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
        var eri1 = new Eri(period);
        var streamResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            var val = eri1.Update(source[i], isNew: true);
            streamResults[i] = val.Value;
        }

        // Eventing via Update(TSeries) which resets and streams
        var eri2 = new Eri(period);
        var eventResults = eri2.Update(source);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(eventResults[i].Value, streamResults[i], 10);
        }
    }

    // ── G) Span API tests ──────────────────────────────────────────────

    [Fact]
    public void Eri_Calculate_MismatchedLengths_ThrowsArgumentException()
    {
        var src = new double[10];
        var output = new double[5];

        var ex = Assert.Throws<ArgumentException>(() => Eri.Calculate(src, output));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Eri_Calculate_InvalidPeriod_ThrowsArgumentException()
    {
        var src = new double[10];
        var output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() => Eri.Calculate(src, output, 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Eri_Calculate_EmptyInput_NoOp()
    {
        ReadOnlySpan<double> src = [];
        Span<double> output = [];
        Eri.Calculate(src, output); // Should not throw
        Assert.True(true); // S2699: assertion confirms no-exception completion
    }

    [Fact]
    public void Eri_Calculate_NaN_HandledGracefully()
    {
        var src = new double[] { 100, double.NaN, 200, 300, double.NaN, 400 };
        var output = new double[6];

        Eri.Calculate(src, output, 3);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"output[{i}] = {output[i]} should be finite");
        }
    }

    [Fact]
    public void Eri_Calculate_LargeData_NoStackOverflow()
    {
        int size = 10_000;
        var src = new double[size];
        var output = new double[size];

        for (int i = 0; i < size; i++)
        {
            src[i] = Math.Sin(i * 0.1) * 100;
        }

        Eri.Calculate(src, output, 13);

        Assert.True(double.IsFinite(output[size - 1]));
    }

    // ── H) Chainability ────────────────────────────────────────────────

    [Fact]
    public void Eri_PubEvent_FiresOnUpdate()
    {
        var eri = new Eri();
        bool eventFired = false;
        eri.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        eri.Update(new TBar(DateTime.UtcNow, 100.0, 110.0, 90.0, 105.0, 1000.0));
        Assert.True(eventFired);
    }

    [Fact]
    public void Eri_Chaining_EventBased()
    {
        var eri1 = new Eri(3);
        var eri2 = new Eri(eri1, 5);

        var time = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double close = 100.0 + (10.0 * Math.Sin(i * 0.3));
            eri1.Update(new TBar(time.AddMinutes(i), close, close + 5, close - 5, close, 1000.0));
        }

        // eri2 should have received updates from eri1's Pub events
        Assert.True(double.IsFinite(eri2.Last.Value));
    }

    [Fact]
    public void Eri_Calculate_StaticFactory_ReturnsResultsAndIndicator()
    {
        var time = DateTime.UtcNow;
        var source = new TSeries();
        for (int i = 0; i < 100; i++)
        {
            source.Add(new TValue(time.AddMinutes(i), 100.0 + i));
        }

        var (results, indicator) = Eri.Calculate(source, 5);

        Assert.Equal(100, results.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Eri_Prime_InitializesState()
    {
        var eri = new Eri(5);
        var source = new double[30];
        for (int i = 0; i < 30; i++)
        {
            source[i] = 100.0 + i;
        }

        eri.Prime(source);
        Assert.True(double.IsFinite(eri.Last.Value));
    }

    [Fact]
    public void Eri_ConstantInput_BullBearPowerConverge()
    {
        var eri = new Eri(5);
        var time = DateTime.UtcNow;

        // When H=L=C=constant, EMA converges to constant, so Bull=Bear=0
        for (int i = 0; i < 200; i++)
        {
            eri.Update(new TBar(time.AddMinutes(i), 42.0, 42.0, 42.0, 42.0, 1000.0));
        }

        Assert.Equal(0.0, eri.Last.Value, 6);  // Bull Power = H - EMA = 0
        Assert.Equal(0.0, eri.BearPower, 6);    // Bear Power = L - EMA = 0
    }

    [Fact]
    public void Eri_BearPower_IsAccessible()
    {
        var eri = new Eri(3);
        var time = DateTime.UtcNow;

        // High=110, Low=90, Close=100 → first bar EMA=100, Bull=10, Bear=-10
        eri.Update(new TBar(time, 100.0, 110.0, 90.0, 100.0, 1000.0));

        Assert.Equal(10.0, eri.Last.Value, 10);  // Bull Power
        Assert.Equal(-10.0, eri.BearPower, 10);   // Bear Power
    }

    [Fact]
    public void Eri_TBar_GBM_StreamingProducesFiniteResults()
    {
        var eri = new Eri(13);
        var gbm = new GBM();

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            eri.Update(bar);
        }

        Assert.True(double.IsFinite(eri.Last.Value), "Bull Power should be finite");
        Assert.True(double.IsFinite(eri.BearPower), "Bear Power should be finite");
        Assert.True(eri.IsHot, "Should be hot after 100 bars with period=13");
    }
}
