using Xunit;

namespace QuanTAlib.Tests;

public class CcorTests
{
    private static readonly GBM TestData = new(startPrice: 100, mu: 0.05, sigma: 0.5, seed: 42);

    private static TSeries GetTestSeries(int count = 500)
    {
        return TestData.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
    }

    // ── A) Constructor validation ──

    [Fact]
    public void Ccor_DefaultConstructor_SetsDefaults()
    {
        var ind = new Ccor();
        Assert.Equal("Ccor(20,9.0)", ind.Name);
        Assert.Equal(20, ind.WarmupPeriod);
    }

    [Fact]
    public void Ccor_CustomPeriod_SetsCorrectName()
    {
        var ind = new Ccor(period: 30, threshold: 5.0);
        Assert.Equal("Ccor(30,5.0)", ind.Name);
        Assert.Equal(30, ind.WarmupPeriod);
    }

    [Fact]
    public void Ccor_ZeroPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Ccor(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Ccor_NegativePeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Ccor(period: -5));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Ccor_ZeroThreshold_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Ccor(period: 20, threshold: 0.0));
        Assert.Equal("threshold", ex.ParamName);
    }

    [Fact]
    public void Ccor_NegativeThreshold_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Ccor(period: 20, threshold: -1.0));
        Assert.Equal("threshold", ex.ParamName);
    }

    [Fact]
    public void Ccor_ChainConstructor_NullSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Ccor(null!, 20, 9.0));
    }

    // ── B) Basic calculation ──

    [Fact]
    public void Ccor_Update_ReturnsTValue()
    {
        var ind = new Ccor();
        var result = ind.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Ccor_AfterUpdate_LastIsAccessible()
    {
        var ind = new Ccor();
        _ = ind.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(ind.Last.Value));
        Assert.Equal("Ccor(20,9.0)", ind.Name);
    }

    [Fact]
    public void Ccor_MultiOutput_AllAccessible()
    {
        var ind = new Ccor();
        var series = GetTestSeries(50);
        foreach (var tv in series)
        {
            _ = ind.Update(tv);
        }

        // All multi-output properties should be accessible and finite
        Assert.True(double.IsFinite(ind.Real));
        Assert.True(double.IsFinite(ind.Imag));
        Assert.True(double.IsFinite(ind.Angle));
        Assert.Contains(ind.MarketState, new[] { -1, 0, 1 });
    }

    [Fact]
    public void Ccor_Real_BoundedMinusOneToOne()
    {
        var ind = new Ccor();
        var series = GetTestSeries(200);
        foreach (var tv in series)
        {
            _ = ind.Update(tv);
            Assert.InRange(ind.Real, -1.0, 1.0);
        }
    }

    [Fact]
    public void Ccor_Imag_BoundedMinusOneToOne()
    {
        var ind = new Ccor();
        var series = GetTestSeries(200);
        foreach (var tv in series)
        {
            _ = ind.Update(tv);
            Assert.InRange(ind.Imag, -1.0, 1.0);
        }
    }

    // ── C) State + bar correction ──

    [Fact]
    public void Ccor_IsNew_True_AdvancesState()
    {
        var ind = new Ccor(period: 10);
        var series = GetTestSeries(20);
        foreach (var tv in series)
        {
            _ = ind.Update(tv, isNew: true);
        }
        Assert.True(ind.IsHot);
    }

    [Fact]
    public void Ccor_IsNew_False_DoesNotAdvance()
    {
        var ind = new Ccor(period: 10);
        var series = GetTestSeries(5);

        // Process 5 bars normally
        foreach (var tv in series)
        {
            _ = ind.Update(tv, isNew: true);
        }

        // Rewrite last bar with same value — should produce same result each time
        _ = ind.Update(series[^1], isNew: false);
        double realAfterFirst = ind.Real;

        _ = ind.Update(series[^1], isNew: false);
        double realAfterSecond = ind.Real;

        Assert.Equal(realAfterFirst, realAfterSecond, 10);
    }

    [Fact]
    public void Ccor_BarCorrection_IterativeUpdatesRestore()
    {
        var ind = new Ccor(period: 10);
        var series = GetTestSeries(30);

        // Process first 25 bars
        for (int i = 0; i < 25; i++)
        {
            _ = ind.Update(series[i]);
        }

        double realSnapshot = ind.Real;

        // Apply 5 corrections (isNew=false)
        for (int i = 0; i < 5; i++)
        {
            _ = ind.Update(new TValue(series[24].Time, 100.0 + i), isNew: false);
        }

        // Reapply original — should restore
        _ = ind.Update(series[24], isNew: false);
        Assert.Equal(realSnapshot, ind.Real, 10);
    }

    [Fact]
    public void Ccor_Reset_ClearsState()
    {
        var ind = new Ccor();
        var series = GetTestSeries(50);
        foreach (var tv in series)
        {
            _ = ind.Update(tv);
        }

        Assert.True(ind.IsHot);

        ind.Reset();

        Assert.False(ind.IsHot);
        Assert.Equal(0.0, ind.Real);
        Assert.Equal(0.0, ind.Imag);
        Assert.Equal(0.0, ind.Angle);
        Assert.Equal(0, ind.MarketState);
        Assert.Equal(default, ind.Last);
    }

    // ── D) Warmup/convergence ──

    [Fact]
    public void Ccor_IsHot_FlipsAtWarmupPeriod()
    {
        int period = 15;
        var ind = new Ccor(period: period);
        var series = GetTestSeries(period + 5);

        for (int i = 0; i < period - 1; i++)
        {
            _ = ind.Update(series[i]);
            Assert.False(ind.IsHot, $"Should not be hot at bar {i + 1}");
        }

        _ = ind.Update(series[period - 1]);
        Assert.True(ind.IsHot, $"Should be hot at bar {period}");
    }

    [Fact]
    public void Ccor_WarmupPeriod_EqualsPeriod()
    {
        var ind = new Ccor(period: 30);
        Assert.Equal(30, ind.WarmupPeriod);
    }

    // ── E) Robustness ──

    [Fact]
    public void Ccor_NaN_UsesLastValid()
    {
        var ind = new Ccor(period: 5);
        var series = GetTestSeries(10);

        for (int i = 0; i < 8; i++)
        {
            _ = ind.Update(series[i]);
        }

        _ = ind.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(ind.Real));
    }

    [Fact]
    public void Ccor_Infinity_UsesLastValid()
    {
        var ind = new Ccor(period: 5);
        var series = GetTestSeries(10);

        for (int i = 0; i < 8; i++)
        {
            _ = ind.Update(series[i]);
        }

        _ = ind.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(ind.Real));
        Assert.True(double.IsFinite(ind.Imag));
    }

    [Fact]
    public void Ccor_BatchNaN_AllFinite()
    {
        var series = GetTestSeries(50);
        var ind = new Ccor(period: 10);

        foreach (var tv in series)
        {
            _ = ind.Update(tv);
        }

        // Inject NaN batch
        for (int i = 0; i < 5; i++)
        {
            _ = ind.Update(new TValue(DateTime.UtcNow, double.NaN));
        }

        Assert.True(double.IsFinite(ind.Real));
        Assert.True(double.IsFinite(ind.Imag));
        Assert.True(double.IsFinite(ind.Angle));
    }

    [Fact]
    public void Ccor_EmptyTSeries_ReturnsEmpty()
    {
        var ind = new Ccor();
        var result = ind.Update(new TSeries());
        Assert.Empty(result);
    }

    [Fact]
    public void Ccor_LargeDataset_NoBlowup()
    {
        var largeData = TestData.Fetch(10000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        var ind = new Ccor();

        for (int i = 0; i < largeData.Count; i++)
        {
            _ = ind.Update(largeData[i]);
            Assert.True(double.IsFinite(ind.Real), $"Non-finite Real at index {i}");
            Assert.True(double.IsFinite(ind.Imag), $"Non-finite Imag at index {i}");
        }
    }

    // ── F) Consistency (4 API modes match) ──

    [Fact]
    public void Ccor_FourApiModes_Match()
    {
        var series = GetTestSeries(100);
        int period = 20;
        double threshold = 9.0;

        // Mode 1: Streaming
        var ind1 = new Ccor(period, threshold);
        var streaming = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            streaming[i] = ind1.Update(series[i]).Value;
        }

        // Mode 2: Batch(TSeries)
        var batchResult = Ccor.Batch(series, period, threshold);

        // Mode 3: Batch(Span)
        double[] srcVals = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            srcVals[i] = series[i].Value;
        }
        double[] spanResult = new double[series.Count];
        Ccor.Batch(srcVals, spanResult, period, threshold);

        // Mode 4: Eventing
        var ind4 = new Ccor(period, threshold);
        var eventResults = new List<double>();
        ind4.Pub += (object? _, in TValueEventArgs e) => eventResults.Add(e.Value.Value);
        foreach (var tv in series)
        {
            _ = ind4.Update(tv);
        }

        // Compare all modes
        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(streaming[i], batchResult[i].Value, 10);
            Assert.Equal(streaming[i], spanResult[i], 10);
            Assert.Equal(streaming[i], eventResults[i], 10);
        }
    }

    // ── G) Span API tests ──

    [Fact]
    public void Ccor_SpanBatch_MismatchedLengths_Throws()
    {
        double[] src = new double[10];
        double[] output = new double[5];
        var ex = Assert.Throws<ArgumentException>(() => Ccor.Batch(src, output));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Ccor_SpanBatch_ZeroPeriod_Throws()
    {
        double[] src = new double[10];
        double[] output = new double[10];
        var ex = Assert.Throws<ArgumentException>(() => Ccor.Batch(src, output, period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Ccor_SpanBatch_ZeroThreshold_Throws()
    {
        double[] src = new double[10];
        double[] output = new double[10];
        var ex = Assert.Throws<ArgumentException>(() => Ccor.Batch(src, output, period: 20, threshold: 0.0));
        Assert.Equal("threshold", ex.ParamName);
    }

    [Fact]
    public void Ccor_SpanBatch_Empty_NoException()
    {
        double[] src = Array.Empty<double>();
        double[] output = Array.Empty<double>();
        Ccor.Batch(src, output); // should not throw
        Assert.Empty(output);
    }

    [Fact]
    public void Ccor_SpanBatch_MatchesTSeries()
    {
        var series = GetTestSeries(100);
        int period = 15;

        var batchResult = Ccor.Batch(series, period);

        double[] srcVals = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            srcVals[i] = series[i].Value;
        }
        double[] spanResult = new double[series.Count];
        Ccor.Batch(srcVals, spanResult, period);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, spanResult[i], 10);
        }
    }

    [Fact]
    public void Ccor_SpanBatch_NaN_Handled()
    {
        double[] src = { 100, 101, double.NaN, 103, 104, 105, 106, 107, 108, 109 };
        double[] output = new double[10];
        Ccor.Batch(src, output, period: 5);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Non-finite at index {i}");
        }
    }

    // ── H) Chainability ──

    [Fact]
    public void Ccor_PubEvent_Fires()
    {
        var ind = new Ccor();
        int count = 0;
        ind.Pub += (object? _, in TValueEventArgs _) => count++;

        var series = GetTestSeries(10);
        foreach (var tv in series)
        {
            _ = ind.Update(tv);
        }

        Assert.Equal(10, count);
    }

    [Fact]
    public void Ccor_EventChaining_Works()
    {
        var source = new Ccor(period: 10);
        var chained = new Ccor(source, period: 5);

        var series = GetTestSeries(50);
        foreach (var tv in series)
        {
            _ = source.Update(tv);
        }

        Assert.True(chained.IsHot);
        Assert.True(double.IsFinite(chained.Real));
    }

    // ── CCOR-specific tests ──

    [Fact]
    public void Ccor_ConstantInput_RealIsZero()
    {
        var ind = new Ccor(period: 10);
        for (int i = 0; i < 30; i++)
        {
            _ = ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0));
        }

        // Constant price → zero variance in x → correlation = 0
        Assert.Equal(0.0, ind.Real, 10);
        Assert.Equal(0.0, ind.Imag, 10);
    }

    [Fact]
    public void Ccor_SineWave_DetectsCorrelation()
    {
        int period = 20;
        var ind = new Ccor(period: period);

        // Feed a perfect sine wave of the same period
        for (int i = 0; i < 100; i++)
        {
            double val = 100.0 + (10.0 * Math.Sin(2.0 * Math.PI * i / period));
            _ = ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), val));
        }

        // After warmup, Real correlation with cosine should be significant (not zero)
        double absReal = Math.Abs(ind.Real);
        double absImag = Math.Abs(ind.Imag);
        Assert.True(absReal > 0.1 || absImag > 0.1,
            $"Sine wave should produce non-trivial correlation: Real={ind.Real:F4}, Imag={ind.Imag:F4}");
    }

    [Fact]
    public void Ccor_AngleMonotonic_NeverDecreases()
    {
        var ind = new Ccor(period: 15);
        var series = GetTestSeries(200);
        double prevAngle = double.MinValue;

        foreach (var tv in series)
        {
            _ = ind.Update(tv);
            Assert.True(ind.Angle >= prevAngle,
                $"Angle decreased: {ind.Angle:F4} < prev {prevAngle:F4}");
            prevAngle = ind.Angle;
        }
    }

    [Fact]
    public void Ccor_MarketState_OnlyValidValues()
    {
        var ind = new Ccor();
        var series = GetTestSeries(200);

        foreach (var tv in series)
        {
            _ = ind.Update(tv);
            Assert.Contains(ind.MarketState, new[] { -1, 0, 1 });
        }
    }

    [Fact]
    public void Ccor_DifferentPeriods_ProduceDifferentResults()
    {
        var series = GetTestSeries(100);
        var ind10 = new Ccor(period: 10);
        var ind30 = new Ccor(period: 30);

        foreach (var tv in series)
        {
            _ = ind10.Update(tv);
            _ = ind30.Update(tv);
        }

        // Different periods should produce different Real values
        Assert.NotEqual(ind10.Real, ind30.Real, 5);
    }

    [Fact]
    public void Ccor_Prime_SetsState()
    {
        var ind = new Ccor(period: 10);
        var series = GetTestSeries(20);
        double[] vals = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            vals[i] = series[i].Value;
        }

        ind.Prime(vals);
        Assert.True(ind.IsHot);
        Assert.True(double.IsFinite(ind.Real));
    }

    [Fact]
    public void Ccor_Calculate_ReturnsBothResultsAndIndicator()
    {
        var series = GetTestSeries(50);
        var (results, indicator) = Ccor.Calculate(series);

        Assert.Equal(50, results.Count);
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(indicator.Real));
        Assert.True(double.IsFinite(indicator.Imag));
    }

    [Fact]
    public void Ccor_Batch_TSeries_CorrectLength()
    {
        var series = GetTestSeries(100);
        var result = Ccor.Batch(series);
        Assert.Equal(100, result.Count);
    }

    [Fact]
    public void Ccor_Update_TSeries_CorrectLength()
    {
        var ind = new Ccor();
        var series = GetTestSeries(100);
        var result = ind.Update(series);
        Assert.Equal(100, result.Count);
    }
}
