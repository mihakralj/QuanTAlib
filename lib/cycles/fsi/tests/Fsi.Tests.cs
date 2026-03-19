namespace QuanTAlib;

public class FsiTests
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
        var fsi = new Fsi();
        Assert.Equal(20, fsi.Period);
        Assert.Equal(0.1, fsi.Bandwidth, 10);
    }

    [Fact]
    public void Constructor_CustomParameters()
    {
        var fsi = new Fsi(period: 40, bandwidth: 0.2);
        Assert.Equal(40, fsi.Period);
        Assert.Equal(0.2, fsi.Bandwidth, 10);
    }

    [Fact]
    public void Constructor_PeriodTooSmall_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Fsi(period: 5));
    }

    [Fact]
    public void Constructor_BandwidthTooSmall_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Fsi(period: 20, bandwidth: 0.0001));
    }

    // ════════════════════════════════════════════════════════
    //  B — Basic Calculation
    // ════════════════════════════════════════════════════════

    [Fact]
    public void FirstBar_OutputIsZero()
    {
        var fsi = new Fsi(20, 0.1);
        var result = fsi.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void SecondBar_OutputIsZero()
    {
        var fsi = new Fsi(20, 0.1);
        fsi.Update(new TValue(DateTime.UtcNow, 100.0));
        var result = fsi.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 101.0));
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void ThirdBar_OutputIsFinite()
    {
        var fsi = new Fsi(20, 0.1);
        fsi.Update(new TValue(DateTime.UtcNow, 100.0));
        fsi.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 101.0));
        var result = fsi.Update(new TValue(DateTime.UtcNow.AddMinutes(2), 102.0));
        Assert.True(double.IsFinite(result.Value));
    }

    // ════════════════════════════════════════════════════════
    //  C — State / Bar Correction
    // ════════════════════════════════════════════════════════

    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var fsi = new Fsi(20, 0.1);
        var series = MakeSeries(100);
        foreach (var bar in series)
        {
            fsi.Update(bar);
        }
        double val1 = fsi.Update(new TValue(DateTime.UtcNow, 105.0), isNew: true).Value;
        double val2 = fsi.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 110.0), isNew: true).Value;
        Assert.NotEqual(val1, val2);
    }

    [Fact]
    public void IsNew_False_CorrectionReproducible()
    {
        var fsi = new Fsi(20, 0.1);
        var series = MakeSeries(100);
        foreach (var bar in series)
        {
            fsi.Update(bar);
        }

        double v1 = fsi.Update(new TValue(DateTime.UtcNow, 105.0), isNew: true).Value;
        _ = fsi.Update(new TValue(DateTime.UtcNow, 108.0), isNew: false).Value;
        double v3 = fsi.Update(new TValue(DateTime.UtcNow, 105.0), isNew: false).Value;
        Assert.Equal(v1, v3, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var fsi = new Fsi(20, 0.1);
        var series = MakeSeries(100);
        foreach (var bar in series)
        {
            fsi.Update(bar);
        }
        fsi.Reset();
        Assert.False(fsi.IsHot);
        Assert.Equal(0.0, fsi.Update(new TValue(DateTime.UtcNow, 100.0)).Value);
    }

    // ════════════════════════════════════════════════════════
    //  D — Warmup
    // ════════════════════════════════════════════════════════

    [Fact]
    public void IsHot_FalseBeforePeriodBars()
    {
        var fsi = new Fsi(20, 0.1);
        Assert.False(fsi.IsHot);
        fsi.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.False(fsi.IsHot);
    }

    [Fact]
    public void IsHot_TrueAfterPeriodBars()
    {
        var fsi = new Fsi(20, 0.1);
        for (int i = 0; i < 20; i++)
        {
            fsi.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + i));
        }
        Assert.True(fsi.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesPeriod()
    {
        var fsi = new Fsi(30, 0.2);
        Assert.Equal(30, fsi.WarmupPeriod);
    }

    // ════════════════════════════════════════════════════════
    //  E — Robustness
    // ════════════════════════════════════════════════════════

    [Fact]
    public void LargeSeries_NoOverflow()
    {
        var fsi = new Fsi(20, 0.1);
        var series = MakeSeries(5000);
        foreach (var bar in series)
        {
            fsi.Update(bar);
        }
        Assert.True(double.IsFinite(fsi.Last.Value));
    }

    [Fact]
    public void VolatileInput_RemainsFinite()
    {
        var fsi = new Fsi(20, 0.1);
        var rng = new Random(123);
        for (int i = 0; i < 1000; i++)
        {
            double price = 100 + (rng.NextDouble() - 0.5) * 50;
            fsi.Update(new TValue(DateTime.UtcNow.AddMinutes(i), price));
        }
        Assert.True(double.IsFinite(fsi.Last.Value));
    }

    [Fact]
    public void NaN_Input_Handled()
    {
        var fsi = new Fsi(20, 0.1);
        for (int i = 0; i < 30; i++)
        {
            fsi.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + i));
        }
        var result = fsi.Update(new TValue(DateTime.UtcNow.AddMinutes(30), double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    // ════════════════════════════════════════════════════════
    //  F — Consistency (4-API mode)
    // ════════════════════════════════════════════════════════

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        var series = MakeSeries(300);
        int p = 20;
        double bw = 0.1;

        // Mode 1: Streaming
        var streaming = new Fsi(p, bw);
        foreach (var bar in series)
        {
            streaming.Update(bar);
        }

        // Mode 2: Batch TSeries
        var batchResult = Fsi.Batch(series, p, bw);

        // Mode 3: Span
        var output = new double[series.Count];
        Fsi.Batch(series.Values, output, p, bw);

        // Mode 4: Calculate
        var (calcResult, _) = Fsi.Calculate(series, p, bw);

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
        int p = 20;
        double bw = 0.1;

        var streaming = new Fsi(p, bw);
        var streamResults = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            streamResults[i] = streaming.Update(series[i]).Value;
        }

        var spanResults = new double[series.Count];
        Fsi.Batch(series.Values, spanResults, p, bw);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(streamResults[i], spanResults[i], 10);
        }
    }

    [Fact]
    public void SpanBatch_EmptyInput_NoThrow()
    {
        var exception = Record.Exception(() => Fsi.Batch(ReadOnlySpan<double>.Empty, Span<double>.Empty, 20, 0.1));
        Assert.Null(exception);
    }

    [Fact]
    public void SpanBatch_MismatchedLengths_Throws()
    {
        var src = new double[10];
        var dst = new double[5];
        Assert.Throws<ArgumentException>(() => Fsi.Batch(src, dst, 20, 0.1));
    }

    // ════════════════════════════════════════════════════════
    //  H — Chainability
    // ════════════════════════════════════════════════════════

    [Fact]
    public void PubSub_ChainWorks()
    {
        var source = new TSeries();
        var fsi = new Fsi(source, period: 20, bandwidth: 0.1);
        for (int i = 0; i < 100; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + i * 0.1));
        }
        Assert.True(double.IsFinite(fsi.Last.Value));
    }

    // ════════════════════════════════════════════════════════
    //  FSI-Specific Behavioral Tests
    // ════════════════════════════════════════════════════════

    [Fact]
    public void ConstantInput_OutputIsZero()
    {
        var fsi = new Fsi(20, 0.1);
        for (int i = 0; i < 300; i++)
        {
            fsi.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0));
        }
        // Constant price → zero 2nd-order difference → BP = 0 → FSI = 0
        Assert.Equal(0.0, fsi.Last.Value, 10);
    }

    [Fact]
    public void SineWave_AtFundamental_ProducesOutput()
    {
        // Sine wave at period=20 (the fundamental) should produce significant output
        var fsi = new Fsi(20, 0.3);
        double lastAbsMax = 0;
        for (int i = 0; i < 200; i++)
        {
            double price = 100.0 + 10.0 * Math.Sin(2.0 * Math.PI * i / 20.0);
            fsi.Update(new TValue(DateTime.UtcNow.AddMinutes(i), price));
            if (i > 100)
            {
                lastAbsMax = Math.Max(lastAbsMax, Math.Abs(fsi.Last.Value));
            }
        }
        Assert.True(lastAbsMax > 0.1,
            $"Expected significant output for fundamental sine, got max={lastAbsMax}");
    }

    [Fact]
    public void SineWave_With2ndHarmonic_IncludesBoth()
    {
        // Composite sine with fundamental + 2nd harmonic
        var fsi = new Fsi(20, 0.3);
        double lastAbsMax = 0;
        for (int i = 0; i < 300; i++)
        {
            double price = 100.0 + 5.0 * Math.Sin(2.0 * Math.PI * i / 20.0)
                                 + 3.0 * Math.Sin(2.0 * Math.PI * i / 10.0);
            fsi.Update(new TValue(DateTime.UtcNow.AddMinutes(i), price));
            if (i > 150)
            {
                lastAbsMax = Math.Max(lastAbsMax, Math.Abs(fsi.Last.Value));
            }
        }
        Assert.True(lastAbsMax > 0.1,
            $"Expected output for composite sine, got max={lastAbsMax}");
    }

    [Fact]
    public void DifferentPeriods_ProduceDifferentResults()
    {
        var series = MakeSeries(300);
        var fsi1 = new Fsi(20, 0.1);
        var fsi2 = new Fsi(40, 0.1);
        foreach (var bar in series)
        {
            fsi1.Update(bar);
            fsi2.Update(bar);
        }
        Assert.NotEqual(fsi1.Last.Value, fsi2.Last.Value);
    }

    [Fact]
    public void DifferentBandwidths_ProduceDifferentResults()
    {
        var series = MakeSeries(300);
        var fsi1 = new Fsi(20, 0.1);
        var fsi2 = new Fsi(20, 0.5);
        foreach (var bar in series)
        {
            fsi1.Update(bar);
            fsi2.Update(bar);
        }
        Assert.NotEqual(fsi1.Last.Value, fsi2.Last.Value);
    }

    [Fact]
    public void Name_IncludesPeriodAndBandwidth()
    {
        var fsi = new Fsi(30, 0.25);
        Assert.Contains("30", fsi.Name, StringComparison.Ordinal);
        Assert.Contains("0.25", fsi.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Calculate_ReturnsIndicatorAndResults()
    {
        var series = MakeSeries(200);
        var (results, indicator) = Fsi.Calculate(series, 20, 0.1);
        Assert.Equal(series.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Prime_SetsState()
    {
        var fsi = new Fsi(20, 0.1);
        var values = new double[100];
        for (int i = 0; i < 100; i++)
        {
            values[i] = 100.0 + i * 0.1;
        }
        fsi.Prime(values);
        Assert.True(fsi.IsHot);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public void VariousPeriods_AllFinite(int period)
    {
        var fsi = new Fsi(period, 0.1);
        var series = MakeSeries(500);
        foreach (var bar in series)
        {
            fsi.Update(bar);
        }
        Assert.True(double.IsFinite(fsi.Last.Value));
    }
}
