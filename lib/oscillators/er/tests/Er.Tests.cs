using Xunit;

namespace QuanTAlib.Tests;

public sealed class ErTests
{
    private const double Tolerance = 1e-9;

    // ───── A) Constructor validation ─────

    [Fact]
    public void Constructor_DefaultPeriod_IsValid()
    {
        var er = new Er();
        Assert.Equal(10, er.Period);
        Assert.Equal("Er(10)", er.Name);
    }

    [Fact]
    public void Constructor_InvalidPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Er(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Er(period: -5));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_CustomPeriod_SetsCorrectly()
    {
        var er = new Er(period: 20);
        Assert.Equal(20, er.Period);
        Assert.Equal("Er(20)", er.Name);
    }

    // ───── B) Basic calculation ─────

    [Fact]
    public void Update_ReturnsTValue()
    {
        var er = new Er(period: 5);
        var result = er.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var er = new Er(period: 5);
        er.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(er.Last.Value));
    }

    [Fact]
    public void Update_TrendingPrices_HighER()
    {
        var er = new Er(period: 10);
        for (int i = 0; i < 20; i++)
        {
            er.Update(new TValue(DateTime.UtcNow, 100.0 + i * 2));
        }
        Assert.True(er.Last.Value > 0.8, "Strongly trending prices should produce high ER");
    }

    [Fact]
    public void Update_ChoppyPrices_LowER()
    {
        var er = new Er(period: 10);
        for (int i = 0; i < 30; i++)
        {
            double price = 100.0 + (i % 2 == 0 ? 5.0 : -5.0);
            er.Update(new TValue(DateTime.UtcNow, price));
        }
        Assert.True(er.Last.Value < 0.3, "Choppy prices should produce low ER");
    }

    [Fact]
    public void Update_Output_ClampedTo01()
    {
        var er = new Er(period: 5);
        for (int i = 0; i < 20; i++)
        {
            var result = er.Update(new TValue(DateTime.UtcNow, 100.0 + i));
            Assert.InRange(result.Value, 0.0, 1.0);
        }
    }

    // ───── C) State + bar correction ─────

    [Fact]
    public void Update_IsNew_False_RollsBack()
    {
        var er = new Er(period: 5);
        for (int i = 0; i < 12; i++)
        {
            er.Update(new TValue(DateTime.UtcNow, 100.0 + i), isNew: true);
        }

        er.Update(new TValue(DateTime.UtcNow, 105.0), isNew: false);
        var corrected = er.Last;

        er.Update(new TValue(DateTime.UtcNow, 105.0), isNew: false);
        var corrected2 = er.Last;

        Assert.Equal(corrected.Value, corrected2.Value, Tolerance);
    }

    [Fact]
    public void Update_IterativeCorrections_Restore()
    {
        var er = new Er(period: 5);
        double[] data = new double[15];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = 100 + i * 2;
        }

        for (int i = 0; i < data.Length; i++)
        {
            er.Update(new TValue(DateTime.UtcNow, data[i]), isNew: true);
        }

        var baseline = er.Last.Value;

        er.Update(new TValue(DateTime.UtcNow, 999.0), isNew: false);
        er.Update(new TValue(DateTime.UtcNow, 888.0), isNew: false);
        er.Update(new TValue(DateTime.UtcNow, data[^1]), isNew: false);

        Assert.Equal(baseline, er.Last.Value, Tolerance);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var er = new Er(period: 5);
        for (int i = 0; i < 10; i++)
        {
            er.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        er.Reset();

        Assert.False(er.IsHot);
        Assert.Equal(0.0, er.Last.Value);
    }

    // ───── D) Warmup/convergence ─────

    [Fact]
    public void IsHot_FlipsWhenBufferFull()
    {
        int period = 10;
        var er = new Er(period);

        for (int i = 0; i < period; i++)
        {
            er.Update(new TValue(DateTime.UtcNow, 100.0 + i));
            Assert.False(er.IsHot);
        }

        er.Update(new TValue(DateTime.UtcNow, 120.0));
        Assert.True(er.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesPeriodPlusOne()
    {
        var er = new Er(period: 14);
        Assert.Equal(15, er.WarmupPeriod);
    }

    // ───── E) Robustness ─────

    [Fact]
    public void Update_NaN_UsesLastValid()
    {
        var er = new Er(period: 5);
        for (int i = 0; i < 10; i++)
        {
            er.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        er.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(er.Last.Value));
    }

    [Fact]
    public void Update_Infinity_UsesLastValid()
    {
        var er = new Er(period: 5);
        for (int i = 0; i < 10; i++)
        {
            er.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        er.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(er.Last.Value));
    }

    [Fact]
    public void Update_BatchNaN_RemainsFinite()
    {
        var er = new Er(period: 5);
        for (int i = 0; i < 3; i++)
        {
            er.Update(new TValue(DateTime.UtcNow, double.NaN));
        }
        Assert.True(double.IsFinite(er.Last.Value));
    }

    // ───── F) Consistency (4 modes match) ─────

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        int period = 10;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        // 1. Streaming
        var streaming = new Er(period);
        var streamResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamResults[i] = streaming.Update(source[i]).Value;
        }

        // 2. Batch TSeries
        TSeries batchSeries = Er.Batch(source, period);

        // 3. Batch Span
        var spanOutput = new double[source.Count];
        Er.Batch(source.Values, spanOutput, period);

        // 4. Event-driven
        var eventSource = new TSeries();
        var eventIndicator = new Er(eventSource, period);
        var eventResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            eventSource.Add(source[i]);
            eventResults[i] = eventIndicator.Last.Value;
        }

        // Compare all modes
        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamResults[i], batchSeries.Values[i], Tolerance);
            Assert.Equal(streamResults[i], spanOutput[i], Tolerance);
            Assert.Equal(streamResults[i], eventResults[i], Tolerance);
        }
    }

    // ───── G) Span API tests ─────

    [Fact]
    public void Batch_Span_MismatchedLength_Throws()
    {
        var src = new double[10];
        var output = new double[5];
        var ex = Assert.Throws<ArgumentException>(() => Er.Batch(src, output, 5));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidPeriod_Throws()
    {
        var src = new double[10];
        var output = new double[10];
        var ex = Assert.Throws<ArgumentException>(() => Er.Batch(src, output, 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_Empty_NoException()
    {
        var src = ReadOnlySpan<double>.Empty;
        var output = Span<double>.Empty;
        Er.Batch(src, output, 5);
        Assert.True(true); // S2699: assertion confirms no-exception completion
    }

    [Fact]
    public void Batch_Span_MatchesTSeries()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        TSeries batchSeries = Er.Batch(source, 10);

        var spanOutput = new double[source.Count];
        Er.Batch(source.Values, spanOutput, 10);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(batchSeries.Values[i], spanOutput[i], Tolerance);
        }
    }

    [Fact]
    public void Batch_Span_NaN_Handled()
    {
        double[] src = [100, double.NaN, 102, 103, 104, 105, 106, 107, 108, 109];
        var output = new double[src.Length];
        Er.Batch(src, output, 5);
        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]));
        }
    }

    // ───── H) Chainability ─────

    [Fact]
    public void Pub_Fires_OnUpdate()
    {
        var er = new Er(period: 5);
        int fireCount = 0;
        er.Pub += (object? _, in TValueEventArgs _) => fireCount++;

        er.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.Equal(1, fireCount);
    }

    [Fact]
    public void EventBased_Chaining_Works()
    {
        var source = new TSeries();
        var er = new Er(source, period: 5);

        source.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(er.Last.Value));
    }
}
