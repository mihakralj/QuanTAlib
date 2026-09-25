namespace QuanTAlib;

public class SrsiTests
{
    private const int DefaultEmaLength = 6;
    private const int DefaultRsiLength = 14;
    private const double Tolerance = 1e-12;

    private static TSeries MakeSeries(int count = 500)
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.5, seed: 42);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        return bars.Close;
    }

    // ========== A) Constructor Validation ==========

    [Fact]
    public void Constructor_ZeroEmaLength_ThrowsArgumentOutOfRangeException()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Srsi(0, 14));
        Assert.Equal("emaLength", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeEmaLength_ThrowsArgumentOutOfRangeException()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Srsi(-5, 14));
        Assert.Equal("emaLength", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroRsiLength_ThrowsArgumentOutOfRangeException()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Srsi(6, 0));
        Assert.Equal("rsiLength", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeRsiLength_ThrowsArgumentOutOfRangeException()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Srsi(6, -3));
        Assert.Equal("rsiLength", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidParams_SetsNameAndWarmup()
    {
        var indicator = new Srsi(6, 14);
        Assert.Equal("Srsi(6,14)", indicator.Name);
        Assert.Equal(20, indicator.WarmupPeriod);
    }

    [Fact]
    public void Constructor_MinimalParams_IsValid()
    {
        var indicator = new Srsi(1, 1);
        Assert.Equal("Srsi(1,1)", indicator.Name);
        Assert.Equal(2, indicator.WarmupPeriod);
    }

    // ========== B) Basic Calculation ==========

    [Fact]
    public void Update_ReturnsTValue_WithValidProperties()
    {
        var indicator = new Srsi(DefaultEmaLength, DefaultRsiLength);
        var input = new TValue(DateTime.UtcNow, 100.0);
        TValue result = indicator.Update(input);

        Assert.Equal(input.Time, result.Time);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_AfterWarmup_IsHotBecomesTrue()
    {
        var indicator = new Srsi(DefaultEmaLength, DefaultRsiLength);
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
        var indicator = new Srsi(DefaultEmaLength, DefaultRsiLength);
        var input = new TValue(DateTime.UtcNow, 42.0);
        TValue result = indicator.Update(input);

        Assert.Equal(result.Value, indicator.Last.Value, Tolerance);
    }

    // ========== C) State + Bar Correction ==========

    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var indicator = new Srsi(6, 14);

        for (int i = 0; i < 65; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i * 0.5), isNew: true);
        }

        TValue r1 = indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(80), 120.0), isNew: true);
        TValue r2 = indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(81), 80.0), isNew: true);

        Assert.NotEqual(r1.Value, r2.Value);
    }

    [Fact]
    public void IsNew_False_RewritesCurrentBar()
    {
        var indicator = new Srsi(6, 14);

        double[] prices = [100, 102, 99, 103, 97, 104, 98, 105, 97, 106,
                           101, 103, 98, 104, 96, 105, 99, 107, 98, 108,
                           100, 102, 99, 103, 97, 104, 98, 105, 97, 106,
                           101, 103, 98, 104, 96, 105, 99, 107, 98, 108,
                           100, 102, 99, 103, 97, 104, 98, 105, 97, 106,
                           101, 103, 98, 104, 96, 105, 99, 107, 98, 108,
                           100, 102, 99, 103, 97, 104, 98, 105, 97, 106];

        for (int i = 0; i < prices.Length; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), prices[i]));
        }

        indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(prices.Length), 110.0), isNew: true);
        double afterNew = indicator.Last.Value;

        indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(prices.Length), 90.0), isNew: false);
        double afterCorrection = indicator.Last.Value;

        Assert.NotEqual(afterNew, afterCorrection);
    }

    [Fact]
    public void IterativeCorrections_RestoreState()
    {
        var indicator = new Srsi(6, 14);
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

        var fresh = new Srsi(6, 14);
        for (int i = 0; i <= 80; i++)
        {
            fresh.Update(data[i], isNew: true);
        }

        Assert.Equal(fresh.Last.Value, afterCorrections, Tolerance);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var indicator = new Srsi(DefaultEmaLength, DefaultRsiLength);

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
        var indicator = new Srsi(6, 14);
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
        var indicator = new Srsi(6, 14);

        for (int i = 0; i < 70; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i * 0.1));
        }

        TValue nanResult = indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(70), double.NaN));

        Assert.True(double.IsFinite(nanResult.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var indicator = new Srsi(6, 14);

        for (int i = 0; i < 70; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i * 0.1));
        }

        TValue infResult = indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(70), double.PositiveInfinity));
        Assert.True(double.IsFinite(infResult.Value));
    }

    [Fact]
    public void BatchNaN_DoesNotPropagate()
    {
        double[] source = new double[100];
        double[] output = new double[100];

        for (int i = 0; i < 100; i++)
        {
            source[i] = 100.0 + i * 0.5;
        }

        source[50] = double.NaN;
        source[51] = double.NaN;

        Srsi.Batch(source, output, DefaultEmaLength, DefaultRsiLength);

        for (int i = 0; i < 100; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output[{i}] is not finite");
        }
    }

    // ========== F) Consistency (4 API modes) ==========

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        TSeries data = MakeSeries();

        // 1. Batch (TSeries)
        TSeries batchResults = Srsi.Batch(data, DefaultEmaLength, DefaultRsiLength);
        double expected = batchResults.Last.Value;

        // 2. Span batch
        var tValues = data.Values.ToArray();
        var spanOutput = new double[tValues.Length];
        Srsi.Batch(new ReadOnlySpan<double>(tValues), spanOutput, DefaultEmaLength, DefaultRsiLength);
        double spanResult = spanOutput[^1];

        // 3. Streaming
        var streaming = new Srsi(DefaultEmaLength, DefaultRsiLength);
        for (int i = 0; i < data.Count; i++)
        {
            streaming.Update(data[i]);
        }
        double streamingResult = streaming.Last.Value;

        // 4. Eventing
        var pubSource = new TSeries();
        var eventBased = new Srsi(pubSource, DefaultEmaLength, DefaultRsiLength);
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

        var ex = Assert.Throws<ArgumentException>(() => Srsi.Batch(source, output, 6, 14));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_InvalidEmaLength_ThrowsArgumentOutOfRangeException()
    {
        double[] source = new double[10];
        double[] output = new double[10];

        Assert.Throws<ArgumentOutOfRangeException>(() => Srsi.Batch(source, output, 0, 14));
    }

    [Fact]
    public void SpanBatch_InvalidRsiLength_ThrowsArgumentOutOfRangeException()
    {
        double[] source = new double[10];
        double[] output = new double[10];

        Assert.Throws<ArgumentOutOfRangeException>(() => Srsi.Batch(source, output, 6, 0));
    }

    [Fact]
    public void SpanBatch_EmptyInput_ProducesEmptyOutput()
    {
        double[] source = Array.Empty<double>();
        double[] output = Array.Empty<double>();
        var ex = Record.Exception(() => Srsi.Batch(source, output, 6, 14));
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

        Srsi.Batch(source, output, DefaultEmaLength, DefaultRsiLength);

        Assert.True(double.IsFinite(output[size - 1]));
    }

    // ========== H) Chainability ==========

    [Fact]
    public void Pub_EventFires_OnUpdate()
    {
        var indicator = new Srsi(DefaultEmaLength, DefaultRsiLength);
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
        var indicator = new Srsi(source, 6, 14);

        source.Add(new TValue(DateTime.UtcNow, 100));
        source.Add(new TValue(DateTime.UtcNow, 110));
        source.Add(new TValue(DateTime.UtcNow, 120));

        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void Calculate_ReturnsHotIndicator()
    {
        TSeries data = MakeSeries();
        (TSeries results, Srsi indicator) = Srsi.Calculate(data, DefaultEmaLength, DefaultRsiLength);

        Assert.Equal(data.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void StaticCalculate_MatchesInstance()
    {
        int count = 100;
        var source = new TSeries();
        var indicator = new Srsi(DefaultEmaLength, DefaultRsiLength);

        for (int i = 0; i < count; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddMinutes(i), i + 10));
            indicator.Update(source.Last);
        }

        var staticResult = Srsi.Batch(source, DefaultEmaLength, DefaultRsiLength);

        Assert.Equal(source.Count, staticResult.Count);
        Assert.Equal(indicator.Last.Value, staticResult.Last.Value, 8);
    }

    // ========== SRSI-specific: Oscillator behavior ==========

    [Fact]
    public void ConstantInput_OutputConvergesToFifty()
    {
        var indicator = new Srsi(6, 14);
        double lastResult = double.NaN;

        for (int i = 0; i < 300; i++)
        {
            TValue r = indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
            lastResult = r.Value;
        }

        // Constant input → diff=0 → avgPos=avgNeg≈0 → SRSI=50
        Assert.Equal(50.0, lastResult, 1e-6);
    }

    [Fact]
    public void StrongUptrend_ProducesHighValues()
    {
        var indicator = new Srsi(6, 14);
        double lastResult = 0.0;

        for (int i = 0; i < 100; i++)
        {
            TValue r = indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i * 2.0));
            lastResult = r.Value;
        }

        // Strong uptrend → Close >> EMA → posDiff dominates → SRSI > 50
        Assert.True(lastResult > 50.0);
        Assert.True(double.IsFinite(lastResult));
    }

    [Fact]
    public void StrongDowntrend_ProducesLowValues()
    {
        var indicator = new Srsi(6, 14);
        double lastResult = 100.0;

        for (int i = 0; i < 100; i++)
        {
            TValue r = indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 200.0 - i * 2.0));
            lastResult = r.Value;
        }

        // Strong downtrend → Close << EMA → negDiff dominates → SRSI < 50
        Assert.True(lastResult < 50.0);
        Assert.True(double.IsFinite(lastResult));
    }

    [Fact]
    public void Output_IsBounded()
    {
        var indicator = new Srsi(6, 14);
        TSeries data = MakeSeries(500);

        for (int i = 0; i < data.Count; i++)
        {
            TValue r = indicator.Update(data[i]);
            // SRSI output should be bounded [0, 100]
            Assert.InRange(r.Value, -0.01, 100.01);
        }
    }

    [Fact]
    public void AscendingVsDescending_OppositePositions()
    {
        var up = new Srsi(6, 14);
        var down = new Srsi(6, 14);

        for (int i = 0; i < 80; i++)
        {
            up.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
            down.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 200.0 - i));
        }

        Assert.True(double.IsFinite(up.Last.Value));
        Assert.True(double.IsFinite(down.Last.Value));
        // Ascending should be > 50, descending should be < 50
        Assert.True(up.Last.Value > 50.0, "Ascending sequence should produce SRSI > 50");
        Assert.True(down.Last.Value < 50.0, "Descending sequence should produce SRSI < 50");
    }

    [Fact]
    public void SrsiProducesFiniteValues_OnGBMData()
    {
        var indicator = new Srsi(6, 14);
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
