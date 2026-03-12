namespace QuanTAlib.Tests;

public class AsiTests
{
    // Manual ASI calculation for bar 2 with limitMove=3.0:
    // Bar1: O=10, H=11, L=9, C=10 (first bar — SI=0, ASI=0)
    // Bar2: O=10, H=12, L=9, C=11
    //   K = max(|12-10|, |9-10|) = max(2, 1) = 2
    //   absHC=2, absLC=1, absHL=3, absC1O1=|10-10|=0
    //   absHL(3) is largest => R = 3 + 0.25*0 = 3
    //   numerator = (11-10) + 0.5*(11-10) + 0.25*(10-10) = 1 + 0.5 + 0 = 1.5
    //   SI = 50 * 1.5 / 3 * (2/3) = 50 * 0.5 * 0.6667 = 16.6667
    private const double LimitMove = 3.0;
    private static readonly TBar Bar1 = new(DateTime.UtcNow, 10, 11, 9, 10, 0);
    private static readonly TBar Bar2 = new(DateTime.UtcNow.AddMinutes(1), 10, 12, 9, 11, 0);
    private const double ExpectedSI2 = 50.0 * 1.5 / 3.0 * (2.0 / 3.0); // ≈ 16.6667

    // ── A) Constructor validation ─────────────────────────────────────────────

    [Fact]
    public void Constructor_LimitMoveZero_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Asi(0.0));
        Assert.Equal("limitMove", ex.ParamName);
    }

    [Fact]
    public void Constructor_LimitMoveNegative_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Asi(-1.0));
        Assert.Equal("limitMove", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidParams_DoesNotThrow()
    {
        var asi = new Asi(3.0);
        Assert.NotNull(asi);
        Assert.Equal(0.0, asi.Last.Value);
        Assert.False(asi.IsHot);
    }

    [Fact]
    public void Constructor_DefaultLimitMove_IsThree()
    {
        var asi = new Asi();
        Assert.Contains("3", asi.Name, StringComparison.Ordinal);
    }

    // ── B) Basic calculation ──────────────────────────────────────────────────

    [Fact]
    public void FirstBar_AlwaysZero()
    {
        var asi = new Asi(LimitMove);
        var result = asi.Update(Bar1);
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void SecondBar_MatchesManualCalculation()
    {
        var asi = new Asi(LimitMove);
        asi.Update(Bar1);
        var result = asi.Update(Bar2);
        Assert.Equal(ExpectedSI2, result.Value, 1e-9);
    }

    [Fact]
    public void IsHot_TrueAfterTwoBars()
    {
        var asi = new Asi(LimitMove);
        Assert.False(asi.IsHot);
        asi.Update(Bar1);
        Assert.False(asi.IsHot);
        asi.Update(Bar2);
        Assert.True(asi.IsHot);
    }

    [Fact]
    public void Name_ContainsLimitMove()
    {
        var asi = new Asi(5.0);
        Assert.Contains("5", asi.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Last_IsAccessibleAfterUpdate()
    {
        var asi = new Asi(LimitMove);
        var result = asi.Update(Bar1);
        Assert.Equal(result.Value, asi.Last.Value);
    }

    // ── C) State + bar correction ─────────────────────────────────────────────

    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var asi = new Asi(LimitMove);
        asi.Update(Bar1, isNew: true);
        asi.Update(Bar2, isNew: true);
        double after2 = asi.Last.Value;
        Assert.Equal(ExpectedSI2, after2, 1e-9);
    }

    [Fact]
    public void IsNew_False_RewritesLastBar()
    {
        var asi = new Asi(LimitMove);
        asi.Update(Bar1, isNew: true);
        asi.Update(Bar2, isNew: true);
        double afterBar2 = asi.Last.Value;

        // Rewrite bar2 with a different close
        var bar2Alt = new TBar(Bar2.Time, 10, 15, 8, 5, 0);
        asi.Update(bar2Alt, isNew: false);
        double afterRewrite = asi.Last.Value;

        Assert.NotEqual(afterBar2, afterRewrite, 1e-9);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var asi = new Asi(LimitMove);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        // Feed 20 bars
        TBar twentiethBar = default;
        for (int i = 0; i < 20; i++)
        {
            twentiethBar = gbm.Next(isNew: true);
            asi.Update(twentiethBar, isNew: true);
        }

        double stateAfterTwenty = asi.Last.Value;

        // 9 rewrites with different data
        for (int i = 0; i < 9; i++)
        {
            var alt = gbm.Next(isNew: false);
            asi.Update(alt, isNew: false);
        }

        // Rewrite again with the original 20th bar
        var final = asi.Update(twentiethBar, isNew: false);
        Assert.Equal(stateAfterTwenty, final.Value, 1e-9);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        var asi = new Asi(LimitMove);
        asi.Update(Bar1);
        asi.Update(Bar2);
        double before = asi.Last.Value;
        Assert.NotEqual(0.0, before);

        asi.Reset();
        Assert.Equal(0.0, asi.Last.Value);
        Assert.False(asi.IsHot);

        // After reset, first bar is 0 again
        var r = asi.Update(Bar1);
        Assert.Equal(0.0, r.Value);
    }

    // ── D) Warmup/convergence ─────────────────────────────────────────────────

    [Fact]
    public void WarmupPeriod_IsTwo()
    {
        var asi = new Asi(LimitMove);
        Assert.Equal(2, asi.WarmupPeriod);
    }

    [Fact]
    public void IsHot_FlipsAtSecondBar()
    {
        var asi = new Asi(LimitMove);
        var gbm = new GBM(startPrice: 100.0, seed: 1);
        var bars = gbm.Fetch(5, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        asi.Update(bars[0]);
        Assert.False(asi.IsHot);
        asi.Update(bars[1]);
        Assert.True(asi.IsHot);
        asi.Update(bars[2]);
        Assert.True(asi.IsHot);
    }

    // ── E) Robustness ─────────────────────────────────────────────────────────

    [Fact]
    public void NaN_Close_UsesLastValidValue()
    {
        var asi = new Asi(LimitMove);
        var gbm = new GBM(startPrice: 100.0, seed: 7);
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 5; i++) { asi.Update(bars[i]); }

        var nanBar = new TBar(DateTime.UtcNow, bars[4].Open, bars[4].High, bars[4].Low, double.NaN, 0);
        var result = asi.Update(nanBar);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Close_UsesLastValidValue()
    {
        var asi = new Asi(LimitMove);
        var gbm = new GBM(startPrice: 100.0, seed: 8);
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 5; i++) { asi.Update(bars[i]); }

        var infBar = new TBar(DateTime.UtcNow, bars[4].Open, bars[4].High, bars[4].Low, double.PositiveInfinity, 0);
        var result = asi.Update(infBar);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void BatchNaN_AllOutputsFinite()
    {
        var gbm = new GBM(startPrice: 100.0, seed: 9);
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] opens = new double[20];
        double[] highs = new double[20];
        double[] lows = new double[20];
        double[] closes = new double[20];
        double[] output = new double[20];

        for (int i = 0; i < 20; i++)
        {
            opens[i] = bars[i].Open;
            highs[i] = bars[i].High;
            lows[i] = bars[i].Low;
            closes[i] = bars[i].Close;
        }
        closes[10] = double.NaN;

        Asi.Batch(opens.AsSpan(), highs.AsSpan(), lows.AsSpan(), closes.AsSpan(), output.AsSpan(), LimitMove);

        foreach (var v in output)
        {
            Assert.True(double.IsFinite(v));
        }
    }

    [Fact]
    public void RIsZero_ProducesSIZero()
    {
        // When H=L=PrevClose=Open, R=0, SI should be 0
        var asi = new Asi(LimitMove);
        var flat1 = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 0);
        var flat2 = new TBar(DateTime.UtcNow.AddMinutes(1), 100, 100, 100, 100, 0);
        asi.Update(flat1);
        var result = asi.Update(flat2);
        Assert.Equal(0.0, result.Value);
    }

    // ── F) Consistency ────────────────────────────────────────────────────────

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        const double lm = 3.0;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 123);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // 1. Streaming mode
        var streaming = new Asi(lm);
        for (int i = 0; i < bars.Count; i++) { streaming.Update(bars[i]); }
        double streamVal = streaming.Last.Value;

        // 2. Batch (static spans)
        double[] opens = new double[bars.Count];
        double[] highs = new double[bars.Count];
        double[] lows = new double[bars.Count];
        double[] closes = new double[bars.Count];
        double[] output = new double[bars.Count];

        for (int i = 0; i < bars.Count; i++)
        {
            opens[i] = bars[i].Open;
            highs[i] = bars[i].High;
            lows[i] = bars[i].Low;
            closes[i] = bars[i].Close;
        }

        Asi.Batch(opens.AsSpan(), highs.AsSpan(), lows.AsSpan(), closes.AsSpan(), output.AsSpan(), lm);
        double batchVal = output[^1];

        // 3. TBarSeries Batch
        var tbatch = new Asi(lm);
        tbatch.Update(bars);
        double tbatchVal = tbatch.Last.Value;

        Assert.Equal(streamVal, batchVal, 1e-9);
        Assert.Equal(streamVal, tbatchVal, 1e-9);
    }

    [Fact]
    public void Eventing_MatchesStreaming()
    {
        const double lm = 3.0;
        var gbm = new GBM(startPrice: 100.0, seed: 55);
        var bars = gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming
        var streaming = new Asi(lm);
        for (int i = 0; i < bars.Count; i++) { streaming.Update(bars[i]); }

        // Eventing via TBarSeries
        var source = new TBarSeries();
        var eventing = new Asi(source, lm);
        for (int i = 0; i < bars.Count; i++) { source.Add(bars[i]); }

        Assert.Equal(streaming.Last.Value, eventing.Last.Value, 1e-9);
    }

    // ── G) Span API tests ─────────────────────────────────────────────────────

    [Fact]
    public void SpanBatch_MismatchedLengths_ThrowsArgumentException()
    {
        double[] opens = new double[5];
        double[] highs = new double[4]; // mismatch
        double[] lows = new double[5];
        double[] closes = new double[5];
        double[] output = new double[5];

        var ex = Assert.Throws<ArgumentException>(() =>
            Asi.Batch(opens.AsSpan(), highs.AsSpan(), lows.AsSpan(), closes.AsSpan(), output.AsSpan(), 3.0));
        Assert.Equal("high", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_InvalidLimitMove_ThrowsArgumentException()
    {
        double[] opens = new double[5];
        double[] highs = new double[5];
        double[] lows = new double[5];
        double[] closes = new double[5];
        double[] output = new double[5];

        var ex = Assert.Throws<ArgumentException>(() =>
            Asi.Batch(opens.AsSpan(), highs.AsSpan(), lows.AsSpan(), closes.AsSpan(), output.AsSpan(), 0.0));
        Assert.Equal("limitMove", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_LargeDataset_NoStackOverflow()
    {
        const int size = 10000;
        var gbm = new GBM(startPrice: 100.0, seed: 99);
        var bars = gbm.Fetch(size, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] opens = new double[size];
        double[] highs = new double[size];
        double[] lows = new double[size];
        double[] closes = new double[size];
        double[] output = new double[size];

        for (int i = 0; i < size; i++)
        {
            opens[i] = bars[i].Open;
            highs[i] = bars[i].High;
            lows[i] = bars[i].Low;
            closes[i] = bars[i].Close;
        }

        Asi.Batch(opens.AsSpan(), highs.AsSpan(), lows.AsSpan(), closes.AsSpan(), output.AsSpan(), 3.0);

        // Last value should be finite
        Assert.True(double.IsFinite(output[^1]));
    }

    // ── H) Chainability ───────────────────────────────────────────────────────

    [Fact]
    public void PubEvent_FiresOnUpdate()
    {
        var asi = new Asi(LimitMove);
        int eventCount = 0;
        asi.Pub += (object? _, in TValueEventArgs e) => eventCount++;

        asi.Update(Bar1);
        asi.Update(Bar2);

        Assert.Equal(2, eventCount);
    }

    [Fact]
    public void ChainViaITValuePublisher_Works()
    {
        // Chain Asi -> Asi (using TValue path — close-only)
        var source = new TSeries();
        var asi1 = new Asi(source, LimitMove);

        source.Add(DateTime.UtcNow.Ticks, 100);
        source.Add(DateTime.UtcNow.AddMinutes(1).Ticks, 105);

        Assert.True(double.IsFinite(asi1.Last.Value));
    }

    // ── Determinism ───────────────────────────────────────────────────────────

    [Fact]
    public void GBM_Seeded_IsDeterministic()
    {
        var gbm1 = new GBM(startPrice: 100.0, seed: 42);
        var gbm2 = new GBM(startPrice: 100.0, seed: 42);
        var bars1 = gbm1.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var bars2 = gbm2.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var asi1 = new Asi(LimitMove);
        var asi2 = new Asi(LimitMove);

        for (int i = 0; i < bars1.Count; i++)
        {
            asi1.Update(bars1[i]);
            asi2.Update(bars2[i]);
        }

        Assert.Equal(asi1.Last.Value, asi2.Last.Value, 1e-12);
    }

    [Fact]
    public void UpTrend_ProducesPositiveASI()
    {
        var asi = new Asi(LimitMove);
        var now = DateTime.UtcNow;

        // Steadily rising prices
        for (int i = 0; i < 20; i++)
        {
            double p = 100.0 + i;
            var bar = new TBar(now.AddMinutes(i), p, p + 1, p - 1, p + 0.5, 0);
            asi.Update(bar);
        }

        Assert.True(asi.Last.Value > 0, $"Uptrend should produce positive ASI, got {asi.Last.Value}");
    }

    [Fact]
    public void DownTrend_ProducesNegativeASI()
    {
        var asi = new Asi(LimitMove);
        var now = DateTime.UtcNow;

        // Steadily falling prices
        for (int i = 0; i < 20; i++)
        {
            double p = 100.0 - i;
            var bar = new TBar(now.AddMinutes(i), p, p + 1, p - 1, p - 0.5, 0);
            asi.Update(bar);
        }

        Assert.True(asi.Last.Value < 0, $"Downtrend should produce negative ASI, got {asi.Last.Value}");
    }
}
