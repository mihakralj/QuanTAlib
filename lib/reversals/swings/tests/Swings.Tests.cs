// SWINGS Tests - Swing High/Low Detection

namespace QuanTAlib.Tests;

// -- A) Constructor Validation ------------------------------------------------
public sealed class SwingsConstructorTests
{
    [Fact]
    public void Constructor_Default_SetsProperties()
    {
        var sw = new Swings();

        Assert.Equal(11, sw.WarmupPeriod);
        Assert.Equal(5, sw.Lookback);
        Assert.Contains("Swings", sw.Name, StringComparison.Ordinal);
        Assert.False(sw.IsHot);
    }

    [Fact]
    public void Constructor_CustomLookback_SetsProperties()
    {
        var sw = new Swings(lookback: 3);

        Assert.Equal(7, sw.WarmupPeriod); // 2*3+1
        Assert.Equal(3, sw.Lookback);
        Assert.Contains("Swings(3)", sw.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_InvalidLookback_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Swings(lookback: 0));
        Assert.Equal("lookback", ex.ParamName);
    }

    [Fact]
    public void Constructor_InitialState_NaN()
    {
        var sw = new Swings();

        Assert.True(double.IsNaN(sw.SwingHigh));
        Assert.True(double.IsNaN(sw.SwingLow));
        Assert.True(double.IsNaN(sw.LastSwingHigh));
        Assert.True(double.IsNaN(sw.LastSwingLow));
    }
}

// -- B) Basic Calculation -----------------------------------------------------
public sealed class SwingsBasicTests
{
    [Fact]
    public void Update_ReturnsTValue()
    {
        var sw = new Swings(lookback: 2);
        var bar = new TBar(DateTime.UtcNow, 97, 100, 95, 98, 1000);

        TValue result = sw.Update(bar);

        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var sw = new Swings(lookback: 2);
        var bar = new TBar(DateTime.UtcNow, 97, 100, 95, 98, 1000);

        _ = sw.Update(bar);

        Assert.True(double.IsFinite(sw.Last.Value) || double.IsNaN(sw.Last.Value));
    }

    [Fact]
    public void Update_KnownSwingHigh_Detected()
    {
        // lookback=2, windowSize=5
        var sw = new Swings(lookback: 2);
        var dt = DateTime.UtcNow;

        // Pattern: center bar (bar[2]) has highest high
        _ = sw.Update(new TBar(dt.AddMinutes(0), 97, 100, 95, 98, 1000), isNew: true);  // high=100
        _ = sw.Update(new TBar(dt.AddMinutes(1), 100, 103, 98, 101, 1000), isNew: true); // high=103
        _ = sw.Update(new TBar(dt.AddMinutes(2), 104, 110, 92, 105, 1000), isNew: true); // high=110 (peak)
        _ = sw.Update(new TBar(dt.AddMinutes(3), 101, 104, 97, 102, 1000), isNew: true); // high=104
        _ = sw.Update(new TBar(dt.AddMinutes(4), 98, 101, 96, 99, 1000), isNew: true);   // high=101

        // center high=110 > {100, 103, 104, 101}
        Assert.Equal(110.0, sw.SwingHigh);
    }

    [Fact]
    public void Update_KnownSwingLow_Detected()
    {
        var sw = new Swings(lookback: 2);
        var dt = DateTime.UtcNow;

        // Pattern: center bar (bar[2]) has lowest low
        _ = sw.Update(new TBar(dt.AddMinutes(0), 102, 105, 100, 103, 1000), isNew: true); // low=100
        _ = sw.Update(new TBar(dt.AddMinutes(1), 100, 103, 98, 101, 1000), isNew: true);  // low=98
        _ = sw.Update(new TBar(dt.AddMinutes(2), 94, 102, 88, 95, 1000), isNew: true);    // low=88 (trough)
        _ = sw.Update(new TBar(dt.AddMinutes(3), 100, 104, 97, 101, 1000), isNew: true);  // low=97
        _ = sw.Update(new TBar(dt.AddMinutes(4), 102, 106, 99, 103, 1000), isNew: true);  // low=99

        // center low=88 < {100, 98, 97, 99}
        Assert.Equal(88.0, sw.SwingLow);
    }

    [Fact]
    public void Update_NoSwing_ReturnsNaN()
    {
        var sw = new Swings(lookback: 2);
        var dt = DateTime.UtcNow;

        // Monotone ascending - no swing high or low
        for (int i = 0; i < 5; i++)
        {
            double price = 100.0 + (i * 5);
            _ = sw.Update(new TBar(dt.AddMinutes(i), price, price + 2, price - 2, price + 1, 1000), isNew: true);
        }

        Assert.True(double.IsNaN(sw.SwingHigh));
    }

    [Fact]
    public void LastSwingHigh_PersistsAcrossBars()
    {
        var sw = new Swings(lookback: 2);
        var dt = DateTime.UtcNow;

        // Create a swing high
        _ = sw.Update(new TBar(dt.AddMinutes(0), 97, 100, 95, 98, 1000), isNew: true);
        _ = sw.Update(new TBar(dt.AddMinutes(1), 100, 103, 98, 101, 1000), isNew: true);
        _ = sw.Update(new TBar(dt.AddMinutes(2), 104, 110, 92, 105, 1000), isNew: true);
        _ = sw.Update(new TBar(dt.AddMinutes(3), 101, 104, 97, 102, 1000), isNew: true);
        _ = sw.Update(new TBar(dt.AddMinutes(4), 98, 101, 96, 99, 1000), isNew: true);

        Assert.Equal(110.0, sw.LastSwingHigh);

        // Feed more bars without a new swing high
        _ = sw.Update(new TBar(dt.AddMinutes(5), 99, 102, 97, 100, 1000), isNew: true);
        _ = sw.Update(new TBar(dt.AddMinutes(6), 100, 103, 98, 101, 1000), isNew: true);

        // LastSwingHigh should persist
        Assert.Equal(110.0, sw.LastSwingHigh);
    }

    [Fact]
    public void Name_ContainsSwings()
    {
        var sw = new Swings();
        Assert.Contains("Swings", sw.Name, StringComparison.Ordinal);
    }
}

// -- C) State + Bar Correction ------------------------------------------------
public sealed class SwingsStateCorrectionTests
{
    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var sw = new Swings(lookback: 2);

        _ = sw.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000), isNew: true);
        var first = sw.Last;

        _ = sw.Update(new TBar(DateTime.UtcNow.AddMinutes(1), 105, 110, 100, 105, 1000), isNew: true);
        var second = sw.Last;

        Assert.NotEqual(first.Time, second.Time);
    }

    [Fact]
    public void IsNew_False_CorrectionRestoresState()
    {
        var sw = new Swings(lookback: 2);
        var dt = DateTime.UtcNow;

        // Feed 4 bars
        for (int i = 0; i < 4; i++)
        {
            double price = 100.0 + i;
            _ = sw.Update(new TBar(dt.AddMinutes(i), price, price + 5, price - 5, price + 1, 1000), isNew: true);
        }

        // New bar
        _ = sw.Update(new TBar(dt.AddMinutes(4), 98, 110, 85, 100, 1000), isNew: true);

        // Correct the bar
        _ = sw.Update(new TBar(dt.AddMinutes(4), 98, 111, 84, 100, 1000), isNew: false);

        // Another correction with same values should produce same result
        _ = sw.Update(new TBar(dt.AddMinutes(4), 98, 111, 84, 100, 1000), isNew: false);
        var corrected1High = sw.SwingHigh;
        var corrected1Low = sw.SwingLow;

        _ = sw.Update(new TBar(dt.AddMinutes(4), 98, 111, 84, 100, 1000), isNew: false);
        var corrected2High = sw.SwingHigh;
        var corrected2Low = sw.SwingLow;

        Assert.Equal(corrected1High, corrected2High);
        Assert.Equal(corrected1Low, corrected2Low);
    }

    [Fact]
    public void IterativeCorrections_ProduceSameResult()
    {
        var sw = new Swings(lookback: 2);
        var dt = DateTime.UtcNow;

        for (int i = 0; i < 4; i++)
        {
            double price = 100.0 + i;
            _ = sw.Update(new TBar(dt.AddMinutes(i), price, price + 5, price - 5, price + 1, 1000), isNew: true);
        }

        _ = sw.Update(new TBar(dt.AddMinutes(4), 105, 110, 90, 100, 1000), isNew: true);

        double[] highResults = new double[3];
        double[] lowResults = new double[3];
        for (int i = 0; i < 3; i++)
        {
            _ = sw.Update(new TBar(dt.AddMinutes(4), 106, 112, 88, 102, 1000), isNew: false);
            highResults[i] = sw.SwingHigh;
            lowResults[i] = sw.SwingLow;
        }

        Assert.Equal(highResults[0], highResults[1]);
        Assert.Equal(highResults[1], highResults[2]);
        Assert.Equal(lowResults[0], lowResults[1]);
        Assert.Equal(lowResults[1], lowResults[2]);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        var sw = new Swings(lookback: 2);

        for (int i = 0; i < 10; i++)
        {
            double price = 100.0 + i;
            _ = sw.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 5, price - 5, price + 1, 1000));
        }

        Assert.True(sw.IsHot);

        sw.Reset();

        Assert.False(sw.IsHot);
        Assert.True(double.IsNaN(sw.SwingHigh));
        Assert.True(double.IsNaN(sw.SwingLow));
        Assert.True(double.IsNaN(sw.LastSwingHigh));
        Assert.True(double.IsNaN(sw.LastSwingLow));
    }
}

// -- D) Warmup / Convergence --------------------------------------------------
public sealed class SwingsWarmupTests
{
    [Fact]
    public void IsHot_FlipsAfterWarmup()
    {
        var sw = new Swings(lookback: 2); // windowSize = 5

        // Feed 4 bars -- should NOT be hot
        for (int i = 0; i < 4; i++)
        {
            double price = 100.0 + i;
            _ = sw.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 2, price - 2, price + 1, 1000));
            Assert.False(sw.IsHot, $"Should not be hot at bar {i}");
        }

        // Feed 5th bar -- should be hot
        double p = 100.0 + 4;
        _ = sw.Update(new TBar(DateTime.UtcNow.AddMinutes(4), p, p + 2, p - 2, p + 1, 1000));
        Assert.True(sw.IsHot, "Should be hot after 5 bars (windowSize)");
    }

    [Fact]
    public void WarmupPeriod_EqualsWindowSize()
    {
        var sw = new Swings(lookback: 3);
        Assert.Equal(7, sw.WarmupPeriod); // 2*3+1
    }

    [Fact]
    public void WarmupPeriod_DefaultLookback5_Equals11()
    {
        var sw = new Swings();
        Assert.Equal(11, sw.WarmupPeriod); // 2*5+1
    }
}

// -- E) Robustness ------------------------------------------------------------
public sealed class SwingsRobustnessTests
{
    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var sw = new Swings(lookback: 2);
        var dt = DateTime.UtcNow;

        // Feed valid bars
        for (int i = 0; i < 5; i++)
        {
            double price = 100.0 + i;
            _ = sw.Update(new TBar(dt.AddMinutes(i), price, price + 5, price - 5, price + 1, 1000));
        }

        Assert.True(sw.IsHot);

        // Feed NaN bar
        _ = sw.Update(new TBar(dt.AddMinutes(5), double.NaN, double.NaN, double.NaN, double.NaN, 0));

        // Should still be hot
        Assert.True(sw.IsHot);
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var sw = new Swings(lookback: 2);
        var dt = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            double price = 100.0 + i;
            _ = sw.Update(new TBar(dt.AddMinutes(i), price, price + 5, price - 5, price + 1, 1000));
        }

        _ = sw.Update(new TBar(dt.AddMinutes(5),
            double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity, 0));

        Assert.True(sw.IsHot);
    }

    [Fact]
    public void FirstBar_NaN_ReturnsNaN()
    {
        var sw = new Swings(lookback: 2);

        _ = sw.Update(new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 0));

        Assert.True(double.IsNaN(sw.Last.Value));
    }
}

// -- F) Consistency -----------------------------------------------------------
public sealed class SwingsConsistencyTests
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
        var streaming = new Swings(lookback: 3);
        var streamHighResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamHighResults[i] = streaming.SwingHigh;
        }

        // Batch
        var batchResults = Swings.Batch(bars, lookback: 3);

        int warmup = 6; // windowSize - 1 = 2*3+1 - 1 = 6
        for (int i = warmup; i < bars.Count; i++)
        {
            if (double.IsNaN(streamHighResults[i]))
            {
                Assert.True(double.IsNaN(batchResults[i].Value), $"Mismatch at {i}: stream=NaN, batch={batchResults[i].Value}");
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
        var streaming = new Swings(lookback: 3);
        var streamHighResults = new double[bars.Count];
        var streamLowResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamHighResults[i] = streaming.SwingHigh;
            streamLowResults[i] = streaming.SwingLow;
        }

        // Span
        var spanHigh = new double[bars.Count];
        var spanLow = new double[bars.Count];
        Swings.Batch(bars.HighValues, bars.LowValues, spanHigh, spanLow, lookback: 3);

        int warmup = 6;
        for (int i = warmup; i < bars.Count; i++)
        {
            if (double.IsNaN(streamHighResults[i]))
            {
                Assert.True(double.IsNaN(spanHigh[i]), $"SwingHigh mismatch at {i}");
            }
            else
            {
                Assert.Equal(streamHighResults[i], spanHigh[i], precision: 10);
            }

            if (double.IsNaN(streamLowResults[i]))
            {
                Assert.True(double.IsNaN(spanLow[i]), $"SwingLow mismatch at {i}");
            }
            else
            {
                Assert.Equal(streamLowResults[i], spanLow[i], precision: 10);
            }
        }
    }

    [Fact]
    public void BatchDual_MatchesSpan()
    {
        var bars = CreateGbmBars();

        var (swingHighs, swingLows) = Swings.BatchDual(bars, lookback: 3);

        var spanHigh = new double[bars.Count];
        var spanLow = new double[bars.Count];
        Swings.Batch(bars.HighValues, bars.LowValues, spanHigh, spanLow, lookback: 3);

        for (int i = 0; i < bars.Count; i++)
        {
            if (double.IsNaN(spanHigh[i]))
            {
                Assert.True(double.IsNaN(swingHighs[i].Value), $"SwingHigh mismatch at {i}");
            }
            else
            {
                Assert.Equal(spanHigh[i], swingHighs[i].Value, precision: 10);
            }

            if (double.IsNaN(spanLow[i]))
            {
                Assert.True(double.IsNaN(swingLows[i].Value), $"SwingLow mismatch at {i}");
            }
            else
            {
                Assert.Equal(spanLow[i], swingLows[i].Value, precision: 10);
            }
        }
    }

    [Fact]
    public void TValue_Update_MatchesTBar_Update()
    {
        var sw1 = new Swings(lookback: 2);
        var sw2 = new Swings(lookback: 2);

        double[] prices = [100, 102, 98, 105, 99, 103, 107, 95, 110, 108];

        for (int i = 0; i < prices.Length; i++)
        {
            double p = prices[i];
            _ = sw1.Update(new TBar(DateTime.UtcNow.AddMinutes(i), p, p, p, p, 0), isNew: true);
            _ = sw2.Update(new TValue(DateTime.UtcNow.AddMinutes(i), p), isNew: true);
        }

        Assert.Equal(sw1.SwingHigh, sw2.SwingHigh);
        Assert.Equal(sw1.SwingLow, sw2.SwingLow);
    }
}

// -- G) Span API Tests --------------------------------------------------------
public sealed class SwingsSpanTests
{
    [Fact]
    public void Batch_Span_MismatchedLengths_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Swings.Batch(new double[10], new double[5], new double[10], new double[10]));
        Assert.Equal("high", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputTooShort_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Swings.Batch(new double[10], new double[10], new double[5], new double[10]));
        Assert.Equal("highOutput", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_LowOutputTooShort_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Swings.Batch(new double[10], new double[10], new double[10], new double[5]));
        Assert.Equal("lowOutput", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidLookback_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Swings.Batch(new double[10], new double[10], new double[10], new double[10], lookback: 0));
        Assert.Equal("lookback", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_Empty_NoException()
    {
        var ex = Record.Exception(() =>
            Swings.Batch(ReadOnlySpan<double>.Empty, ReadOnlySpan<double>.Empty,
                Span<double>.Empty, Span<double>.Empty));
        Assert.Null(ex);
    }
}

// -- H) Event / Chainability -------------------------------------------------
public sealed class SwingsEventTests
{
    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var sw = new Swings(lookback: 2);
        int fireCount = 0;

        sw.Pub += (object? _, in TValueEventArgs _e) => { fireCount++; };

        _ = sw.Update(new TBar(DateTime.UtcNow, 97, 100, 95, 98, 1000));

        Assert.Equal(1, fireCount);
    }

    [Fact]
    public void Pub_FiresOnEachUpdate()
    {
        var sw = new Swings(lookback: 2);
        int fireCount = 0;

        sw.Pub += (object? _, in TValueEventArgs _e) => { fireCount++; };

        for (int i = 0; i < 5; i++)
        {
            double price = 100.0 + i;
            _ = sw.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 2, price - 2, price + 1, 1000));
        }

        Assert.Equal(5, fireCount);
    }
}

// -- I) Prime Tests -----------------------------------------------------------
public sealed class SwingsPrimeTests
{
    [Fact]
    public void Prime_TBarSeries_SetsState()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.20, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var sw = new Swings(lookback: 3);
        sw.Prime(bars);

        Assert.True(sw.IsHot);
    }

    [Fact]
    public void Prime_EmptySource_NoException()
    {
        var sw = new Swings(lookback: 2);
        var bars = new TBarSeries();

        var ex = Record.Exception(() => sw.Prime(bars));
        Assert.Null(ex);
        Assert.False(sw.IsHot);
    }
}
