namespace QuanTAlib;

public class DoscTests
{
    private const int DefaultRsi = 14;
    private const int DefaultEma1 = 5;
    private const int DefaultEma2 = 3;
    private const int DefaultSig = 9;
    private const double Tolerance = 1e-12;

    private static TSeries MakeSeries(int count = 500)
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.5, seed: 42);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        return bars.Close;
    }

    // ========== A) Constructor Validation ==========

    [Fact]
    public void Constructor_ZeroRsiPeriod_ThrowsArgumentOutOfRangeException()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Dosc(rsiPeriod: 0));
        Assert.Equal("rsiPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeRsiPeriod_ThrowsArgumentOutOfRangeException()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Dosc(rsiPeriod: -5));
        Assert.Equal("rsiPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroEma1Period_ThrowsArgumentOutOfRangeException()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Dosc(rsiPeriod: 14, ema1Period: 0));
        Assert.Equal("ema1Period", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroEma2Period_ThrowsArgumentOutOfRangeException()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Dosc(rsiPeriod: 14, ema1Period: 5, ema2Period: 0));
        Assert.Equal("ema2Period", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroSigPeriod_ThrowsArgumentOutOfRangeException()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Dosc(rsiPeriod: 14, ema1Period: 5, ema2Period: 3, sigPeriod: 0));
        Assert.Equal("sigPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidDefaults_SetsNameAndWarmup()
    {
        var indicator = new Dosc();
        Assert.Equal("Dosc(14,5,3,9)", indicator.Name);
        Assert.Equal(14 + 9, indicator.WarmupPeriod); // rsiPeriod + sigPeriod
    }

    [Fact]
    public void Constructor_CustomParams_SetsName()
    {
        var indicator = new Dosc(rsiPeriod: 7, ema1Period: 3, ema2Period: 2, sigPeriod: 5);
        Assert.Equal("Dosc(7,3,2,5)", indicator.Name);
    }

    // ========== B) Basic Calculation ==========

    [Fact]
    public void Update_ReturnsTValue_WithValidProperties()
    {
        var indicator = new Dosc();
        var input = new TValue(DateTime.UtcNow, 100.0);
        TValue result = indicator.Update(input);

        Assert.Equal(input.Time, result.Time);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_AfterWarmup_IsHotBecomesTrue()
    {
        var indicator = new Dosc();
        Assert.False(indicator.IsHot);

        for (int i = 0; i < 500; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + (i * 0.1)));
        }

        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Update_LastProperty_MatchesReturnValue()
    {
        var indicator = new Dosc();
        TValue result = indicator.Update(new TValue(DateTime.UtcNow, 42.0));
        Assert.Equal(result.Value, indicator.Last.Value, Tolerance);
    }

    // ========== C) State + Bar Correction ==========

    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var indicator = new Dosc(DefaultRsi, DefaultEma1, DefaultEma2, DefaultSig);

        // Warm up well past the period threshold
        for (int i = 0; i < DefaultRsi + DefaultSig + 10; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + (i * 0.5)), isNew: true);
        }

        TValue r1 = indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(50), 120.0), isNew: true);
        TValue r2 = indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(51), 80.0), isNew: true);

        Assert.NotEqual(r1.Value, r2.Value);
    }

    [Fact]
    public void IsNew_False_RewritesCurrentBar()
    {
        var indicator = new Dosc(DefaultRsi, DefaultEma1, DefaultEma2, DefaultSig);

        for (int i = 0; i < 60; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }

        indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(60), 200.0), isNew: true);
        double afterNew = indicator.Last.Value;

        indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(60), 150.0), isNew: false);
        double afterCorrection = indicator.Last.Value;

        Assert.NotEqual(afterNew, afterCorrection);
    }

    [Fact]
    public void IterativeCorrections_RestoreState()
    {
        var indicator = new Dosc(DefaultRsi, DefaultEma1, DefaultEma2, DefaultSig);
        TSeries data = MakeSeries();

        for (int i = 0; i < 50; i++)
        {
            indicator.Update(data[i], isNew: true);
        }

        indicator.Update(data[50], isNew: true);

        for (int j = 0; j < 5; j++)
        {
            indicator.Update(data[50], isNew: false);
        }

        double afterCorrections = indicator.Last.Value;

        var fresh = new Dosc(DefaultRsi, DefaultEma1, DefaultEma2, DefaultSig);
        for (int i = 0; i <= 50; i++)
        {
            fresh.Update(data[i], isNew: true);
        }

        Assert.Equal(fresh.Last.Value, afterCorrections, Tolerance);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var indicator = new Dosc();

        for (int i = 0; i < 100; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }

        Assert.True(indicator.IsHot);

        indicator.Reset();

        Assert.False(indicator.IsHot);
        Assert.Equal(default, indicator.Last);
    }

    // ========== D) Warmup/Convergence ==========

    [Fact]
    public void IsHot_FlipsAtCorrectTime()
    {
        var indicator = new Dosc(rsiPeriod: 5, ema1Period: 3, ema2Period: 2, sigPeriod: 5);
        int hotAt = -1;

        for (int i = 0; i < 200; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + (i * 0.1)));
            if (indicator.IsHot && hotAt < 0)
            {
                hotAt = i;
                break;
            }
        }

        Assert.InRange(hotAt, 1, 200);
    }

    // ========== E) Robustness ==========

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var indicator = new Dosc();

        for (int i = 0; i < 30; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }

        TValue nanResult = indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(30), double.NaN));
        Assert.True(double.IsFinite(nanResult.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var indicator = new Dosc();

        for (int i = 0; i < 30; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }

        TValue infResult = indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(30), double.PositiveInfinity));
        Assert.True(double.IsFinite(infResult.Value));
    }

    [Fact]
    public void BatchNaN_DoesNotPropagate()
    {
        double[] source = new double[100];
        double[] output = new double[100];

        for (int i = 0; i < 100; i++)
        {
            source[i] = 100.0 + (i * 0.5);
        }

        source[50] = double.NaN;
        source[51] = double.NaN;

        Dosc.Batch(source, output, 14, 5, 3, 9);

        for (int i = 0; i < 100; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output[{i}] is not finite");
        }
    }

    // ========== F) Consistency (4 API modes) ==========

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        int rsi = 7, e1 = 3, e2 = 2, sig = 5;
        TSeries data = MakeSeries();

        // 1. Batch (TSeries)
        TSeries batchResults = Dosc.Batch(data, rsi, e1, e2, sig);
        double expected = batchResults.Last.Value;

        // 2. Span batch
        var values = data.Values.ToArray();
        var spanOutput = new double[values.Length];
        Dosc.Batch(new ReadOnlySpan<double>(values), spanOutput, rsi, e1, e2, sig);
        double spanResult = spanOutput[^1];

        // 3. Streaming
        var streaming = new Dosc(rsi, e1, e2, sig);
        for (int i = 0; i < data.Count; i++)
        {
            streaming.Update(data[i]);
        }
        double streamingResult = streaming.Last.Value;

        // 4. Eventing
        var pubSource = new TSeries();
        var eventBased = new Dosc(pubSource, rsi, e1, e2, sig);
        for (int i = 0; i < data.Count; i++)
        {
            pubSource.Add(data[i]);
        }
        double eventingResult = eventBased.Last.Value;

        Assert.Equal(expected, spanResult, precision: 9);
        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, eventingResult, precision: 9);
    }

    // ========== G) Span API Tests ==========

    [Fact]
    public void SpanBatch_MismatchedLengths_ThrowsArgumentException()
    {
        double[] source = new double[10];
        double[] output = new double[5];

        var ex = Assert.Throws<ArgumentException>(() => Dosc.Batch(source, output, 14, 5, 3, 9));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_ZeroRsiPeriod_ThrowsArgumentOutOfRangeException()
    {
        double[] source = new double[10];
        double[] output = new double[10];

        Assert.Throws<ArgumentOutOfRangeException>(() => Dosc.Batch(source, output, rsiPeriod: 0));
    }

    [Fact]
    public void SpanBatch_ZeroSigPeriod_ThrowsArgumentOutOfRangeException()
    {
        double[] source = new double[10];
        double[] output = new double[10];

        Assert.Throws<ArgumentOutOfRangeException>(() => Dosc.Batch(source, output, sigPeriod: 0));
    }

    [Fact]
    public void SpanBatch_EmptyInput_ProducesNoException()
    {
        double[] source = Array.Empty<double>();
        double[] output = Array.Empty<double>();
        var ex = Record.Exception(() => Dosc.Batch(source, output, 14, 5, 3, 9));
        Assert.Null(ex);
    }

    [Fact]
    public void SpanBatch_LargeData_DoesNotStackOverflow()
    {
        int size = 5000;
        double[] source = new double[size];
        double[] output = new double[size];

        for (int i = 0; i < size; i++)
        {
            source[i] = 100.0 + (i * 0.1);
        }

        Dosc.Batch(source, output, 14, 5, 3, 9);
        Assert.True(double.IsFinite(output[size - 1]));
    }

    // ========== H) Chainability ==========

    [Fact]
    public void Pub_EventFires_OnUpdate()
    {
        var indicator = new Dosc();
        int eventCount = 0;

        indicator.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        for (int i = 0; i < 10; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }

        Assert.Equal(10, eventCount);
    }

    [Fact]
    public void EventBased_Chaining_Works()
    {
        var source = new TSeries();
        var indicator = new Dosc(source, 5, 3, 2, 4);

        source.Add(new TValue(DateTime.UtcNow, 100));
        source.Add(new TValue(DateTime.UtcNow, 110));
        source.Add(new TValue(DateTime.UtcNow, 120));

        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void Calculate_ReturnsHotIndicator()
    {
        TSeries data = MakeSeries();
        (TSeries results, Dosc indicator) = Dosc.Calculate(data);

        Assert.Equal(data.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    // ========== DOSC-specific: Oscillator behavior ==========

    [Fact]
    public void ConstantInput_OutputConvergesToZero()
    {
        // With all constant input, RSI is constant, EMA1 == EMA2 == constant,
        // SMA signal converges to the same constant → DOSC → 0
        var indicator = new Dosc(rsiPeriod: 5, ema1Period: 3, ema2Period: 2, sigPeriod: 5);
        double lastResult = double.NaN;

        for (int i = 0; i < 500; i++)
        {
            TValue r = indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
            lastResult = r.Value;
        }

        Assert.True(Math.Abs(lastResult) < 1e-6, $"Expected near-zero for constant input, got {lastResult}");
    }

    [Fact]
    public void DoscProducesFiniteValues_OnGBMData()
    {
        var indicator = new Dosc();
        TSeries data = MakeSeries(200);

        int nonFiniteCount = 0;
        for (int i = 0; i < data.Count; i++)
        {
            TValue r = indicator.Update(data[i]);
            if (!double.IsFinite(r.Value))
            {
                nonFiniteCount++;
            }
        }

        Assert.Equal(0, nonFiniteCount);
    }
}
