using Xunit;

namespace QuanTAlib.Tests;

public sealed class PslTests
{
    private const double Tolerance = 1e-9;

    // ───── A) Constructor validation ─────
    [Fact] public void Constructor_DefaultPeriod_IsValid() { var p = new Psl(); Assert.Equal(12, p.Period); Assert.Equal("Psl(12)", p.Name); }
    [Fact] public void Constructor_InvalidPeriod_Throws() { var ex = Assert.Throws<ArgumentException>(() => new Psl(period: 0)); Assert.Equal("period", ex.ParamName); }
    [Fact] public void Constructor_NegativePeriod_Throws() { var ex = Assert.Throws<ArgumentException>(() => new Psl(period: -5)); Assert.Equal("period", ex.ParamName); }
    [Fact] public void Constructor_CustomPeriod_SetsCorrectly() { var p = new Psl(period: 20); Assert.Equal(20, p.Period); Assert.Equal("Psl(20)", p.Name); }

    // ───── B) Basic calculation ─────
    [Fact] public void Update_ReturnsTValue() { var p = new Psl(5); Assert.IsType<TValue>(p.Update(new TValue(DateTime.UtcNow, 100.0))); }
    [Fact] public void Update_Last_IsAccessible() { var p = new Psl(5); p.Update(new TValue(DateTime.UtcNow, 100.0)); Assert.True(double.IsFinite(p.Last.Value)); }
    [Fact]
    public void Update_RisingPrices_PslAbove50()
    {
        var p = new Psl(5);
        for (int i = 0; i < 20; i++)
        {
            p.Update(new TValue(DateTime.UtcNow, 100.0 + i * 2));
        }
        Assert.True(p.Last.Value > 50, "Rising prices should produce PSL > 50");
    }
    [Fact]
    public void Update_FallingPrices_PslBelow50()
    {
        var p = new Psl(5);
        for (int i = 0; i < 20; i++)
        {
            p.Update(new TValue(DateTime.UtcNow, 200.0 - i * 2));
        }
        Assert.True(p.Last.Value < 50, "Falling prices should produce PSL < 50");
    }
    [Fact]
    public void Update_OutputInRange()
    {
        var p = new Psl(5);
        for (int i = 0; i < 20; i++)
        {
            p.Update(new TValue(DateTime.UtcNow, 100.0 + i));
            Assert.InRange(p.Last.Value, 0.0, 100.0);
        }
    }

    // ───── C) State + bar correction ─────
    [Fact]
    public void Update_IsNew_False_RollsBack()
    {
        var p = new Psl(5);
        for (int i = 0; i < 12; i++)
        {
            p.Update(new TValue(DateTime.UtcNow, 100.0 + i), isNew: true);
        }
        p.Update(new TValue(DateTime.UtcNow, 105.0), isNew: false);
        var c1 = p.Last;
        p.Update(new TValue(DateTime.UtcNow, 105.0), isNew: false);
        Assert.Equal(c1.Value, p.Last.Value, Tolerance);
    }
    [Fact]
    public void Update_IterativeCorrections_Restore()
    {
        var p = new Psl(5);
        double[] data = new double[15];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = 100 + i * 2;
        }
        for (int i = 0; i < data.Length; i++)
        {
            p.Update(new TValue(DateTime.UtcNow, data[i]), isNew: true);
        }
        var baseline = p.Last.Value;
        p.Update(new TValue(DateTime.UtcNow, 999.0), isNew: false);
        p.Update(new TValue(DateTime.UtcNow, 888.0), isNew: false);
        p.Update(new TValue(DateTime.UtcNow, data[^1]), isNew: false);
        Assert.Equal(baseline, p.Last.Value, Tolerance);
    }
    [Fact]
    public void Reset_ClearsState()
    {
        var p = new Psl(5);
        for (int i = 0; i < 10; i++)
        {
            p.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        p.Reset();
        Assert.False(p.IsHot);
        Assert.Equal(0.0, p.Last.Value);
    }

    // ───── D) Warmup/convergence ─────
    [Fact]
    public void IsHot_FlipsAfterPeriod()
    {
        int period = 10;
        var p = new Psl(period);
        for (int i = 0; i < period - 1; i++)
        {
            p.Update(new TValue(DateTime.UtcNow, 100.0 + i));
            Assert.False(p.IsHot);
        }
        p.Update(new TValue(DateTime.UtcNow, 110.0));
        Assert.True(p.IsHot);
    }
    [Fact] public void WarmupPeriod_MatchesPeriod() { Assert.Equal(12, new Psl(12).WarmupPeriod); }

    // ───── E) Robustness ─────
    [Fact]
    public void Update_NaN_UsesLastValid()
    {
        var p = new Psl(5);
        for (int i = 0; i < 10; i++)
        {
            p.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        p.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(p.Last.Value));
    }
    [Fact]
    public void Update_Infinity_UsesLastValid()
    {
        var p = new Psl(5);
        for (int i = 0; i < 10; i++)
        {
            p.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        p.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(p.Last.Value));
    }
    [Fact]
    public void Update_BatchNaN_RemainsFinite()
    {
        var p = new Psl(5);
        for (int i = 0; i < 3; i++)
        {
            p.Update(new TValue(DateTime.UtcNow, double.NaN));
        }
        Assert.True(double.IsFinite(p.Last.Value));
    }

    // ───── F) Consistency (4 modes match) ─────
    [Fact]
    public void AllModes_ProduceSameResults()
    {
        int period = 10;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        var streaming = new Psl(period);
        var streamResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamResults[i] = streaming.Update(source[i]).Value;
        }

        TSeries batchSeries = Psl.Batch(source, period);
        var spanOutput = new double[source.Count];
        Psl.Batch(source.Values, spanOutput, period);

        var eventSource = new TSeries();
        var eventIndicator = new Psl(eventSource, period);
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

    // ───── G) Span API tests ─────
    [Fact] public void Batch_Span_MismatchedLength_Throws() { var ex = Assert.Throws<ArgumentException>(() => Psl.Batch(new double[10], new double[5], 5)); Assert.Equal("output", ex.ParamName); }
    [Fact] public void Batch_Span_InvalidPeriod_Throws() { var ex = Assert.Throws<ArgumentException>(() => Psl.Batch(new double[10], new double[10], 0)); Assert.Equal("period", ex.ParamName); }
    [Fact] public void Batch_Span_Empty_NoException() { Psl.Batch(ReadOnlySpan<double>.Empty, Span<double>.Empty, 5); Assert.True(true); }
    [Fact]
    public void Batch_Span_NaN_Handled()
    {
        double[] src = [100, double.NaN, 102, 103, 104, 105, 106, 107, 108, 109];
        var output = new double[src.Length];
        Psl.Batch(src, output, 5);
        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]));
        }
    }

    // ───── H) Chainability ─────
    [Fact]
    public void Pub_Fires_OnUpdate()
    {
        var p = new Psl(5); int f = 0;
        p.Pub += (object? _, in TValueEventArgs _) => f++;
        p.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(1, f);
    }
    [Fact]
    public void EventBased_Chaining_Works()
    {
        var source = new TSeries();
        var p = new Psl(source, 5);
        source.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(p.Last.Value));
    }
}
