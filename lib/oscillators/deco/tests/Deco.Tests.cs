namespace QuanTAlib;

public class DecoTests
{
    private const double Tolerance = 1e-10;

    // ── A) Constructor validation ──

    [Fact]
    public void Constructor_DefaultParameters_SetsCorrectly()
    {
        var deco = new Deco();
        Assert.Equal("Deco(30,60)", deco.Name);
        Assert.Equal(30, deco.ShortPeriod);
        Assert.Equal(60, deco.LongPeriod);
        Assert.Equal(60, deco.WarmupPeriod);
    }

    [Fact]
    public void Constructor_CustomParameters_SetsCorrectly()
    {
        var deco = new Deco(shortPeriod: 10, longPeriod: 40);
        Assert.Equal("Deco(10,40)", deco.Name);
        Assert.Equal(10, deco.ShortPeriod);
        Assert.Equal(40, deco.LongPeriod);
    }

    [Fact]
    public void Constructor_ZeroShortPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Deco(shortPeriod: 0));
        Assert.Equal("shortPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeShortPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Deco(shortPeriod: -1));
        Assert.Equal("shortPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_LongNotGreaterThanShort_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Deco(shortPeriod: 30, longPeriod: 30));
        Assert.Equal("longPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_LongLessThanShort_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Deco(shortPeriod: 30, longPeriod: 20));
        Assert.Equal("longPeriod", ex.ParamName);
    }

    // ── B) Basic calculation ──

    [Fact]
    public void Update_ReturnsFiniteValue()
    {
        var deco = new Deco(5, 10);
        TValue result = default;
        for (int i = 0; i < 20; i++)
        {
            result = deco.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_Last_MatchesReturnValue()
    {
        var deco = new Deco(5, 10);
        var result = deco.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(result.Value, deco.Last.Value);
    }

    [Fact]
    public void Update_Name_AccessibleAfterUpdate()
    {
        var deco = new Deco(5, 10);
        _ = deco.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Contains("Deco", deco.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Update_FirstTwoBars_ReturnZero()
    {
        var deco = new Deco(5, 10);
        var r0 = deco.Update(new TValue(DateTime.UtcNow, 100.0));
        var r1 = deco.Update(new TValue(DateTime.UtcNow.AddSeconds(1), 101.0));
        Assert.Equal(0.0, r0.Value);
        Assert.Equal(0.0, r1.Value);
    }

    // ── C) State + bar correction ──

    [Fact]
    public void Update_IsNew_True_AdvancesState()
    {
        var deco = new Deco(5, 10);
        var r1 = deco.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        var r2 = deco.Update(new TValue(DateTime.UtcNow.AddSeconds(1), 101.0), isNew: true);
        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
    }

    [Fact]
    public void Update_IsNew_False_RewritesLastBar()
    {
        var deco = new Deco(5, 10);
        for (int i = 0; i < 10; i++)
        {
            deco.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i), isNew: true);
        }

        var before = deco.Update(new TValue(DateTime.UtcNow.AddSeconds(10), 120.0), isNew: true);
        var correction = deco.Update(new TValue(DateTime.UtcNow.AddSeconds(10), 115.0), isNew: false);

        Assert.NotEqual(before.Value, correction.Value);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoreState()
    {
        var deco = new Deco(5, 10);
        for (int i = 0; i < 10; i++)
        {
            deco.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i), isNew: true);
        }

        _ = deco.Update(new TValue(DateTime.UtcNow.AddSeconds(10), 120.0), isNew: true);

        var restored = deco.Update(new TValue(DateTime.UtcNow.AddSeconds(10), 110.0), isNew: false);
        var again = deco.Update(new TValue(DateTime.UtcNow.AddSeconds(10), 110.0), isNew: false);

        Assert.Equal(restored.Value, again.Value, Tolerance);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var deco = new Deco(5, 10);
        for (int i = 0; i < 20; i++)
        {
            deco.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }

        deco.Reset();

        Assert.False(deco.IsHot);
        Assert.Equal(0.0, deco.Last.Value);
    }

    // ── D) Warmup / convergence ──

    [Fact]
    public void IsHot_FlipsWhenWarmupReached()
    {
        var deco = new Deco(5, 10);
        for (int i = 0; i < 9; i++)
        {
            deco.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
            Assert.False(deco.IsHot);
        }
        deco.Update(new TValue(DateTime.UtcNow.AddSeconds(10), 110.0));
        Assert.True(deco.IsHot);
    }

    [Fact]
    public void WarmupPeriod_EqualsLongPeriod()
    {
        var deco = new Deco(20, 60);
        Assert.Equal(60, deco.WarmupPeriod);
    }

    // ── E) Robustness ──

    [Fact]
    public void Update_NaN_UsesLastValidValue()
    {
        var deco = new Deco(5, 10);
        for (int i = 0; i < 5; i++)
        {
            deco.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }

        var result = deco.Update(new TValue(DateTime.UtcNow.AddSeconds(5), double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_Infinity_UsesLastValidValue()
    {
        var deco = new Deco(5, 10);
        for (int i = 0; i < 5; i++)
        {
            deco.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }

        var result = deco.Update(new TValue(DateTime.UtcNow.AddSeconds(5), double.PositiveInfinity));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Batch_NaN_Safe()
    {
        double[] src = [100, 101, double.NaN, 103, 104, 105, 106, 107, 108, 109];
        double[] output = new double[src.Length];
        Deco.Batch(src, output, 3, 6);
        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]));
        }
    }

    // ── F) Consistency (4 modes match) ──

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        int shortP = 10, longP = 20;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        // 1. Streaming
        var streaming = new Deco(shortP, longP);
        var streamResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamResults[i] = streaming.Update(source[i]).Value;
        }

        // 2. Batch TSeries
        TSeries batchSeries = Deco.Batch(source, shortP, longP);

        // 3. Batch Span
        var spanOutput = new double[source.Count];
        Deco.Batch(source.Values, spanOutput, shortP, longP);

        // 4. Event-based
        var eventSource = new TSeries();
        var eventIndicator = new Deco(eventSource, shortP, longP);
        var eventResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            eventSource.Add(source[i]);
            eventResults[i] = eventIndicator.Last.Value;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamResults[i], batchSeries.Values[i], Tolerance);
            Assert.Equal(streamResults[i], spanOutput[i], Tolerance);
            Assert.Equal(streamResults[i], eventResults[i], Tolerance);
        }
    }

    // ── G) Span API tests ──

    [Fact]
    public void Batch_MismatchedLengths_Throws()
    {
        double[] src = [1, 2, 3];
        double[] output = new double[2];
        var ex = Assert.Throws<ArgumentException>(() => Deco.Batch(src, output, 1, 2));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_ZeroShortPeriod_Throws()
    {
        double[] src = [1, 2, 3];
        double[] output = new double[3];
        var ex = Assert.Throws<ArgumentException>(() => Deco.Batch(src, output, 0, 2));
        Assert.Equal("shortPeriod", ex.ParamName);
    }

    [Fact]
    public void Batch_LongNotGreater_Throws()
    {
        double[] src = [1, 2, 3];
        double[] output = new double[3];
        var ex = Assert.Throws<ArgumentException>(() => Deco.Batch(src, output, 5, 5));
        Assert.Equal("longPeriod", ex.ParamName);
    }

    [Fact]
    public void Batch_EmptyInput_NoOp()
    {
        double[] src = [];
        double[] output = [];
        var ex = Record.Exception(() => Deco.Batch(src, output, 5, 10));
        Assert.Null(ex);
    }

    [Fact]
    public void Batch_Span_MatchesTSeries()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 7);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;
        int shortP = 10, longP = 20;

        TSeries batchTs = Deco.Batch(source, shortP, longP);
        var spanOutput = new double[source.Count];
        Deco.Batch(source.Values, spanOutput, shortP, longP);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(batchTs.Values[i], spanOutput[i], Tolerance);
        }
    }

    // ── H) Chainability ──

    [Fact]
    public void PubEvent_FiresOnUpdate()
    {
        var deco = new Deco(5, 10);
        int firedCount = 0;
        deco.Pub += (object? _, in TValueEventArgs _) => firedCount++;

        deco.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(1, firedCount);
    }

    [Fact]
    public void Chained_Constructor_ReceivesEvents()
    {
        var src = new TSeries();
        var deco = new Deco(src, 5, 10);

        src.Add(new TValue(DateTime.UtcNow, 100.0));
        src.Add(new TValue(DateTime.UtcNow.AddSeconds(1), 101.0));
        src.Add(new TValue(DateTime.UtcNow.AddSeconds(2), 102.0));

        Assert.True(double.IsFinite(deco.Last.Value));
    }

    // ── Additional: Oscillator behavior ──

    [Fact]
    public void ConstantInput_ProducesZeroOutput()
    {
        var deco = new Deco(5, 10);
        for (int i = 0; i < 30; i++)
        {
            var result = deco.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
            if (i >= 2)
            {
                Assert.Equal(0.0, result.Value, Tolerance);
            }
        }
    }

    [Fact]
    public void NonLinearInput_NonZeroOutput()
    {
        // Use quadratic input (non-zero second derivative) since HP filter
        // removes linear trends (which have zero second derivative)
        var deco = new Deco(5, 10);
        TValue last = default;
        for (int i = 0; i < 30; i++)
        {
            last = deco.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i * i * 0.1));
        }
        Assert.NotEqual(0.0, last.Value);
    }

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 99);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;
        var (results, indicator) = Deco.Calculate(source, 10, 20);
        Assert.Equal(source.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Prime_InitializesState()
    {
        var deco = new Deco(5, 10);
        double[] primeData = [100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111];
        deco.Prime(primeData);
        Assert.True(deco.IsHot);
    }
}
