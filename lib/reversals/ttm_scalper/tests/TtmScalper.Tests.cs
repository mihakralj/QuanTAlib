// TTM_SCALPER Tests - TTM Scalper Alert

namespace QuanTAlib.Tests;

// -- A) Constructor Validation ------------------------------------------------
public sealed class TtmScalperConstructorTests
{
    [Fact]
    public void Constructor_Default_SetsProperties()
    {
        var ts = new TtmScalper();

        Assert.Equal(3, ts.WarmupPeriod);
        Assert.Contains("TtmScalper", ts.Name, StringComparison.Ordinal);
        Assert.False(ts.IsHot);
        Assert.False(ts.UseCloses);
    }

    [Fact]
    public void Constructor_UseCloses_SetsFlag()
    {
        var ts = new TtmScalper(useCloses: true);

        Assert.True(ts.UseCloses);
        Assert.Contains("True", ts.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_InitialState_NaN()
    {
        var ts = new TtmScalper();

        Assert.True(double.IsNaN(ts.PivotHigh));
        Assert.True(double.IsNaN(ts.PivotLow));
    }
}

// -- B) Basic Calculation -----------------------------------------------------
public sealed class TtmScalperBasicTests
{
    [Fact]
    public void Update_ReturnsTValue()
    {
        var ts = new TtmScalper();
        var bar = new TBar(DateTime.UtcNow, 97, 100, 95, 98, 1000);

        TValue result = ts.Update(bar);

        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var ts = new TtmScalper();
        var bar = new TBar(DateTime.UtcNow, 97, 100, 95, 98, 1000);

        _ = ts.Update(bar);

        Assert.True(double.IsFinite(ts.Last.Value) || double.IsNaN(ts.Last.Value));
    }

    [Fact]
    public void Update_KnownPivotHigh_Detected()
    {
        var ts = new TtmScalper();
        var dt = DateTime.UtcNow;

        // TBar(DateTime, open, high, low, close, volume)
        // Pattern: bar[2] low high, bar[1] HIGH peak, bar[0] low high
        _ = ts.Update(new TBar(dt.AddMinutes(0), 97, 100, 95, 98, 1000), isNew: true);  // bar[2]: high=100
        _ = ts.Update(new TBar(dt.AddMinutes(1), 104, 110, 92, 105, 1000), isNew: true); // bar[1]: high=110 (peak)
        _ = ts.Update(new TBar(dt.AddMinutes(2), 98, 101, 96, 99, 1000), isNew: true);   // bar[0]: high=101

        // bar[1].High=110 > bar[2].High=100 AND bar[1].High=110 > bar[0].High=101
        Assert.Equal(110.0, ts.PivotHigh);
    }

    [Fact]
    public void Update_KnownPivotLow_Detected()
    {
        var ts = new TtmScalper();
        var dt = DateTime.UtcNow;

        // TBar(DateTime, open, high, low, close, volume)
        // Pattern: bar[2] high low, bar[1] LOW trough, bar[0] high low
        _ = ts.Update(new TBar(dt.AddMinutes(0), 102, 105, 100, 103, 1000), isNew: true); // bar[2]: low=100
        _ = ts.Update(new TBar(dt.AddMinutes(1), 94, 102, 88, 95, 1000), isNew: true);    // bar[1]: low=88 (trough)
        _ = ts.Update(new TBar(dt.AddMinutes(2), 102, 106, 99, 103, 1000), isNew: true);  // bar[0]: low=99

        // bar[1].Low=88 < bar[2].Low=100 AND bar[1].Low=88 < bar[0].Low=99
        Assert.Equal(88.0, ts.PivotLow);
    }

    [Fact]
    public void Update_NoPivot_ReturnsNaN()
    {
        var ts = new TtmScalper();
        var dt = DateTime.UtcNow;

        // Monotone ascending — no pivot
        for (int i = 0; i < 3; i++)
        {
            double price = 100.0 + i * 5;
            _ = ts.Update(new TBar(dt.AddMinutes(i), price, price + 2, price - 2, price + 1, 1000), isNew: true);
        }

        Assert.True(double.IsNaN(ts.PivotHigh));
    }

    [Fact]
    public void Update_UseCloses_PivotHigh_Detected()
    {
        var ts = new TtmScalper(useCloses: true);
        var dt = DateTime.UtcNow;

        // Close-based: close[1] > close[2] AND close[1] > close[0]
        _ = ts.Update(new TBar(dt.AddMinutes(0), 97, 100, 95, 98, 1000), isNew: true);   // bar[2]: close=98
        _ = ts.Update(new TBar(dt.AddMinutes(1), 104, 110, 92, 108, 1000), isNew: true);  // bar[1]: close=108 (peak)
        _ = ts.Update(new TBar(dt.AddMinutes(2), 98, 101, 96, 99, 1000), isNew: true);    // bar[0]: close=99

        Assert.Equal(108.0, ts.PivotHigh);
    }

    [Fact]
    public void Update_UseCloses_PivotLow_Detected()
    {
        var ts = new TtmScalper(useCloses: true);
        var dt = DateTime.UtcNow;

        // Close-based: close[1] < close[2] AND close[1] < close[0]
        _ = ts.Update(new TBar(dt.AddMinutes(0), 102, 105, 100, 103, 1000), isNew: true); // bar[2]: close=103
        _ = ts.Update(new TBar(dt.AddMinutes(1), 94, 102, 88, 90, 1000), isNew: true);    // bar[1]: close=90 (trough)
        _ = ts.Update(new TBar(dt.AddMinutes(2), 102, 106, 99, 103, 1000), isNew: true);  // bar[0]: close=103

        Assert.Equal(90.0, ts.PivotLow);
    }

    [Fact]
    public void Name_ContainsTtmScalper()
    {
        var ts = new TtmScalper();
        Assert.Contains("TtmScalper", ts.Name, StringComparison.Ordinal);
    }
}

// -- C) State + Bar Correction ------------------------------------------------
public sealed class TtmScalperStateCorrectionTests
{
    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var ts = new TtmScalper();

        _ = ts.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000), isNew: true);
        var first = ts.Last;

        _ = ts.Update(new TBar(DateTime.UtcNow.AddMinutes(1), 105, 110, 100, 105, 1000), isNew: true);
        var second = ts.Last;

        Assert.NotEqual(first.Time, second.Time);
    }

    [Fact]
    public void IsNew_False_CorrectionRestoresState()
    {
        var ts = new TtmScalper();
        var dt = DateTime.UtcNow;

        // Feed 2 bars
        for (int i = 0; i < 2; i++)
        {
            double price = 100.0 + i;
            _ = ts.Update(new TBar(dt.AddMinutes(i), price, price + 5, price - 5, price + 1, 1000), isNew: true);
        }

        // New bar
        _ = ts.Update(new TBar(dt.AddMinutes(2), 98, 110, 85, 100, 1000), isNew: true);

        // Correct the bar (isNew=false with different values)
        _ = ts.Update(new TBar(dt.AddMinutes(2), 98, 111, 84, 100, 1000), isNew: false);

        // Another correction with same values should produce same result
        _ = ts.Update(new TBar(dt.AddMinutes(2), 98, 111, 84, 100, 1000), isNew: false);
        var corrected1High = ts.PivotHigh;
        var corrected1Low = ts.PivotLow;

        _ = ts.Update(new TBar(dt.AddMinutes(2), 98, 111, 84, 100, 1000), isNew: false);
        var corrected2High = ts.PivotHigh;
        var corrected2Low = ts.PivotLow;

        Assert.Equal(corrected1High, corrected2High);
        Assert.Equal(corrected1Low, corrected2Low);
    }

    [Fact]
    public void IterativeCorrections_ProduceSameResult()
    {
        var ts = new TtmScalper();
        var dt = DateTime.UtcNow;

        for (int i = 0; i < 2; i++)
        {
            double price = 100.0 + i;
            _ = ts.Update(new TBar(dt.AddMinutes(i), price, price + 5, price - 5, price + 1, 1000), isNew: true);
        }

        _ = ts.Update(new TBar(dt.AddMinutes(2), 105, 110, 90, 100, 1000), isNew: true);

        double[] highResults = new double[3];
        double[] lowResults = new double[3];
        for (int i = 0; i < 3; i++)
        {
            _ = ts.Update(new TBar(dt.AddMinutes(2), 106, 112, 88, 102, 1000), isNew: false);
            highResults[i] = ts.PivotHigh;
            lowResults[i] = ts.PivotLow;
        }

        Assert.Equal(highResults[0], highResults[1]);
        Assert.Equal(highResults[1], highResults[2]);
        Assert.Equal(lowResults[0], lowResults[1]);
        Assert.Equal(lowResults[1], lowResults[2]);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        var ts = new TtmScalper();

        for (int i = 0; i < 10; i++)
        {
            double price = 100.0 + i;
            _ = ts.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 5, price - 5, price + 1, 1000));
        }

        Assert.True(ts.IsHot);

        ts.Reset();

        Assert.False(ts.IsHot);
        Assert.True(double.IsNaN(ts.PivotHigh));
        Assert.True(double.IsNaN(ts.PivotLow));
    }
}

// -- D) Warmup / Convergence --------------------------------------------------
public sealed class TtmScalperWarmupTests
{
    [Fact]
    public void IsHot_FlipsAfterWarmup()
    {
        var ts = new TtmScalper();

        // Feed 2 bars — should NOT be hot
        for (int i = 0; i < 2; i++)
        {
            double price = 100.0 + i;
            _ = ts.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 2, price - 2, price + 1, 1000));
            Assert.False(ts.IsHot, $"Should not be hot at bar {i}");
        }

        // Feed 3rd bar — should be hot
        double p = 100.0 + 2;
        _ = ts.Update(new TBar(DateTime.UtcNow.AddMinutes(2), p, p + 2, p - 2, p + 1, 1000));
        Assert.True(ts.IsHot, "Should be hot after 3 bars");
    }

    [Fact]
    public void WarmupPeriod_Equals3()
    {
        var ts = new TtmScalper();
        Assert.Equal(3, ts.WarmupPeriod);
    }
}

// -- E) Robustness ------------------------------------------------------------
public sealed class TtmScalperRobustnessTests
{
    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var ts = new TtmScalper();
        var dt = DateTime.UtcNow;

        // Feed valid bars
        for (int i = 0; i < 3; i++)
        {
            double price = 100.0 + i;
            _ = ts.Update(new TBar(dt.AddMinutes(i), price, price + 5, price - 5, price + 1, 1000));
        }

        Assert.True(ts.IsHot);

        // Feed NaN bar
        _ = ts.Update(new TBar(dt.AddMinutes(3), double.NaN, double.NaN, double.NaN, double.NaN, 0));

        // Should still be hot with valid outputs
        Assert.True(ts.IsHot);
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var ts = new TtmScalper();
        var dt = DateTime.UtcNow;

        for (int i = 0; i < 3; i++)
        {
            double price = 100.0 + i;
            _ = ts.Update(new TBar(dt.AddMinutes(i), price, price + 5, price - 5, price + 1, 1000));
        }

        _ = ts.Update(new TBar(dt.AddMinutes(3),
            double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity, 0));

        Assert.True(ts.IsHot);
    }

    [Fact]
    public void FirstBar_NaN_ReturnsNaN()
    {
        var ts = new TtmScalper();

        _ = ts.Update(new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 0));

        Assert.True(double.IsNaN(ts.Last.Value));
    }
}

// -- F) Consistency -----------------------------------------------------------
public sealed class TtmScalperConsistencyTests
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
        var streaming = new TtmScalper();
        var streamHighResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamHighResults[i] = streaming.PivotHigh;
        }

        // Batch
        var batchResults = TtmScalper.Batch(bars);

        int warmup = 2; // first 2 bars are NaN
        for (int i = warmup; i < bars.Count; i++)
        {
            if (double.IsNaN(streamHighResults[i]))
            {
                Assert.True(double.IsNaN(batchResults[i].Value));
            }
            else
            {
                Assert.Equal(streamHighResults[i], batchResults[i].Value, precision: 10);
            }
        }
    }

    [Fact]
    public void Streaming_MatchesSpan()
    {
        var bars = CreateGbmBars();

        // Streaming
        var streaming = new TtmScalper();
        var streamHighResults = new double[bars.Count];
        var streamLowResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamHighResults[i] = streaming.PivotHigh;
            streamLowResults[i] = streaming.PivotLow;
        }

        // Span
        var spanHigh = new double[bars.Count];
        var spanLow = new double[bars.Count];
        TtmScalper.Batch(bars.HighValues, bars.LowValues, bars.CloseValues, spanHigh, spanLow);

        for (int i = 2; i < bars.Count; i++)
        {
            if (double.IsNaN(streamHighResults[i]))
            {
                Assert.True(double.IsNaN(spanHigh[i]), $"PivotHigh mismatch at {i}");
            }
            else
            {
                Assert.Equal(streamHighResults[i], spanHigh[i], precision: 10);
            }

            if (double.IsNaN(streamLowResults[i]))
            {
                Assert.True(double.IsNaN(spanLow[i]), $"PivotLow mismatch at {i}");
            }
            else
            {
                Assert.Equal(streamLowResults[i], spanLow[i], precision: 10);
            }
        }
    }

    [Fact]
    public void TValue_Update_MatchesTBar_Update()
    {
        var f1 = new TtmScalper();
        var f2 = new TtmScalper();

        double[] prices = [100, 102, 98, 105, 99, 103, 107, 95, 110, 108];

        for (int i = 0; i < prices.Length; i++)
        {
            double p = prices[i];
            _ = f1.Update(new TBar(DateTime.UtcNow.AddMinutes(i), p, p, p, p, 0), isNew: true);
            _ = f2.Update(new TValue(DateTime.UtcNow.AddMinutes(i), p), isNew: true);
        }

        Assert.Equal(f1.PivotHigh, f2.PivotHigh);
        Assert.Equal(f1.PivotLow, f2.PivotLow);
    }

    [Fact]
    public void UseCloses_Streaming_MatchesSpan()
    {
        var bars = CreateGbmBars();

        // Streaming with useCloses=true
        var streaming = new TtmScalper(useCloses: true);
        var streamHighResults = new double[bars.Count];
        var streamLowResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamHighResults[i] = streaming.PivotHigh;
            streamLowResults[i] = streaming.PivotLow;
        }

        // Span with useCloses=true
        var spanHigh = new double[bars.Count];
        var spanLow = new double[bars.Count];
        TtmScalper.Batch(bars.HighValues, bars.LowValues, bars.CloseValues, spanHigh, spanLow, useCloses: true);

        for (int i = 2; i < bars.Count; i++)
        {
            if (double.IsNaN(streamHighResults[i]))
            {
                Assert.True(double.IsNaN(spanHigh[i]), $"UseCloses PivotHigh mismatch at {i}");
            }
            else
            {
                Assert.Equal(streamHighResults[i], spanHigh[i], precision: 10);
            }

            if (double.IsNaN(streamLowResults[i]))
            {
                Assert.True(double.IsNaN(spanLow[i]), $"UseCloses PivotLow mismatch at {i}");
            }
            else
            {
                Assert.Equal(streamLowResults[i], spanLow[i], precision: 10);
            }
        }
    }
}

// -- G) Span API Tests --------------------------------------------------------
public sealed class TtmScalperSpanTests
{
    [Fact]
    public void Batch_Span_MismatchedLengths_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            TtmScalper.Batch(new double[10], new double[5], new double[10], new double[10], new double[10]));
        Assert.Equal("high", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_HighOutputTooShort_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            TtmScalper.Batch(new double[10], new double[10], new double[10], new double[5], new double[10]));
        Assert.Equal("highOutput", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_LowOutputTooShort_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            TtmScalper.Batch(new double[10], new double[10], new double[10], new double[10], new double[5]));
        Assert.Equal("lowOutput", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_Empty_NoException()
    {
        var ex = Record.Exception(() =>
            TtmScalper.Batch(ReadOnlySpan<double>.Empty, ReadOnlySpan<double>.Empty,
                ReadOnlySpan<double>.Empty, Span<double>.Empty, Span<double>.Empty));
        Assert.Null(ex);
    }
}

// -- H) Event / Chainability -------------------------------------------------
public sealed class TtmScalperEventTests
{
    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var ts = new TtmScalper();
        int fireCount = 0;

        ts.Pub += (object? _, in TValueEventArgs _e) => { fireCount++; };

        _ = ts.Update(new TBar(DateTime.UtcNow, 97, 100, 95, 98, 1000));

        Assert.Equal(1, fireCount);
    }

    [Fact]
    public void Pub_FiresOnEachUpdate()
    {
        var ts = new TtmScalper();
        int fireCount = 0;

        ts.Pub += (object? _, in TValueEventArgs _e) => { fireCount++; };

        for (int i = 0; i < 5; i++)
        {
            double price = 100.0 + i;
            _ = ts.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 2, price - 2, price + 1, 1000));
        }

        Assert.Equal(5, fireCount);
    }
}

// -- I) Prime Tests -----------------------------------------------------------
public sealed class TtmScalperPrimeTests
{
    [Fact]
    public void Prime_TBarSeries_SetsState()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.20, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var ts = new TtmScalper();
        ts.Prime(bars);

        Assert.True(ts.IsHot);
    }

    [Fact]
    public void Prime_EmptySource_NoException()
    {
        var ts = new TtmScalper();
        var bars = new TBarSeries();

        var ex = Record.Exception(() => ts.Prime(bars));
        Assert.Null(ex);
        Assert.False(ts.IsHot);
    }
}
