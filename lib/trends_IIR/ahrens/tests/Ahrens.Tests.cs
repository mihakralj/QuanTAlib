namespace QuanTAlib.Tests;

public class AhrensTests
{
    private static TSeries MakeSeries(int count = 500)
    {
        var gbm = new GBM(startPrice: 100, seed: 42);
        var series = new TSeries();
        for (int i = 0; i < count; i++)
        {
            series.Add(gbm.Next());
        }
        return series;
    }

    // ── A) Constructor validation ──

    [Fact]
    public void Constructor_Period0_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Ahrens(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Ahrens(period: -1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_Period1_Valid()
    {
        var ind = new Ahrens(period: 1);
        Assert.Equal("Ahrens(1)", ind.Name);
    }

    [Fact]
    public void Constructor_DefaultPeriod_Is9()
    {
        var ind = new Ahrens();
        Assert.Equal("Ahrens(9)", ind.Name);
        Assert.Equal(9, ind.WarmupPeriod);
    }

    [Fact]
    public void Constructor_SetsPeriodName()
    {
        var ind = new Ahrens(period: 20);
        Assert.Equal("Ahrens(20)", ind.Name);
        Assert.Equal(20, ind.WarmupPeriod);
    }

    // ── B) Basic calculation ──

    [Fact]
    public void Update_ReturnsTValue()
    {
        var ind = new Ahrens(9);
        TValue result = ind.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_LastIsAccessible()
    {
        var ind = new Ahrens(9);
        ind.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(ind.Last.Value));
    }

    [Fact]
    public void Update_FirstBar_SeedsWithSource()
    {
        var ind = new Ahrens(9);
        TValue result = ind.Update(new TValue(DateTime.UtcNow, 50.0));
        // First bar: prev=source, lagged=source (empty buffer), midpoint=source
        // result = source + (source - source) / period = source
        Assert.Equal(50.0, result.Value, 10);
    }

    [Fact]
    public void Update_ConstantInput_ConvergesToConstant()
    {
        var ind = new Ahrens(9);
        double constant = 42.0;
        TValue result = default;
        for (int i = 0; i < 200; i++)
        {
            result = ind.Update(new TValue(DateTime.UtcNow, constant));
        }

        Assert.Equal(constant, result.Value, 6);
    }

    // ── C) State + bar correction ──

    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var ind = new Ahrens(9);
        TSeries src = MakeSeries(20);
        for (int i = 0; i < 20; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, src.Values[i]), isNew: true);
        }

        Assert.True(ind.IsHot);
    }

    [Fact]
    public void IsNew_False_RewritesSameBar()
    {
        var ind = new Ahrens(9);
        TSeries src = MakeSeries(15);
        for (int i = 0; i < 14; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, src.Values[i]));
        }

        TValue first = ind.Update(new TValue(DateTime.UtcNow, 100.0));
        TValue second = ind.Update(new TValue(DateTime.UtcNow, 100.0), isNew: false);
        Assert.Equal(first.Value, second.Value, 10);
    }

    [Fact]
    public void BarCorrection_Idempotent()
    {
        var ind = new Ahrens(9);
        TSeries src = MakeSeries(20);
        for (int i = 0; i < 19; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, src.Values[i]));
        }

        TValue first = ind.Update(new TValue(DateTime.UtcNow, 55.0));
        _ = ind.Update(new TValue(DateTime.UtcNow, 60.0), isNew: false);
        _ = ind.Update(new TValue(DateTime.UtcNow, 65.0), isNew: false);
        TValue last = ind.Update(new TValue(DateTime.UtcNow, 55.0), isNew: false);
        Assert.Equal(first.Value, last.Value, 10);
    }

    [Fact]
    public void IterativeCorrection_Restores()
    {
        var ind = new Ahrens(9);
        TSeries src = MakeSeries(30);
        for (int i = 0; i < 25; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, src.Values[i]));
        }

        _ = ind.Update(new TValue(DateTime.UtcNow, 999.0));
        _ = ind.Update(new TValue(DateTime.UtcNow, src.Values[25]), isNew: false);
        Assert.True(double.IsFinite(ind.Last.Value));
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var ind = new Ahrens(9);
        TSeries src = MakeSeries(20);
        for (int i = 0; i < 20; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, src.Values[i]));
        }

        Assert.True(ind.IsHot);
        ind.Reset();
        Assert.False(ind.IsHot);

        TValue result = ind.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(100.0, result.Value, 10);
    }

    // ── D) Warmup / convergence ──

    [Fact]
    public void IsHot_FlipsAtPeriod()
    {
        var ind = new Ahrens(5);
        TSeries src = MakeSeries(10);
        for (int i = 0; i < 4; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, src.Values[i]));
            Assert.False(ind.IsHot);
        }

        ind.Update(new TValue(DateTime.UtcNow, src.Values[4]));
        Assert.True(ind.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesPeriod()
    {
        var ind = new Ahrens(15);
        Assert.Equal(15, ind.WarmupPeriod);
    }

    // ── E) Robustness ──

    [Fact]
    public void NaN_UsesLastValidValue()
    {
        var ind = new Ahrens(9);
        ind.Update(new TValue(DateTime.UtcNow, 100.0));
        ind.Update(new TValue(DateTime.UtcNow, 110.0));

        ind.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(ind.Last.Value));
    }

    [Fact]
    public void Infinity_UsesLastValidValue()
    {
        var ind = new Ahrens(9);
        ind.Update(new TValue(DateTime.UtcNow, 100.0));
        ind.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(ind.Last.Value));
    }

    [Fact]
    public void AllNaN_ReturnsNaN()
    {
        var ind = new Ahrens(9);
        TValue result = ind.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsNaN(result.Value));
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        double[] src = [1, 2, double.NaN, 4, 5];
        double[] output = new double[5];
        Ahrens.Batch(src, output, period: 3);
        Assert.True(double.IsFinite(output[0]));
        Assert.True(double.IsFinite(output[4]));
    }

    // ── F) Consistency (4 API modes) ──

    [Fact]
    public void AllModes_Match()
    {
        TSeries src = MakeSeries(200);
        int period = 9;

        // Mode 1: Streaming
        var streaming = new Ahrens(period);
        double[] streamVals = new double[src.Count];
        for (int i = 0; i < src.Count; i++)
        {
            streamVals[i] = streaming.Update(new TValue(DateTime.UtcNow, src.Values[i])).Value;
        }

        // Mode 2: Batch TSeries
        TSeries batch = Ahrens.Batch(src, period);

        // Mode 3: Span
        double[] spanOut = new double[src.Count];
        Ahrens.Batch(src.Values, spanOut, period);

        // Mode 4: Event-based
        var pub = new TSeries();
        var listener = new Ahrens(pub, period);
        double[] eventVals = new double[src.Count];
        for (int i = 0; i < src.Count; i++)
        {
            pub.Add(new TValue(DateTime.UtcNow, src.Values[i]));
            eventVals[i] = listener.Last.Value;
        }

        // Compare after warmup
        for (int i = period; i < src.Count; i++)
        {
            Assert.Equal(streamVals[i], batch.Values[i], 10);
            Assert.Equal(streamVals[i], spanOut[i], 10);
            Assert.Equal(streamVals[i], eventVals[i], 10);
        }
    }

    // ── G) Span API tests ──

    [Fact]
    public void Batch_Span_LengthMismatch_Throws()
    {
        double[] src = [1, 2, 3];
        double[] output = new double[2];
        var ex = Assert.Throws<ArgumentException>(() => Ahrens.Batch(src, output, period: 3));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidPeriod_Throws()
    {
        double[] src = [1, 2, 3];
        double[] output = new double[3];
        Assert.Throws<ArgumentOutOfRangeException>(() => Ahrens.Batch(src, output, period: 0));
    }

    [Fact]
    public void Batch_Span_EmptySource_NoOp()
    {
        Ahrens.Batch(ReadOnlySpan<double>.Empty, Span<double>.Empty, period: 9);
        Assert.True(true); // no-throw is the assertion
    }

    [Fact]
    public void Batch_Span_MatchesTSeries()
    {
        TSeries src = MakeSeries(100);
        TSeries batchResult = Ahrens.Batch(src, 9);
        double[] spanOut = new double[src.Count];
        Ahrens.Batch(src.Values, spanOut, 9);

        for (int i = 9; i < src.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], spanOut[i], 10);
        }
    }

    [Fact]
    public void Batch_Span_LargeData_NoStackOverflow()
    {
        int size = 5000;
        double[] src = new double[size];
        double[] output = new double[size];
        for (int i = 0; i < size; i++)
        {
            src[i] = 100.0 + (i * 0.01);
        }

        Ahrens.Batch(src, output, period: 500);
        Assert.True(double.IsFinite(output[size - 1]));
    }

    // ── H) Chainability ──

    [Fact]
    public void PubFires()
    {
        var ind = new Ahrens(9);
        int fires = 0;
        ind.Pub += (object? sender, in TValueEventArgs e) => fires++;
        ind.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(1, fires);
    }

    [Fact]
    public void EventChaining_Works()
    {
        var src = new TSeries();
        var ahrens1 = new Ahrens(src, 9);
        var ahrens2 = new Ahrens(ahrens1, 5);

        for (int i = 0; i < 30; i++)
        {
            src.Add(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        Assert.True(double.IsFinite(ahrens2.Last.Value));
        Assert.True(ahrens1.IsHot);
    }

    [Fact]
    public void Dispose_UnsubscribesPublisher()
    {
        var src = new TSeries();
        var ind = new Ahrens(src, 9);
        src.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(ind.Last.Value));

        ind.Dispose();
        double before = ind.Last.Value;
        src.Add(new TValue(DateTime.UtcNow, 200.0));
        Assert.Equal(before, ind.Last.Value, 10);
    }

    // ── AHRENS-specific ──

    [Fact]
    public void Period1_EqualsSource()
    {
        var ind = new Ahrens(period: 1);
        TSeries src = MakeSeries(50);
        for (int i = 0; i < 50; i++)
        {
            TValue result = ind.Update(new TValue(DateTime.UtcNow, src.Values[i]));
            // period=1: lagged = buffer oldest = previous result, prev = previous result
            // midpoint = (prev + prev) / 2 = prev
            // result = prev + (source - prev) / 1 = source
            Assert.Equal(src.Values[i], result.Value, 10);
        }
    }

    [Fact]
    public void Calculate_ReturnsBoth()
    {
        TSeries src = MakeSeries(100);
        (TSeries results, Ahrens indicator) = Ahrens.Calculate(src, 9);
        Assert.Equal(100, results.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void SelfDampening_SmoothsOutput()
    {
        var ind = new Ahrens(20);
        TSeries src = MakeSeries(500);
        double[] outputs = new double[500];
        for (int i = 0; i < 500; i++)
        {
            outputs[i] = ind.Update(new TValue(DateTime.UtcNow, src.Values[i])).Value;
        }

        // Compare variance of last 100 values — output should be smoother
        double srcMean = 0, outMean = 0;
        for (int i = 400; i < 500; i++)
        {
            srcMean += src.Values[i];
            outMean += outputs[i];
        }

        srcMean /= 100;
        outMean /= 100;

        double srcVar = 0, outVar = 0;
        for (int i = 400; i < 500; i++)
        {
            double d1 = src.Values[i] - srcMean;
            srcVar += d1 * d1;
            double d2 = outputs[i] - outMean;
            outVar += d2 * d2;
        }

        Assert.True(outVar < srcVar);
    }
}
