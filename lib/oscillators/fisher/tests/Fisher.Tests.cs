using Xunit;

namespace QuanTAlib.Tests;

public sealed class FisherTests
{
    private const double Tolerance = 1e-9;

    // ───── A) Constructor validation ─────

    [Fact]
    public void Constructor_DefaultPeriod_IsValid()
    {
        var fisher = new Fisher();
        Assert.Equal(10, fisher.Period);
        Assert.Equal("Fisher(10)", fisher.Name);
    }

    [Fact]
    public void Constructor_InvalidPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Fisher(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Fisher(period: -5));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_InvalidAlpha_Zero_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Fisher(period: 10, alpha: 0));
        Assert.Equal("alpha", ex.ParamName);
    }

    [Fact]
    public void Constructor_InvalidAlpha_OverOne_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Fisher(period: 10, alpha: 1.5));
        Assert.Equal("alpha", ex.ParamName);
    }

    [Fact]
    public void Constructor_CustomPeriod_SetsCorrectly()
    {
        var fisher = new Fisher(period: 20);
        Assert.Equal(20, fisher.Period);
        Assert.Equal("Fisher(20)", fisher.Name);
    }

    // ───── B) Basic calculation ─────

    [Fact]
    public void Update_ReturnsTValue()
    {
        var fisher = new Fisher(period: 5);
        var result = fisher.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var fisher = new Fisher(period: 5);
        fisher.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(fisher.Last.Value));
    }

    [Fact]
    public void Update_FisherAndSignal_Accessible()
    {
        var fisher = new Fisher(period: 5);
        for (int i = 0; i < 10; i++)
        {
            fisher.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        Assert.True(double.IsFinite(fisher.FisherValue));
        Assert.True(double.IsFinite(fisher.Signal));
    }

    [Fact]
    public void Update_RisingPrices_PositiveFisher()
    {
        var fisher = new Fisher(period: 5);
        for (int i = 0; i < 20; i++)
        {
            fisher.Update(new TValue(DateTime.UtcNow, 100.0 + (i * 2)));
        }
        Assert.True(fisher.FisherValue > 0, "Rising prices should produce positive Fisher");
    }

    [Fact]
    public void Update_FallingPrices_NegativeFisher()
    {
        var fisher = new Fisher(period: 5);
        for (int i = 0; i < 20; i++)
        {
            fisher.Update(new TValue(DateTime.UtcNow, 200.0 - (i * 2)));
        }
        Assert.True(fisher.FisherValue < 0, "Falling prices should produce negative Fisher");
    }

    // ───── C) State + bar correction ─────

    [Fact]
    public void Update_IsNew_False_RollsBack()
    {
        var fisher = new Fisher(period: 5);
        for (int i = 0; i < 12; i++)
        {
            fisher.Update(new TValue(DateTime.UtcNow, 100.0 + i), isNew: true);
        }

        fisher.Update(new TValue(DateTime.UtcNow, 105.0), isNew: false);
        var corrected = fisher.Last;

        fisher.Update(new TValue(DateTime.UtcNow, 105.0), isNew: false);
        var corrected2 = fisher.Last;

        Assert.Equal(corrected.Value, corrected2.Value, Tolerance);
    }

    [Fact]
    public void Update_IterativeCorrections_Restore()
    {
        var fisher = new Fisher(period: 5);
        double[] data = new double[15];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = 100 + (i * 2);
        }

        for (int i = 0; i < data.Length; i++)
        {
            fisher.Update(new TValue(DateTime.UtcNow, data[i]), isNew: true);
        }

        var baseline = fisher.Last.Value;

        fisher.Update(new TValue(DateTime.UtcNow, 999.0), isNew: false);
        fisher.Update(new TValue(DateTime.UtcNow, 888.0), isNew: false);
        fisher.Update(new TValue(DateTime.UtcNow, data[^1]), isNew: false);

        Assert.Equal(baseline, fisher.Last.Value, Tolerance);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var fisher = new Fisher(period: 5);
        for (int i = 0; i < 10; i++)
        {
            fisher.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        fisher.Reset();

        Assert.False(fisher.IsHot);
        Assert.Equal(0.0, fisher.Last.Value);
    }

    // ───── D) Warmup/convergence ─────

    [Fact]
    public void IsHot_FlipsAfterPeriod()
    {
        int period = 10;
        var fisher = new Fisher(period);

        for (int i = 0; i < period - 1; i++)
        {
            fisher.Update(new TValue(DateTime.UtcNow, 100.0 + i));
            Assert.False(fisher.IsHot);
        }

        fisher.Update(new TValue(DateTime.UtcNow, 110.0));
        Assert.True(fisher.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesPeriod()
    {
        var fisher = new Fisher(period: 14);
        Assert.Equal(14, fisher.WarmupPeriod);
    }

    // ───── E) Robustness ─────

    [Fact]
    public void Update_NaN_UsesLastValid()
    {
        var fisher = new Fisher(period: 5);
        for (int i = 0; i < 10; i++)
        {
            fisher.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        _ = fisher.Last.Value;
        fisher.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(fisher.Last.Value));
    }

    [Fact]
    public void Update_Infinity_UsesLastValid()
    {
        var fisher = new Fisher(period: 5);
        for (int i = 0; i < 10; i++)
        {
            fisher.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        fisher.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(fisher.Last.Value));
    }

    [Fact]
    public void Update_BatchNaN_RemainsFinite()
    {
        var fisher = new Fisher(period: 5);
        for (int i = 0; i < 3; i++)
        {
            fisher.Update(new TValue(DateTime.UtcNow, double.NaN));
        }
        Assert.True(double.IsFinite(fisher.Last.Value));
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
        var streaming = new Fisher(period);
        var streamResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamResults[i] = streaming.Update(source[i]).Value;
        }

        // 2. Batch TSeries
        TSeries batchSeries = Fisher.Batch(source, period);

        // 3. Batch Span
        var spanOutput = new double[source.Count];
        Fisher.Batch(source.Values, spanOutput, period);

        // 4. Event-based
        var eventSource = new TSeries();
        var eventIndicator = new Fisher(eventSource, period);
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

    [Fact]
    public void Batch_Span_MismatchedLengths_Throws()
    {
        var src = new double[10];
        var output = new double[5];
        var ex = Assert.Throws<ArgumentException>(() => Fisher.Batch(src, output, 5));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidPeriod_Throws()
    {
        var src = new double[10];
        var output = new double[10];
        var ex = Assert.Throws<ArgumentException>(() => Fisher.Batch(src, output, 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_Empty_NoException()
    {
        var src = ReadOnlySpan<double>.Empty;
        var output = Span<double>.Empty;
        Fisher.Batch(src, output, 5);
        Assert.True(true);
    }

    [Fact]
    public void Batch_Span_MatchesTSeries()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        TSeries batchSeries = Fisher.Batch(source, 10);

        var spanOutput = new double[source.Count];
        Fisher.Batch(source.Values, spanOutput, 10);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(batchSeries.Values[i], spanOutput[i], 12);
        }
    }

    [Fact]
    public void Batch_Span_NaN_Handled()
    {
        double[] src = [100, 101, double.NaN, 103, 104, 105, 106, 107, 108, 109];
        var output = new double[src.Length];
        Fisher.Batch(src, output, 5);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]));
        }
    }

    // ───── H) Chainability ─────

    [Fact]
    public void Event_PubFires()
    {
        var source = new TSeries();
        var fisher = new Fisher(source, period: 5);
        int count = 0;
        fisher.Pub += (object? _, in TValueEventArgs _) => count++;

        source.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(1, count);
    }

    [Fact]
    public void Event_ChainingWorks()
    {
        var source = new TSeries();
        var fisher = new Fisher(source, period: 5);

        for (int i = 0; i < 20; i++)
        {
            source.Add(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        Assert.True(fisher.IsHot);
        Assert.True(double.IsFinite(fisher.Last.Value));
    }

    // ───── Domain-specific tests ─────

    [Fact]
    public void FisherTransform_MathematicalProperties()
    {
        // Fisher Transform is arctanh: should be odd function
        // For normalized input 0, Fisher should be 0
        var fisher = new Fisher(period: 5);

        // Feed constant price → normalized = 0 → Fisher ≈ 0
        for (int i = 0; i < 20; i++)
        {
            fisher.Update(new TValue(DateTime.UtcNow, 100.0));
        }

        Assert.True(Math.Abs(fisher.FisherValue) < 0.1,
            $"Constant price should produce Fisher near 0, got {fisher.FisherValue}");
    }

    [Fact]
    public void FisherTransform_OutputIsUnbounded()
    {
        // Fisher can exceed ±2 with strong trends
        var fisher = new Fisher(period: 5);

        // Create a very strong uptrend
        for (int i = 0; i < 30; i++)
        {
            fisher.Update(new TValue(DateTime.UtcNow, 100.0 + (i * 10)));
        }

        // Fisher should be significantly positive
        Assert.True(fisher.FisherValue > 1.0,
            $"Strong uptrend should produce Fisher > 1, got {fisher.FisherValue}");
    }

    [Fact]
    public void Signal_LagseFisher()
    {
        // Signal is EMA of Fisher, so under strong trend it should lag
        var fisher = new Fisher(period: 5);

        for (int i = 0; i < 30; i++)
        {
            fisher.Update(new TValue(DateTime.UtcNow, 100.0 + (i * 5)));
        }

        // Both should be positive in uptrend
        Assert.True(fisher.FisherValue > 0);
        Assert.True(fisher.Signal > 0);
    }
}
