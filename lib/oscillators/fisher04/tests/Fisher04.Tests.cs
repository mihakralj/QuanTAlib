using Xunit;

namespace QuanTAlib.Tests;

public sealed class Fisher04Tests
{
    private const double Tolerance = 1e-9;

    // ───── A) Constructor validation ─────

    [Fact]
    public void Constructor_DefaultPeriod_IsValid()
    {
        var fisher = new Fisher04();
        Assert.Equal(10, fisher.Period);
        Assert.Equal("Fisher04(10)", fisher.Name);
    }

    [Fact]
    public void Constructor_InvalidPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Fisher04(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Fisher04(period: -5));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_CustomPeriod_SetsCorrectly()
    {
        var fisher = new Fisher04(period: 20);
        Assert.Equal(20, fisher.Period);
        Assert.Equal("Fisher04(20)", fisher.Name);
    }

    // ───── B) Basic calculation ─────

    [Fact]
    public void Update_ReturnsTValue()
    {
        var fisher = new Fisher04(period: 5);
        var result = fisher.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var fisher = new Fisher04(period: 5);
        fisher.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(fisher.Last.Value));
    }

    [Fact]
    public void Update_FisherAndSignal_Accessible()
    {
        var fisher = new Fisher04(period: 5);
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
        var fisher = new Fisher04(period: 5);
        for (int i = 0; i < 20; i++)
        {
            fisher.Update(new TValue(DateTime.UtcNow, 100.0 + (i * 2)));
        }
        Assert.True(fisher.FisherValue > 0, "Rising prices should produce positive Fisher04");
    }

    [Fact]
    public void Update_FallingPrices_NegativeFisher()
    {
        var fisher = new Fisher04(period: 5);
        for (int i = 0; i < 20; i++)
        {
            fisher.Update(new TValue(DateTime.UtcNow, 200.0 - (i * 2)));
        }
        Assert.True(fisher.FisherValue < 0, "Falling prices should produce negative Fisher04");
    }

    // ───── C) State + bar correction ─────

    [Fact]
    public void Update_IsNew_False_RollsBack()
    {
        var fisher = new Fisher04(period: 5);
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
        var fisher = new Fisher04(period: 5);
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
        var fisher = new Fisher04(period: 5);
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
        var fisher = new Fisher04(period);

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
        var fisher = new Fisher04(period: 14);
        Assert.Equal(14, fisher.WarmupPeriod);
    }

    // ───── E) Robustness ─────

    [Fact]
    public void Update_NaN_UsesLastValid()
    {
        var fisher = new Fisher04(period: 5);
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
        var fisher = new Fisher04(period: 5);
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
        var fisher = new Fisher04(period: 5);
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
        var streaming = new Fisher04(period);
        var streamResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamResults[i] = streaming.Update(source[i]).Value;
        }

        // 2. Batch TSeries
        TSeries batchSeries = Fisher04.Batch(source, period);

        // 3. Batch Span
        var spanOutput = new double[source.Count];
        Fisher04.Batch(source.Values, spanOutput, period);

        // 4. Event-based
        var eventSource = new TSeries();
        var eventIndicator = new Fisher04(eventSource, period);
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
        var ex = Assert.Throws<ArgumentException>(() => Fisher04.Batch(src, output, 5));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidPeriod_Throws()
    {
        var src = new double[10];
        var output = new double[10];
        var ex = Assert.Throws<ArgumentException>(() => Fisher04.Batch(src, output, 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_Empty_NoException()
    {
        var src = ReadOnlySpan<double>.Empty;
        var output = Span<double>.Empty;
        Fisher04.Batch(src, output, 5);
        Assert.True(true);
    }

    [Fact]
    public void Batch_Span_MatchesTSeries()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        TSeries batchSeries = Fisher04.Batch(source, 10);

        var spanOutput = new double[source.Count];
        Fisher04.Batch(source.Values, spanOutput, 10);

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
        Fisher04.Batch(src, output, 5);

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
        var fisher = new Fisher04(source, period: 5);
        int count = 0;
        fisher.Pub += (object? _, in TValueEventArgs _) => count++;

        source.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(1, count);
    }

    [Fact]
    public void Event_ChainingWorks()
    {
        var source = new TSeries();
        var fisher = new Fisher04(source, period: 5);

        for (int i = 0; i < 20; i++)
        {
            source.Add(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        Assert.True(fisher.IsHot);
        Assert.True(double.IsFinite(fisher.Last.Value));
    }

    // ───── Domain-specific tests ─────

    [Fact]
    public void Fisher04_DifferentFromFisher2002()
    {
        // Fisher04 uses different coefficients (0.25 arctanh mult vs 0.5)
        // so results MUST differ from Fisher (2002)
        int period = 10;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        var fisher02 = new Fisher(period);
        var fisher04 = new Fisher04(period);

        double last02 = 0, last04 = 0;
        for (int i = 0; i < source.Count; i++)
        {
            last02 = fisher02.Update(source[i]).Value;
            last04 = fisher04.Update(source[i]).Value;
        }

        Assert.NotEqual(last02, last04, 1e-3);
    }

    [Fact]
    public void Fisher04_SmallerAmplitudeThanFisher2002()
    {
        // The 0.25 multiplier (vs 0.5) means Fisher04 should generally
        // produce smaller absolute values than Fisher 2002
        int period = 10;
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        var fisher02 = new Fisher(period);
        var fisher04 = new Fisher04(period);

        double sum02 = 0, sum04 = 0;
        for (int i = 0; i < source.Count; i++)
        {
            sum02 += Math.Abs(fisher02.Update(source[i]).Value);
            sum04 += Math.Abs(fisher04.Update(source[i]).Value);
        }

        Assert.True(sum04 < sum02,
            $"Fisher04 avg abs ({sum04 / source.Count:F4}) should be smaller than Fisher ({sum02 / source.Count:F4})");
    }

    [Fact]
    public void FisherTransform_MathematicalProperties()
    {
        // Fisher Transform is arctanh: should be odd function
        // For normalized input 0, Fisher should be 0
        var fisher = new Fisher04(period: 5);

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
        // Fisher can exceed ±2 with strong trends (though Fisher04 is gentler)
        var fisher = new Fisher04(period: 5);

        // Create a very strong uptrend
        for (int i = 0; i < 30; i++)
        {
            fisher.Update(new TValue(DateTime.UtcNow, 100.0 + (i * 10)));
        }

        // Fisher04 should be positive for uptrend
        Assert.True(fisher.FisherValue > 0.5,
            $"Strong uptrend should produce Fisher04 > 0.5, got {fisher.FisherValue}");
    }

    [Fact]
    public void Signal_LagsFisher()
    {
        // Signal is Fish[1], so under strong trend it should lag
        var fisher = new Fisher04(period: 5);

        for (int i = 0; i < 30; i++)
        {
            fisher.Update(new TValue(DateTime.UtcNow, 100.0 + (i * 5)));
        }

        // Both should be positive in uptrend
        Assert.True(fisher.FisherValue > 0);
        Assert.True(fisher.Signal > 0);
    }

    [Fact]
    public void ManualCalculation_MatchesExpected()
    {
        // Verify the 2004 algorithm coefficients against manual computation
        var fisher = new Fisher04(period: 3);

        // Feed 3 values to fill the buffer
        fisher.Update(new TValue(DateTime.UtcNow, 10.0), isNew: true);
        fisher.Update(new TValue(DateTime.UtcNow, 12.0), isNew: true);
        fisher.Update(new TValue(DateTime.UtcNow, 11.0), isNew: true);

        // Manual: buffer = [10, 12, 11], min=10, max=12, range=2
        // norm = (11-10)/2 - 0.5 = 0.5 - 0.5 = 0.0
        // But we have IIR from previous bars...
        // Bar 0: val=10, min=max=10, range=0 → Value1=0, Fish=0
        // Bar 1: val=12, min=10,max=12,range=2, norm=(12-10)/2-0.5=0.5
        //   Value1 = 0.5 + 0.5*0 = 0.5
        //   Fish = 0.25*ln((1.5)/(0.5)) + 0.5*0 = 0.25*ln(3) = 0.25*1.0986... = 0.27465...
        // Bar 2: val=11, min=10,max=12,range=2, norm=(11-10)/2-0.5=0.0
        //   Value1 = 0.0 + 0.5*0.5 = 0.25
        //   Fish = 0.25*ln(1.25/0.75) + 0.5*0.27465... = 0.25*ln(1.6667) + 0.13733...
        //        = 0.25*0.51083... + 0.13733... = 0.12771... + 0.13733... = 0.26504...

        double expectedBar1Fish = 0.25 * Math.Log(1.5 / 0.5);
        double expectedBar2Value1 = 0.25;
        double expectedBar2Fish = (0.25 * Math.Log((1.0 + expectedBar2Value1) / (1.0 - expectedBar2Value1)))
            + (0.5 * expectedBar1Fish);

        Assert.Equal(expectedBar2Fish, fisher.FisherValue, 1e-10);
    }
}
