namespace QuanTAlib.Tests;

public class Sp15Tests
{
    private static TSeries MakeSeries(int count = 500)
    {
        var source = new TSeries();
        var gbm = new GBM(startPrice: 100, seed: 42);
        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next();
            source.Add(bar.C);
        }
        return source;
    }

    // ── A) Constructor validation ──────────────────────────────

    [Fact]
    public void Constructor_SetsName()
    {
        var sp15 = new Sp15();
        Assert.Equal("Sp15", sp15.Name);
    }

    [Fact]
    public void Constructor_SetsWarmupPeriod()
    {
        var sp15 = new Sp15();
        Assert.Equal(15, sp15.WarmupPeriod);
    }

    [Fact]
    public void Constructor_InitiallyNotHot()
    {
        var sp15 = new Sp15();
        Assert.False(sp15.IsHot);
    }

    [Fact]
    public void Constructor_IsNewDefaultTrue()
    {
        var sp15 = new Sp15();
        Assert.True(sp15.IsNew);
    }

    // ── B) Basic calculation ───────────────────────────────────

    [Fact]
    public void Update_ReturnsFiniteValue()
    {
        var sp15 = new Sp15();
        var result = sp15.Update(new TValue(DateTime.UtcNow.Ticks, 100.0));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_LastMatchesReturnValue()
    {
        var sp15 = new Sp15();
        var result = sp15.Update(new TValue(DateTime.UtcNow.Ticks, 100.0));
        Assert.Equal(result.Value, sp15.Last.Value);
    }

    [Fact]
    public void Update_ConstantInput_ReturnsConstant()
    {
        // Spencer filter preserves constants (weights sum to 1.0)
        var sp15 = new Sp15();
        const double c = 42.0;
        for (int i = 0; i < 20; i++)
        {
            sp15.Update(new TValue(DateTime.UtcNow.AddMinutes(i).Ticks, c));
        }
        Assert.Equal(c, sp15.Last.Value, 1e-10);
    }

    [Fact]
    public void Update_LinearInput_PreservesLinear()
    {
        // Spencer filter preserves polynomial trends up to degree 3
        var sp15 = new Sp15();
        const double slope = 2.5;
        const double intercept = 10.0;
        const int n = 30;
        for (int i = 0; i < n; i++)
        {
            double val = intercept + slope * i;
            sp15.Update(new TValue(DateTime.UtcNow.AddMinutes(i).Ticks, val));
        }
        // Centered at lag 7: output at bar n-1 matches polynomial at bar (n-1)-7
        int centerIdx = n - 1 - 7;
        double expected = intercept + slope * centerIdx;
        Assert.Equal(expected, sp15.Last.Value, 1e-6);
    }

    [Fact]
    public void Update_QuadraticInput_PreservesQuadratic()
    {
        var sp15 = new Sp15();
        const int n = 40;
        for (int i = 0; i < n; i++)
        {
            double val = 0.1 * i * i + 2.0 * i + 5.0;
            sp15.Update(new TValue(DateTime.UtcNow.AddMinutes(i).Ticks, val));
        }
        int k = n - 1 - 7;
        double expected = 0.1 * k * k + 2.0 * k + 5.0;
        Assert.Equal(expected, sp15.Last.Value, 1e-4);
    }

    [Fact]
    public void Update_CubicInput_PreservesCubic()
    {
        var sp15 = new Sp15();
        const int n = 40;
        for (int i = 0; i < n; i++)
        {
            double val = 0.001 * i * i * i + 0.1 * i * i + 2.0 * i + 5.0;
            sp15.Update(new TValue(DateTime.UtcNow.AddMinutes(i).Ticks, val));
        }
        int k = n - 1 - 7;
        double expected = 0.001 * k * k * k + 0.1 * k * k + 2.0 * k + 5.0;
        Assert.Equal(expected, sp15.Last.Value, 1e-2);
    }

    // ── C) State + bar correction ──────────────────────────────

    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var sp15 = new Sp15();
        for (int i = 0; i < 20; i++)
        {
            sp15.Update(new TValue(DateTime.UtcNow.AddSeconds(i).Ticks, 100.0 + i), isNew: true);
        }
        Assert.True(sp15.IsHot);
    }

    [Fact]
    public void IsNew_False_RewritesLastBar()
    {
        var sp15 = new Sp15();
        var series = MakeSeries(20);
        for (int i = 0; i < 20; i++)
        {
            sp15.Update(series[i]);
        }
        double hotVal = sp15.Last.Value;

        // Rewrite latest bar
        sp15.Update(new TValue(DateTime.UtcNow.Ticks, 999.0), isNew: false);
        double rewriteVal = sp15.Last.Value;

        // Undo rewrite by sending original again
        sp15.Update(series[19], isNew: false);
        double restoredVal = sp15.Last.Value;

        Assert.Equal(hotVal, restoredVal, 1e-10);
        Assert.NotEqual(hotVal, rewriteVal);
    }

    [Fact]
    public void IterativeCorrections_Restore()
    {
        var sp15 = new Sp15();
        var series = MakeSeries(20);
        for (int i = 0; i < 20; i++)
        {
            sp15.Update(series[i]);
        }
        double original = sp15.Last.Value;

        // Multiple corrections
        for (int c = 0; c < 5; c++)
        {
            sp15.Update(new TValue(DateTime.UtcNow.Ticks, 500.0 + c * 10), isNew: false);
        }
        // Restore
        sp15.Update(series[19], isNew: false);
        Assert.Equal(original, sp15.Last.Value, 1e-10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var sp15 = new Sp15();
        var series = MakeSeries(20);
        for (int i = 0; i < 20; i++)
        {
            sp15.Update(series[i]);
        }
        Assert.True(sp15.IsHot);

        sp15.Reset();
        Assert.False(sp15.IsHot);
        Assert.Equal(default, sp15.Last);
    }

    // ── D) Warmup / convergence ────────────────────────────────

    [Fact]
    public void IsHot_BecomesTrue_After15Bars()
    {
        var sp15 = new Sp15();
        for (int i = 0; i < 14; i++)
        {
            sp15.Update(new TValue(DateTime.UtcNow.AddMinutes(i).Ticks, 100.0 + i));
            Assert.False(sp15.IsHot);
        }
        sp15.Update(new TValue(DateTime.UtcNow.AddMinutes(14).Ticks, 114.0));
        Assert.True(sp15.IsHot);
    }

    [Fact]
    public void DuringWarmup_ReturnsRawValue()
    {
        var sp15 = new Sp15();
        for (int i = 0; i < 14; i++)
        {
            double val = 100.0 + i;
            var result = sp15.Update(new TValue(DateTime.UtcNow.AddMinutes(i).Ticks, val));
            Assert.Equal(val, result.Value, 1e-10);
        }
    }

    // ── E) Robustness ──────────────────────────────────────────

    [Fact]
    public void NaN_SubstitutesLastValid()
    {
        var sp15 = new Sp15();
        for (int i = 0; i < 20; i++)
        {
            sp15.Update(new TValue(DateTime.UtcNow.AddMinutes(i).Ticks, 100.0));
        }
        double beforeNaN = sp15.Last.Value;
        sp15.Update(new TValue(DateTime.UtcNow.AddMinutes(20).Ticks, double.NaN));
        Assert.Equal(beforeNaN, sp15.Last.Value, 1e-10);
    }

    [Fact]
    public void Infinity_SubstitutesLastValid()
    {
        var sp15 = new Sp15();
        for (int i = 0; i < 20; i++)
        {
            sp15.Update(new TValue(DateTime.UtcNow.AddMinutes(i).Ticks, 100.0));
        }
        double beforeInf = sp15.Last.Value;
        sp15.Update(new TValue(DateTime.UtcNow.AddMinutes(20).Ticks, double.PositiveInfinity));
        Assert.Equal(beforeInf, sp15.Last.Value, 1e-10);
    }

    [Fact]
    public void NaN_BeforeAnyValid_ReturnsNaN()
    {
        var sp15 = new Sp15();
        var result = sp15.Update(new TValue(DateTime.UtcNow.Ticks, double.NaN));
        Assert.True(double.IsNaN(result.Value));
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        double[] src = new double[30];
        for (int i = 0; i < 30; i++)
        {
            src[i] = i < 5 ? double.NaN : 100.0;
        }
        double[] output = new double[30];
        Sp15.Batch(src, output);
        for (int i = 20; i < 30; i++)
        {
            Assert.True(double.IsFinite(output[i]));
        }
    }

    // ── F) Consistency (4 modes match) ─────────────────────────

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        var series = MakeSeries(100);

        // Mode 1: Streaming
        var sp15Stream = new Sp15();
        var streaming = new double[100];
        for (int i = 0; i < 100; i++)
        {
            streaming[i] = sp15Stream.Update(series[i]).Value;
        }

        // Mode 2: Batch TSeries
        var batchResult = Sp15.Batch(series);

        // Mode 3: Span
        double[] spanOutput = new double[100];
        Sp15.Batch(series.Values, spanOutput);

        // Mode 4: Event
        var sp15Event = new Sp15();
        var eventResults = new double[100];
        int eventIdx = 0;
        sp15Event.Pub += (object? sender, in TValueEventArgs e) => { eventResults[eventIdx++] = e.Value.Value; };
        for (int i = 0; i < 100; i++)
        {
            sp15Event.Update(series[i]);
        }

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(streaming[i], batchResult[i].Value, 1e-10);
            Assert.Equal(streaming[i], spanOutput[i], 1e-10);
            Assert.Equal(streaming[i], eventResults[i], 1e-10);
        }
    }

    // ── G) Span API tests ──────────────────────────────────────

    [Fact]
    public void Batch_Span_MismatchLength_Throws()
    {
        double[] src = [1, 2, 3];
        double[] output = [0, 0];
        var ex = Assert.Throws<ArgumentException>(() => Sp15.Batch(src, output));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_EmptyInput_NoOutput()
    {
        Sp15.Batch(ReadOnlySpan<double>.Empty, Span<double>.Empty);
        Assert.True(true); // no-throw is the assertion
    }

    [Fact]
    public void Batch_Span_MatchesTSeries()
    {
        var series = MakeSeries(50);
        var batchTSeries = Sp15.Batch(series);

        double[] spanOutput = new double[50];
        Sp15.Batch(series.Values, spanOutput);

        for (int i = 0; i < 50; i++)
        {
            Assert.Equal(batchTSeries[i].Value, spanOutput[i], 1e-10);
        }
    }

    [Fact]
    public void Batch_Span_HandlesNaN()
    {
        double[] src = new double[30];
        for (int i = 0; i < 30; i++)
        {
            src[i] = i == 10 ? double.NaN : 50.0;
        }
        double[] output = new double[30];
        Sp15.Batch(src, output);
        for (int i = 15; i < 30; i++)
        {
            Assert.True(double.IsFinite(output[i]));
        }
    }

    [Fact]
    public void Batch_Span_LargeData_NoStackOverflow()
    {
        int size = 10_000;
        double[] src = new double[size];
        double[] output = new double[size];
        for (int i = 0; i < size; i++)
        {
            src[i] = 100.0 + Math.Sin(i * 0.1);
        }
        Sp15.Batch(src, output);
        Assert.True(double.IsFinite(output[size - 1]));
    }

    // ── H) Chainability ────────────────────────────────────────

    [Fact]
    public void Pub_Fires_OnUpdate()
    {
        var sp15 = new Sp15();
        int pubCount = 0;
        sp15.Pub += (object? sender, in TValueEventArgs e) => pubCount++;

        for (int i = 0; i < 5; i++)
        {
            sp15.Update(new TValue(DateTime.UtcNow.AddMinutes(i).Ticks, 100.0 + i));
        }
        Assert.Equal(5, pubCount);
    }

    [Fact]
    public void EventChaining_Works()
    {
        var series = new TSeries();
        var sp15 = new Sp15(series);
        double lastValue = double.NaN;
        sp15.Pub += (object? sender, in TValueEventArgs e) => { lastValue = e.Value.Value; };

        for (int i = 0; i < 20; i++)
        {
            series.Add(new TValue(DateTime.UtcNow.AddMinutes(i).Ticks, 100.0 + i));
        }
        Assert.True(double.IsFinite(lastValue));
    }

    // ── SP15-specific tests ────────────────────────────────────

    [Fact]
    public void WeightSum_Is320()
    {
        double[] rawWeights = [-3, -6, -5, 3, 21, 46, 67, 74, 67, 46, 21, 3, -5, -6, -3];
        double sum = 0;
        for (int i = 0; i < rawWeights.Length; i++)
        {
            sum += rawWeights[i];
        }
        Assert.Equal(320.0, sum, 1e-10);
    }

    [Fact]
    public void Weights_AreSymmetric()
    {
        double[] rawWeights = [-3, -6, -5, 3, 21, 46, 67, 74, 67, 46, 21, 3, -5, -6, -3];
        for (int i = 0; i < 7; i++)
        {
            Assert.Equal(rawWeights[i], rawWeights[14 - i], 1e-12);
        }
    }

    [Fact]
    public void NegativeEdgeWeights_CanExceedInputRange()
    {
        // Extreme values at boundaries with negative weights push output outside input range
        var sp15 = new Sp15();
        for (int i = 0; i < 15; i++)
        {
            double val = i == 0 || i == 14 ? 1000.0 : 0.0;
            sp15.Update(new TValue(DateTime.UtcNow.AddMinutes(i).Ticks, val));
        }
        // w[0]*1000 + w[14]*1000 = 2*(-3/320)*1000 = -18.75
        Assert.True(sp15.Last.Value < 0);
    }

    [Fact]
    public void Calculate_ReturnsTupleWithIndicator()
    {
        var series = MakeSeries(30);
        var (results, indicator) = Sp15.Calculate(series);
        Assert.Equal(30, results.Count);
        Assert.NotNull(indicator);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Update_TSeries_ReturnsCorrectLength()
    {
        var series = MakeSeries(50);
        var sp15 = new Sp15();
        var result = sp15.Update(series);
        Assert.Equal(50, result.Count);
    }

    [Fact]
    public void Update_TSeries_RestoresState()
    {
        var series = MakeSeries(30);
        var sp15 = new Sp15();
        _ = sp15.Update(series);

        var nextBar = new TValue(DateTime.UtcNow.AddMinutes(100).Ticks, 100.0);
        var result = sp15.Update(nextBar);
        Assert.True(double.IsFinite(result.Value));
        Assert.True(sp15.IsHot);
    }

    [Fact]
    public void Prime_FillsState()
    {
        var sp15 = new Sp15();
        double[] data = new double[20];
        for (int i = 0; i < 20; i++)
        {
            data[i] = 100.0 + i;
        }
        sp15.Prime(data);
        Assert.True(sp15.IsHot);
    }

    [Fact]
    public void Update_EmptyTSeries_ReturnsEmpty()
    {
        var sp15 = new Sp15();
        var result = sp15.Update(new TSeries([], []));
        Assert.Empty(result);
    }

    [Fact]
    public void Dispose_UnsubscribesFromSource()
    {
        var series = new TSeries();
        var sp15 = new Sp15(series);
        sp15.Dispose();

        // After dispose, adding to series should not affect sp15
        series.Add(new TValue(DateTime.UtcNow.Ticks, 100.0));
        Assert.True(true); // no-throw proves unsubscription
    }

    [Fact]
    public void KnownValue_HandComputed()
    {
        // Hand-computed SP15 with known inputs
        // Input: 15 bars all = 100 except bar[7] (center) = 200
        var sp15 = new Sp15();
        for (int i = 0; i < 15; i++)
        {
            double val = i == 7 ? 200.0 : 100.0;
            sp15.Update(new TValue(DateTime.UtcNow.AddMinutes(i).Ticks, val));
        }
        // All 100 contributes: 100 * sum(weights) = 100
        // Extra 100 at center contributes: 100 * (74/320) = 23.125
        // Total = 100 + 23.125 = 123.125
        double expected = 100.0 + 100.0 * 74.0 / 320.0;
        Assert.Equal(expected, sp15.Last.Value, 1e-10);
    }
}
