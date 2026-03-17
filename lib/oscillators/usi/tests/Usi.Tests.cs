namespace QuanTAlib;

public class UsiTests
{
    private const int DefaultPeriod = 28;
    private const double Tolerance = 1e-12;

    private static TSeries MakeSeries(int count = 500)
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.5, seed: 42);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        return bars.Close;
    }

    // ========== A) Constructor Validation ==========

    [Fact]
    public void Constructor_ZeroPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Usi(0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Usi(-5));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidPeriod_SetsNameAndWarmup()
    {
        var indicator = new Usi(28);
        Assert.Equal("Usi(28)", indicator.Name);
        Assert.Equal(32, indicator.WarmupPeriod); // 28 + 4
    }

    [Fact]
    public void Constructor_PeriodOne_IsValid()
    {
        var indicator = new Usi(1);
        Assert.Equal("Usi(1)", indicator.Name);
        Assert.Equal(5, indicator.WarmupPeriod); // 1 + 4
    }

    [Fact]
    public void Constructor_DefaultPeriod_IsTwentyEight()
    {
        var indicator = new Usi();
        Assert.Equal("Usi(28)", indicator.Name);
    }

    // ========== B) Basic Calculation ==========

    [Fact]
    public void Update_ReturnsTValue_WithValidProperties()
    {
        var indicator = new Usi(DefaultPeriod);
        var input = new TValue(DateTime.UtcNow, 100.0);
        TValue result = indicator.Update(input);

        Assert.Equal(input.Time, result.Time);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_AfterWarmup_IsHotBecomesTrue()
    {
        var indicator = new Usi(DefaultPeriod);
        Assert.False(indicator.IsHot);

        for (int i = 0; i < 500; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i * 0.1));
        }

        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Update_LastProperty_MatchesReturnValue()
    {
        var indicator = new Usi(DefaultPeriod);
        var input = new TValue(DateTime.UtcNow, 42.0);
        TValue result = indicator.Update(input);

        Assert.Equal(result.Value, indicator.Last.Value, Tolerance);
    }

    // ========== C) State + Bar Correction ==========

    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var indicator = new Usi(10);

        for (int i = 0; i < 50; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i * 0.5), isNew: true);
        }

        TValue r1 = indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(60), 200.0), isNew: true);
        TValue r2 = indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(61), 50.0), isNew: true);

        Assert.NotEqual(r1.Value, r2.Value);
    }

    [Fact]
    public void IsNew_False_RewritesCurrentBar()
    {
        var indicator = new Usi(10);

        double[] prices = [100, 102, 99, 103, 97, 104, 98, 105, 97, 106,
                           101, 103, 98, 104, 96, 105, 99, 107, 98, 108,
                           100, 102, 99, 103, 97, 104, 98, 105, 97, 106,
                           101, 103, 98, 104, 96, 105, 99, 107, 98, 108,
                           100, 102, 99, 103, 97, 104, 98, 105, 97, 106];

        for (int i = 0; i < prices.Length; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), prices[i]));
        }

        indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(prices.Length), 200.0), isNew: true);
        double afterNew = indicator.Last.Value;

        indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(prices.Length), 50.0), isNew: false);
        double afterCorrection = indicator.Last.Value;

        Assert.NotEqual(afterNew, afterCorrection);
    }

    [Fact]
    public void IterativeCorrections_RestoreState()
    {
        var indicator = new Usi(10);
        TSeries data = MakeSeries();

        for (int i = 0; i < 80; i++)
        {
            indicator.Update(data[i], isNew: true);
        }

        indicator.Update(data[80], isNew: true);

        for (int j = 0; j < 5; j++)
        {
            indicator.Update(data[80], isNew: false);
        }

        double afterCorrections = indicator.Last.Value;

        var fresh = new Usi(10);
        for (int i = 0; i <= 80; i++)
        {
            fresh.Update(data[i], isNew: true);
        }

        Assert.Equal(fresh.Last.Value, afterCorrections, Tolerance);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var indicator = new Usi(DefaultPeriod);

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
        var indicator = new Usi(10);
        int hotAt = -1;

        for (int i = 0; i < 200; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i * 0.1));
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
        var indicator = new Usi(10);

        for (int i = 0; i < 50; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i * 0.1));
        }

        TValue nanResult = indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(50), double.NaN));

        Assert.True(double.IsFinite(nanResult.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var indicator = new Usi(10);

        for (int i = 0; i < 50; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i * 0.1));
        }

        TValue infResult = indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(50), double.PositiveInfinity));
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
            source[i] = 100.0 + i * 0.5;
        }

        source[50] = double.NaN;
        source[51] = double.NaN;

        Usi.Batch(source, output, period);

        for (int i = 0; i < 100; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output[{i}] is not finite");
        }
    }

    // ========== F) Consistency (4 API modes) ==========

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        int period = 14;
        TSeries data = MakeSeries();

        // 1. Batch (TSeries)
        TSeries batchResults = Usi.Batch(data, period);
        double expected = batchResults.Last.Value;

        // 2. Span batch
        var tValues = data.Values.ToArray();
        var spanOutput = new double[tValues.Length];
        Usi.Batch(new ReadOnlySpan<double>(tValues), spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming
        var streaming = new Usi(period);
        for (int i = 0; i < data.Count; i++)
        {
            streaming.Update(data[i]);
        }
        double streamingResult = streaming.Last.Value;

        // 4. Eventing
        var pubSource = new TSeries();
        var eventBased = new Usi(pubSource, period);
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

        var ex = Assert.Throws<ArgumentException>(() => Usi.Batch(source, output, 14));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_ZeroPeriod_ThrowsArgumentException()
    {
        double[] source = new double[10];
        double[] output = new double[10];

        Assert.Throws<ArgumentException>(() => Usi.Batch(source, output, 0));
    }

    [Fact]
    public void SpanBatch_EmptyInput_ProducesEmptyOutput()
    {
        double[] source = Array.Empty<double>();
        double[] output = Array.Empty<double>();
        var ex = Record.Exception(() => Usi.Batch(source, output, 10));
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
            source[i] = 100.0 + i * 0.1;
        }

        Usi.Batch(source, output, 28);

        Assert.True(double.IsFinite(output[size - 1]));
    }

    // ========== H) Chainability ==========

    [Fact]
    public void Pub_EventFires_OnUpdate()
    {
        var indicator = new Usi(DefaultPeriod);
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
        var indicator = new Usi(source, 5);

        source.Add(new TValue(DateTime.UtcNow, 100));
        source.Add(new TValue(DateTime.UtcNow, 110));
        source.Add(new TValue(DateTime.UtcNow, 120));

        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void Calculate_ReturnsHotIndicator()
    {
        TSeries data = MakeSeries();
        (TSeries results, Usi indicator) = Usi.Calculate(data, DefaultPeriod);

        Assert.Equal(data.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void StaticCalculate_MatchesInstance()
    {
        const int period = 14;
        int count = 100;
        var source = new TSeries();
        var indicator = new Usi(period);

        for (int i = 0; i < count; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddMinutes(i), i + 10));
            indicator.Update(source.Last);
        }

        var staticResult = Usi.Batch(source, period);

        Assert.Equal(source.Count, staticResult.Count);
        Assert.Equal(indicator.Last.Value, staticResult.Last.Value, 8);
    }

    // ========== USI-specific: Oscillator behavior ==========

    [Fact]
    public void ConstantInput_OutputConvergesToZero()
    {
        var indicator = new Usi(14);
        double lastResult = double.NaN;

        for (int i = 0; i < 300; i++)
        {
            TValue r = indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
            lastResult = r.Value;
        }

        // Constant input → SU=0, SD=0 → USI stays at 0
        Assert.Equal(0.0, lastResult, 1e-10);
    }

    [Fact]
    public void StrongUptrend_USI_ApproachesPositiveOne()
    {
        var indicator = new Usi(14);
        double lastResult = 0.0;

        for (int i = 0; i < 200; i++)
        {
            TValue r = indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i * 2.0));
            lastResult = r.Value;
        }

        // Strong uptrend: SU always > 0, SD always = 0
        // USI should approach +1
        Assert.True(lastResult > 0.5, $"Expected USI > 0.5 for uptrend, got {lastResult}");
    }

    [Fact]
    public void StrongDowntrend_USI_ApproachesNegativeOne()
    {
        var indicator = new Usi(14);
        double lastResult = 0.0;

        for (int i = 0; i < 200; i++)
        {
            TValue r = indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 500.0 - i * 2.0));
            lastResult = r.Value;
        }

        // Strong downtrend: SD always > 0, SU always = 0
        // USI should approach -1
        Assert.True(lastResult < -0.5, $"Expected USI < -0.5 for downtrend, got {lastResult}");
    }

    [Fact]
    public void Output_IsBounded()
    {
        var indicator = new Usi(14);
        TSeries data = MakeSeries(500);

        for (int i = 0; i < data.Count; i++)
        {
            TValue r = indicator.Update(data[i]);
            Assert.InRange(r.Value, -1.01, 1.01);
        }
    }

    [Fact]
    public void UsiIsSymmetric_UpVsDown()
    {
        var up = new Usi(14);
        var down = new Usi(14);

        for (int i = 0; i < 100; i++)
        {
            up.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
            down.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 200.0 - i));
        }

        Assert.True(double.IsFinite(up.Last.Value));
        Assert.True(double.IsFinite(down.Last.Value));
        // USI of uptrend and downtrend should have opposite signs
        Assert.True(up.Last.Value > 0, "Uptrend USI should be positive");
        Assert.True(down.Last.Value < 0, "Downtrend USI should be negative");
    }

    [Fact]
    public void UsiProducesFiniteValues_OnGBMData()
    {
        var indicator = new Usi(14);
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

    [Theory]
    [InlineData(5)]
    [InlineData(14)]
    [InlineData(28)]
    [InlineData(56)]
    public void DifferentPeriods_AllProduceFiniteResults(int period)
    {
        var indicator = new Usi(period);
        TSeries data = MakeSeries(300);

        for (int i = 0; i < data.Count; i++)
        {
            TValue r = indicator.Update(data[i]);
            Assert.True(double.IsFinite(r.Value), $"Non-finite at bar {i} with period {period}");
        }
    }
}
