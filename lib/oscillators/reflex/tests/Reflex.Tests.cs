namespace QuanTAlib;

public class ReflexTests
{
    private const int DefaultPeriod = 20;
    private const double Tolerance = 1e-12;

    private static TSeries MakeSeries(int count = 500)
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.5, seed: 42);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        return bars.Close;
    }

    // ========== A) Constructor Validation ==========

    [Fact]
    public void Constructor_ZeroPeriod_ThrowsArgumentOutOfRangeException()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Reflex(0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_OnePeriod_ThrowsArgumentOutOfRangeException()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Reflex(1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_ThrowsArgumentOutOfRangeException()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Reflex(-5));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidPeriod_SetsNameAndWarmup()
    {
        var indicator = new Reflex(20);
        Assert.Equal("Reflex(20)", indicator.Name);
        Assert.Equal(20, indicator.WarmupPeriod);
    }

    [Fact]
    public void Constructor_PeriodTwo_IsValid()
    {
        var indicator = new Reflex(2);
        Assert.Equal("Reflex(2)", indicator.Name);
        Assert.Equal(2, indicator.WarmupPeriod);
    }

    // ========== B) Basic Calculation ==========

    [Fact]
    public void Update_ReturnsTValue_WithValidProperties()
    {
        var indicator = new Reflex(DefaultPeriod);
        var input = new TValue(DateTime.UtcNow, 100.0);
        TValue result = indicator.Update(input);

        Assert.Equal(input.Time, result.Time);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_AfterWarmup_IsHotBecomesTrue()
    {
        var indicator = new Reflex(DefaultPeriod);
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
        var indicator = new Reflex(DefaultPeriod);
        var input = new TValue(DateTime.UtcNow, 42.0);
        TValue result = indicator.Update(input);

        Assert.Equal(result.Value, indicator.Last.Value, Tolerance);
    }

    // ========== C) State + Bar Correction ==========

    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var indicator = new Reflex(DefaultPeriod);

        // Warm up past the period threshold first
        for (int i = 0; i < DefaultPeriod + 5; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + (i * 0.5)), isNew: true);
        }

        TValue r1 = indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(30), 120.0), isNew: true);
        TValue r2 = indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(31), 80.0), isNew: true);

        // Two very different bars after warmup must produce different results
        Assert.NotEqual(r1.Value, r2.Value);
    }

    [Fact]
    public void IsNew_False_RewritesCurrentBar()
    {
        var indicator = new Reflex(DefaultPeriod);

        for (int i = 0; i < 50; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }

        indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(50), 200.0), isNew: true);
        double afterNew = indicator.Last.Value;

        indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(50), 150.0), isNew: false);
        double afterCorrection = indicator.Last.Value;

        Assert.NotEqual(afterNew, afterCorrection);
    }

    [Fact]
    public void IterativeCorrections_RestoreState()
    {
        var indicator = new Reflex(DefaultPeriod);
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

        var fresh = new Reflex(DefaultPeriod);
        for (int i = 0; i <= 50; i++)
        {
            fresh.Update(data[i], isNew: true);
        }

        Assert.Equal(fresh.Last.Value, afterCorrections, Tolerance);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var indicator = new Reflex(DefaultPeriod);

        for (int i = 0; i < 50; i++)
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
        var indicator = new Reflex(10);
        int hotAt = -1;

        for (int i = 0; i < 200; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
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
        var indicator = new Reflex(DefaultPeriod);

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
        var indicator = new Reflex(DefaultPeriod);

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
        int period = 10;
        double[] source = new double[100];
        double[] output = new double[100];

        for (int i = 0; i < 100; i++)
        {
            source[i] = 100.0 + (i * 0.5);
        }

        source[50] = double.NaN;
        source[51] = double.NaN;

        Reflex.Batch(source, output, period);

        for (int i = 0; i < 100; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output[{i}] is not finite");
        }
    }

    // ========== F) Consistency (4 API modes) ==========

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        int period = 10;
        TSeries data = MakeSeries();

        // 1. Batch (TSeries)
        TSeries batchResults = Reflex.Batch(data, period);
        double expected = batchResults.Last.Value;

        // 2. Span batch
        var tValues = data.Values.ToArray();
        var spanOutput = new double[tValues.Length];
        Reflex.Batch(new ReadOnlySpan<double>(tValues), spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming
        var streaming = new Reflex(period);
        for (int i = 0; i < data.Count; i++)
        {
            streaming.Update(data[i]);
        }
        double streamingResult = streaming.Last.Value;

        // 4. Eventing
        var pubSource = new TSeries();
        var eventBased = new Reflex(pubSource, period);
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

        var ex = Assert.Throws<ArgumentException>(() => Reflex.Batch(source, output, 5));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_PeriodOne_ThrowsArgumentOutOfRangeException()
    {
        double[] source = new double[10];
        double[] output = new double[10];

        Assert.Throws<ArgumentOutOfRangeException>(() => Reflex.Batch(source, output, 1));
    }

    [Fact]
    public void SpanBatch_EmptyInput_ProducesEmptyOutput()
    {
        double[] source = Array.Empty<double>();
        double[] output = Array.Empty<double>();
        var ex = Record.Exception(() => Reflex.Batch(source, output, 10));
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

        Reflex.Batch(source, output, 20);

        Assert.True(double.IsFinite(output[size - 1]));
    }

    // ========== H) Chainability ==========

    [Fact]
    public void Pub_EventFires_OnUpdate()
    {
        var indicator = new Reflex(DefaultPeriod);
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
        var indicator = new Reflex(source, 5);

        source.Add(new TValue(DateTime.UtcNow, 100));
        source.Add(new TValue(DateTime.UtcNow, 110));
        source.Add(new TValue(DateTime.UtcNow, 120));

        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void Calculate_ReturnsHotIndicator()
    {
        TSeries data = MakeSeries();
        (TSeries results, Reflex indicator) = Reflex.Calculate(data, DefaultPeriod);

        Assert.Equal(data.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void StaticCalculate_MatchesInstance()
    {
        const int period = 10;
        int count = 100;
        var source = new TSeries();
        var indicator = new Reflex(period);

        for (int i = 0; i < count; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddMinutes(i), i + 10));
            indicator.Update(source.Last);
        }

        var staticResult = Reflex.Batch(source, period);

        Assert.Equal(source.Count, staticResult.Count);
        Assert.Equal(indicator.Last.Value, staticResult.Last.Value, 8);
    }

    // ========== Reflex-specific: Oscillator behavior ==========

    [Fact]
    public void ConstantInput_OutputConvergesToZero()
    {
        var indicator = new Reflex(10);
        double lastResult = double.NaN;

        for (int i = 0; i < 300; i++)
        {
            TValue r = indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
            lastResult = r.Value;
        }

        // Constant input → zero deviation from linear extrapolation → zero output
        Assert.Equal(0.0, lastResult, 1e-10);
    }

    [Fact]
    public void ReflexProducesFiniteValues_OnGBMData()
    {
        var indicator = new Reflex(10);
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
