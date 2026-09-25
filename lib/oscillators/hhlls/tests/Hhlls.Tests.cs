namespace QuanTAlib;

public class HhllsTests
{
    private static readonly Random _rng = new(42);

    private static TBarSeries MakeBars(int count = 500)
    {
        var bars = new TBarSeries();
        double price = 100;
        DateTime t = DateTime.UtcNow;
        for (int i = 0; i < count; i++)
        {
            double change = (_rng.NextDouble() - 0.5) * 2;
            double high = price + Math.Abs(change) + _rng.NextDouble();
            double low = price - Math.Abs(change) - _rng.NextDouble();
            double close = low + (high - low) * _rng.NextDouble();
            bars.Add(new TBar(t, price, high, low, close, 1000));
            price = close;
            t = t.AddMinutes(1);
        }
        return bars;
    }

    private static TBarSeries MakeConstantBars(int count = 100, double value = 50)
    {
        var bars = new TBarSeries();
        DateTime t = DateTime.UtcNow;
        for (int i = 0; i < count; i++)
        {
            bars.Add(new TBar(t, value, value, value, value, 1000));
            t = t.AddMinutes(1);
        }
        return bars;
    }

    private static TBarSeries MakeUptrendBars(int count = 100)
    {
        var bars = new TBarSeries();
        DateTime t = DateTime.UtcNow;
        double price = 100;
        for (int i = 0; i < count; i++)
        {
            double high = price + 1.5;
            double low = price - 0.5;
            double close = price + 1.0;
            bars.Add(new TBar(t, price, high, low, close, 1000));
            price = close;
            t = t.AddMinutes(1);
        }
        return bars;
    }

    private static TBarSeries MakeDowntrendBars(int count = 100)
    {
        var bars = new TBarSeries();
        DateTime t = DateTime.UtcNow;
        double price = 200;
        for (int i = 0; i < count; i++)
        {
            double high = price + 0.5;
            double low = price - 1.5;
            double close = price - 1.0;
            bars.Add(new TBar(t, price, high, low, close, 1000));
            price = close;
            t = t.AddMinutes(1);
        }
        return bars;
    }

    // ────────────── A: Constructor & Parameter Validation ──────────────

    [Fact]
    public void Constructor_DefaultPeriod_Is20()
    {
        var h = new Hhlls();
        Assert.Equal("Hhlls(20)", h.Name);
        Assert.Equal(20, h.WarmupPeriod);
    }

    [Fact]
    public void Constructor_CustomPeriod()
    {
        var h = new Hhlls(10);
        Assert.Equal("Hhlls(10)", h.Name);
    }

    [Fact]
    public void Constructor_PeriodLessThan2_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Hhlls(1));
    }

    [Fact]
    public void Constructor_PeriodZero_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Hhlls(0));
    }

    [Fact]
    public void Constructor_NegativePeriod_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Hhlls(-5));
    }

    // ────────────── B: Basic Calculation ──────────────

    [Fact]
    public void ConstantInput_BothOutputsConvergeToZero()
    {
        var bars = MakeConstantBars(100);
        var h = new Hhlls(10);

        for (int i = 0; i < bars.Count; i++)
        {
            h.Update(bars[i], true);
        }

        // Constant prices: no higher highs, no lower lows → raw = 0 → EMA → 0
        Assert.True(h.Hhs.Value < 1.0, $"HHS should be near 0:  {h.Hhs.Value}");
        Assert.True(h.Lls.Value < 1.0, $"LLS should be near 0: {h.Lls.Value}");
    }

    [Fact]
    public void Uptrend_HhsRises_LlsDecays()
    {
        var bars = MakeUptrendBars(100);
        var h = new Hhlls(10);

        for (int i = 0; i < bars.Count; i++)
        {
            h.Update(bars[i], true);
        }

        Assert.True(h.Hhs.Value > 20, $"HHS in uptrend should be elevated: {h.Hhs.Value}");
        Assert.True(h.Lls.Value < 5, $"LLS in uptrend should be near 0: {h.Lls.Value}");
    }

    [Fact]
    public void Downtrend_LlsRises_HhsDecays()
    {
        var bars = MakeDowntrendBars(100);
        var h = new Hhlls(10);

        for (int i = 0; i < bars.Count; i++)
        {
            h.Update(bars[i], true);
        }

        Assert.True(h.Lls.Value > 20, $"LLS in downtrend should be elevated: {h.Lls.Value}");
        Assert.True(h.Hhs.Value < 5, $"HHS in downtrend should be near 0: {h.Hhs.Value}");
    }

    [Fact]
    public void OutputRange_BoundedZeroToHundred()
    {
        var bars = MakeBars(1000);
        var h = new Hhlls(20);

        for (int i = 0; i < bars.Count; i++)
        {
            h.Update(bars[i], true);
            if (h.IsHot)
            {
                Assert.InRange(h.Hhs.Value, -0.01, 100.01);
                Assert.InRange(h.Lls.Value, -0.01, 100.01);
            }
        }
    }

    // ────────────── C: IsNew / Bar Correction ──────────────

    [Fact]
    public void BarCorrection_IsNewFalse_RestoresState()
    {
        var bars = MakeBars(50);
        var h = new Hhlls(10);

        // Feed first 40 bars
        for (int i = 0; i < 40; i++)
        {
            h.Update(bars[i], true);
        }

        // New bar
        h.Update(bars[40], true);
        double hhsAfterNew = h.Hhs.Value;

        // Correction (isNew=false) with different data
        var corrected = new TBar(bars[40].Time, 999, 999, 1, 500, 1000);
        h.Update(corrected, false);

        // Another correction restoring original
        h.Update(bars[40], false);

        Assert.Equal(hhsAfterNew, h.Hhs.Value, 10);
    }

    // ────────────── D: Reset ──────────────

    [Fact]
    public void Reset_ClearsState()
    {
        var bars = MakeBars(50);
        var h = new Hhlls(10);

        for (int i = 0; i < 50; i++)
        {
            h.Update(bars[i], true);
        }

        Assert.True(h.IsHot);
        h.Reset();
        Assert.False(h.IsHot);
        Assert.Equal(default, h.Last);
    }

    [Fact]
    public void Reset_ReconvergesFromScratch()
    {
        var bars = MakeBars(100);
        var h1 = new Hhlls(10);
        var h2 = new Hhlls(10);

        for (int i = 0; i < 100; i++)
        {
            h1.Update(bars[i], true);
        }

        // h2 feeds first 50, resets, feeds all 100
        for (int i = 0; i < 50; i++)
        {
            h2.Update(bars[i], true);
        }
        h2.Reset();
        for (int i = 0; i < 100; i++)
        {
            h2.Update(bars[i], true);
        }

        Assert.Equal(h1.Hhs.Value, h2.Hhs.Value, 10);
        Assert.Equal(h1.Lls.Value, h2.Lls.Value, 10);
    }

    // ────────────── E: IsHot / Warmup ──────────────

    [Fact]
    public void IsHot_FalseBeforeWarmup()
    {
        var bars = MakeBars(30);
        var h = new Hhlls(20);

        for (int i = 0; i < 19; i++)
        {
            h.Update(bars[i], true);
            Assert.False(h.IsHot);
        }

        h.Update(bars[19], true);
        Assert.True(h.IsHot);
    }

    // ────────────── F: NaN / Infinity Handling ──────────────

    [Fact]
    public void NaN_Input_SubstitutesLastValid()
    {
        var h = new Hhlls(10);
        DateTime t = DateTime.UtcNow;

        // Feed 15 valid bars
        for (int i = 0; i < 15; i++)
        {
            double v = 100 + i;
            h.Update(new TBar(t.AddMinutes(i), v, v + 1, v - 1, v, 1000), true);
        }

        // Feed NaN bar
        h.Update(new TBar(t.AddMinutes(15), double.NaN, double.NaN, double.NaN, double.NaN, 1000), true);

        // Should not be NaN
        Assert.True(double.IsFinite(h.Hhs.Value));
        Assert.True(double.IsFinite(h.Lls.Value));
    }

    // ────────────── G: Streaming vs Batch Consistency ──────────────

    [Fact]
    public void StreamingMatchesBatch()
    {
        var bars = MakeBars(200);
        var h = new Hhlls(20);

        // Streaming
        var streamHhs = new double[200];
        var streamLls = new double[200];
        for (int i = 0; i < 200; i++)
        {
            h.Update(bars[i], true);
            streamHhs[i] = h.Hhs.Value;
            streamLls[i] = h.Lls.Value;
        }

        // Batch (span)
        var hhsOut = new double[200];
        var llsOut = new double[200];
        Hhlls.Batch(bars.HighValues, bars.LowValues, hhsOut, llsOut, 20);

        for (int i = 0; i < 200; i++)
        {
            Assert.Equal(streamHhs[i], hhsOut[i], 10);
            Assert.Equal(streamLls[i], llsOut[i], 10);
        }
    }

    [Fact]
    public void Batch_TBarSeries_MatchesStreaming()
    {
        var bars = MakeBars(200);
        var h = new Hhlls(20);

        // Streaming
        for (int i = 0; i < 200; i++)
        {
            h.Update(bars[i], true);
        }

        var (batchHhs, batchLls) = Hhlls.Batch(bars, 20);

        Assert.Equal(h.Hhs.Value, batchHhs.Values[^1], 10);
        Assert.Equal(h.Lls.Value, batchLls.Values[^1], 10);
    }

    // ────────────── H: Span Batch API ──────────────

    [Fact]
    public void BatchSpan_EmptySource_DoesNotThrow()
    {
        var hhs = Array.Empty<double>();
        var lls = Array.Empty<double>();
        Hhlls.Batch(ReadOnlySpan<double>.Empty, ReadOnlySpan<double>.Empty, hhs, lls, 20);
        Assert.Empty(hhs);
    }

    [Fact]
    public void BatchSpan_MismatchedLengths_Throws()
    {
        var h = new double[10];
        var l = new double[5];
        var hhs = new double[10];
        var lls = new double[10];
        Assert.Throws<ArgumentException>(() => Hhlls.Batch(h, l, hhs, lls, 20));
    }

    [Fact]
    public void BatchSpan_InvalidPeriod_Throws()
    {
        var h = new double[10];
        var l = new double[10];
        var hhs = new double[10];
        var lls = new double[10];
        Assert.Throws<ArgumentOutOfRangeException>(() => Hhlls.Batch(h, l, hhs, lls, 1));
    }

    // ────────────── I: Event / Chaining ──────────────

    [Fact]
    public void PubEvent_FiresOnUpdate()
    {
        var h = new Hhlls(10);
        int count = 0;
        h.Pub += (object? _, in TValueEventArgs _) => count++;

        var bars = MakeBars(20);
        for (int i = 0; i < 20; i++)
        {
            h.Update(bars[i], true);
        }

        Assert.Equal(20, count);
    }

    [Fact]
    public void TBarSeries_Constructor_Subscribes()
    {
        var bars = MakeBars(30);
        var h = new Hhlls(bars, 10);

        Assert.True(h.IsHot);
        Assert.True(double.IsFinite(h.Hhs.Value));
        Assert.True(double.IsFinite(h.Lls.Value));
    }

    // ────────────── J: Calculate API ──────────────

    [Fact]
    public void Calculate_ReturnsHotIndicator()
    {
        var bars = MakeBars(100);
        var ((hhs, lls), indicator) = Hhlls.Calculate(bars, 20);

        Assert.Equal(100, hhs.Count);
        Assert.Equal(100, lls.Count);
        Assert.True(indicator.IsHot);
    }

    // ────────────── K: TValue Fallback ──────────────

    [Fact]
    public void TValue_Update_Works()
    {
        var h = new Hhlls(10);
        DateTime t = DateTime.UtcNow;

        for (int i = 0; i < 15; i++)
        {
            h.Update(new TValue(t.AddMinutes(i), 100 + i), true);
        }

        Assert.True(h.IsHot);
    }

    // ────────────── L: Large Dataset ──────────────

    [Fact]
    public void LargeDataset_Stability()
    {
        var bars = MakeBars(10000);
        var h = new Hhlls(20);

        for (int i = 0; i < bars.Count; i++)
        {
            h.Update(bars[i], true);
        }

        Assert.True(double.IsFinite(h.Hhs.Value));
        Assert.True(double.IsFinite(h.Lls.Value));
        Assert.InRange(h.Hhs.Value, 0, 100);
        Assert.InRange(h.Lls.Value, 0, 100);
    }

    // ────────────── M: Different Periods ──────────────

    [Fact]
    public void DifferentPeriods_ProduceDifferentResults()
    {
        var bars = MakeBars(200);
        var h10 = new Hhlls(10);
        var h30 = new Hhlls(30);

        for (int i = 0; i < 200; i++)
        {
            h10.Update(bars[i], true);
            h30.Update(bars[i], true);
        }

        Assert.NotEqual(h10.Hhs.Value, h30.Hhs.Value);
    }
}
