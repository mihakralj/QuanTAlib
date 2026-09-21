using Xunit;

namespace QuanTAlib.Tests;

public sealed class KriTests
{
    private const double Tolerance = 1e-9;

    // ───── A) Constructor validation ─────
    [Fact] public void Constructor_DefaultPeriod_IsValid() { var k = new Kri(); Assert.Equal(14, k.Period); Assert.Equal("Kri(14)", k.Name); }
    [Fact] public void Constructor_InvalidPeriod_Throws() { var ex = Assert.Throws<ArgumentException>(() => new Kri(period: 0)); Assert.Equal("period", ex.ParamName); }
    [Fact] public void Constructor_NegativePeriod_Throws() { var ex = Assert.Throws<ArgumentException>(() => new Kri(period: -5)); Assert.Equal("period", ex.ParamName); }
    [Fact] public void Constructor_CustomPeriod_SetsCorrectly() { var k = new Kri(period: 20); Assert.Equal(20, k.Period); Assert.Equal("Kri(20)", k.Name); }

    // ───── B) Basic calculation ─────
    [Fact] public void Update_ReturnsTValue() { var k = new Kri(5); Assert.IsType<TValue>(k.Update(new TValue(DateTime.UtcNow, 100.0))); }
    [Fact] public void Update_Last_IsAccessible() { var k = new Kri(5); k.Update(new TValue(DateTime.UtcNow, 100.0)); Assert.True(double.IsFinite(k.Last.Value)); }
    [Fact]
    public void Update_PriceAboveSMA_PositiveKRI()
    {
        var k = new Kri(5);
        for (int i = 0; i < 20; i++)
        {
            k.Update(new TValue(DateTime.UtcNow, 100.0 + (i * 2)));
        }
        Assert.True(k.Last.Value > 0, "Price above SMA should produce positive KRI");
    }
    [Fact]
    public void Update_PriceBelowSMA_NegativeKRI()
    {
        var k = new Kri(5);
        for (int i = 0; i < 20; i++)
        {
            k.Update(new TValue(DateTime.UtcNow, 200.0 - (i * 2)));
        }
        Assert.True(k.Last.Value < 0, "Price below SMA should produce negative KRI");
    }

    // ───── C) State + bar correction ─────
    [Fact]
    public void Update_IsNew_False_RollsBack()
    {
        var k = new Kri(5);
        for (int i = 0; i < 12; i++)
        {
            k.Update(new TValue(DateTime.UtcNow, 100.0 + i), isNew: true);
        }
        k.Update(new TValue(DateTime.UtcNow, 105.0), isNew: false);
        var c1 = k.Last;
        k.Update(new TValue(DateTime.UtcNow, 105.0), isNew: false);
        Assert.Equal(c1.Value, k.Last.Value, Tolerance);
    }
    [Fact]
    public void Update_IterativeCorrections_Restore()
    {
        var k = new Kri(5);
        double[] data = new double[15];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = 100 + (i * 2);
        }
        for (int i = 0; i < data.Length; i++)
        {
            k.Update(new TValue(DateTime.UtcNow, data[i]), isNew: true);
        }
        var baseline = k.Last.Value;
        k.Update(new TValue(DateTime.UtcNow, 999.0), isNew: false);
        k.Update(new TValue(DateTime.UtcNow, 888.0), isNew: false);
        k.Update(new TValue(DateTime.UtcNow, data[^1]), isNew: false);
        Assert.Equal(baseline, k.Last.Value, Tolerance);
    }
    [Fact]
    public void Reset_ClearsState()
    {
        var k = new Kri(5);
        for (int i = 0; i < 10; i++)
        {
            k.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        k.Reset();
        Assert.False(k.IsHot);
        Assert.Equal(0.0, k.Last.Value);
    }

    // ───── D) Warmup/convergence ─────
    [Fact]
    public void IsHot_FlipsAfterPeriod()
    {
        int period = 10;
        var k = new Kri(period);
        for (int i = 0; i < period - 1; i++)
        {
            k.Update(new TValue(DateTime.UtcNow, 100.0 + i));
            Assert.False(k.IsHot);
        }
        k.Update(new TValue(DateTime.UtcNow, 110.0));
        Assert.True(k.IsHot);
    }
    [Fact] public void WarmupPeriod_MatchesPeriod() { Assert.Equal(14, new Kri(14).WarmupPeriod); }

    // ───── E) Robustness ─────
    [Fact]
    public void Update_NaN_UsesLastValid()
    {
        var k = new Kri(5);
        for (int i = 0; i < 10; i++)
        {
            k.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        k.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(k.Last.Value));
    }
    [Fact]
    public void Update_Infinity_UsesLastValid()
    {
        var k = new Kri(5);
        for (int i = 0; i < 10; i++)
        {
            k.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        k.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(k.Last.Value));
    }
    [Fact]
    public void Update_BatchNaN_RemainsFinite()
    {
        var k = new Kri(5);
        for (int i = 0; i < 3; i++)
        {
            k.Update(new TValue(DateTime.UtcNow, double.NaN));
        }
        Assert.True(double.IsFinite(k.Last.Value));
    }

    // ───── F) Consistency (4 modes match) ─────
    [Fact]
    public void AllModes_ProduceSameResults()
    {
        int period = 10;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        var streaming = new Kri(period);
        var streamResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamResults[i] = streaming.Update(source[i]).Value;
        }

        TSeries batchSeries = Kri.Batch(source, period);
        var spanOutput = new double[source.Count];
        Kri.Batch(source.Values, spanOutput, period);

        var eventSource = new TSeries();
        var eventIndicator = new Kri(eventSource, period);
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
    [Fact] public void Batch_Span_MismatchedLength_Throws() { var ex = Assert.Throws<ArgumentException>(() => Kri.Batch(new double[10], new double[5], 5)); Assert.Equal("output", ex.ParamName); }
    [Fact] public void Batch_Span_InvalidPeriod_Throws() { var ex = Assert.Throws<ArgumentException>(() => Kri.Batch(new double[10], new double[10], 0)); Assert.Equal("period", ex.ParamName); }
    [Fact] public void Batch_Span_Empty_NoException() { Kri.Batch(ReadOnlySpan<double>.Empty, Span<double>.Empty, 5); Assert.True(true); }
    [Fact]
    public void Batch_Span_NaN_Handled()
    {
        double[] src = [100, double.NaN, 102, 103, 104, 105, 106, 107, 108, 109];
        var output = new double[src.Length];
        Kri.Batch(src, output, 5);
        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]));
        }
    }

    // ───── H) Chainability ─────
    [Fact]
    public void Pub_Fires_OnUpdate()
    {
        var k = new Kri(5); int f = 0;
        k.Pub += (object? _, in TValueEventArgs _) => f++;
        k.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(1, f);
    }
    [Fact]
    public void EventBased_Chaining_Works()
    {
        var source = new TSeries();
        var k = new Kri(source, 5);
        source.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(k.Last.Value));
    }
}
