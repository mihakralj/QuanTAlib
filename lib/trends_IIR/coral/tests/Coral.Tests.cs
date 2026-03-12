namespace QuanTAlib.Tests;

public class CoralTests
{
    private readonly GBM _gbm;

    public CoralTests()
    {
        _gbm = new GBM();
    }

    // A) Constructor validation
    [Fact]
    public void Constructor_PeriodZero_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Coral(0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_PeriodNegative_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Coral(-1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_CdNegative_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Coral(10, cd: -0.1));
        Assert.Equal("cd", ex.ParamName);
    }

    [Fact]
    public void Constructor_CdAboveOne_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Coral(10, cd: 1.1));
        Assert.Equal("cd", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidParams_SetsName()
    {
        var coral = new Coral(21, 0.4);
        Assert.Contains("Coral", coral.Name, StringComparison.Ordinal);
        Assert.Contains("21", coral.Name, StringComparison.Ordinal);
    }

    // B) Basic calculation
    [Fact]
    public void Update_ReturnsValue()
    {
        var coral = new Coral(10);
        TValue result = coral.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var coral = new Coral(10);
        coral.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(coral.Last.Value));
    }

    [Fact]
    public void Update_ConstantInput_ConvergesToInput()
    {
        var coral = new Coral(5, 0.4);
        double constant = 50.0;
        for (int i = 0; i < 200; i++)
        {
            coral.Update(new TValue(DateTime.UtcNow.AddMinutes(i), constant));
        }
        Assert.Equal(constant, coral.Last.Value, 6);
    }

    // C) State + bar correction
    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var coral = new Coral(5);
        coral.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        double first = coral.Last.Value;
        coral.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 110.0), isNew: true);
        double second = coral.Last.Value;
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void IsNew_False_UpdatesWithoutAdvancing()
    {
        var coral = new Coral(5);
        coral.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);

        coral.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 200.0), isNew: true);

        // Correct with isNew=false — should revert to state after first bar
        coral.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 100.0), isNew: false);
        double corrected = coral.Last.Value;

        var coral2 = new Coral(5);
        coral2.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        coral2.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 100.0), isNew: true);
        Assert.Equal(coral2.Last.Value, corrected, 12);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        int period = 10;
        var bars = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        var coral = new Coral(period);

        for (int i = 0; i < series.Count; i++)
        {
            coral.Update(series[i]);
        }
        double stateAfterN = coral.Last.Value;

        // Multiple corrections
        for (int j = 0; j < 10; j++)
        {
            coral.Update(new TValue(DateTime.UtcNow.AddMinutes(50 + j), 999.0), isNew: false);
        }

        // Restore original Nth value
        coral.Update(series[^1], isNew: false);
        Assert.Equal(stateAfterN, coral.Last.Value, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var coral = new Coral(10);
        var bars = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        for (int i = 0; i < series.Count; i++)
        {
            coral.Update(series[i]);
        }
        Assert.True(coral.IsHot);

        coral.Reset();
        Assert.False(coral.IsHot);
        Assert.Equal(default, coral.Last);
    }

    // D) Warmup/convergence
    [Fact]
    public void IsHot_BecomesTrueAtWarmupPeriod()
    {
        int period = 10;
        var coral = new Coral(period);

        for (int i = 0; i < period - 1; i++)
        {
            coral.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + i));
            Assert.False(coral.IsHot, $"Should not be hot at bar {i + 1}");
        }

        coral.Update(new TValue(DateTime.UtcNow.AddMinutes(period), 100.0 + period));
        Assert.True(coral.IsHot);
    }

    // E) Robustness
    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var coral = new Coral(5);
        coral.Update(new TValue(DateTime.UtcNow, 100.0));

        coral.Update(new TValue(DateTime.UtcNow.AddMinutes(1), double.NaN));
        Assert.True(double.IsFinite(coral.Last.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var coral = new Coral(5);
        coral.Update(new TValue(DateTime.UtcNow, 100.0));

        coral.Update(new TValue(DateTime.UtcNow.AddMinutes(1), double.PositiveInfinity));
        Assert.True(double.IsFinite(coral.Last.Value));

        coral.Update(new TValue(DateTime.UtcNow.AddMinutes(2), double.NegativeInfinity));
        Assert.True(double.IsFinite(coral.Last.Value));
    }

    [Fact]
    public void MultipleNaN_ContinuesWithLastValid()
    {
        var coral = new Coral(5);
        coral.Update(new TValue(DateTime.UtcNow, 100.0));

        for (int i = 0; i < 10; i++)
        {
            coral.Update(new TValue(DateTime.UtcNow.AddMinutes(i + 1), double.NaN));
            Assert.True(double.IsFinite(coral.Last.Value));
        }
    }

    // F) Consistency — all 4 modes produce same result
    [Fact]
    public void AllModes_ProduceSameResult()
    {
        const int period = 10;
        const double cd = 0.4;
        var bars = _gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = new Coral(period, cd).Update(series);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Coral.Batch(spanInput, spanOutput, period, cd);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingCoral = new Coral(period, cd);
        for (int i = 0; i < series.Count; i++)
        {
            streamingCoral.Update(series[i]);
        }
        double streamingResult = streamingCoral.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingCoral = new Coral(pubSource, period, cd);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingCoral.Last.Value;

        Assert.Equal(expected, spanResult, 1e-9);
        Assert.Equal(expected, streamingResult, 1e-9);
        Assert.Equal(expected, eventingResult, 1e-9);
    }

    // G) Span API tests
    [Fact]
    public void Batch_Span_MismatchedLengths_Throws()
    {
        double[] src = [1.0, 2.0, 3.0];
        double[] dst = [0.0, 0.0];
        var ex = Assert.Throws<ArgumentException>(() => Coral.Batch(src.AsSpan(), dst.AsSpan(), 5));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_PeriodZero_Throws()
    {
        double[] src = [1.0, 2.0, 3.0];
        double[] dst = new double[3];
        Assert.Throws<ArgumentOutOfRangeException>(() => Coral.Batch(src.AsSpan(), dst.AsSpan(), 0));
    }

    [Fact]
    public void Batch_Span_Empty_NoException()
    {
        double[] src = [];
        double[] dst = [];
        Coral.Batch(src.AsSpan(), dst.AsSpan(), 10);
        Assert.Empty(dst);
    }

    [Fact]
    public void Batch_Span_MatchesTSeriesBatch()
    {
        const int period = 14;
        const double cd = 0.4;
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var tseriesResult = Coral.Batch(series, period, cd);

        var srcArray = series.Values.ToArray();
        var spanOutput = new double[srcArray.Length];
        Coral.Batch(srcArray.AsSpan(), spanOutput.AsSpan(), period, cd);

        for (int i = 0; i < tseriesResult.Count; i++)
        {
            Assert.Equal(tseriesResult[i].Value, spanOutput[i], 10);
        }
    }

    [Fact]
    public void Batch_Span_HandlesNaN()
    {
        double[] src = [100, 101, double.NaN, 103, 104];
        double[] dst = new double[5];
        Coral.Batch(src.AsSpan(), dst.AsSpan(), 3);

        for (int i = 0; i < dst.Length; i++)
        {
            Assert.True(double.IsFinite(dst[i]), $"Output at index {i} should be finite");
        }
    }

    // H) Chainability
    [Fact]
    public void EventChaining_Works()
    {
        var source = new TSeries();
        var coral = new Coral(source, 5);
        bool eventFired = false;
        coral.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        source.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(eventFired);
    }

    // Coral-specific tests
    [Fact]
    public void Cd_Zero_OutputIsFinite()
    {
        // When cd=0: c3=0, c4=0, c5=1, -cd³=0 → bfr = i3 (triple EMA)
        var coral = new Coral(10, cd: 0.0);
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        for (int i = 0; i < series.Count; i++)
        {
            coral.Update(series[i]);
        }
        Assert.True(double.IsFinite(coral.Last.Value));
    }

    [Fact]
    public void Cd_One_OutputIsFinite()
    {
        var coral = new Coral(10, cd: 1.0);
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        for (int i = 0; i < series.Count; i++)
        {
            coral.Update(series[i]);
        }
        Assert.True(double.IsFinite(coral.Last.Value));
    }

    [Fact]
    public void HigherCd_ProducesDifferentSmoothing()
    {
        const int period = 10;
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var coral04 = new Coral(period, 0.4);
        var coral08 = new Coral(period, 0.8);

        for (int i = 0; i < series.Count; i++)
        {
            coral04.Update(series[i]);
            coral08.Update(series[i]);
        }

        Assert.NotEqual(coral04.Last.Value, coral08.Last.Value);
    }

    [Fact]
    public void Prime_InitializesState()
    {
        const int period = 10;
        var bars = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        var values = series.Values.ToArray();

        var coral = new Coral(period);
        coral.Prime(values.AsSpan());

        Assert.True(coral.IsHot);
        Assert.True(double.IsFinite(coral.Last.Value));
    }
}
