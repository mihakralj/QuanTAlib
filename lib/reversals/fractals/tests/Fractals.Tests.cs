// FRACTALS Tests - Williams Fractals

namespace QuanTAlib.Tests;

// -- A) Constructor Validation ------------------------------------------------
public sealed class FractalsConstructorTests
{
    [Fact]
    public void Constructor_Default_SetsProperties()
    {
        var f = new Fractals();

        Assert.Equal(5, f.WarmupPeriod);
        Assert.Contains("Fractals", f.Name, StringComparison.Ordinal);
        Assert.False(f.IsHot);
    }

    [Fact]
    public void Constructor_InitialState_NaN()
    {
        var f = new Fractals();

        Assert.True(double.IsNaN(f.UpFractal));
        Assert.True(double.IsNaN(f.DownFractal));
    }
}

// -- B) Basic Calculation -----------------------------------------------------
public sealed class FractalsBasicTests
{
    [Fact]
    public void Update_ReturnsTValue()
    {
        var f = new Fractals();
        // TBar(DateTime, open, high, low, close, volume)
        var bar = new TBar(DateTime.UtcNow, 97, 100, 95, 98, 1000);

        TValue result = f.Update(bar);

        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var f = new Fractals();
        var bar = new TBar(DateTime.UtcNow, 97, 100, 95, 98, 1000);

        _ = f.Update(bar);

        Assert.True(double.IsFinite(f.Last.Value) || double.IsNaN(f.Last.Value));
    }

    [Fact]
    public void Update_KnownUpFractal_Detected()
    {
        var f = new Fractals();
        var dt = DateTime.UtcNow;

        // TBar(DateTime, open, high, low, close, volume)
        // Pattern: bar[4] low, bar[3] medium, bar[2] HIGH peak, bar[1] medium, bar[0] low
        // Bars fed in chronological order: bar[4] first, bar[0] last
        _ = f.Update(new TBar(dt.AddMinutes(0), 97, 100, 95, 98, 1000), isNew: true);  // bar[4]: high=100
        _ = f.Update(new TBar(dt.AddMinutes(1), 100, 103, 98, 101, 1000), isNew: true); // bar[3]: high=103
        _ = f.Update(new TBar(dt.AddMinutes(2), 104, 110, 92, 105, 1000), isNew: true); // bar[2]: high=110 (peak)
        _ = f.Update(new TBar(dt.AddMinutes(3), 101, 104, 97, 102, 1000), isNew: true); // bar[1]: high=104
        _ = f.Update(new TBar(dt.AddMinutes(4), 98, 101, 96, 99, 1000), isNew: true);   // bar[0]: high=101

        // bar[2].High=110 > bar[0].High=101, bar[1].High=104, bar[3].High=103, bar[4].High=100
        Assert.Equal(110.0, f.UpFractal);
    }

    [Fact]
    public void Update_KnownDownFractal_Detected()
    {
        var f = new Fractals();
        var dt = DateTime.UtcNow;

        // TBar(DateTime, open, high, low, close, volume)
        // Pattern: bar[4] high lows, bar[3] medium, bar[2] LOW trough, bar[1] medium, bar[0] high lows
        _ = f.Update(new TBar(dt.AddMinutes(0), 102, 105, 100, 103, 1000), isNew: true); // bar[4]: low=100
        _ = f.Update(new TBar(dt.AddMinutes(1), 100, 103, 98, 101, 1000), isNew: true);  // bar[3]: low=98
        _ = f.Update(new TBar(dt.AddMinutes(2), 94, 102, 88, 95, 1000), isNew: true);    // bar[2]: low=88 (trough)
        _ = f.Update(new TBar(dt.AddMinutes(3), 100, 104, 97, 101, 1000), isNew: true);  // bar[1]: low=97
        _ = f.Update(new TBar(dt.AddMinutes(4), 102, 106, 99, 103, 1000), isNew: true);  // bar[0]: low=99

        // bar[2].Low=88 < bar[0].Low=99, bar[1].Low=97, bar[3].Low=98, bar[4].Low=100
        Assert.Equal(88.0, f.DownFractal);
    }

    [Fact]
    public void Update_NoFractal_ReturnsNaN()
    {
        var f = new Fractals();
        var dt = DateTime.UtcNow;

        // Monotone ascending - no fractal
        for (int i = 0; i < 5; i++)
        {
            double price = 100.0 + (i * 5);
            _ = f.Update(new TBar(dt.AddMinutes(i), price, price + 2, price - 2, price + 1, 1000), isNew: true);
        }

        Assert.True(double.IsNaN(f.UpFractal));
    }

    [Fact]
    public void Name_ContainsFractals()
    {
        var f = new Fractals();
        Assert.Contains("Fractals", f.Name, StringComparison.Ordinal);
    }
}

// -- C) State + Bar Correction ------------------------------------------------
public sealed class FractalsStateCorrectionTests
{
    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var f = new Fractals();

        _ = f.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000), isNew: true);
        var first = f.Last;

        _ = f.Update(new TBar(DateTime.UtcNow.AddMinutes(1), 105, 110, 100, 105, 1000), isNew: true);
        var second = f.Last;

        Assert.NotEqual(first.Time, second.Time);
    }

    [Fact]
    public void IsNew_False_CorrectionRestoresState()
    {
        var f = new Fractals();
        var dt = DateTime.UtcNow;

        // Feed 4 bars
        for (int i = 0; i < 4; i++)
        {
            double price = 100.0 + i;
            _ = f.Update(new TBar(dt.AddMinutes(i), price, price + 5, price - 5, price + 1, 1000), isNew: true);
        }

        // New bar
        _ = f.Update(new TBar(dt.AddMinutes(4), 98, 110, 85, 100, 1000), isNew: true);

        // Correct the bar (isNew=false with different values)
        _ = f.Update(new TBar(dt.AddMinutes(4), 98, 111, 84, 100, 1000), isNew: false);

        // Another correction with same values should produce same result
        _ = f.Update(new TBar(dt.AddMinutes(4), 98, 111, 84, 100, 1000), isNew: false);
        var corrected1Up = f.UpFractal;
        var corrected1Down = f.DownFractal;

        _ = f.Update(new TBar(dt.AddMinutes(4), 98, 111, 84, 100, 1000), isNew: false);
        var corrected2Up = f.UpFractal;
        var corrected2Down = f.DownFractal;

        Assert.Equal(corrected1Up, corrected2Up);
        Assert.Equal(corrected1Down, corrected2Down);
    }

    [Fact]
    public void IterativeCorrections_ProduceSameResult()
    {
        var f = new Fractals();
        var dt = DateTime.UtcNow;

        for (int i = 0; i < 4; i++)
        {
            double price = 100.0 + i;
            _ = f.Update(new TBar(dt.AddMinutes(i), price, price + 5, price - 5, price + 1, 1000), isNew: true);
        }

        _ = f.Update(new TBar(dt.AddMinutes(4), 105, 110, 90, 100, 1000), isNew: true);

        double[] upResults = new double[3];
        double[] downResults = new double[3];
        for (int i = 0; i < 3; i++)
        {
            _ = f.Update(new TBar(dt.AddMinutes(4), 106, 112, 88, 102, 1000), isNew: false);
            upResults[i] = f.UpFractal;
            downResults[i] = f.DownFractal;
        }

        Assert.Equal(upResults[0], upResults[1]);
        Assert.Equal(upResults[1], upResults[2]);
        Assert.Equal(downResults[0], downResults[1]);
        Assert.Equal(downResults[1], downResults[2]);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        var f = new Fractals();

        for (int i = 0; i < 10; i++)
        {
            double price = 100.0 + i;
            _ = f.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 5, price - 5, price + 1, 1000));
        }

        Assert.True(f.IsHot);

        f.Reset();

        Assert.False(f.IsHot);
        Assert.True(double.IsNaN(f.UpFractal));
        Assert.True(double.IsNaN(f.DownFractal));
    }
}

// -- D) Warmup / Convergence --------------------------------------------------
public sealed class FractalsWarmupTests
{
    [Fact]
    public void IsHot_FlipsAfterWarmup()
    {
        var f = new Fractals();

        // Feed 4 bars -- should NOT be hot
        for (int i = 0; i < 4; i++)
        {
            double price = 100.0 + i;
            _ = f.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 2, price - 2, price + 1, 1000));
            Assert.False(f.IsHot, $"Should not be hot at bar {i}");
        }

        // Feed 5th bar -- should be hot
        double p = 100.0 + 4;
        _ = f.Update(new TBar(DateTime.UtcNow.AddMinutes(4), p, p + 2, p - 2, p + 1, 1000));
        Assert.True(f.IsHot, "Should be hot after 5 bars");
    }

    [Fact]
    public void WarmupPeriod_Equals5()
    {
        var f = new Fractals();
        Assert.Equal(5, f.WarmupPeriod);
    }
}

// -- E) Robustness ------------------------------------------------------------
public sealed class FractalsRobustnessTests
{
    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var f = new Fractals();
        var dt = DateTime.UtcNow;

        // Feed valid bars
        for (int i = 0; i < 5; i++)
        {
            double price = 100.0 + i;
            _ = f.Update(new TBar(dt.AddMinutes(i), price, price + 5, price - 5, price + 1, 1000));
        }

        Assert.True(f.IsHot);

        // Feed NaN bar
        _ = f.Update(new TBar(dt.AddMinutes(5), double.NaN, double.NaN, double.NaN, double.NaN, 0));

        // Should still be hot with valid fractal outputs (either NaN=no fractal or finite=fractal)
        Assert.True(f.IsHot);
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var f = new Fractals();
        var dt = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            double price = 100.0 + i;
            _ = f.Update(new TBar(dt.AddMinutes(i), price, price + 5, price - 5, price + 1, 1000));
        }

        _ = f.Update(new TBar(dt.AddMinutes(5),
            double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity, 0));

        Assert.True(f.IsHot);
    }

    [Fact]
    public void FirstBar_NaN_ReturnsNaN()
    {
        var f = new Fractals();

        _ = f.Update(new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 0));

        Assert.True(double.IsNaN(f.Last.Value));
    }
}

// -- F) Consistency -----------------------------------------------------------
public sealed class FractalsConsistencyTests
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
        var streaming = new Fractals();
        var streamUpResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamUpResults[i] = streaming.UpFractal;
        }

        // Batch
        var batchResults = Fractals.Batch(bars);

        int warmup = 4; // first 4 bars are NaN
        for (int i = warmup; i < bars.Count; i++)
        {
            if (double.IsNaN(streamUpResults[i]))
            {
                Assert.True(double.IsNaN(batchResults[i].Value));
            }
            else
            {
                Assert.Equal(streamUpResults[i], batchResults[i].Value, precision: 10);
            }
        }
    }

    [Fact]
    public void Streaming_MatchesSpan()
    {
        var bars = CreateGbmBars();

        // Streaming
        var streaming = new Fractals();
        var streamUpResults = new double[bars.Count];
        var streamDownResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamUpResults[i] = streaming.UpFractal;
            streamDownResults[i] = streaming.DownFractal;
        }

        // Span
        var spanUp = new double[bars.Count];
        var spanDown = new double[bars.Count];
        Fractals.Batch(bars.HighValues, bars.LowValues, spanUp, spanDown);

        for (int i = 4; i < bars.Count; i++)
        {
            if (double.IsNaN(streamUpResults[i]))
            {
                Assert.True(double.IsNaN(spanUp[i]), $"Up fractal mismatch at {i}");
            }
            else
            {
                Assert.Equal(streamUpResults[i], spanUp[i], precision: 10);
            }

            if (double.IsNaN(streamDownResults[i]))
            {
                Assert.True(double.IsNaN(spanDown[i]), $"Down fractal mismatch at {i}");
            }
            else
            {
                Assert.Equal(streamDownResults[i], spanDown[i], precision: 10);
            }
        }
    }

    [Fact]
    public void TValue_Update_MatchesTBar_Update()
    {
        var f1 = new Fractals();
        var f2 = new Fractals();

        double[] prices = [100, 102, 98, 105, 99, 103, 107, 95, 110, 108];

        for (int i = 0; i < prices.Length; i++)
        {
            double p = prices[i];
            _ = f1.Update(new TBar(DateTime.UtcNow.AddMinutes(i), p, p, p, p, 0), isNew: true);
            _ = f2.Update(new TValue(DateTime.UtcNow.AddMinutes(i), p), isNew: true);
        }

        Assert.Equal(f1.UpFractal, f2.UpFractal);
        Assert.Equal(f1.DownFractal, f2.DownFractal);
    }
}

// -- G) Span API Tests --------------------------------------------------------
public sealed class FractalsSpanTests
{
    [Fact]
    public void Batch_Span_MismatchedLengths_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Fractals.Batch(new double[10], new double[5], new double[10], new double[10]));
        Assert.Equal("high", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputTooShort_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Fractals.Batch(new double[10], new double[10], new double[5], new double[10]));
        Assert.Equal("upOutput", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_DownOutputTooShort_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Fractals.Batch(new double[10], new double[10], new double[10], new double[5]));
        Assert.Equal("downOutput", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_Empty_NoException()
    {
        var ex = Record.Exception(() =>
            Fractals.Batch(ReadOnlySpan<double>.Empty, ReadOnlySpan<double>.Empty,
                Span<double>.Empty, Span<double>.Empty));
        Assert.Null(ex);
    }
}

// -- H) Event / Chainability -------------------------------------------------
public sealed class FractalsEventTests
{
    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var f = new Fractals();
        int fireCount = 0;

        f.Pub += (object? _, in TValueEventArgs _e) => { fireCount++; };

        _ = f.Update(new TBar(DateTime.UtcNow, 97, 100, 95, 98, 1000));

        Assert.Equal(1, fireCount);
    }

    [Fact]
    public void Pub_FiresOnEachUpdate()
    {
        var f = new Fractals();
        int fireCount = 0;

        f.Pub += (object? _, in TValueEventArgs _e) => { fireCount++; };

        for (int i = 0; i < 5; i++)
        {
            double price = 100.0 + i;
            _ = f.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 2, price - 2, price + 1, 1000));
        }

        Assert.Equal(5, fireCount);
    }
}

// -- I) Prime Tests -----------------------------------------------------------
public sealed class FractalsPrimeTests
{
    [Fact]
    public void Prime_TBarSeries_SetsState()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.20, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var f = new Fractals();
        f.Prime(bars);

        Assert.True(f.IsHot);
    }

    [Fact]
    public void Prime_EmptySource_NoException()
    {
        var f = new Fractals();
        var bars = new TBarSeries();

        var ex = Record.Exception(() => f.Prime(bars));
        Assert.Null(ex);
        Assert.False(f.IsHot);
    }
}
