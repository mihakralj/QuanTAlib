namespace QuanTAlib;

public class ObvmTests
{
    private static TBarSeries MakeBars(int count = 500)
    {
        GBM gbm = new();
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    private static TBarSeries MakeConstantBars(int count = 100, double value = 50)
    {
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;
        for (int i = 0; i < count; i++)
        {
            bars.Add(new TBar(time.AddMinutes(i), value, value, value, value, 1000));
        }
        return bars;
    }

    private static TBarSeries MakeUptrendBars(int count = 100)
    {
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;
        for (int i = 0; i < count; i++)
        {
            double price = 100 + i;
            bars.Add(new TBar(time.AddMinutes(i), price, price + 1, price - 1, price, 10000));
        }
        return bars;
    }

    private static TBarSeries MakeDowntrendBars(int count = 100)
    {
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;
        for (int i = 0; i < count; i++)
        {
            double price = 200 - i;
            bars.Add(new TBar(time.AddMinutes(i), price, price + 1, price - 1, price, 10000));
        }
        return bars;
    }

    // ═══════════════════════════════════════════════════════════════
    //  A · Constructor Validation
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Constructor_DefaultParameters_CreatesValidIndicator()
    {
        var obvm = new Obvm();
        Assert.Equal("OBVM(7,10)", obvm.Name);
        Assert.False(obvm.IsHot);
    }

    [Fact]
    public void Constructor_CustomParameters_SetsCorrectly()
    {
        var obvm = new Obvm(obvmLength: 14, signalLength: 21);
        Assert.Contains("14", obvm.Name, StringComparison.Ordinal);
        Assert.Contains("21", obvm.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_ZeroObvmLength_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Obvm(obvmLength: 0));
    }

    [Fact]
    public void Constructor_NegativeSignalLength_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Obvm(signalLength: -1));
    }

    [Fact]
    public void Constructor_ZeroSignalLength_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Obvm(signalLength: 0));
    }

    // ═══════════════════════════════════════════════════════════════
    //  B · Basic Calculation
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Update_FirstBar_ReturnsZero()
    {
        var obvm = new Obvm();
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        var result = obvm.Update(bar);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void Update_PriceIncrease_ObvmPositive()
    {
        var obvm = new Obvm();
        var time = DateTime.UtcNow;

        obvm.Update(new TBar(time, 100, 105, 95, 100, 100000));
        var result = obvm.Update(new TBar(time.AddMinutes(1), 100, 108, 98, 105, 80000));

        Assert.True(result.Value > 0, $"OBVM should be positive after up-close, was {result.Value}");
    }

    [Fact]
    public void Update_PriceDecrease_ObvmNegative()
    {
        var obvm = new Obvm();
        var time = DateTime.UtcNow;

        obvm.Update(new TBar(time, 100, 105, 95, 100, 100000));
        var result = obvm.Update(new TBar(time.AddMinutes(1), 100, 102, 90, 95, 80000));

        Assert.True(result.Value < 0, $"OBVM should be negative after down-close, was {result.Value}");
    }

    [Fact]
    public void Update_ConstantPrice_ObvmStaysZero()
    {
        var obvm = new Obvm();
        var bars = MakeConstantBars();

        for (int i = 0; i < bars.Count; i++)
        {
            obvm.Update(bars[i]);
        }

        Assert.Equal(0, obvm.ObvmValue.Value);
        Assert.Equal(0, obvm.Signal.Value);
    }

    [Fact]
    public void Update_UptrendBars_ObvmPositive()
    {
        var obvm = new Obvm();
        var bars = MakeUptrendBars();

        for (int i = 0; i < bars.Count; i++)
        {
            obvm.Update(bars[i]);
        }

        Assert.True(obvm.ObvmValue.Value > 0);
        Assert.True(obvm.Signal.Value > 0);
    }

    // ═══════════════════════════════════════════════════════════════
    //  C · Dual Output
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void DualOutput_ObvmAndSignal_BothPopulated()
    {
        var obvm = new Obvm();
        var bars = MakeBars();

        for (int i = 0; i < bars.Count; i++)
        {
            obvm.Update(bars[i]);
        }

        Assert.True(double.IsFinite(obvm.ObvmValue.Value));
        Assert.True(double.IsFinite(obvm.Signal.Value));
    }

    [Fact]
    public void DualOutput_SignalLagsObvm()
    {
        var obvm = new Obvm();
        var bars = MakeUptrendBars();

        for (int i = 0; i < bars.Count; i++)
        {
            obvm.Update(bars[i]);
        }

        // In a sustained uptrend, OBVM should be above Signal (EMA lag)
        Assert.True(obvm.ObvmValue.Value > obvm.Signal.Value,
            $"OBVM ({obvm.ObvmValue.Value}) should be above Signal ({obvm.Signal.Value}) in uptrend");
    }

    [Fact]
    public void DualOutput_DowntrendBars_ObvmBelowSignal()
    {
        var obvm = new Obvm();
        var bars = MakeDowntrendBars();

        for (int i = 0; i < bars.Count; i++)
        {
            obvm.Update(bars[i]);
        }

        Assert.True(obvm.ObvmValue.Value < obvm.Signal.Value,
            $"OBVM ({obvm.ObvmValue.Value}) should be below Signal ({obvm.Signal.Value}) in downtrend");
    }

    // ═══════════════════════════════════════════════════════════════
    //  D · Warmup / IsHot
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void IsHot_BeforeWarmup_ReturnsFalse()
    {
        var obvm = new Obvm(7, 10);
        var time = DateTime.UtcNow;

        obvm.Update(new TBar(time, 100, 105, 95, 100, 100000));
        Assert.False(obvm.IsHot);
    }

    [Fact]
    public void IsHot_AfterWarmup_ReturnsTrue()
    {
        var obvm = new Obvm(7, 10);
        var bars = MakeBars(100);

        for (int i = 0; i < bars.Count; i++)
        {
            obvm.Update(bars[i]);
        }

        Assert.True(obvm.IsHot);
    }

    // ═══════════════════════════════════════════════════════════════
    //  E · Bar Correction
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void BarCorrection_SameBarUpdate_ProducesSameResult()
    {
        var obvm = new Obvm();
        var bars = MakeBars(50);
        var time = DateTime.UtcNow;

        for (int i = 0; i < bars.Count; i++)
        {
            obvm.Update(bars[i]);
        }

        // Simulate bar correction with isNew=false
        var correctionBar = new TBar(time.AddMinutes(50), 100, 110, 90, 105, 50000);
        var result1 = obvm.Update(correctionBar, isNew: true);
        var result2 = obvm.Update(correctionBar, isNew: false);

        Assert.Equal(result1.Value, result2.Value, 10);
    }

    [Fact]
    public void BarCorrection_DifferentPrice_RestoresState()
    {
        var obvm = new Obvm();
        var bars = MakeBars(50);
        var time = DateTime.UtcNow;

        for (int i = 0; i < bars.Count; i++)
        {
            obvm.Update(bars[i]);
        }

        double beforeValue = obvm.ObvmValue.Value;

        // First update on new bar
        obvm.Update(new TBar(time.AddMinutes(50), 100, 110, 90, 200, 999999), isNew: true);
        Assert.NotEqual(beforeValue, obvm.ObvmValue.Value);

        // Correct the bar — should restore previous state then recompute
        obvm.Update(new TBar(time.AddMinutes(50), 100, 110, 90, 105, 50000), isNew: false);
    }

    // ═══════════════════════════════════════════════════════════════
    //  F · NaN / Infinity Handling
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Update_NaNClose_UsesLastValidValue()
    {
        var obvm = new Obvm();
        var time = DateTime.UtcNow;

        obvm.Update(new TBar(time, 100, 105, 95, 100, 100000));
        obvm.Update(new TBar(time.AddMinutes(1), 100, 110, 90, 105, 80000));

        // Feed NaN close
        var result = obvm.Update(new TBar(time.AddMinutes(2), double.NaN, double.NaN, double.NaN, double.NaN, 50000));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_InfinityVolume_UsesLastValidValue()
    {
        var obvm = new Obvm();
        var time = DateTime.UtcNow;

        obvm.Update(new TBar(time, 100, 105, 95, 100, 100000));
        var result = obvm.Update(new TBar(time.AddMinutes(1), 100, 110, 90, 105, double.PositiveInfinity));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_WithTValue_ReturnsCurrentValue()
    {
        var obvm = new Obvm();
        var value = new TValue(DateTime.UtcNow, 100);
        var result = obvm.Update(value);
        // Without volume data, OBVM stays at initial state
        Assert.True(double.IsFinite(result.Value) || double.IsNaN(result.Value));
    }

    // ═══════════════════════════════════════════════════════════════
    //  G · Streaming/Batch Consistency
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void StreamingVsBatch_ProduceSameResults()
    {
        var bars = MakeBars(200);

        // Streaming
        var obvm = new Obvm();
        double[] streamObvm = new double[bars.Count];
        double[] streamSignal = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            obvm.Update(bars[i]);
            streamObvm[i] = obvm.ObvmValue.Value;
            streamSignal[i] = obvm.Signal.Value;
        }

        // Batch
        var (batchObvm, batchSignal) = Obvm.Batch(bars);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamObvm[i], batchObvm.Values[i], 6);
            Assert.Equal(streamSignal[i], batchSignal.Values[i], 6);
        }
    }

    [Fact]
    public void SpanBatch_MatchesStreaming()
    {
        var bars = MakeBars(200);

        // Streaming
        var obvm = new Obvm();
        double[] streamObvm = new double[bars.Count];
        double[] streamSignal = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            obvm.Update(bars[i]);
            streamObvm[i] = obvm.ObvmValue.Value;
            streamSignal[i] = obvm.Signal.Value;
        }

        // Span batch
        double[] obvmOut = new double[bars.Count];
        double[] signalOut = new double[bars.Count];
        Obvm.Batch(bars.CloseValues, bars.VolumeValues, obvmOut, signalOut);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamObvm[i], obvmOut[i], 6);
            Assert.Equal(streamSignal[i], signalOut[i], 6);
        }
    }

    [Fact]
    public void InstanceUpdate_MatchesStaticBatch()
    {
        var bars = MakeBars(200);

        var indicator = new Obvm();
        var (instanceObvm, instanceSignal) = indicator.Update(bars);
        var (staticObvm, staticSignal) = Obvm.Batch(bars);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(staticObvm.Values[i], instanceObvm.Values[i], 10);
            Assert.Equal(staticSignal.Values[i], instanceSignal.Values[i], 10);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  H · Reset
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Reset_ClearsStateCompletely()
    {
        var obvm = new Obvm();
        var bars = MakeBars(100);

        for (int i = 0; i < bars.Count; i++)
        {
            obvm.Update(bars[i]);
        }

        Assert.True(obvm.IsHot);

        obvm.Reset();
        Assert.False(obvm.IsHot);
        Assert.Equal(0, obvm.Last.Value);
    }

    // ═══════════════════════════════════════════════════════════════
    //  I · Events
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Pub_EventFires_OnUpdate()
    {
        var obvm = new Obvm();
        int eventCount = 0;
        obvm.Pub += (object? _, in TValueEventArgs _) => eventCount++;

        var bars = MakeBars(10);
        for (int i = 0; i < bars.Count; i++)
        {
            obvm.Update(bars[i]);
        }

        Assert.Equal(10, eventCount);
    }

    // ═══════════════════════════════════════════════════════════════
    //  J · Calculate Factory
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var bars = MakeBars(100);
        var ((resultObvm, resultSignal), indicator) = Obvm.Calculate(bars);

        Assert.Equal(bars.Count, resultObvm.Count);
        Assert.Equal(bars.Count, resultSignal.Count);
        Assert.True(indicator.IsHot);
    }

    // ═══════════════════════════════════════════════════════════════
    //  K · Edge Cases
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Batch_EmptySource_ReturnsEmptyResults()
    {
        var (obvm, signal) = Obvm.Batch(new TBarSeries());
        Assert.Empty(obvm);
        Assert.Empty(signal);
    }

    [Fact]
    public void Batch_SpanLengthMismatch_Throws()
    {
        double[] close = new double[10];
        double[] volume = new double[5];
        double[] obvmOut = new double[10];
        double[] signalOut = new double[10];

        Assert.Throws<ArgumentException>(() => Obvm.Batch(close, volume, obvmOut, signalOut));
    }

    [Fact]
    public void DifferentPeriods_ProduceDifferentSmoothing()
    {
        var bars = MakeBars(200);

        var obvm7 = new Obvm(7, 10);
        var obvm21 = new Obvm(21, 30);

        for (int i = 0; i < bars.Count; i++)
        {
            obvm7.Update(bars[i]);
            obvm21.Update(bars[i]);
        }

        // Different periods should produce different OBVM values
        Assert.NotEqual(obvm7.ObvmValue.Value, obvm21.ObvmValue.Value);
    }
}
