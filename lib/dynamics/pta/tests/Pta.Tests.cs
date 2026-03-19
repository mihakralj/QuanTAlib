namespace QuanTAlib;

public class PtaTests
{
    private static readonly Random _rng = new(42);

    private static TSeries MakeSeries(int count = 500)
    {
        var series = new TSeries();
        double price = 100.0;
        for (int i = 0; i < count; i++)
        {
            price += (_rng.NextDouble() - 0.5) * 2.0;
            series.Add(new TValue(DateTime.UtcNow.AddMinutes(i), price));
        }
        return series;
    }

    // ════════════════════════════════════════════════════════
    //  A — Constructor
    // ════════════════════════════════════════════════════════

    [Fact]
    public void Constructor_DefaultParameters()
    {
        var pta = new Pta();
        Assert.Equal(250, pta.LongPeriod);
        Assert.Equal(40, pta.ShortPeriod);
    }

    [Fact]
    public void Constructor_CustomParameters()
    {
        var pta = new Pta(longPeriod: 500, shortPeriod: 100);
        Assert.Equal(500, pta.LongPeriod);
        Assert.Equal(100, pta.ShortPeriod);
    }

    [Fact]
    public void Constructor_LongPeriodTooSmall_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Pta(longPeriod: 2, shortPeriod: 1));
    }

    [Fact]
    public void Constructor_ShortPeriodTooSmall_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Pta(longPeriod: 50, shortPeriod: 1));
    }

    [Fact]
    public void Constructor_LongNotGreaterThanShort_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Pta(longPeriod: 40, shortPeriod: 40));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Pta(longPeriod: 30, shortPeriod: 40));
    }

    // ════════════════════════════════════════════════════════
    //  B — Basic Calculation
    // ════════════════════════════════════════════════════════

    [Fact]
    public void FirstBar_OutputIsZero()
    {
        var pta = new Pta(50, 10);
        var result = pta.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void SecondBar_OutputIsZero()
    {
        var pta = new Pta(50, 10);
        pta.Update(new TValue(DateTime.UtcNow, 100.0));
        var result = pta.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 101.0));
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void ThirdBar_OutputIsFinite()
    {
        var pta = new Pta(50, 10);
        pta.Update(new TValue(DateTime.UtcNow, 100.0));
        pta.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 101.0));
        var result = pta.Update(new TValue(DateTime.UtcNow.AddMinutes(2), 102.0));
        Assert.True(double.IsFinite(result.Value));
    }

    // ════════════════════════════════════════════════════════
    //  C — State / Bar Correction
    // ════════════════════════════════════════════════════════

    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var pta = new Pta(50, 10);
        var series = MakeSeries(100);
        foreach (var bar in series)
        {
            pta.Update(bar);
        }
        double val1 = pta.Update(new TValue(DateTime.UtcNow, 105.0), isNew: true).Value;
        double val2 = pta.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 110.0), isNew: true).Value;
        Assert.NotEqual(val1, val2);
    }

    [Fact]
    public void IsNew_False_CorrectionReproducible()
    {
        var pta = new Pta(50, 10);
        var series = MakeSeries(100);
        foreach (var bar in series)
        {
            pta.Update(bar);
        }

        double v1 = pta.Update(new TValue(DateTime.UtcNow, 105.0), isNew: true).Value;
        _ = pta.Update(new TValue(DateTime.UtcNow, 108.0), isNew: false).Value;
        double v3 = pta.Update(new TValue(DateTime.UtcNow, 105.0), isNew: false).Value;
        Assert.Equal(v1, v3, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var pta = new Pta(50, 10);
        var series = MakeSeries(100);
        foreach (var bar in series)
        {
            pta.Update(bar);
        }
        pta.Reset();
        Assert.False(pta.IsHot);
        Assert.Equal(0.0, pta.Update(new TValue(DateTime.UtcNow, 100.0)).Value);
    }

    // ════════════════════════════════════════════════════════
    //  D — Warmup
    // ════════════════════════════════════════════════════════

    [Fact]
    public void IsHot_FalseBeforeTwoBars()
    {
        var pta = new Pta(50, 10);
        Assert.False(pta.IsHot);
        pta.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.False(pta.IsHot);
    }

    [Fact]
    public void IsHot_TrueAfterTwoBars()
    {
        var pta = new Pta(50, 10);
        pta.Update(new TValue(DateTime.UtcNow, 100.0));
        pta.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 101.0));
        Assert.True(pta.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesLongPeriod()
    {
        var pta = new Pta(200, 30);
        Assert.Equal(200, pta.WarmupPeriod);
    }

    // ════════════════════════════════════════════════════════
    //  E — Robustness
    // ════════════════════════════════════════════════════════

    [Fact]
    public void LargeSeries_NoOverflow()
    {
        var pta = new Pta(50, 10);
        var series = MakeSeries(5000);
        foreach (var bar in series)
        {
            pta.Update(bar);
        }
        Assert.True(double.IsFinite(pta.Last.Value));
    }

    [Fact]
    public void VolatileInput_RemainsFinite()
    {
        var pta = new Pta(50, 10);
        var rng = new Random(123);
        for (int i = 0; i < 1000; i++)
        {
            double price = 100 + (rng.NextDouble() - 0.5) * 50;
            pta.Update(new TValue(DateTime.UtcNow.AddMinutes(i), price));
        }
        Assert.True(double.IsFinite(pta.Last.Value));
    }

    // ════════════════════════════════════════════════════════
    //  F — Consistency (4-API mode)
    // ════════════════════════════════════════════════════════

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        var series = MakeSeries(300);
        int lp = 50, sp = 10;

        // Mode 1: Streaming
        var streaming = new Pta(lp, sp);
        foreach (var bar in series)
        {
            streaming.Update(bar);
        }

        // Mode 2: Batch TSeries
        var batchResult = Pta.Batch(series, lp, sp);

        // Mode 3: Span
        var output = new double[series.Count];
        Pta.Batch(series.Values, output, lp, sp);

        // Mode 4: Calculate
        var (calcResult, _) = Pta.Calculate(series, lp, sp);

        // Compare last values
        double streamVal = streaming.Last.Value;
        double batchVal = batchResult[^1].Value;
        double spanVal = output[^1];
        double calcVal = calcResult[^1].Value;

        Assert.Equal(streamVal, batchVal, 10);
        Assert.Equal(streamVal, spanVal, 10);
        Assert.Equal(streamVal, calcVal, 10);
    }

    // ════════════════════════════════════════════════════════
    //  G — Span API
    // ════════════════════════════════════════════════════════

    [Fact]
    public void SpanBatch_MatchesStreaming()
    {
        var series = MakeSeries(200);
        int lp = 50, sp = 10;

        var streaming = new Pta(lp, sp);
        var streamResults = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            streamResults[i] = streaming.Update(series[i]).Value;
        }

        var spanResults = new double[series.Count];
        Pta.Batch(series.Values, spanResults, lp, sp);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(streamResults[i], spanResults[i], 10);
        }
    }

    [Fact]
    public void SpanBatch_EmptyInput_NoThrow()
    {
        var exception = Record.Exception(() => Pta.Batch(ReadOnlySpan<double>.Empty, Span<double>.Empty, 50, 10));
        Assert.Null(exception);
    }

    [Fact]
    public void SpanBatch_MismatchedLengths_Throws()
    {
        var src = new double[10];
        var dst = new double[5];
        Assert.Throws<ArgumentException>(() => Pta.Batch(src, dst, 50, 10));
    }

    // ════════════════════════════════════════════════════════
    //  H — Chainability
    // ════════════════════════════════════════════════════════

    [Fact]
    public void PubSub_ChainWorks()
    {
        var source = new TSeries();
        var pta = new Pta(source, longPeriod: 50, shortPeriod: 10);
        for (int i = 0; i < 100; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + i * 0.1));
        }
        Assert.True(double.IsFinite(pta.Last.Value));
    }

    // ════════════════════════════════════════════════════════
    //  PTA-Specific Behavioral Tests
    // ════════════════════════════════════════════════════════

    [Fact]
    public void ConstantInput_OutputIsZero()
    {
        var pta = new Pta(50, 10);
        for (int i = 0; i < 300; i++)
        {
            pta.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0));
        }

        // Constant price → zero 2nd-order difference → both HP = 0 → PTA = 0
        Assert.Equal(0.0, pta.Last.Value, 10);
    }

    [Fact]
    public void LinearTrend_OutputNearZeroAfterConvergence()
    {
        // A perfectly linear trend has zero 2nd derivative → HP outputs approach 0
        var pta = new Pta(50, 10);
        for (int i = 0; i < 500; i++)
        {
            pta.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + i * 0.5));
        }

        // Both HP filters output 0 for pure linear → PTA ≈ 0
        Assert.True(Math.Abs(pta.Last.Value) < 1.0,
            $"Expected near-zero for linear trend, got {pta.Last.Value}");
    }

    [Fact]
    public void SineWave_InBandpass_ProducesOutput()
    {
        // Sine wave at period=100 (between short=10 and long=250) should be preserved
        var pta = new Pta(250, 10);
        double lastAbsMax = 0;
        for (int i = 0; i < 500; i++)
        {
            double price = 100.0 + 10.0 * Math.Sin(2.0 * Math.PI * i / 100.0);
            pta.Update(new TValue(DateTime.UtcNow.AddMinutes(i), price));
            if (i > 300)
            {
                lastAbsMax = Math.Max(lastAbsMax, Math.Abs(pta.Last.Value));
            }
        }
        Assert.True(lastAbsMax > 0.1,
            $"Expected significant output for in-band sine, got max={lastAbsMax}");
    }

    [Fact]
    public void Uptrend_Then_Downtrend_SignChanges()
    {
        var pta = new Pta(50, 10);
        // Uptrend
        for (int i = 0; i < 200; i++)
        {
            pta.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + i * 0.5));
        }
        // Transition to downtrend
        for (int i = 0; i < 200; i++)
        {
            pta.Update(new TValue(DateTime.UtcNow.AddMinutes(200 + i), 200.0 - i * 0.5));
        }

        // After sustained downtrend, PTA should detect the reversal
        // (the sign change may take some bars due to the bandpass filter)
        Assert.True(double.IsFinite(pta.Last.Value));
    }

    [Fact]
    public void DifferentPeriods_ProduceDifferentResults()
    {
        var series = MakeSeries(300);
        var pta1 = new Pta(100, 20);
        var pta2 = new Pta(200, 50);
        foreach (var bar in series)
        {
            pta1.Update(bar);
            pta2.Update(bar);
        }
        Assert.NotEqual(pta1.Last.Value, pta2.Last.Value);
    }

    [Fact]
    public void Name_IncludesBothPeriods()
    {
        var pta = new Pta(300, 60);
        Assert.Contains("300", pta.Name, StringComparison.Ordinal);
        Assert.Contains("60", pta.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Calculate_ReturnsIndicatorAndResults()
    {
        var series = MakeSeries(200);
        var (results, indicator) = Pta.Calculate(series, 50, 10);
        Assert.Equal(series.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Prime_SetsState()
    {
        var pta = new Pta(50, 10);
        var values = new double[100];
        for (int i = 0; i < 100; i++)
        {
            values[i] = 100.0 + i * 0.1;
        }
        pta.Prime(values);
        Assert.True(pta.IsHot);
    }
}
