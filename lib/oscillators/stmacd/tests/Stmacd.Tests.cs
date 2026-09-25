namespace QuanTAlib;

public class StmacdTests
{
    private const int DefaultPeriods = 45;
    private const int DefaultFast = 12;
    private const int DefaultSlow = 26;
    private const int DefaultSignal = 9;

    private static TBarSeries MakeBars(int count = 500)
    {
        var gbm = new GBM(mu: 0.05, sigma: 0.2);
        return gbm.Fetch(count, DateTime.UtcNow.AddDays(-count).Ticks,
            TimeSpan.FromDays(1));
    }

    private static TBarSeries MakeConstantBars(int count = 100, double value = 50)
    {
        var bars = new TBarSeries();
        var t = DateTime.UtcNow.AddDays(-count);
        for (int i = 0; i < count; i++)
        {
            bars.Add(new TBar(t.AddDays(i), value, value, value, value, 1000));
        }
        return bars;
    }

    private static TBarSeries MakeUptrendBars(int count = 100)
    {
        var bars = new TBarSeries();
        var t = DateTime.UtcNow.AddDays(-count);
        for (int i = 0; i < count; i++)
        {
            double c = 50 + i * 0.5;
            bars.Add(new TBar(t.AddDays(i), c + 1, c + 2, c - 1, c, 1000));
        }
        return bars;
    }

    private static TBarSeries MakeDowntrendBars(int count = 100)
    {
        var bars = new TBarSeries();
        var t = DateTime.UtcNow.AddDays(-count);
        for (int i = 0; i < count; i++)
        {
            double c = 150 - i * 0.5;
            bars.Add(new TBar(t.AddDays(i), c + 1, c + 2, c - 1, c, 1000));
        }
        return bars;
    }

    // ── Constructor ───────────────────────────────────────────

    [Fact]
    public void Ctor_Default_SetsCorrectName()
    {
        var ind = new Stmacd();
        Assert.Equal("STMACD(45,12,26,9)", ind.Name);
    }

    [Fact]
    public void Ctor_Custom_SetsCorrectName()
    {
        var ind = new Stmacd(30, 8, 20, 5);
        Assert.Equal("STMACD(30,8,20,5)", ind.Name);
    }

    [Fact]
    public void Ctor_ZeroPeriods_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Stmacd(0));
    }

    [Fact]
    public void Ctor_NegativeFast_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Stmacd(45, -1));
    }

    [Fact]
    public void Ctor_NegativeSignal_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Stmacd(45, 12, 26, 0));
    }

    // ── Warmup ────────────────────────────────────────────────

    [Fact]
    public void WarmupPeriod_EqualsPeriods()
    {
        var ind = new Stmacd(30, 8, 20, 5);
        Assert.Equal(30, ind.WarmupPeriod);
    }

    [Fact]
    public void IsHot_FalseBeforeWarmup()
    {
        var ind = new Stmacd(10, 5, 10, 3);
        var bars = MakeBars(9);
        for (int i = 0; i < bars.Count; i++)
        {
            ind.Update(bars[i], true);
        }
        Assert.False(ind.IsHot);
    }

    [Fact]
    public void IsHot_TrueAfterWarmup()
    {
        var ind = new Stmacd(10, 5, 10, 3);
        var bars = MakeBars(20);
        for (int i = 0; i < bars.Count; i++)
        {
            ind.Update(bars[i], true);
        }
        Assert.True(ind.IsHot);
    }

    // ── Output Properties ─────────────────────────────────────

    [Fact]
    public void DualOutput_StmacdAndSignal_AreDifferent()
    {
        var ind = new Stmacd();
        var bars = MakeBars(100);
        for (int i = 0; i < bars.Count; i++)
        {
            ind.Update(bars[i], true);
        }
        // After warmup, signal is a smoothed version → generally different
        Assert.NotEqual(ind.StmacdValue.Value, ind.Signal.Value);
    }

    [Fact]
    public void Last_EqualsStmacdValue()
    {
        var ind = new Stmacd();
        var bars = MakeBars(100);
        for (int i = 0; i < bars.Count; i++)
        {
            ind.Update(bars[i], true);
        }
        Assert.Equal(ind.StmacdValue.Value, ind.Last.Value);
    }

    // ── Streaming Behaviour ───────────────────────────────────

    [Fact]
    public void ConstantInput_ProducesZero()
    {
        var ind = new Stmacd(10, 5, 10, 3);
        var bars = MakeConstantBars(50, 100);
        for (int i = 0; i < bars.Count; i++)
        {
            ind.Update(bars[i], true);
        }
        // Fast and slow EMAs converge to same value → STMACD = 0
        Assert.Equal(0.0, ind.StmacdValue.Value, 6);
        Assert.Equal(0.0, ind.Signal.Value, 6);
    }

    [Fact]
    public void Uptrend_ProducesPositiveValues()
    {
        var ind = new Stmacd(20, 5, 15, 3);
        var bars = MakeUptrendBars(100);
        for (int i = 0; i < bars.Count; i++)
        {
            ind.Update(bars[i], true);
        }
        Assert.True(ind.StmacdValue.Value > 0, $"Expected positive STMACD in uptrend, got {ind.StmacdValue.Value}");
    }

    [Fact]
    public void Downtrend_ProducesNegativeValues()
    {
        var ind = new Stmacd(20, 5, 15, 3);
        var bars = MakeDowntrendBars(100);
        for (int i = 0; i < bars.Count; i++)
        {
            ind.Update(bars[i], true);
        }
        Assert.True(ind.StmacdValue.Value < 0, $"Expected negative STMACD in downtrend, got {ind.StmacdValue.Value}");
    }

    [Fact]
    public void SingleBar_ProducesZero()
    {
        var ind = new Stmacd();
        var bar = new TBar(DateTime.UtcNow, 100, 102, 98, 100, 1000);
        ind.Update(bar, true);
        // First bar: fastEma=slowEma=close → STMACD=0
        Assert.Equal(0.0, ind.StmacdValue.Value, 10);
    }

    // ── Bar Correction (isNew=false) ──────────────────────────

    [Fact]
    public void BarCorrection_RestoresState()
    {
        var ind = new Stmacd(10, 5, 10, 3);
        var bars = MakeBars(30);

        for (int i = 0; i < 20; i++)
        {
            ind.Update(bars[i], true);
        }

        // Process bar 20 as new
        ind.Update(bars[20], true);
        double stmacdAfterNew = ind.StmacdValue.Value;

        // Correct bar 20 with different value
        var corrected = new TBar(bars[20].Time, bars[20].Open * 1.1,
            bars[20].High * 1.1, bars[20].Low * 0.9, bars[20].Close * 1.05, bars[20].Volume);
        ind.Update(corrected, false);

        // Process bar 20 again with original → should restore to same state-path
        ind.Update(bars[20], false);

        // The values after restoring the original bar should match the initial processing
        Assert.Equal(stmacdAfterNew, ind.StmacdValue.Value, 10);
    }

    [Fact]
    public void BarCorrection_ProducesDifferentResult()
    {
        var ind = new Stmacd(10, 5, 10, 3);
        var bars = MakeBars(30);

        for (int i = 0; i < 20; i++)
        {
            ind.Update(bars[i], true);
        }

        ind.Update(bars[20], true);
        double original = ind.StmacdValue.Value;

        // Correct with significantly different close
        var corrected = new TBar(bars[20].Time, bars[20].Open,
            bars[20].High * 2, bars[20].Low * 0.5, bars[20].Close * 1.5, bars[20].Volume);
        ind.Update(corrected, false);
        double correctedVal = ind.StmacdValue.Value;

        Assert.NotEqual(original, correctedVal);
    }

    // ── Batch Methods ─────────────────────────────────────────

    [Fact]
    public void Batch_Static_MatchesStreaming()
    {
        var bars = MakeBars(200);
        var (bStmacd, bSignal) = Stmacd.Batch(bars);

        var streaming = new Stmacd();
        for (int i = 0; i < bars.Count; i++)
        {
            streaming.Update(bars[i], true);
        }

        // Compare last values
        Assert.Equal(bStmacd.Values[^1], streaming.StmacdValue.Value, 8);
        Assert.Equal(bSignal.Values[^1], streaming.Signal.Value, 8);
    }

    [Fact]
    public void Batch_Instance_MatchesStreaming()
    {
        var bars = MakeBars(200);

        var ind = new Stmacd();
        var (iStmacd, iSignal) = ind.Update(bars);

        var streaming = new Stmacd();
        for (int i = 0; i < bars.Count; i++)
        {
            streaming.Update(bars[i], true);
        }

        Assert.Equal(iStmacd.Values[^1], streaming.StmacdValue.Value, 8);
        Assert.Equal(iSignal.Values[^1], streaming.Signal.Value, 8);
    }

    [Fact]
    public void Batch_ReturnsCorrectLength()
    {
        var bars = MakeBars(100);
        var (bStmacd, bSignal) = Stmacd.Batch(bars);

        Assert.Equal(100, bStmacd.Count);
        Assert.Equal(100, bSignal.Count);
    }

    [Fact]
    public void Batch_EmptySource_ReturnsEmpty()
    {
        var empty = new TBarSeries();
        var (bStmacd, bSignal) = Stmacd.Batch(empty);

        Assert.Empty(bStmacd);
        Assert.Empty(bSignal);
    }

    [Fact]
    public void Batch_CustomParams_MatchesStreaming()
    {
        var bars = MakeBars(200);
        int p = 30, f = 8, s = 20, sig = 5;

        var (bStmacd, bSignal) = Stmacd.Batch(bars, p, f, s, sig);

        var streaming = new Stmacd(p, f, s, sig);
        for (int i = 0; i < bars.Count; i++)
        {
            streaming.Update(bars[i], true);
        }

        Assert.Equal(bStmacd.Values[^1], streaming.StmacdValue.Value, 8);
        Assert.Equal(bSignal.Values[^1], streaming.Signal.Value, 8);
    }

    // ── Calculate ─────────────────────────────────────────────

    [Fact]
    public void Calculate_ReturnsBothResultsAndIndicator()
    {
        var bars = MakeBars(100);
        var (results, indicator) = Stmacd.Calculate(bars);

        Assert.True(indicator.IsHot);
        Assert.Equal(100, results.Stmacd.Count);
        Assert.Equal(100, results.Signal.Count);
    }

    // ── Reset ─────────────────────────────────────────────────

    [Fact]
    public void Reset_ClearsState()
    {
        var ind = new Stmacd(10, 5, 10, 3);
        var bars = MakeBars(30);
        for (int i = 0; i < bars.Count; i++)
        {
            ind.Update(bars[i], true);
        }

        ind.Reset();

        Assert.False(ind.IsHot);
        Assert.Equal(default, ind.Last);
        Assert.Equal(default, ind.StmacdValue);
        Assert.Equal(default, ind.Signal);
    }

    [Fact]
    public void Reset_AllowsReuse()
    {
        var ind = new Stmacd(10, 5, 10, 3);
        var bars = MakeBars(50);

        for (int i = 0; i < bars.Count; i++)
        {
            ind.Update(bars[i], true);
        }
        double firstRun = ind.StmacdValue.Value;

        ind.Reset();

        for (int i = 0; i < bars.Count; i++)
        {
            ind.Update(bars[i], true);
        }
        double secondRun = ind.StmacdValue.Value;

        Assert.Equal(firstRun, secondRun, 10);
    }

    // ── NaN Handling ──────────────────────────────────────────

    [Fact]
    public void NaN_Input_ReturnsNaN_WhenNoValidPrior()
    {
        var ind = new Stmacd();
        var nanBar = new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 0);
        ind.Update(nanBar, true);
        Assert.True(double.IsNaN(ind.StmacdValue.Value));
    }

    [Fact]
    public void NaN_AfterValid_UsesLastValid()
    {
        var ind = new Stmacd(5, 3, 5, 2);
        var bars = MakeBars(20);

        for (int i = 0; i < bars.Count; i++)
        {
            ind.Update(bars[i], true);
        }
        double validValue = ind.StmacdValue.Value;
        Assert.True(double.IsFinite(validValue));

        // Feed NaN — should use last valid values
        var nanBar = new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 0);
        ind.Update(nanBar, true);
        Assert.True(double.IsFinite(ind.StmacdValue.Value));
    }

    // ── TValue convenience ────────────────────────────────────

    [Fact]
    public void TValue_Input_TreatedAsHLCSame()
    {
        var ind1 = new Stmacd(10, 5, 10, 3);
        var ind2 = new Stmacd(10, 5, 10, 3);
        var t = DateTime.UtcNow;

        for (int i = 0; i < 30; i++)
        {
            double v = 100 + i;
            var time = t.AddDays(i);
            ind1.Update(new TValue(time, v), true);
            ind2.Update(new TBar(time, v, v, v, v, 0), true);
        }

        Assert.Equal(ind1.StmacdValue.Value, ind2.StmacdValue.Value, 10);
        Assert.Equal(ind1.Signal.Value, ind2.Signal.Value, 10);
    }

    // ── Pub/Sub ───────────────────────────────────────────────

    [Fact]
    public void PubSub_FiresOnUpdate()
    {
        var ind = new Stmacd(5, 3, 5, 2);
        int count = 0;
        ind.Pub += (object? _, in TValueEventArgs _) => count++;

        var bars = MakeBars(10);
        for (int i = 0; i < bars.Count; i++)
        {
            ind.Update(bars[i], true);
        }

        Assert.Equal(10, count);
    }

    [Fact]
    public void PubSub_SourceConstructor()
    {
        var bars = MakeBars(20);
        int count = 0;

        var ind = new Stmacd(bars, 10, 5, 10, 3);
        ind.Pub += (object? _, in TValueEventArgs _) => count++;

        // Publishing to the source should trigger indicator updates
        bars.Add(new TBar(DateTime.UtcNow, 100, 102, 98, 100, 1000));
        Assert.Equal(1, count);
    }

    // ── Prime ─────────────────────────────────────────────────

    [Fact]
    public void Prime_MatchesStreamingPlayback()
    {
        var bars = MakeBars(100);

        var primed = new Stmacd(10, 5, 10, 3);
        primed.Prime(bars);

        var streamed = new Stmacd(10, 5, 10, 3);
        for (int i = 0; i < bars.Count; i++)
        {
            streamed.Update(bars[i], true);
        }

        Assert.Equal(primed.StmacdValue.Value, streamed.StmacdValue.Value, 10);
        Assert.Equal(primed.Signal.Value, streamed.Signal.Value, 10);
    }

    // ── Batch Span Validation ─────────────────────────────────

    [Fact]
    public void Batch_Span_InvalidPeriods_Throws()
    {
        double[] h = new double[10];
        double[] l = new double[10];
        double[] c = new double[10];
        double[] sOut = new double[10];
        double[] sigOut = new double[10];

        Assert.Throws<ArgumentException>(() =>
            Stmacd.Batch(h, l, c, sOut, sigOut, 0));
    }

    [Fact]
    public void Batch_Span_MismatchedLengths_Throws()
    {
        double[] h = new double[10];
        double[] l = new double[5];
        double[] c = new double[10];
        double[] sOut = new double[10];
        double[] sigOut = new double[10];

        Assert.Throws<ArgumentException>(() =>
            Stmacd.Batch(h, l, c, sOut, sigOut));
    }
}
