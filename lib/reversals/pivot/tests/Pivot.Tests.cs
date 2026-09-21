// PIVOT Tests - Classic Pivot Points (Floor Trader Pivots)

namespace QuanTAlib.Tests;

// -- A) Constructor Validation ------------------------------------------------
public sealed class PivotConstructorTests
{
    [Fact]
    public void Constructor_Default_SetsProperties()
    {
        var p = new Pivot();

        Assert.Equal(2, p.WarmupPeriod);
        Assert.Contains("Pivot", p.Name, StringComparison.Ordinal);
        Assert.False(p.IsHot);
    }

    [Fact]
    public void Constructor_InitialState_AllNaN()
    {
        var p = new Pivot();

        Assert.True(double.IsNaN(p.PP));
        Assert.True(double.IsNaN(p.R1));
        Assert.True(double.IsNaN(p.R2));
        Assert.True(double.IsNaN(p.R3));
        Assert.True(double.IsNaN(p.S1));
        Assert.True(double.IsNaN(p.S2));
        Assert.True(double.IsNaN(p.S3));
    }
}

// -- B) Basic Calculation -----------------------------------------------------
public sealed class PivotBasicTests
{
    [Fact]
    public void Update_ReturnsTValue()
    {
        var p = new Pivot();
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000);

        TValue result = p.Update(bar);

        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var p = new Pivot();
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000);

        _ = p.Update(bar);

        Assert.True(double.IsFinite(p.Last.Value) || double.IsNaN(p.Last.Value));
    }

    [Fact]
    public void Update_KnownValues_CorrectPivotLevels()
    {
        // Given previous bar H=110, L=90, C=100
        // PP = (110+90+100)/3 = 100
        // R1 = 2*100-90 = 110, S1 = 2*100-110 = 90
        // R2 = 100+(110-90) = 120, S2 = 100-(110-90) = 80
        // R3 = 110+2*(100-90) = 130, S3 = 90-2*(110-100) = 70
        var p = new Pivot();
        var dt = DateTime.UtcNow;

        // First bar: stores HLC, no output yet
        _ = p.Update(new TBar(dt, 100, 110, 90, 100, 1000), isNew: true);
        Assert.True(double.IsNaN(p.PP));

        // Second bar: computes from first bar's HLC
        _ = p.Update(new TBar(dt.AddMinutes(1), 105, 115, 95, 105, 1000), isNew: true);

        Assert.Equal(100.0, p.PP, precision: 10);
        Assert.Equal(110.0, p.R1, precision: 10);
        Assert.Equal(90.0, p.S1, precision: 10);
        Assert.Equal(120.0, p.R2, precision: 10);
        Assert.Equal(80.0, p.S2, precision: 10);
        Assert.Equal(130.0, p.R3, precision: 10);
        Assert.Equal(70.0, p.S3, precision: 10);
    }

    [Fact]
    public void Update_SecondKnownValues_CorrectPivotLevels()
    {
        // Given previous bar H=120, L=100, C=115
        // PP = (120+100+115)/3 = 111.666...
        // R1 = 2*111.666... - 100 = 123.333...
        // S1 = 2*111.666... - 120 = 103.333...
        // R2 = 111.666... + 20 = 131.666...
        // S2 = 111.666... - 20 = 91.666...
        // R3 = 120 + 2*(111.666...-100) = 143.333...
        // S3 = 100 - 2*(120-111.666...) = 83.333...
        var p = new Pivot();
        var dt = DateTime.UtcNow;

        _ = p.Update(new TBar(dt, 110, 120, 100, 115, 1000), isNew: true);
        _ = p.Update(new TBar(dt.AddMinutes(1), 115, 125, 105, 120, 1000), isNew: true);

        double expectedPP = (120.0 + 100.0 + 115.0) / 3.0;
        Assert.Equal(expectedPP, p.PP, precision: 10);
        Assert.Equal((2.0 * expectedPP) - 100.0, p.R1, precision: 10);
        Assert.Equal((2.0 * expectedPP) - 120.0, p.S1, precision: 10);
        Assert.Equal(expectedPP + 20.0, p.R2, precision: 10);
        Assert.Equal(expectedPP - 20.0, p.S2, precision: 10);
        Assert.Equal(120.0 + (2.0 * (expectedPP - 100.0)), p.R3, precision: 10);
        Assert.Equal(100.0 - (2.0 * (120.0 - expectedPP)), p.S3, precision: 10);
    }

    [Fact]
    public void Update_LevelsHaveCorrectOrdering()
    {
        // For any normal bar, S3 < S2 < S1 < PP < R1 < R2 < R3
        var p = new Pivot();
        var dt = DateTime.UtcNow;

        _ = p.Update(new TBar(dt, 100, 110, 90, 100, 1000), isNew: true);
        _ = p.Update(new TBar(dt.AddMinutes(1), 105, 115, 95, 105, 1000), isNew: true);

        Assert.True(p.S3 < p.S2);
        Assert.True(p.S2 < p.S1);
        Assert.True(p.S1 < p.PP);
        Assert.True(p.PP < p.R1);
        Assert.True(p.R1 < p.R2);
        Assert.True(p.R2 < p.R3);
    }

    [Fact]
    public void Name_ContainsPivot()
    {
        var p = new Pivot();
        Assert.Contains("Pivot", p.Name, StringComparison.Ordinal);
    }
}

// -- C) State + Bar Correction ------------------------------------------------
public sealed class PivotStateCorrectionTests
{
    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var p = new Pivot();

        _ = p.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000), isNew: true);
        var first = p.Last;

        _ = p.Update(new TBar(DateTime.UtcNow.AddMinutes(1), 105, 115, 95, 105, 1000), isNew: true);
        var second = p.Last;

        Assert.NotEqual(first.Time, second.Time);
    }

    [Fact]
    public void IsNew_False_CorrectionRestoresState()
    {
        var p = new Pivot();
        var dt = DateTime.UtcNow;

        // Two bars: first stores HLC, second computes
        _ = p.Update(new TBar(dt, 100, 110, 90, 100, 1000), isNew: true);
        _ = p.Update(new TBar(dt.AddMinutes(1), 105, 115, 95, 105, 1000), isNew: true);
        double ppBefore = p.PP;

        // Correct the second bar (isNew=false)
        _ = p.Update(new TBar(dt.AddMinutes(1), 108, 118, 92, 108, 1000), isNew: false);

        // PP should still be based on bar 0's HLC (H=110, L=90, C=100)
        // since isNew=false doesn't change the "previous" bar
        Assert.Equal(ppBefore, p.PP, precision: 10);
    }

    [Fact]
    public void IterativeCorrections_ProduceSameResult()
    {
        var p = new Pivot();
        var dt = DateTime.UtcNow;

        _ = p.Update(new TBar(dt, 100, 110, 90, 100, 1000), isNew: true);
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
        var p = new Pivot();
        var dt = DateTime.UtcNow;

        _ = p.Update(new TBar(dt, 100, 110, 90, 100, 1000), isNew: true);
        _ = p.Update(new TBar(dt.AddMinutes(1), 105, 115, 95, 105, 1000), isNew: true);

        _ = p.Update(new TBar(dt.AddMinutes(1), 108, 120, 88, 110, 1000), isNew: false);
        double r1a = p.R1, s1a = p.S1, r2a = p.R2, s2a = p.S2, r3a = p.R3, s3a = p.S3;

        _ = p.Update(new TBar(dt.AddMinutes(1), 108, 120, 88, 110, 1000), isNew: false);
        Assert.Equal(r1a, p.R1);
        Assert.Equal(s1a, p.S1);
        Assert.Equal(r2a, p.R2);
        Assert.Equal(s2a, p.S2);
        Assert.Equal(r3a, p.R3);
        Assert.Equal(s3a, p.S3);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        var p = new Pivot();
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
        Assert.True(double.IsNaN(p.R2));
        Assert.True(double.IsNaN(p.R3));
        Assert.True(double.IsNaN(p.S1));
        Assert.True(double.IsNaN(p.S2));
        Assert.True(double.IsNaN(p.S3));
    }
}

// -- D) Warmup / Convergence --------------------------------------------------
public sealed class PivotWarmupTests
{
    [Fact]
    public void IsHot_FlipsAfterWarmup()
    {
        var p = new Pivot();

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
        var p = new Pivot();
        Assert.Equal(2, p.WarmupPeriod);
    }
}

// -- E) Robustness ------------------------------------------------------------
public sealed class PivotRobustnessTests
{
    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var p = new Pivot();
        var dt = DateTime.UtcNow;

        // Feed valid bars
        _ = p.Update(new TBar(dt, 100, 110, 90, 100, 1000), isNew: true);
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
        var p = new Pivot();
        var dt = DateTime.UtcNow;

        _ = p.Update(new TBar(dt, 100, 110, 90, 100, 1000), isNew: true);
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
        var p = new Pivot();

        _ = p.Update(new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 0));

        Assert.True(double.IsNaN(p.Last.Value));
        Assert.True(double.IsNaN(p.PP));
    }
}

// -- F) Consistency -----------------------------------------------------------
public sealed class PivotConsistencyTests
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
        var streaming = new Pivot();
        var streamPP = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamPP[i] = streaming.PP;
        }

        // Batch
        var batchResults = Pivot.Batch(bars);

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
        var streaming = new Pivot();
        var streamPP = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamPP[i] = streaming.PP;
        }

        // Span
        var spanPP = new double[bars.Count];
        Pivot.Batch(bars.HighValues, bars.LowValues, bars.CloseValues, spanPP);

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
        var streaming = new Pivot();
        var sPP = new double[bars.Count];
        var sR1 = new double[bars.Count];
        var sS1 = new double[bars.Count];
        var sR2 = new double[bars.Count];
        var sS2 = new double[bars.Count];
        var sR3 = new double[bars.Count];
        var sS3 = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            sPP[i] = streaming.PP;
            sR1[i] = streaming.R1;
            sS1[i] = streaming.S1;
            sR2[i] = streaming.R2;
            sS2[i] = streaming.S2;
            sR3[i] = streaming.R3;
            sS3[i] = streaming.S3;
        }

        // BatchAll
        var bPP = new double[bars.Count];
        var bR1 = new double[bars.Count];
        var bS1 = new double[bars.Count];
        var bR2 = new double[bars.Count];
        var bS2 = new double[bars.Count];
        var bR3 = new double[bars.Count];
        var bS3 = new double[bars.Count];

        Pivot.BatchAll(bars.HighValues, bars.LowValues, bars.CloseValues,
            bPP, bR1, bS1, bR2, bS2, bR3, bS3);

        for (int i = 1; i < bars.Count; i++)
        {
            if (double.IsNaN(sPP[i])) { Assert.True(double.IsNaN(bPP[i])); continue; }

            Assert.Equal(sPP[i], bPP[i], precision: 10);
            Assert.Equal(sR1[i], bR1[i], precision: 10);
            Assert.Equal(sS1[i], bS1[i], precision: 10);
            Assert.Equal(sR2[i], bR2[i], precision: 10);
            Assert.Equal(sS2[i], bS2[i], precision: 10);
            Assert.Equal(sR3[i], bR3[i], precision: 10);
            Assert.Equal(sS3[i], bS3[i], precision: 10);
        }
    }

    [Fact]
    public void TValue_Update_MatchesTBar_Update()
    {
        var p1 = new Pivot();
        var p2 = new Pivot();

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
public sealed class PivotSpanTests
{
    [Fact]
    public void Batch_Span_MismatchedLengths_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Pivot.Batch(new double[10], new double[5], new double[10], new double[10]));
        Assert.Equal("high", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputTooShort_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Pivot.Batch(new double[10], new double[10], new double[10], new double[5]));
        Assert.Equal("ppOutput", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_Empty_NoException()
    {
        var ex = Record.Exception(() =>
            Pivot.Batch(ReadOnlySpan<double>.Empty, ReadOnlySpan<double>.Empty,
                ReadOnlySpan<double>.Empty, Span<double>.Empty));
        Assert.Null(ex);
    }

    [Fact]
    public void BatchAll_OutputTooShort_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Pivot.BatchAll(new double[10], new double[10], new double[10],
                new double[10], new double[5], new double[10],
                new double[10], new double[10], new double[10], new double[10]));
        Assert.Equal("r1Out", ex.ParamName);
    }
}

// -- H) Event / Chainability -------------------------------------------------
public sealed class PivotEventTests
{
    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var p = new Pivot();
        int fireCount = 0;

        p.Pub += (object? _, in TValueEventArgs _e) => { fireCount++; };

        _ = p.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));

        Assert.Equal(1, fireCount);
    }

    [Fact]
    public void Pub_FiresOnEachUpdate()
    {
        var p = new Pivot();
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
public sealed class PivotPrimeTests
{
    [Fact]
    public void Prime_TBarSeries_SetsState()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.20, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var p = new Pivot();
        p.Prime(bars);

        Assert.True(p.IsHot);
    }

    [Fact]
    public void Prime_EmptySource_NoException()
    {
        var p = new Pivot();
        var bars = new TBarSeries();

        var ex = Record.Exception(() => p.Prime(bars));
        Assert.Null(ex);
        Assert.False(p.IsHot);
    }
}
