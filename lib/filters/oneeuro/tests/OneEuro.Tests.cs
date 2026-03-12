namespace QuanTAlib;

public class OneEuroTests
{
    private const double Tolerance = 1e-10;

    private static TSeries CreateSeries(params double[] values)
    {
        var series = new TSeries();
        for (int i = 0; i < values.Length; i++)
        {
            series.Add(new TValue(DateTime.UtcNow.AddMinutes(i), values[i]));
        }
        return series;
    }

    // ═══════════════════════════════════════════════════════
    // A) Constructor Validation
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void Constructor_SetsName()
    {
        var ind = new OneEuro(1.0, 0.007, 1.0);
        Assert.Contains("OneEuro", ind.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_SetsWarmupPeriod()
    {
        var ind = new OneEuro();
        Assert.Equal(1, ind.WarmupPeriod);
    }

    [Fact]
    public void Constructor_ExposesProperties()
    {
        var ind = new OneEuro(2.0, 0.01, 0.5);
        Assert.Equal(2.0, ind.MinCutoff);
        Assert.Equal(0.01, ind.Beta);
        Assert.Equal(0.5, ind.DCutoff);
    }

    [Fact]
    public void Constructor_ValidatesMinCutoff()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new OneEuro(0.0));
        Assert.Equal("minCutoff", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidatesMinCutoff_Negative()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new OneEuro(-1.0));
        Assert.Equal("minCutoff", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidatesBeta()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new OneEuro(1.0, -0.1));
        Assert.Equal("beta", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidatesDCutoff()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new OneEuro(1.0, 0.0, 0.0));
        Assert.Equal("dCutoff", ex.ParamName);
    }

    // ═══════════════════════════════════════════════════════
    // B) Basic Calculation
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void FirstBar_IsPassthrough()
    {
        var ind = new OneEuro();
        var result = ind.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(100.0, result.Value, Tolerance);
    }

    [Fact]
    public void Update_ReturnsTValue()
    {
        var ind = new OneEuro();
        var result = ind.Update(new TValue(DateTime.UtcNow, 50.0));
        Assert.IsType<TValue>(result);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Last_IsAccessible()
    {
        var ind = new OneEuro();
        ind.Update(new TValue(DateTime.UtcNow, 42.0));
        Assert.Equal(42.0, ind.Last.Value, Tolerance);
    }

    [Fact]
    public void IsHot_AfterFirstBar()
    {
        var ind = new OneEuro();
        ind.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(ind.IsHot);
    }

    [Fact]
    public void SmoothsNoisySignal()
    {
        var ind = new OneEuro(minCutoff: 0.5, beta: 0.0);
        double[] noisy = [100, 102, 98, 103, 97, 101, 99, 100, 102, 98];
        double sumAbsDiff = 0;
        double prev = noisy[0];
        foreach (double v in noisy)
        {
            var result = ind.Update(new TValue(DateTime.UtcNow, v));
            sumAbsDiff += Math.Abs(result.Value - prev);
            prev = result.Value;
        }
        // Filtered should have less total variation than raw
        double rawVariation = 0;
        for (int i = 1; i < noisy.Length; i++)
        {
            rawVariation += Math.Abs(noisy[i] - noisy[i - 1]);
        }
        Assert.True(sumAbsDiff < rawVariation);
    }

    // ═══════════════════════════════════════════════════════
    // C) State + Bar Correction
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void State_IsNew_True_Advances()
    {
        var ind = new OneEuro();
        ind.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        double v1 = ind.Update(new TValue(DateTime.UtcNow, 110.0), isNew: true).Value;
        double v2 = ind.Update(new TValue(DateTime.UtcNow, 120.0), isNew: true).Value;
        Assert.NotEqual(v1, v2);
    }

    [Fact]
    public void State_IsNew_False_Rewrites()
    {
        var ind = new OneEuro();
        ind.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        double v1 = ind.Update(new TValue(DateTime.UtcNow, 110.0), isNew: true).Value;
        double v2 = ind.Update(new TValue(DateTime.UtcNow, 115.0), isNew: false).Value;
        // Corrections with different input should give different result
        Assert.NotEqual(v1, v2);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var ind = new OneEuro();
        ind.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        ind.Update(new TValue(DateTime.UtcNow, 110.0), isNew: true);
        double baseVal = ind.Last.Value;

        // Apply corrections
        ind.Update(new TValue(DateTime.UtcNow, 200.0), isNew: false);
        ind.Update(new TValue(DateTime.UtcNow, 300.0), isNew: false);

        // Restore original value
        double restored = ind.Update(new TValue(DateTime.UtcNow, 110.0), isNew: false).Value;
        Assert.Equal(baseVal, restored, Tolerance);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var ind = new OneEuro();
        ind.Update(new TValue(DateTime.UtcNow, 100.0));
        ind.Update(new TValue(DateTime.UtcNow, 110.0));
        ind.Reset();
        Assert.False(ind.IsHot);
        Assert.Equal(default, ind.Last);
    }

    // ═══════════════════════════════════════════════════════
    // D) Warmup / Convergence
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void WarmupPeriod_IsOne()
    {
        // Validates WarmupPeriod independently from constructor test
        var ind = new OneEuro(2.0, 0.01, 0.5);
        Assert.Equal(1, ind.WarmupPeriod);
    }

    [Fact]
    public void IsHot_FalseBeforeFirstBar()
    {
        var ind = new OneEuro();
        Assert.False(ind.IsHot);
    }

    // ═══════════════════════════════════════════════════════
    // E) Robustness
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var ind = new OneEuro();
        ind.Update(new TValue(DateTime.UtcNow, 100.0));
        var result = ind.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var ind = new OneEuro();
        ind.Update(new TValue(DateTime.UtcNow, 100.0));
        var result = ind.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        double[] src = [100, double.NaN, 102, double.NaN, 104];
        double[] output = new double[src.Length];
        OneEuro.Batch(src, output);
        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"output[{i}] should be finite");
        }
    }

    // ═══════════════════════════════════════════════════════
    // F) Consistency — All 4 Modes Must Match
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void AllModes_ProduceConsistentResults()
    {
        var series = CreateSeries(100, 102, 98, 105, 97, 103, 99, 101, 104, 96);
        double mc = 1.0, b = 0.007, dc = 1.0;

        // Mode 1: Streaming Update
        var ind1 = new OneEuro(mc, b, dc);
        double[] streaming = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            streaming[i] = ind1.Update(series[i]).Value;
        }

        // Mode 2: Batch TSeries
        var batchResult = OneEuro.Batch(series, mc, b, dc);
        double[] batch = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            batch[i] = batchResult[i].Value;
        }

        // Mode 3: Span
        var srcSpan = series.Values;
        double[] spanOut = new double[series.Count];
        OneEuro.Batch(srcSpan, spanOut, mc, b, dc);

        // Mode 4: Event-based
        var pubSource = new TSeries();
        var ind4 = new OneEuro(pubSource, mc, b, dc);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(streaming[i], batch[i], 1e-10);
            Assert.Equal(streaming[i], spanOut[i], 1e-10);
        }
        Assert.Equal(streaming[^1], ind4.Last.Value, 1e-10);
    }

    // ═══════════════════════════════════════════════════════
    // G) Span API Tests
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void SpanCalc_ShortOutputThrows()
    {
        double[] src = [1, 2, 3];
        double[] output = new double[2];
        var ex = Assert.Throws<ArgumentException>(() => OneEuro.Batch(src, output));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void SpanCalc_EmptyInput_NoOp()
    {
        double[] src = [];
        double[] output = [];
        var ex = Record.Exception(() => OneEuro.Batch(src, output));
        Assert.Null(ex);
    }

    [Fact]
    public void SpanCalc_OutputLengthMatches()
    {
        double[] src = [100, 102, 98, 105, 97];
        double[] output = new double[src.Length];
        OneEuro.Batch(src, output);
        Assert.All(output, v => Assert.True(double.IsFinite(v)));
    }

    [Fact]
    public void SpanCalc_SingleBar_ReturnsPassthrough()
    {
        double[] src = [42.0];
        double[] output = new double[1];
        OneEuro.Batch(src, output);
        Assert.Equal(42.0, output[0], Tolerance);
    }

    [Fact]
    public void BatchSpan_MatchesBatchTSeries()
    {
        var series = CreateSeries(100, 105, 98, 110, 95, 102);

        var batchResult = OneEuro.Batch(series);
        double[] spanOut = new double[series.Count];
        OneEuro.Batch(series.Values, spanOut);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, spanOut[i], 1e-10);
        }
    }

    // ═══════════════════════════════════════════════════════
    // H) Chainability
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var ind = new OneEuro();
        int count = 0;
        ind.Pub += (object? _, in TValueEventArgs _) => count++;
        ind.Update(new TValue(DateTime.UtcNow, 100.0));
        ind.Update(new TValue(DateTime.UtcNow, 110.0));
        Assert.Equal(2, count);
    }

    [Fact]
    public void EventChaining_Works()
    {
        var ind1 = new OneEuro(1.0, 0.007, 1.0);
        var ind2 = new OneEuro(ind1, 1.0, 0.007, 1.0);

        ind1.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(ind2.Last.Value));
    }

    [Fact]
    public void Dispose_UnsubscribesFromSource()
    {
        var ind1 = new OneEuro();
        var ind2 = new OneEuro(ind1);

        ind1.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(ind2.Last.Value));

        ind2.Dispose();

        ind1.Update(new TValue(DateTime.UtcNow, 200.0));
        // ind2 should not update after dispose
        Assert.NotEqual(200.0, ind2.Last.Value);
    }

    [Fact]
    public void Prime_SetsUpState()
    {
        var ind = new OneEuro();
        ind.Prime(new double[] { 100, 102, 98, 105, 97 });
        Assert.True(ind.IsHot);
        Assert.True(double.IsFinite(ind.Last.Value));
    }

    [Fact]
    public void DifferentParameters_ProduceDifferentResults()
    {
        var series = CreateSeries(100, 110, 90, 120, 80, 100);

        var r1 = OneEuro.Batch(series, 0.1, 0.0, 1.0);
        var r2 = OneEuro.Batch(series, 5.0, 0.0, 1.0);

        // Different minCutoff should give different smoothing
        bool anyDifferent = false;
        for (int i = 1; i < series.Count; i++)
        {
            if (Math.Abs(r1[i].Value - r2[i].Value) > 1e-10)
            {
                anyDifferent = true;
                break;
            }
        }
        Assert.True(anyDifferent);
    }

    [Fact]
    public void HighBeta_ReducesLag()
    {
        // With high beta, should track rapid changes better
        var series = CreateSeries(100, 100, 100, 200, 200, 200);

        var slowResult = OneEuro.Batch(series, 0.5, 0.0, 1.0);
        var fastResult = OneEuro.Batch(series, 0.5, 1.0, 1.0);

        // After the step at index 3, high-beta should be closer to 200
        double slowAt4 = slowResult[4].Value;
        double fastAt4 = fastResult[4].Value;
        Assert.True(Math.Abs(200.0 - fastAt4) < Math.Abs(200.0 - slowAt4),
            $"High beta ({fastAt4:F4}) should track 200 closer than low beta ({slowAt4:F4})");
    }

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var series = CreateSeries(100, 102, 98, 105, 97);
        var (results, indicator) = OneEuro.Calculate(series);
        Assert.Equal(series.Count, results.Count);
        Assert.True(indicator.IsHot);
    }
}
