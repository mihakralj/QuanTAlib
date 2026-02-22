namespace QuanTAlib.Tests;

public class Tukey_wTests
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

    // === A) Constructor validation ===

    [Fact]
    public void Constructor_DefaultPeriod_Is20()
    {
        var tw = new Tukey_w();
        Assert.Equal("Tukey_w(20,0.50)", tw.Name);
    }

    [Fact]
    public void Constructor_CustomPeriodAndAlpha()
    {
        var tw = new Tukey_w(period: 10, alpha: 0.3);
        Assert.Equal("Tukey_w(10,0.30)", tw.Name);
    }

    [Fact]
    public void Constructor_Period2_IsValid()
    {
        var tw = new Tukey_w(period: 2);
        Assert.Equal("Tukey_w(2,0.50)", tw.Name);
    }

    [Fact]
    public void Constructor_PeriodBelow2_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Tukey_w(period: 1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Tukey_w(period: -5));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_AlphaBelowZero_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Tukey_w(period: 10, alpha: -0.1));
        Assert.Equal("alpha", ex.ParamName);
    }

    [Fact]
    public void Constructor_AlphaAboveOne_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Tukey_w(period: 10, alpha: 1.1));
        Assert.Equal("alpha", ex.ParamName);
    }

    [Fact]
    public void Constructor_AlphaZero_IsValid()
    {
        var tw = new Tukey_w(period: 5, alpha: 0.0);
        Assert.Contains("0.00", tw.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_AlphaOne_IsValid()
    {
        var tw = new Tukey_w(period: 5, alpha: 1.0);
        Assert.Contains("1.00", tw.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_SetsWarmupPeriod()
    {
        var tw = new Tukey_w(period: 8);
        Assert.Equal(8, tw.WarmupPeriod);
    }

    // === B) Basic calculation ===

    [Fact]
    public void Update_ReturnsTValue()
    {
        var tw = new Tukey_w(period: 4);
        var result = tw.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_ConstantInput_ReturnsConstant()
    {
        var tw = new Tukey_w(period: 5, alpha: 0.5);
        for (int i = 0; i < 10; i++)
        {
            tw.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 42.0));
        }
        Assert.Equal(42.0, tw.Last.Value, 1e-10);
    }

    [Fact]
    public void Update_Alpha0_EquivalentToSMA()
    {
        // alpha=0 → rectangular window → SMA
        int period = 5;
        var tw = new Tukey_w(period: period, alpha: 0.0);
        double[] vals = { 10, 20, 30, 40, 50 };
        for (int i = 0; i < vals.Length; i++)
        {
            tw.Update(new TValue(DateTime.UtcNow.AddSeconds(i), vals[i]));
        }
        // SMA(5) = (10+20+30+40+50)/5 = 30
        Assert.Equal(30.0, tw.Last.Value, 1e-10);
    }

    [Fact]
    public void Update_Alpha0_LargerSeries_MatchesSMA()
    {
        var src = MakeSeries(100);
        int period = 10;

        var tw = Tukey_w.Batch(src, period, alpha: 0.0);
        var sma = Sma.Batch(src, period);

        // After warmup, should match SMA exactly
        for (int i = period - 1; i < src.Count; i++)
        {
            Assert.Equal(sma[i].Value, tw[i].Value, 1e-10);
        }
    }

    // === C) State + bar correction ===

    [Fact]
    public void Update_IsNew_True_AdvancesState()
    {
        var tw = new Tukey_w(period: 4);
        tw.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        tw.Update(new TValue(DateTime.UtcNow.AddSeconds(1), 110.0), isNew: true);
        var r1 = tw.Last;
        tw.Update(new TValue(DateTime.UtcNow.AddSeconds(2), 120.0), isNew: true);
        Assert.NotEqual(r1.Value, tw.Last.Value);
    }

    [Fact]
    public void Update_IsNew_False_Rewrites()
    {
        var tw = new Tukey_w(period: 4, alpha: 0.5);
        for (int i = 0; i < 5; i++)
        {
            tw.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i), isNew: true);
        }
        var afterNew = tw.Last;

        tw.Update(new TValue(DateTime.UtcNow.AddSeconds(4), 104.0), isNew: false);
        Assert.Equal(afterNew.Value, tw.Last.Value, 1e-10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var tw = new Tukey_w(period: 4);
        for (int i = 0; i < 10; i++)
        {
            tw.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }
        Assert.True(tw.IsHot);

        tw.Reset();
        Assert.False(tw.IsHot);
        Assert.Equal(default, tw.Last);
    }

    // === D) Warmup/convergence ===

    [Fact]
    public void IsHot_FlipsAtPeriod()
    {
        var tw = new Tukey_w(period: 5);
        for (int i = 0; i < 4; i++)
        {
            tw.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
            Assert.False(tw.IsHot);
        }
        tw.Update(new TValue(DateTime.UtcNow.AddSeconds(4), 104.0));
        Assert.True(tw.IsHot);
    }

    [Fact]
    public void DuringWarmup_ReturnsRawValue()
    {
        var tw = new Tukey_w(period: 5);
        var result = tw.Update(new TValue(DateTime.UtcNow, 42.0));
        Assert.Equal(42.0, result.Value, 1e-10);
    }

    // === E) Robustness ===

    [Fact]
    public void Update_NaN_UsesLastValid()
    {
        var tw = new Tukey_w(period: 4);
        for (int i = 0; i < 5; i++)
        {
            tw.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }

        tw.Update(new TValue(DateTime.UtcNow.AddSeconds(5), double.NaN));
        Assert.True(double.IsFinite(tw.Last.Value));
    }

    [Fact]
    public void Update_Infinity_UsesLastValid()
    {
        var tw = new Tukey_w(period: 4);
        for (int i = 0; i < 5; i++)
        {
            tw.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }

        tw.Update(new TValue(DateTime.UtcNow.AddSeconds(5), double.PositiveInfinity));
        Assert.True(double.IsFinite(tw.Last.Value));
    }

    [Fact]
    public void Update_FirstValueNaN_ReturnsNaN()
    {
        var tw = new Tukey_w(period: 4);
        var result = tw.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsNaN(result.Value));
    }

    [Fact]
    public void Batch_BatchNaN_Safe()
    {
        double[] source = { 10, 20, double.NaN, 40, 50, 60 };
        double[] output = new double[source.Length];
        Tukey_w.Batch(source, output, period: 3, alpha: 0.5);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"output[{i}] should be finite");
        }
    }

    // === F) Consistency (4 modes match) ===

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        var src = MakeSeries(100);
        int period = 6;
        double alpha = 0.4;

        // Mode 1: Streaming
        var streaming = new Tukey_w(period, alpha);
        var streamResults = new List<double>();
        for (int i = 0; i < src.Count; i++)
        {
            streamResults.Add(streaming.Update(src[i]).Value);
        }

        // Mode 2: Batch TSeries
        var batchResults = Tukey_w.Batch(src, period, alpha);

        // Mode 3: Span API
        var spanOutput = new double[src.Count];
        Tukey_w.Batch(src.Values, spanOutput, period, alpha);

        // Mode 4: Event-based
        var publisher = new TSeries();
        var eventResults = new List<double>();
        var eventTw = new Tukey_w(publisher, period, alpha);
        eventTw.Pub += (object? sender, in TValueEventArgs e) => eventResults.Add(e.Value.Value);
        for (int i = 0; i < src.Count; i++)
        {
            publisher.Add(src[i]);
        }

        Assert.Equal(src.Count, batchResults.Count);
        Assert.Equal(src.Count, eventResults.Count);

        for (int i = 0; i < src.Count; i++)
        {
            double s = streamResults[i];
            double b = batchResults[i].Value;
            double sp = spanOutput[i];
            double ev = eventResults[i];

            if (double.IsNaN(s))
            {
                Assert.True(double.IsNaN(b));
                Assert.True(double.IsNaN(sp));
                Assert.True(double.IsNaN(ev));
            }
            else
            {
                Assert.Equal(s, b, 1e-10);
                Assert.Equal(s, sp, 1e-10);
                Assert.Equal(s, ev, 1e-10);
            }
        }
    }

    // === G) Span API tests ===

    [Fact]
    public void Batch_Span_MismatchedLengths_Throws()
    {
        double[] source = { 1, 2, 3 };
        double[] output = new double[2];

        var ex = Assert.Throws<ArgumentException>(() => Tukey_w.Batch(source, output, period: 2));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_PeriodBelow2_Throws()
    {
        double[] source = { 1, 2, 3 };
        double[] output = new double[3];

        var ex = Assert.Throws<ArgumentException>(() => Tukey_w.Batch(source, output, period: 1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_AlphaOutOfRange_Throws()
    {
        double[] source = { 1, 2, 3 };
        double[] output = new double[3];

        Assert.Throws<ArgumentException>(() => Tukey_w.Batch(source, output, period: 2, alpha: -0.1));
        Assert.Throws<ArgumentException>(() => Tukey_w.Batch(source, output, period: 2, alpha: 1.5));
    }

    [Fact]
    public void Batch_Span_EmptyInput_NoOutput()
    {
        Tukey_w.Batch(ReadOnlySpan<double>.Empty, Span<double>.Empty, period: 4);
        Assert.True(true);
    }

    [Fact]
    public void Batch_Span_LargeData_NoStackOverflow()
    {
        int count = 10_000;
        double[] source = new double[count];
        double[] output = new double[count];
        for (int i = 0; i < count; i++)
        {
            source[i] = 100.0 + (i % 50);
        }

        Tukey_w.Batch(source, output, period: 20, alpha: 0.5);
        Assert.True(double.IsFinite(output[^1]));
    }

    // === H) Chainability ===

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var tw = new Tukey_w(period: 4);
        int pubCount = 0;
        tw.Pub += (object? sender, in TValueEventArgs e) => pubCount++;

        tw.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(1, pubCount);
    }

    [Fact]
    public void EventBased_Chaining_Works()
    {
        var publisher = new TSeries();
        var tw = new Tukey_w(publisher, period: 4, alpha: 0.5);
        int resultCount = 0;
        tw.Pub += (object? sender, in TValueEventArgs e) => resultCount++;

        for (int i = 0; i < 10; i++)
        {
            publisher.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }
        Assert.Equal(10, resultCount);
    }

    // === Additional ===

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var src = MakeSeries(50);
        var (results, indicator) = Tukey_w.Calculate(src, period: 5, alpha: 0.5);

        Assert.Equal(50, results.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Dispose_UnsubscribesFromSource()
    {
        var publisher = new TSeries();
        var tw = new Tukey_w(publisher, period: 4);
        int pubCount = 0;
        tw.Pub += (object? sender, in TValueEventArgs e) => pubCount++;

        publisher.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(1, pubCount);

        tw.Dispose();

        publisher.Add(new TValue(DateTime.UtcNow.AddSeconds(1), 200.0));
        Assert.Equal(1, pubCount);
    }

    [Fact]
    public void Prime_SetsStateFromSpan()
    {
        var tw = new Tukey_w(period: 4);
        double[] data = { 10, 20, 30, 40, 50 };
        tw.Prime(data);

        Assert.True(tw.IsHot);
        Assert.True(double.IsFinite(tw.Last.Value));
    }

    [Fact]
    public void Output_BoundedByInputRange()
    {
        // All weights non-negative: output bounded by min/max input
        var tw = new Tukey_w(period: 5, alpha: 0.5);
        double[] vals = { 10, 20, 30, 40, 50 };
        for (int i = 0; i < vals.Length; i++)
        {
            tw.Update(new TValue(DateTime.UtcNow.AddSeconds(i), vals[i]));
        }
        Assert.InRange(tw.Last.Value, 10.0, 50.0);
    }

    [Fact]
    public void Update_TSeries_EmptySource_ReturnsEmpty()
    {
        var tw = new Tukey_w(period: 4);
        var result = tw.Update(new TSeries());
        Assert.Empty(result);
    }

    [Fact]
    public void Update_TSeries_ProducesCorrectLength()
    {
        var src = MakeSeries(100);
        var tw = new Tukey_w(period: 5);
        var result = tw.Update(src);
        Assert.Equal(100, result.Count);
    }
}
