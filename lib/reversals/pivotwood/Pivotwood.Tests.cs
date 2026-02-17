// PIVOTWOOD Tests - Woodie's Pivot Points

namespace QuanTAlib.Tests;

// ── A) Constructor Tests ────────────────────────────────────────────
public sealed class PivotwoodConstructorTests
{
    [Fact]
    public void DefaultConstructor_SetsExpectedDefaults()
    {
        var ind = new Pivotwood();
        Assert.Equal("Pivotwood", ind.Name);
        Assert.Equal(2, ind.WarmupPeriod);
        Assert.False(ind.IsHot);
        Assert.True(double.IsNaN(ind.PP));
    }

    [Fact]
    public void SourceConstructor_PrimesFromSource()
    {
        var bars = new TBarSeries();
        var dt = DateTime.UtcNow;
        bars.Add(new TBar(dt, 110, 110, 90, 100, 1000));
        bars.Add(new TBar(dt.AddMinutes(1), 115, 115, 95, 105, 1000));

        var ind = new Pivotwood(bars);
        Assert.True(ind.IsHot);
        Assert.False(double.IsNaN(ind.PP));
    }
}

// ── B) Basic Calculation Tests ──────────────────────────────────────
public sealed class PivotwoodBasicTests
{
    [Fact]
    public void Update_ReturnsTValue()
    {
        var ind = new Pivotwood();
        var bar = new TBar(DateTime.UtcNow, 110, 110, 90, 100, 1000);
        TValue result = ind.Update(bar);
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Last_IsAccessible()
    {
        var ind = new Pivotwood();
        var dt = DateTime.UtcNow;
        ind.Update(new TBar(dt, 110, 110, 90, 100, 1000), isNew: true);
        ind.Update(new TBar(dt.AddMinutes(1), 105, 105, 95, 100, 1000), isNew: true);
        Assert.True(ind.IsHot);
        Assert.True(double.IsFinite(ind.Last.Value));
    }

    [Fact]
    public void KnownValues_WoodieLevels()
    {
        // H=110, L=90, C=100 → PP=(110+90+200)/4=100, range=20
        // R1=2*100-90=110, S1=2*100-110=90
        // R2=100+20=120,   S2=100-20=80
        // R3=110+2*(100-90)=130, S3=90-2*(110-100)=70
        var ind = new Pivotwood();
        var dt = DateTime.UtcNow;
        ind.Update(new TBar(dt, 110, 110, 90, 100, 1000), isNew: true);
        ind.Update(new TBar(dt.AddMinutes(1), 105, 105, 95, 100, 1000), isNew: true);

        Assert.Equal(100.0, ind.PP, 10);
        Assert.Equal(110.0, ind.R1, 10);
        Assert.Equal(90.0, ind.S1, 10);
        Assert.Equal(120.0, ind.R2, 10);
        Assert.Equal(80.0, ind.S2, 10);
        Assert.Equal(130.0, ind.R3, 10);
        Assert.Equal(70.0, ind.S3, 10);
    }

    [Fact]
    public void LevelOrdering_S3_LessThan_S2_LessThan_S1_LessThan_PP_LessThan_R1_LessThan_R2_LessThan_R3()
    {
        var ind = new Pivotwood();
        var dt = DateTime.UtcNow;
        ind.Update(new TBar(dt, 110, 110, 90, 100, 1000), isNew: true);
        ind.Update(new TBar(dt.AddMinutes(1), 105, 105, 95, 100, 1000), isNew: true);

        Assert.True(ind.S3 < ind.S2);
        Assert.True(ind.S2 < ind.S1);
        Assert.True(ind.S1 < ind.PP);
        Assert.True(ind.PP < ind.R1);
        Assert.True(ind.R1 < ind.R2);
        Assert.True(ind.R2 < ind.R3);
    }

    [Fact]
    public void WoodieFormula_CloseWeightedTwice()
    {
        // Verify PP = (H + L + 2C) / 4 (NOT (H+L+C)/3)
        // H=120, L=80, C=110 → PP=(120+80+220)/4 = 420/4 = 105
        // Classic PP would be (120+80+110)/3 = 103.33
        var ind = new Pivotwood();
        var dt = DateTime.UtcNow;
        ind.Update(new TBar(dt, 120, 120, 80, 110, 1000), isNew: true);
        ind.Update(new TBar(dt.AddMinutes(1), 105, 105, 95, 100, 1000), isNew: true);

        Assert.Equal(105.0, ind.PP, 10);
    }

    [Fact]
    public void Name_ReturnsExpectedString()
    {
        var ind = new Pivotwood();
        Assert.Equal("Pivotwood", ind.Name);
    }
}

// ── C) State + Bar Correction Tests ─────────────────────────────────
public sealed class PivotwoodStateCorrectionTests
{
    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var ind = new Pivotwood();
        var dt = DateTime.UtcNow;
        ind.Update(new TBar(dt, 110, 110, 90, 100, 1000), isNew: true);
        ind.Update(new TBar(dt.AddMinutes(1), 115, 115, 95, 105, 1000), isNew: true);
        double pp1 = ind.PP;

        ind.Update(new TBar(dt.AddMinutes(2), 120, 120, 100, 110, 1000), isNew: true);
        double pp2 = ind.PP;

        Assert.NotEqual(pp1, pp2);
    }

    [Fact]
    public void IsNew_False_RewritesCurrentBar()
    {
        var ind = new Pivotwood();
        var dt = DateTime.UtcNow;
        ind.Update(new TBar(dt, 110, 110, 90, 100, 1000), isNew: true);
        ind.Update(new TBar(dt.AddMinutes(1), 115, 115, 95, 105, 1000), isNew: true);
        double pp1 = ind.PP;

        // Correction: rewrite the same bar
        ind.Update(new TBar(dt.AddMinutes(1), 120, 120, 100, 110, 1000), isNew: false);
        double pp2 = ind.PP;

        // PP should be unchanged (still based on previous bar H=110,L=90,C=100)
        Assert.Equal(pp1, pp2, 10);
    }

    [Fact]
    public void IterativeCorrections_RestoreState()
    {
        var ind = new Pivotwood();
        var dt = DateTime.UtcNow;
        ind.Update(new TBar(dt, 110, 110, 90, 100, 1000), isNew: true);
        ind.Update(new TBar(dt.AddMinutes(1), 115, 115, 95, 105, 1000), isNew: true);
        double ppBefore = ind.PP;

        // Apply multiple corrections
        ind.Update(new TBar(dt.AddMinutes(1), 200, 200, 50, 125, 1000), isNew: false);
        ind.Update(new TBar(dt.AddMinutes(1), 300, 300, 10, 155, 1000), isNew: false);
        ind.Update(new TBar(dt.AddMinutes(1), 115, 115, 95, 105, 1000), isNew: false);

        Assert.Equal(ppBefore, ind.PP, 10);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        var ind = new Pivotwood();
        var dt = DateTime.UtcNow;
        ind.Update(new TBar(dt, 110, 110, 90, 100, 1000), isNew: true);
        ind.Update(new TBar(dt.AddMinutes(1), 115, 115, 95, 105, 1000), isNew: true);
        Assert.True(ind.IsHot);

        ind.Reset();
        Assert.False(ind.IsHot);
        Assert.True(double.IsNaN(ind.PP));
        Assert.True(double.IsNaN(ind.R1));
        Assert.True(double.IsNaN(ind.S1));
    }

    [Fact]
    public void Reset_ThenReplay_MatchesOriginal()
    {
        var ind = new Pivotwood();
        var dt = DateTime.UtcNow;
        var bar1 = new TBar(dt, 110, 110, 90, 100, 1000);
        var bar2 = new TBar(dt.AddMinutes(1), 115, 115, 95, 105, 1000);
        var bar3 = new TBar(dt.AddMinutes(2), 120, 120, 100, 110, 1000);

        ind.Update(bar1, isNew: true);
        ind.Update(bar2, isNew: true);
        ind.Update(bar3, isNew: true);
        double ppOriginal = ind.PP;
        double r1Original = ind.R1;
        double s1Original = ind.S1;

        ind.Reset();
        ind.Update(bar1, isNew: true);
        ind.Update(bar2, isNew: true);
        ind.Update(bar3, isNew: true);

        Assert.Equal(ppOriginal, ind.PP, 10);
        Assert.Equal(r1Original, ind.R1, 10);
        Assert.Equal(s1Original, ind.S1, 10);
    }
}

// ── D) Warmup / Convergence Tests ───────────────────────────────────
public sealed class PivotwoodWarmupTests
{
    [Fact]
    public void IsHot_FlipsAfterWarmupPeriod()
    {
        var ind = new Pivotwood();
        var dt = DateTime.UtcNow;

        ind.Update(new TBar(dt, 110, 110, 90, 100, 1000), isNew: true);
        Assert.False(ind.IsHot);

        ind.Update(new TBar(dt.AddMinutes(1), 115, 115, 95, 105, 1000), isNew: true);
        Assert.True(ind.IsHot);
    }

    [Fact]
    public void WarmupPeriod_IsTwo()
    {
        var ind = new Pivotwood();
        Assert.Equal(2, ind.WarmupPeriod);
    }
}

// ── E) Robustness Tests ─────────────────────────────────────────────
public sealed class PivotwoodRobustnessTests
{
    [Fact]
    public void NaN_UsesLastValidValue()
    {
        var ind = new Pivotwood();
        var dt = DateTime.UtcNow;

        ind.Update(new TBar(dt, 110, 110, 90, 100, 1000), isNew: true);
        ind.Update(new TBar(dt.AddMinutes(1), 115, 115, 95, 105, 1000), isNew: true);

        // Feed NaN bar - should use last valid values and still produce valid PP
        ind.Update(new TBar(dt.AddMinutes(2), double.NaN, double.NaN, double.NaN, double.NaN, 0), isNew: true);
        Assert.False(double.IsNaN(ind.PP));
    }

    [Fact]
    public void Infinity_UsesLastValidValue()
    {
        var ind = new Pivotwood();
        var dt = DateTime.UtcNow;

        ind.Update(new TBar(dt, 110, 110, 90, 100, 1000), isNew: true);
        ind.Update(new TBar(dt.AddMinutes(1), 115, 115, 95, 105, 1000), isNew: true);

        ind.Update(new TBar(dt.AddMinutes(2), double.PositiveInfinity, double.PositiveInfinity,
            double.NegativeInfinity, double.PositiveInfinity, 0), isNew: true);
        Assert.False(double.IsNaN(ind.PP));
        Assert.True(double.IsFinite(ind.PP));
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.20, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ind = new Pivotwood();
        for (int i = 0; i < bars.Count; i++)
        {
            ind.Update(bars[i], isNew: true);
        }
        Assert.True(ind.IsHot);
        Assert.False(double.IsNaN(ind.PP));
    }
}

// ── F) Consistency Tests ────────────────────────────────────────────
public sealed class PivotwoodConsistencyTests
{
    private static TBarSeries CreateGbmBars(int count = 500)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.20, seed: 42);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Streaming_Matches_Batch()
    {
        var bars = CreateGbmBars();

        // Streaming
        var ind = new Pivotwood();
        var streamResults = new List<double>(bars.Count);
        for (int i = 0; i < bars.Count; i++)
        {
            ind.Update(bars[i], isNew: true);
            streamResults.Add(ind.PP);
        }

        // Batch
        var batchResult = Pivotwood.Batch(bars);

        Assert.Equal(bars.Count, batchResult.Count);
        for (int i = 0; i < bars.Count; i++)
        {
            if (double.IsNaN(streamResults[i]) && double.IsNaN(batchResult[i].Value))
            {
                continue;
            }
            Assert.Equal(streamResults[i], batchResult[i].Value, 10);
        }
    }

    [Fact]
    public void Streaming_Matches_Span()
    {
        var bars = CreateGbmBars();

        // Streaming
        var ind = new Pivotwood();
        var streamResults = new List<double>(bars.Count);
        for (int i = 0; i < bars.Count; i++)
        {
            ind.Update(bars[i], isNew: true);
            streamResults.Add(ind.PP);
        }

        // Span
        int len = bars.Count;
        var ppOut = new double[len];
        Pivotwood.Batch(bars.HighValues, bars.LowValues, bars.CloseValues, ppOut);

        for (int i = 0; i < len; i++)
        {
            if (double.IsNaN(streamResults[i]) && double.IsNaN(ppOut[i]))
            {
                continue;
            }
            Assert.Equal(streamResults[i], ppOut[i], 10);
        }
    }

    [Fact]
    public void Streaming_Matches_BatchAll()
    {
        var bars = CreateGbmBars();

        // Streaming - collect all 7 levels
        var ind = new Pivotwood();
        var sPP = new List<double>(bars.Count);
        var sR1 = new List<double>(bars.Count);
        var sS1 = new List<double>(bars.Count);
        var sR2 = new List<double>(bars.Count);
        var sS2 = new List<double>(bars.Count);
        var sR3 = new List<double>(bars.Count);
        var sS3 = new List<double>(bars.Count);
        for (int i = 0; i < bars.Count; i++)
        {
            ind.Update(bars[i], isNew: true);
            sPP.Add(ind.PP);
            sR1.Add(ind.R1);
            sS1.Add(ind.S1);
            sR2.Add(ind.R2);
            sS2.Add(ind.S2);
            sR3.Add(ind.R3);
            sS3.Add(ind.S3);
        }

        // BatchAll
        int len = bars.Count;
        var ppOut = new double[len];
        var r1Out = new double[len];
        var s1Out = new double[len];
        var r2Out = new double[len];
        var s2Out = new double[len];
        var r3Out = new double[len];
        var s3Out = new double[len];

        Pivotwood.BatchAll(
            bars.HighValues, bars.LowValues, bars.CloseValues,
            ppOut, r1Out, s1Out, r2Out, s2Out, r3Out, s3Out);

        for (int i = 0; i < len; i++)
        {
            if (double.IsNaN(sPP[i])) { Assert.True(double.IsNaN(ppOut[i])); continue; }
            Assert.Equal(sPP[i], ppOut[i], 10);
            Assert.Equal(sR1[i], r1Out[i], 10);
            Assert.Equal(sS1[i], s1Out[i], 10);
            Assert.Equal(sR2[i], r2Out[i], 10);
            Assert.Equal(sS2[i], s2Out[i], 10);
            Assert.Equal(sR3[i], r3Out[i], 10);
            Assert.Equal(sS3[i], s3Out[i], 10);
        }
    }

    [Fact]
    public void EventBased_MatchesStreaming()
    {
        var bars = CreateGbmBars();

        // Streaming
        var ind1 = new Pivotwood();
        var streamResults = new List<double>(bars.Count);
        for (int i = 0; i < bars.Count; i++)
        {
            ind1.Update(bars[i], isNew: true);
            streamResults.Add(ind1.PP);
        }

        // Event-based via Update(TBarSeries)
        var ind2 = new Pivotwood();
        var batchTSeries = ind2.Update(bars);

        for (int i = 0; i < bars.Count; i++)
        {
            if (double.IsNaN(streamResults[i]) && double.IsNaN(batchTSeries[i].Value))
            {
                continue;
            }
            Assert.Equal(streamResults[i], batchTSeries[i].Value, 10);
        }
    }
}

// ── G) Span API Tests ───────────────────────────────────────────────
public sealed class PivotwoodSpanTests
{
    [Fact]
    public void Batch_MismatchedInputLengths_Throws()
    {
        var high = new double[10];
        var low = new double[9];
        var close = new double[10];
        var output = new double[10];
        Assert.Throws<ArgumentException>(() => Pivotwood.Batch(high, low, close, output));
    }

    [Fact]
    public void Batch_OutputTooShort_Throws()
    {
        var high = new double[10];
        var low = new double[10];
        var close = new double[10];
        var output = new double[5];
        Assert.Throws<ArgumentException>(() => Pivotwood.Batch(high, low, close, output));
    }

    [Fact]
    public void BatchAll_MismatchedInputLengths_Throws()
    {
        var high = new double[10];
        var low = new double[9];
        var close = new double[10];
        var pp = new double[10];
        var r1 = new double[10];
        var s1 = new double[10];
        var r2 = new double[10];
        var s2 = new double[10];
        var r3 = new double[10];
        var s3 = new double[10];
        Assert.Throws<ArgumentException>(() => Pivotwood.BatchAll(high, low, close, pp, r1, s1, r2, s2, r3, s3));
    }

    [Fact]
    public void BatchAll_OutputTooShort_Throws()
    {
        var high = new double[10];
        var low = new double[10];
        var close = new double[10];
        var pp = new double[5];
        var r1 = new double[10];
        var s1 = new double[10];
        var r2 = new double[10];
        var s2 = new double[10];
        var r3 = new double[10];
        var s3 = new double[10];
        Assert.Throws<ArgumentException>(() => Pivotwood.BatchAll(high, low, close, pp, r1, s1, r2, s2, r3, s3));
    }
}

// ── H) Event / Chainability Tests ───────────────────────────────────
public sealed class PivotwoodEventTests
{
    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var ind = new Pivotwood();
        int fireCount = 0;
        ind.Pub += (object? _, in TValueEventArgs _e) => { fireCount++; };

        var dt = DateTime.UtcNow;
        ind.Update(new TBar(dt, 110, 110, 90, 100, 1000), isNew: true);
        ind.Update(new TBar(dt.AddMinutes(1), 115, 115, 95, 105, 1000), isNew: true);

        Assert.Equal(2, fireCount);
    }

    [Fact]
    public void EventChaining_Works()
    {
        var bars = new TBarSeries();
        var ind = new Pivotwood(bars);

        var receivedValues = new List<double>();
        ind.Pub += (object? _, in TValueEventArgs e) => { receivedValues.Add(e.Value.Value); };

        var dt = DateTime.UtcNow;
        bars.Add(new TBar(dt, 110, 110, 90, 100, 1000), isNew: true);
        bars.Add(new TBar(dt.AddMinutes(1), 115, 115, 95, 105, 1000), isNew: true);

        Assert.True(receivedValues.Count >= 2);
    }
}

// ── I) Prime Tests ──────────────────────────────────────────────────
public sealed class PivotwoodPrimeTests
{
    [Fact]
    public void Prime_TBarSeries_SetsState()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.20, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ind = new Pivotwood();
        ind.Prime(bars);
        Assert.True(ind.IsHot);
        Assert.False(double.IsNaN(ind.PP));
    }

    [Fact]
    public void Prime_ReadOnlySpan_SetsState()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.20, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var values = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            values[i] = bars[i].Close;
        }

        var ind = new Pivotwood();
        ind.Prime(values);
        Assert.True(ind.IsHot);
        Assert.False(double.IsNaN(ind.PP));
    }
}
