// PIVOTDEM Tests - DeMark Pivot Points

namespace QuanTAlib.Tests;

// -- A) Constructor Validation ------------------------------------------------
public sealed class PivotdemConstructorTests
{
    [Fact]
    public void Constructor_Default_SetsProperties()
    {
        var p = new Pivotdem();

        Assert.Equal(2, p.WarmupPeriod);
        Assert.Contains("Pivotdem", p.Name, StringComparison.Ordinal);
        Assert.False(p.IsHot);
    }

    [Fact]
    public void Constructor_InitialState_AllNaN()
    {
        var p = new Pivotdem();

        Assert.True(double.IsNaN(p.PP));
        Assert.True(double.IsNaN(p.R1));
        Assert.True(double.IsNaN(p.S1));
    }
}

// -- B) Basic Calculation -----------------------------------------------------
public sealed class PivotdemBasicTests
{
    [Fact]
    public void Update_ReturnsTValue()
    {
        var p = new Pivotdem();
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000);

        TValue result = p.Update(bar);

        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var p = new Pivotdem();
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000);

        _ = p.Update(bar);

        Assert.True(double.IsFinite(p.Last.Value) || double.IsNaN(p.Last.Value));
    }

    [Fact]
    public void Update_KnownValues_Bullish_CorrectLevels()
    {
        // Previous bar: O=100, H=110, L=90, C=105 => C>O (bullish)
        // x = 2*H + L + C = 220 + 90 + 105 = 415
        // PP = 415/4 = 103.75
        // R1 = 415/2 - L = 207.5 - 90 = 117.5
        // S1 = 415/2 - H = 207.5 - 110 = 97.5
        var p = new Pivotdem();
        var dt = DateTime.UtcNow;

        // First bar: stores OHLC, no output yet
        _ = p.Update(new TBar(dt, 100, 110, 90, 105, 1000), isNew: true);
        Assert.True(double.IsNaN(p.PP));

        // Second bar: computes from first bar's OHLC
        _ = p.Update(new TBar(dt.AddMinutes(1), 105, 115, 95, 108, 1000), isNew: true);

        Assert.Equal(103.75, p.PP, precision: 10);
        Assert.Equal(117.5, p.R1, precision: 10);
        Assert.Equal(97.5, p.S1, precision: 10);
    }

    [Fact]
    public void Update_KnownValues_Bearish_CorrectLevels()
    {
        // Previous bar: O=105, H=110, L=90, C=100 => C<O (bearish)
        // x = H + 2*L + C = 110 + 180 + 100 = 390
        // PP = 390/4 = 97.5
        // R1 = 390/2 - L = 195 - 90 = 105
        // S1 = 390/2 - H = 195 - 110 = 85
        var p = new Pivotdem();
        var dt = DateTime.UtcNow;

        _ = p.Update(new TBar(dt, 105, 110, 90, 100, 1000), isNew: true);
        _ = p.Update(new TBar(dt.AddMinutes(1), 100, 115, 95, 102, 1000), isNew: true);

        Assert.Equal(97.5, p.PP, precision: 10);
        Assert.Equal(105.0, p.R1, precision: 10);
        Assert.Equal(85.0, p.S1, precision: 10);
    }

    [Fact]
    public void Update_KnownValues_Doji_CorrectLevels()
    {
        // Previous bar: O=100, H=110, L=90, C=100 => C==O (doji)
        // x = H + L + 2*C = 110 + 90 + 200 = 400
        // PP = 400/4 = 100
        // R1 = 400/2 - L = 200 - 90 = 110
        // S1 = 400/2 - H = 200 - 110 = 90
        var p = new Pivotdem();
        var dt = DateTime.UtcNow;

        _ = p.Update(new TBar(dt, 100, 110, 90, 100, 1000), isNew: true);
        _ = p.Update(new TBar(dt.AddMinutes(1), 100, 115, 95, 102, 1000), isNew: true);

        Assert.Equal(100.0, p.PP, precision: 10);
        Assert.Equal(110.0, p.R1, precision: 10);
        Assert.Equal(90.0, p.S1, precision: 10);
    }

    [Fact]
    public void Update_LevelsHaveCorrectOrdering()
    {
        // For a normal bar with H > L, S1 < PP < R1
        var p = new Pivotdem();
        var dt = DateTime.UtcNow;

        _ = p.Update(new TBar(dt, 100, 110, 90, 100, 1000), isNew: true);
        _ = p.Update(new TBar(dt.AddMinutes(1), 105, 115, 95, 105, 1000), isNew: true);

        Assert.True(p.S1 < p.PP);
        Assert.True(p.PP < p.R1);
    }

    [Fact]
    public void Name_ContainsPivotdem()
    {
        var p = new Pivotdem();
        Assert.Contains("Pivotdem", p.Name, StringComparison.Ordinal);
    }
}

// -- C) State + Bar Correction ------------------------------------------------
public sealed class PivotdemStateCorrectionTests
{
    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var p = new Pivotdem();

        _ = p.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000), isNew: true);
        var first = p.Last;

        _ = p.Update(new TBar(DateTime.UtcNow.AddMinutes(1), 105, 115, 95, 105, 1000), isNew: true);
        var second = p.Last;

        Assert.NotEqual(first.Time, second.Time);
    }

    [Fact]
    public void IsNew_False_CorrectionRestoresState()
    {
        var p = new Pivotdem();
        var dt = DateTime.UtcNow;

        // Two bars: first stores OHLC, second computes
        _ = p.Update(new TBar(dt, 100, 110, 90, 105, 1000), isNew: true);
        _ = p.Update(new TBar(dt.AddMinutes(1), 105, 115, 95, 105, 1000), isNew: true);
        double ppBefore = p.PP;

        // Correct the second bar (isNew=false)
        _ = p.Update(new TBar(dt.AddMinutes(1), 108, 118, 92, 108, 1000), isNew: false);

        // PP should still be based on bar 0's OHLC
        Assert.Equal(ppBefore, p.PP, precision: 10);
    }

    [Fact]
    public void IterativeCorrections_ProduceSameResult()
    {
        var p = new Pivotdem();
        var dt = DateTime.UtcNow;

        _ = p.Update(new TBar(dt, 100, 110, 90, 105, 1000), isNew: true);
        _ = p.Update(new TBar(dt.AddMinutes(1), 105, 115, 95, 105, 1000), isNew: true);

        double[] ppResults = new double[3];
        for (int i = 0; i < 3; i++)
        {
            _ = p.Update(new TBar(dt.AddMinutes(1), 108, 120, 88, 110, 1000), isNew: false);
            ppResults[i] = p.PP;
        }

        Assert.Equal(ppResults[0], ppResults[1]);
        Assert.Equal(ppResults[1], ppResults[2]);
    }

    [Fact]
    public void IsNew_False_AllLevelsStable()
    {
        var p = new Pivotdem();
        var dt = DateTime.UtcNow;

        _ = p.Update(new TBar(dt, 100, 110, 90, 105, 1000), isNew: true);
        _ = p.Update(new TBar(dt.AddMinutes(1), 105, 115, 95, 105, 1000), isNew: true);

        _ = p.Update(new TBar(dt.AddMinutes(1), 108, 120, 88, 110, 1000), isNew: false);
        double r1a = p.R1, s1a = p.S1;

        _ = p.Update(new TBar(dt.AddMinutes(1), 108, 120, 88, 110, 1000), isNew: false);
        Assert.Equal(r1a, p.R1);
        Assert.Equal(s1a, p.S1);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        var p = new Pivotdem();
        var dt = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            double price = 100.0 + i;
            _ = p.Update(new TBar(dt.AddMinutes(i), price, price + 5, price - 5, price + 1, 1000));
        }

        Assert.True(p.IsHot);

        p.Reset();

        Assert.False(p.IsHot);
        Assert.True(double.IsNaN(p.PP));
        Assert.True(double.IsNaN(p.R1));
        Assert.True(double.IsNaN(p.S1));
    }
}

// -- D) Warmup / Convergence --------------------------------------------------
public sealed class PivotdemWarmupTests
{
    [Fact]
    public void IsHot_FlipsAfterWarmup()
    {
        var p = new Pivotdem();

        // First bar - not hot
        _ = p.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));
        Assert.False(p.IsHot, "Should not be hot after 1 bar");

        // Second bar - should be hot
        _ = p.Update(new TBar(DateTime.UtcNow.AddMinutes(1), 105, 115, 95, 105, 1000));
        Assert.True(p.IsHot, "Should be hot after 2 bars");
    }

    [Fact]
    public void WarmupPeriod_Equals2()
    {
        var p = new Pivotdem();
        Assert.Equal(2, p.WarmupPeriod);
    }
}

// -- E) Robustness ------------------------------------------------------------
public sealed class PivotdemRobustnessTests
{
    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var p = new Pivotdem();
        var dt = DateTime.UtcNow;

        // Feed valid bars
        _ = p.Update(new TBar(dt, 100, 110, 90, 105, 1000), isNew: true);
        _ = p.Update(new TBar(dt.AddMinutes(1), 105, 115, 95, 105, 1000), isNew: true);

        Assert.True(p.IsHot);

        // Feed NaN bar
        _ = p.Update(new TBar(dt.AddMinutes(2), double.NaN, double.NaN, double.NaN, double.NaN, 0), isNew: true);

        // Should still be hot and produce valid pivots from last-valid values
        Assert.True(p.IsHot);
        Assert.True(double.IsFinite(p.PP));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var p = new Pivotdem();
        var dt = DateTime.UtcNow;

        _ = p.Update(new TBar(dt, 100, 110, 90, 105, 1000), isNew: true);
        _ = p.Update(new TBar(dt.AddMinutes(1), 105, 115, 95, 105, 1000), isNew: true);

        _ = p.Update(new TBar(dt.AddMinutes(2),
            double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity, 0),
            isNew: true);

        Assert.True(p.IsHot);
        Assert.True(double.IsFinite(p.PP));
    }

    [Fact]
    public void FirstBar_NaN_ReturnsNaN()
    {
        var p = new Pivotdem();

        _ = p.Update(new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 0));

        Assert.True(double.IsNaN(p.Last.Value));
        Assert.True(double.IsNaN(p.PP));
    }
}

// -- F) Consistency -----------------------------------------------------------
public sealed class PivotdemConsistencyTests
{
    private static TBarSeries CreateGbmBars(int count = 500)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.20, seed: 42);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Streaming_MatchesBatch()
    {
        var bars = CreateGbmBars();

        // Streaming
        var streaming = new Pivotdem();
        var streamPP = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamPP[i] = streaming.PP;
        }

        // Batch
        var batchResults = Pivotdem.Batch(bars);

        for (int i = 1; i < bars.Count; i++)
        {
            if (double.IsNaN(streamPP[i]))
            {
                Assert.True(double.IsNaN(batchResults[i].Value), $"Mismatch at {i}");
            }
            else
            {
                Assert.Equal(streamPP[i], batchResults[i].Value, precision: 10);
            }
        }
    }

    [Fact]
    public void Streaming_MatchesSpan()
    {
        var bars = CreateGbmBars();

        // Streaming
        var streaming = new Pivotdem();
        var streamPP = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamPP[i] = streaming.PP;
        }

        // Span
        var spanPP = new double[bars.Count];
        Pivotdem.Batch(bars.OpenValues, bars.HighValues, bars.LowValues, bars.CloseValues, spanPP);

        for (int i = 1; i < bars.Count; i++)
        {
            if (double.IsNaN(streamPP[i]))
            {
                Assert.True(double.IsNaN(spanPP[i]), $"PP mismatch at {i}");
            }
            else
            {
                Assert.Equal(streamPP[i], spanPP[i], precision: 10);
            }
        }
    }

    [Fact]
    public void Streaming_MatchesBatchAll_AllLevels()
    {
        var bars = CreateGbmBars(count: 200);

        // Streaming
        var streaming = new Pivotdem();
        var sPP = new double[bars.Count];
        var sR1 = new double[bars.Count];
        var sS1 = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            sPP[i] = streaming.PP;
            sR1[i] = streaming.R1;
            sS1[i] = streaming.S1;
        }

        // BatchAll
        var bPP = new double[bars.Count];
        var bR1 = new double[bars.Count];
        var bS1 = new double[bars.Count];

        Pivotdem.BatchAll(bars.OpenValues, bars.HighValues, bars.LowValues, bars.CloseValues,
            bPP, bR1, bS1);

        for (int i = 1; i < bars.Count; i++)
        {
            if (double.IsNaN(sPP[i])) { Assert.True(double.IsNaN(bPP[i])); continue; }

            Assert.Equal(sPP[i], bPP[i], precision: 10);
            Assert.Equal(sR1[i], bR1[i], precision: 10);
            Assert.Equal(sS1[i], bS1[i], precision: 10);
        }
    }

    [Fact]
    public void TValue_Update_MatchesTBar_Update()
    {
        var p1 = new Pivotdem();
        var p2 = new Pivotdem();

        double[] prices = [100, 102, 98, 105, 99, 103, 107, 95, 110, 108];

        for (int i = 0; i < prices.Length; i++)
        {
            double pr = prices[i];
            _ = p1.Update(new TBar(DateTime.UtcNow.AddMinutes(i), pr, pr, pr, pr, 0), isNew: true);
            _ = p2.Update(new TValue(DateTime.UtcNow.AddMinutes(i), pr), isNew: true);
        }

        Assert.Equal(p1.PP, p2.PP);
        Assert.Equal(p1.R1, p2.R1);
        Assert.Equal(p1.S1, p2.S1);
    }
}

// -- G) Span API Tests --------------------------------------------------------
public sealed class PivotdemSpanTests
{
    [Fact]
    public void Batch_Span_MismatchedLengths_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Pivotdem.Batch(new double[10], new double[5], new double[10], new double[10], new double[10]));
        Assert.Equal("high", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputTooShort_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Pivotdem.Batch(new double[10], new double[10], new double[10], new double[10], new double[5]));
        Assert.Equal("ppOutput", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_Empty_NoException()
    {
        var ex = Record.Exception(() =>
            Pivotdem.Batch(ReadOnlySpan<double>.Empty, ReadOnlySpan<double>.Empty,
                ReadOnlySpan<double>.Empty, ReadOnlySpan<double>.Empty, Span<double>.Empty));
        Assert.Null(ex);
    }

    [Fact]
    public void BatchAll_OutputTooShort_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Pivotdem.BatchAll(new double[10], new double[10], new double[10], new double[10],
                new double[10], new double[5], new double[10]));
        Assert.Equal("r1Out", ex.ParamName);
    }
}

// -- H) Event / Chainability -------------------------------------------------
public sealed class PivotdemEventTests
{
    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var p = new Pivotdem();
        int fireCount = 0;

        p.Pub += (object? _, in TValueEventArgs _e) => { fireCount++; };

        _ = p.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));

        Assert.Equal(1, fireCount);
    }

    [Fact]
    public void Pub_FiresOnEachUpdate()
    {
        var p = new Pivotdem();
        int fireCount = 0;

        p.Pub += (object? _, in TValueEventArgs _e) => { fireCount++; };

        for (int i = 0; i < 5; i++)
        {
            double price = 100.0 + i;
            _ = p.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 5, price - 5, price + 1, 1000));
        }

        Assert.Equal(5, fireCount);
    }
}

// -- I) Prime Tests -----------------------------------------------------------
public sealed class PivotdemPrimeTests
{
    [Fact]
    public void Prime_TBarSeries_SetsState()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.20, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var p = new Pivotdem();
        p.Prime(bars);

        Assert.True(p.IsHot);
    }

    [Fact]
    public void Prime_EmptySource_NoException()
    {
        var p = new Pivotdem();
        var bars = new TBarSeries();

        var ex = Record.Exception(() => p.Prime(bars));
        Assert.Null(ex);
        Assert.False(p.IsHot);
    }
}
