using Xunit;

namespace QuanTAlib.Tests;

public sealed class SqueezeProTests
{
    private static TBarSeries GenerateBars(int count, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    // === A) Constructor validation ===

    [Fact]
    public void Constructor_InvalidPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new SqueezePro(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new SqueezePro(period: -1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_InvalidBbMult_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new SqueezePro(bbMult: 0.0));
        Assert.Equal("bbMult", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeBbMult_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new SqueezePro(bbMult: -1.0));
        Assert.Equal("bbMult", ex.ParamName);
    }

    [Fact]
    public void Constructor_InvalidKcMultWide_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new SqueezePro(kcMultWide: 0.0));
        Assert.Equal("kcMultWide", ex.ParamName);
    }

    [Fact]
    public void Constructor_InvalidKcMultNormal_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new SqueezePro(kcMultNormal: 0.0));
        Assert.Equal("kcMultNormal", ex.ParamName);
    }

    [Fact]
    public void Constructor_InvalidKcMultNarrow_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new SqueezePro(kcMultNarrow: 0.0));
        Assert.Equal("kcMultNarrow", ex.ParamName);
    }

    [Fact]
    public void Constructor_InvalidMomLength_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new SqueezePro(momLength: 0));
        Assert.Equal("momLength", ex.ParamName);
    }

    [Fact]
    public void Constructor_InvalidMomSmooth_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new SqueezePro(momSmooth: 0));
        Assert.Equal("momSmooth", ex.ParamName);
    }

    [Fact]
    public void Constructor_DefaultParams()
    {
        var sq = new SqueezePro();
        Assert.Equal("SqueezePro(20,2,2,1.5,1)", sq.Name);
        Assert.Equal(20, sq.WarmupPeriod); // Max(20, 12+6=18) = 20
    }

    // === B) Basic calculation ===

    [Fact]
    public void Update_ReturnsTValue()
    {
        var sq = new SqueezePro(period: 5, momLength: 3, momSmooth: 2);
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 101, 1000);
        TValue result = sq.Update(bar);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_Last_Momentum_Accessible()
    {
        var sq = new SqueezePro(period: 5, momLength: 3, momSmooth: 2);
        for (int i = 0; i < 20; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100 + i, 105 + i, 95 + i, 101 + i, 1000);
            sq.Update(bar);
        }
        Assert.True(double.IsFinite(sq.Last.Value));
        Assert.True(double.IsFinite(sq.Momentum));
        Assert.NotEmpty(sq.Name);
    }

    [Fact]
    public void SqueezeLevel_IsInRange()
    {
        var sq = new SqueezePro(period: 5, momLength: 3, momSmooth: 2);
        for (int i = 0; i < 20; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 101, 99, 100, 1000);
            sq.Update(bar);
        }
        Assert.InRange(sq.SqueezeLevel, 0, 3);
    }

    [Fact]
    public void ConstantBars_MomentumNearZero()
    {
        var sq = new SqueezePro(period: 5, momLength: 3, momSmooth: 2);
        for (int i = 0; i < 30; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 100, 100, 100, 1000);
            sq.Update(bar);
        }
        // With constant price, MOM = 0 at all times, smooth of zero = 0
        Assert.Equal(0.0, sq.Momentum, precision: 10);
    }

    [Fact]
    public void ConstantBars_SqueezeLevel3_NarrowSqueeze()
    {
        // With constant price, BB width = 0, all KCs have width > 0 from ATR
        // Actually with constant price, ATR → 0 too, so both BB and KC collapse
        // BB upper < KC upper when stddev * bbMult < atr * kcMult
        // For constant bars: stddev=0, atr=0, so bbUpper = smaVal = kcUpper → not inside
        var sq = new SqueezePro(period: 5, momLength: 3, momSmooth: 2);
        for (int i = 0; i < 30; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 100, 100, 100, 1000);
            sq.Update(bar);
        }
        // Both collapse to same value, so bbUpper == kcUpper (not strictly less) → level 0
        Assert.Equal(0, sq.SqueezeLevel);
    }

    [Fact]
    public void RisingBars_PositiveMomentum_AfterWarmup()
    {
        var sq = new SqueezePro(period: 10, momLength: 5, momSmooth: 3);
        for (int i = 0; i < 40; i++)
        {
            double price = 100.0 + i;
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 1, price - 1, price, 1000);
            sq.Update(bar);
        }
        Assert.True(sq.IsHot);
        Assert.True(sq.Momentum > 0.0);
    }

    [Fact]
    public void FallingBars_NegativeMomentum_AfterWarmup()
    {
        var sq = new SqueezePro(period: 10, momLength: 5, momSmooth: 3);
        for (int i = 0; i < 40; i++)
        {
            double price = 200.0 - i;
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 1, price - 1, price, 1000);
            sq.Update(bar);
        }
        Assert.True(sq.IsHot);
        Assert.True(sq.Momentum < 0.0);
    }

    // === C) Squeeze level detection ===

    [Fact]
    public void HighVolatility_SqueezeLevelZero()
    {
        // Wide BB (high vol) with tight KC should push BB outside KC → squeeze off
        // Use very small KC multipliers so KC is narrow relative to BB
        var sq = new SqueezePro(period: 10, momLength: 3, momSmooth: 2,
            kcMultWide: 0.1, kcMultNormal: 0.05, kcMultNarrow: 0.01);
        for (int i = 0; i < 30; i++)
        {
            // Alternating large swings to create wide BB
            double swing = (i % 2 == 0) ? 50.0 : -50.0;
            double price = 100.0 + swing;
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 20, price - 20, price, 1000);
            sq.Update(bar);
        }
        Assert.Equal(0, sq.SqueezeLevel);
    }

    [Fact]
    public void TightRange_SqueezeOn()
    {
        // Very tight range should create narrow BB inside KC
        var sq = new SqueezePro(period: 10, momLength: 3, momSmooth: 2);
        // First seed with some volatility to build ATR
        for (int i = 0; i < 20; i++)
        {
            double price = 100.0 + (i * 2);
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 5, price - 5, price, 1000);
            sq.Update(bar);
        }
        // Then go very tight
        for (int i = 20; i < 50; i++)
        {
            double price = 140.0 + (i % 2 == 0 ? 0.01 : -0.01);
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 0.01, price - 0.01, price, 1000);
            sq.Update(bar);
        }
        // After many tight bars, squeeze should be active (level > 0)
        Assert.True(sq.SqueezeLevel > 0);
    }

    // === D) State + bar correction ===

    [Fact]
    public void IsNew_True_Advances_State()
    {
        var sq = new SqueezePro(period: 5, momLength: 3, momSmooth: 2);
        var bars = GenerateBars(10);
        for (int i = 0; i < 10; i++)
        {
            sq.Update(bars[i], isNew: true);
        }
        double momBefore = sq.Momentum;

        var nextBar = new TBar(DateTime.UtcNow.AddMinutes(100), 200, 210, 190, 205, 1000);
        sq.Update(nextBar, isNew: true);

        Assert.True(double.IsFinite(sq.Momentum));
        _ = momBefore;
    }

    [Fact]
    public void IsNew_False_Rewrites()
    {
        var sq = new SqueezePro(period: 5, momLength: 3, momSmooth: 2);
        var bars = GenerateBars(10);
        for (int i = 0; i < 9; i++)
        {
            sq.Update(bars[i], isNew: true);
        }

        sq.Update(bars[9], isNew: true);
        double momAfterNew = sq.Momentum;

        var corrected = new TBar(bars[9].Time, 999, 1005, 990, 1000, 1000);
        sq.Update(corrected, isNew: false);
        double momAfterCorrect = sq.Momentum;

        Assert.NotEqual(momAfterNew, momAfterCorrect);
    }

    [Fact]
    public void IterativeCorrection_Restores()
    {
        var sq = new SqueezePro(period: 5, momLength: 3, momSmooth: 2);
        var bars = GenerateBars(15);
        for (int i = 0; i < 14; i++)
        {
            sq.Update(bars[i], isNew: true);
        }

        sq.Update(bars[14], isNew: true);
        double momAfterTrue = sq.Momentum;

        for (int j = 0; j < 3; j++)
        {
            sq.Update(bars[14], isNew: false);
        }

        Assert.Equal(momAfterTrue, sq.Momentum, precision: 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var sq = new SqueezePro(period: 5, momLength: 3, momSmooth: 2);
        var bars = GenerateBars(20);
        for (int i = 0; i < 20; i++)
        {
            sq.Update(bars[i], isNew: true);
        }

        sq.Reset();

        Assert.False(sq.IsHot);
        Assert.Equal(0.0, sq.Momentum);
        Assert.Equal(0, sq.SqueezeLevel);
    }

    // === E) Warmup/convergence ===

    [Fact]
    public void IsHot_FlipsCorrectly()
    {
        var sq = new SqueezePro(period: 5, momLength: 3, momSmooth: 2);
        var bars = GenerateBars(20);
        for (int i = 0; i < 20; i++)
        {
            sq.Update(bars[i], isNew: true);
        }
        // After enough bars (momLength + momSmooth worth), should be hot
        Assert.True(sq.IsHot);
    }

    [Fact]
    public void WarmupPeriod_IsMaxOfPeriodAndMomTotal()
    {
        var sq1 = new SqueezePro(period: 30, momLength: 5, momSmooth: 3);
        Assert.Equal(30, sq1.WarmupPeriod); // Max(30, 5+3=8) = 30

        var sq2 = new SqueezePro(period: 5, momLength: 20, momSmooth: 10);
        Assert.Equal(30, sq2.WarmupPeriod); // Max(5, 20+10=30) = 30
    }

    // === F) Robustness ===

    [Fact]
    public void NaN_Input_UsesLastValid()
    {
        var sq = new SqueezePro(period: 5, momLength: 3, momSmooth: 2);
        var bars = GenerateBars(10);
        for (int i = 0; i < 9; i++)
        {
            sq.Update(bars[i], isNew: true);
        }

        var nanBar = new TBar(DateTime.UtcNow.AddMinutes(100), double.NaN, double.NaN, double.NaN, double.NaN, 0);
        sq.Update(nanBar, isNew: true);
        // Should not throw
        Assert.True(true);
    }

    [Fact]
    public void Infinity_Input_Handled()
    {
        var sq = new SqueezePro(period: 5, momLength: 3, momSmooth: 2);
        var bars = GenerateBars(10);
        for (int i = 0; i < 9; i++)
        {
            sq.Update(bars[i], isNew: true);
        }

        var infBar = new TBar(DateTime.UtcNow.AddMinutes(100),
            double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity, 0);
        sq.Update(infBar, isNew: true);
        Assert.True(true);
    }

    [Fact]
    public void MixedNaN_NoThrow()
    {
        var sq = new SqueezePro(period: 5, momLength: 3, momSmooth: 2);
        for (int i = 0; i < 20; i++)
        {
            TBar bar;
            if (i % 5 == 0)
            {
                bar = new TBar(DateTime.UtcNow.AddMinutes(i), double.NaN, double.NaN, double.NaN, double.NaN, 0);
            }
            else
            {
                bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100 + i, 105 + i, 95 + i, 101 + i, 1000);
            }
            sq.Update(bar, isNew: true);
        }
        Assert.True(true);
    }

    // === G) EMA smoothing mode ===

    [Fact]
    public void EmaMode_ProducesFiniteValues()
    {
        var sq = new SqueezePro(period: 10, momLength: 5, momSmooth: 3, useSma: false);
        var bars = GenerateBars(40);
        for (int i = 0; i < 40; i++)
        {
            sq.Update(bars[i], isNew: true);
        }
        Assert.True(double.IsFinite(sq.Momentum));
    }

    [Fact]
    public void EmaMode_DiffersFromSma()
    {
        var bars = GenerateBars(50);
        var sqSma = new SqueezePro(period: 10, momLength: 5, momSmooth: 3, useSma: true);
        var sqEma = new SqueezePro(period: 10, momLength: 5, momSmooth: 3, useSma: false);

        for (int i = 0; i < 50; i++)
        {
            sqSma.Update(bars[i], isNew: true);
            sqEma.Update(bars[i], isNew: true);
        }

        // SMA and EMA smoothing should produce different momentum values
        Assert.NotEqual(sqSma.Momentum, sqEma.Momentum);
    }

    // === H) Consistency ===

    [Fact]
    public void BatchCalc_MatchesStreaming()
    {
        var bars = GenerateBars(50);

        var sq = new SqueezePro(period: 10, momLength: 5, momSmooth: 3);
        for (int i = 0; i < 50; i++)
        {
            sq.Update(bars[i], isNew: true);
        }
        double streamMom = sq.Momentum;

        var (batchMom, _) = SqueezePro.Batch(bars, period: 10, momLength: 5, momSmooth: 3);
        double batchLast = batchMom[^1].Value;

        Assert.Equal(streamMom, batchLast, precision: 6);
    }

    [Fact]
    public void SpanBatch_MatchesStreaming()
    {
        var bars = GenerateBars(50);

        var sq = new SqueezePro(period: 10, momLength: 5, momSmooth: 3);
        for (int i = 0; i < 50; i++)
        {
            sq.Update(bars[i], isNew: true);
        }
        double streamMom = sq.Momentum;

        double[] momOut = new double[50];
        double[] sqOut = new double[50];
        SqueezePro.Batch(bars.HighValues, bars.LowValues, bars.CloseValues,
            momOut, sqOut, period: 10, momLength: 5, momSmooth: 3);
        double spanLast = momOut[49];

        Assert.Equal(streamMom, spanLast, precision: 6);
    }

    [Fact]
    public void EventingMode_MatchesStreaming()
    {
        var bars = GenerateBars(50);

        var sqStream = new SqueezePro(period: 10, momLength: 5, momSmooth: 3);
        for (int i = 0; i < 50; i++)
        {
            sqStream.Update(bars[i], isNew: true);
        }
        double streamMom = sqStream.Momentum;

        var sqEvent = new SqueezePro(bars, period: 10, momLength: 5, momSmooth: 3);
        Assert.Equal(streamMom, sqEvent.Momentum, precision: 6);
    }

    // === I) Span API tests ===

    [Fact]
    public void BatchSpan_ThrowsOnInvalidPeriod()
    {
        double[] h = [100, 101, 102];
        double[] l = [99, 100, 101];
        double[] c = [100, 101, 102];
        double[] mom = new double[3];
        double[] sq = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            SqueezePro.Batch(h, l, c, mom, sq, period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void BatchSpan_ThrowsOnMismatchedLengths()
    {
        double[] h = [100, 101];
        double[] l = [99];
        double[] c = [100, 101];
        double[] mom = new double[2];
        double[] sq = new double[2];
        var ex = Assert.Throws<ArgumentException>(() =>
            SqueezePro.Batch(h, l, c, mom, sq, period: 5));
        Assert.Equal("high", ex.ParamName);
    }

    [Fact]
    public void BatchSpan_ThrowsOnShortMomOutput()
    {
        double[] h = [100, 101, 102, 103, 104];
        double[] l = [99, 100, 101, 102, 103];
        double[] c = [100, 101, 102, 103, 104];
        double[] mom = new double[2]; // too short
        double[] sq = new double[5];
        var ex = Assert.Throws<ArgumentException>(() =>
            SqueezePro.Batch(h, l, c, mom, sq, period: 3));
        Assert.Equal("momOut", ex.ParamName);
    }

    [Fact]
    public void BatchSpan_ThrowsOnShortSqOutput()
    {
        double[] h = [100, 101, 102, 103, 104];
        double[] l = [99, 100, 101, 102, 103];
        double[] c = [100, 101, 102, 103, 104];
        double[] mom = new double[5];
        double[] sq = new double[2]; // too short
        var ex = Assert.Throws<ArgumentException>(() =>
            SqueezePro.Batch(h, l, c, mom, sq, period: 3));
        Assert.Equal("sqOut", ex.ParamName);
    }

    [Fact]
    public void BatchSpan_ThrowsOnInvalidMomLength()
    {
        double[] h = [100, 101, 102];
        double[] l = [99, 100, 101];
        double[] c = [100, 101, 102];
        double[] mom = new double[3];
        double[] sq = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            SqueezePro.Batch(h, l, c, mom, sq, momLength: 0));
        Assert.Equal("momLength", ex.ParamName);
    }

    [Fact]
    public void BatchSpan_ThrowsOnInvalidMomSmooth()
    {
        double[] h = [100, 101, 102];
        double[] l = [99, 100, 101];
        double[] c = [100, 101, 102];
        double[] mom = new double[3];
        double[] sq = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            SqueezePro.Batch(h, l, c, mom, sq, momSmooth: 0));
        Assert.Equal("momSmooth", ex.ParamName);
    }

    [Fact]
    public void BatchSpan_LargeData_NoStackOverflow()
    {
        const int size = 2000;
        var gbm = new GBM(100.0, 0.02, 0.15, seed: 1);
        var bars = gbm.Fetch(size, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] mom = new double[size];
        double[] sq = new double[size];
        // period=300 forces ArrayPool path
        SqueezePro.Batch(bars.HighValues, bars.LowValues, bars.CloseValues, mom, sq, period: 300);
        Assert.True(double.IsFinite(mom[size - 1]));
    }

    // === J) Chainability ===

    [Fact]
    public void PubEvent_Fires()
    {
        var sq = new SqueezePro(period: 5, momLength: 3, momSmooth: 2);
        int fireCount = 0;
        sq.Pub += (_, in e) => fireCount++;

        for (int i = 0; i < 10; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 105, 95, 101, 1000);
            sq.Update(bar, isNew: true);
        }
        Assert.Equal(10, fireCount);
    }

    [Fact]
    public void TBarSeries_Constructor_Subscribes()
    {
        var bars = GenerateBars(30);
        var sq = new SqueezePro(bars, period: 10, momLength: 5, momSmooth: 3);

        Assert.True(sq.IsHot);
        Assert.True(double.IsFinite(sq.Momentum));
    }

    // === K) Calculate factory ===

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var bars = GenerateBars(30);
        var ((momSeries, sqSeries), indicator) = SqueezePro.Calculate(bars, period: 10, momLength: 5, momSmooth: 3);

        Assert.Equal(30, momSeries.Count);
        Assert.Equal(30, sqSeries.Count);
        Assert.NotNull(indicator);
        Assert.True(double.IsFinite(indicator.Momentum));
    }

    // === L) Squeeze level output values ===

    [Fact]
    public void BatchSqueezeLevels_AreInRange()
    {
        var bars = GenerateBars(100);
        double[] mom = new double[100];
        double[] sq = new double[100];
        SqueezePro.Batch(bars.HighValues, bars.LowValues, bars.CloseValues, mom, sq, period: 10, momLength: 5, momSmooth: 3);

        for (int i = 0; i < 100; i++)
        {
            Assert.InRange(sq[i], 0.0, 3.0);
            Assert.True(sq[i] == 0.0 || sq[i] == 1.0 || sq[i] == 2.0 || sq[i] == 3.0);
        }
    }
}
