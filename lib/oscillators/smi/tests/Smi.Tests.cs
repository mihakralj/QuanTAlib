using Xunit;

namespace QuanTAlib.Tests;

public sealed class SmiTests
{
    private const double Tolerance = 1e-10;

    // --- A) Constructor validation ---

    [Fact]
    public void Constructor_ZeroKPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Smi(kPeriod: 0));
        Assert.Equal("kPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroKSmooth_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Smi(kSmooth: 0));
        Assert.Equal("kSmooth", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroDSmooth_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Smi(dSmooth: 0));
        Assert.Equal("dSmooth", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeKPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Smi(kPeriod: -1));
        Assert.Equal("kPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_Valid_SetsNameAndWarmup()
    {
        var smi = new Smi(10, 3, 3);
        Assert.Equal("Smi(10,3,3)", smi.Name);
        Assert.Equal(10 + 3 + 3, smi.WarmupPeriod);
        Assert.False(smi.IsHot);
    }

    // --- B) Basic calculation ---

    [Fact]
    public void Update_ConstantBars_KIsZero()
    {
        var smi = new Smi(5, 3, 3);

        for (int i = 0; i < 50; i++)
        {
            long t = DateTime.UtcNow.Ticks + i;
            smi.Update(new TBar(t, 100, 100, 100, 100, 1000));
        }

        Assert.Equal(0.0, smi.K.Value, 1e-6);
        Assert.Equal(0.0, smi.D.Value, 1e-6);
    }

    [Fact]
    public void Update_RisingClose_PositiveK()
    {
        var smi = new Smi(5, 3, 3);

        for (int i = 0; i < 30; i++)
        {
            long t = DateTime.UtcNow.Ticks + i;
            double c = 100.0 + i;
            smi.Update(new TBar(t, c, c + 5, c - 5, c, 1000));
        }

        Assert.True(smi.K.Value > 0.0, "Rising close should produce positive K");
    }

    [Fact]
    public void Update_FallingClose_NegativeK()
    {
        var smi = new Smi(5, 3, 3);

        for (int i = 0; i < 30; i++)
        {
            long t = DateTime.UtcNow.Ticks + i;
            double c = 200.0 - i;
            smi.Update(new TBar(t, c, c + 5, c - 5, c, 1000));
        }

        Assert.True(smi.K.Value < 0.0, "Falling close should produce negative K");
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var smi = new Smi(5, 3, 3);

        for (int i = 0; i < 20; i++)
        {
            long t = DateTime.UtcNow.Ticks + i;
            smi.Update(new TBar(t, 100 + i, 110 + i, 90 + i, 105 + i, 1000));
        }

        Assert.True(double.IsFinite(smi.Last.Value));
        Assert.True(double.IsFinite(smi.K.Value));
        Assert.True(double.IsFinite(smi.D.Value));
        Assert.True(smi.IsHot);
    }

    // --- C) State + bar correction ---

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var smi = new Smi(5, 3, 3);
        long t = DateTime.UtcNow.Ticks;

        for (int i = 0; i < 10; i++)
        {
            smi.Update(new TBar(t + i, 100 + i, 110 + i, 90 + i, 105 + i, 1000), isNew: true);
        }

        double k1 = smi.K.Value;
        smi.Update(new TBar(t + 10, 120, 130, 110, 125, 1000), isNew: true);
        Assert.NotEqual(k1, smi.K.Value);
    }

    [Fact]
    public void Update_IsNewFalse_Rollback()
    {
        var smi = new Smi(5, 3, 3);
        long t = DateTime.UtcNow.Ticks;

        for (int i = 0; i < 10; i++)
        {
            smi.Update(new TBar(t + i, 100 + i, 110 + i, 90 + i, 105 + i, 1000), isNew: true);
        }

        double k1 = smi.K.Value;
        smi.Update(new TBar(t + 9, 200, 210, 190, 205, 1000), isNew: false);
        smi.Update(new TBar(t + 9, 100 + 9, 110 + 9, 90 + 9, 105 + 9, 1000), isNew: false);
        Assert.Equal(k1, smi.K.Value, 1e-10);
    }

    [Fact]
    public void Update_IterativeCorrection_Restores()
    {
        var smi = new Smi(5, 3, 3);
        long t = DateTime.UtcNow.Ticks;

        for (int i = 0; i < 10; i++)
        {
            smi.Update(new TBar(t + i, 100 + i, 110 + i, 90 + i, 105 + i, 1000), isNew: true);
        }

        double k1 = smi.K.Value;

        // Multiple corrections
        for (int c = 0; c < 5; c++)
        {
            smi.Update(new TBar(t + 9, 150 + c, 160 + c, 140 + c, 155 + c, 1000), isNew: false);
        }

        // Restore original
        smi.Update(new TBar(t + 9, 109, 119, 99, 114, 1000), isNew: false);
        Assert.Equal(k1, smi.K.Value, 1e-10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var smi = new Smi(5, 3, 3);
        long t = DateTime.UtcNow.Ticks;

        for (int i = 0; i < 20; i++)
        {
            smi.Update(new TBar(t + i, 100 + i, 110 + i, 90 + i, 105 + i, 1000));
        }

        Assert.True(smi.IsHot);

        smi.Reset();

        Assert.False(smi.IsHot);
        Assert.Equal(default, smi.Last);
        Assert.Equal(default, smi.K);
        Assert.Equal(default, smi.D);
    }

    // --- D) Warmup/convergence ---

    [Fact]
    public void IsHot_FlipsAtKPeriod()
    {
        var smi = new Smi(5, 3, 3);
        long t = DateTime.UtcNow.Ticks;

        for (int i = 0; i < 4; i++)
        {
            smi.Update(new TBar(t + i, 100, 110, 90, 100, 1000));
            Assert.False(smi.IsHot);
        }

        smi.Update(new TBar(t + 4, 100, 110, 90, 100, 1000));
        Assert.True(smi.IsHot);
    }

    // --- E) Robustness ---

    [Fact]
    public void Update_NaN_UsesLastValid()
    {
        var smi = new Smi(5, 3, 3);
        long t = DateTime.UtcNow.Ticks;

        for (int i = 0; i < 10; i++)
        {
            smi.Update(new TBar(t + i, 100 + i, 110 + i, 90 + i, 105 + i, 1000));
        }

        _ = smi.K.Value;

        smi.Update(new TBar(t + 10, double.NaN, double.NaN, double.NaN, double.NaN, 1000));
        Assert.True(double.IsFinite(smi.K.Value));
    }

    [Fact]
    public void Update_Infinity_UsesLastValid()
    {
        var smi = new Smi(5, 3, 3);
        long t = DateTime.UtcNow.Ticks;

        for (int i = 0; i < 10; i++)
        {
            smi.Update(new TBar(t + i, 100 + i, 110 + i, 90 + i, 105 + i, 1000));
        }

        smi.Update(new TBar(t + 10, double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity, 1000));
        Assert.True(double.IsFinite(smi.K.Value));
    }

    // --- F) Consistency ---

    [Fact]
    public void AllModes_ProduceConsistentResults_Blau()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        const int kPeriod = 10;
        const int kSmooth = 3;
        const int dSmooth = 3;
        const bool blau = true;

        // Streaming
        var smiStream = new Smi(kPeriod, kSmooth, dSmooth, blau);
        var streamK = new double[bars.Count];
        var streamD = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            smiStream.Update(bars[i]);
            streamK[i] = smiStream.K.Value;
            streamD[i] = smiStream.D.Value;
        }

        // Batch (TBarSeries)
        var (batchK, batchD) = Smi.Batch(bars, kPeriod, kSmooth, dSmooth, blau);

        // Span
        var spanK = new double[bars.Count];
        var spanD = new double[bars.Count];
        Smi.Batch(bars.High.Values, bars.Low.Values, bars.Close.Values,
            spanK, spanD, kPeriod, kSmooth, dSmooth, blau);

        // Event
        var smiEvent = new Smi(kPeriod, kSmooth, dSmooth, blau);
        var eventK = new double[bars.Count];
        var eventD = new double[bars.Count];
        int idx = 0;
        smiEvent.Pub += (_, in e) =>
        {
            if (idx < bars.Count)
            {
                eventK[idx] = smiEvent.K.Value;
                eventD[idx] = smiEvent.D.Value;
                idx++;
            }
        };
        for (int i = 0; i < bars.Count; i++)
        {
            smiEvent.Update(bars[i]);
        }

        // Compare last 50 values (after warmup stabilizes)
        for (int i = 150; i < bars.Count; i++)
        {
            Assert.Equal(streamK[i], batchK[i].Value, 1e-6);
            Assert.Equal(streamD[i], batchD[i].Value, 1e-6);
            Assert.Equal(streamK[i], spanK[i], 1e-6);
            Assert.Equal(streamD[i], spanD[i], 1e-6);
            Assert.Equal(streamK[i], eventK[i], Tolerance);
            Assert.Equal(streamD[i], eventD[i], Tolerance);
        }
    }

    [Fact]
    public void AllModes_ProduceConsistentResults_ChandeKroll()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        const int kPeriod = 10;
        const int kSmooth = 3;
        const int dSmooth = 3;
        const bool blau = false;

        var smiStream = new Smi(kPeriod, kSmooth, dSmooth, blau);
        var streamK = new double[bars.Count];
        var streamD = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            smiStream.Update(bars[i]);
            streamK[i] = smiStream.K.Value;
            streamD[i] = smiStream.D.Value;
        }

        var spanK = new double[bars.Count];
        var spanD = new double[bars.Count];
        Smi.Batch(bars.High.Values, bars.Low.Values, bars.Close.Values,
            spanK, spanD, kPeriod, kSmooth, dSmooth, blau);

        for (int i = 150; i < bars.Count; i++)
        {
            Assert.Equal(streamK[i], spanK[i], 1e-6);
            Assert.Equal(streamD[i], spanD[i], 1e-6);
        }
    }

    // --- G) Span API tests ---

    [Fact]
    public void SpanBatch_MismatchedInputLength_Throws()
    {
        var high = new double[10];
        var low = new double[5];
        var close = new double[10];
        var kOut = new double[10];
        var dOut = new double[10];

        Assert.Throws<ArgumentException>(() =>
            Smi.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(), kOut.AsSpan(), dOut.AsSpan()));
    }

    [Fact]
    public void SpanBatch_OutputTooSmall_Throws()
    {
        var high = new double[10];
        var low = new double[10];
        var close = new double[10];
        var kOut = new double[5]; // too small
        var dOut = new double[10];

        Assert.Throws<ArgumentException>(() =>
            Smi.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(), kOut.AsSpan(), dOut.AsSpan()));
    }

    [Fact]
    public void SpanBatch_DOutputTooSmall_Throws()
    {
        var high = new double[10];
        var low = new double[10];
        var close = new double[10];
        var kOut = new double[10];
        var dOut = new double[5]; // too small

        Assert.Throws<ArgumentException>(() =>
            Smi.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(), kOut.AsSpan(), dOut.AsSpan()));
    }

    [Fact]
    public void SpanBatch_Empty_NoException()
    {
        var empty = Array.Empty<double>();
        Smi.Batch(empty, empty, empty, empty, empty);
        Assert.True(true);
    }

    [Fact]
    public void SpanBatch_InvalidKPeriod_Throws()
    {
        var h = new double[10];
        var l = new double[10];
        var c = new double[10];
        var k = new double[10];
        var d = new double[10];

        var ex = Assert.Throws<ArgumentException>(() =>
            Smi.Batch(h, l, c, k, d, kPeriod: 0));
        Assert.Equal("kPeriod", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_LargeData_NoStackOverflow()
    {
        int size = 1000;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 99);
        var bars = gbm.Fetch(size, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var kOut = new double[size];
        var dOut = new double[size];

        Smi.Batch(bars.High.Values, bars.Low.Values, bars.Close.Values, kOut, dOut);
        Assert.True(double.IsFinite(kOut[size - 1]));
        Assert.True(double.IsFinite(dOut[size - 1]));
    }

    // --- H) Chainability ---

    [Fact]
    public void PubEvent_Fires()
    {
        var smi = new Smi(5, 3, 3);
        int pubCount = 0;
        smi.Pub += (_, in _) => pubCount++;

        for (int i = 0; i < 10; i++)
        {
            long t = DateTime.UtcNow.Ticks + i;
            smi.Update(new TBar(t, 100 + i, 110 + i, 90 + i, 105 + i, 1000));
        }

        Assert.Equal(10, pubCount);
    }

    [Fact]
    public void EventChaining_Works()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var smi = new Smi(bars, 10, 3, 3);

        Assert.True(smi.IsHot);
        Assert.True(double.IsFinite(smi.K.Value));
        Assert.True(double.IsFinite(smi.D.Value));
    }

    // --- TValue overload ---

    [Fact]
    public void Update_TValue_ReturnsFinite()
    {
        var smi = new Smi(5, 3, 3);
        for (int i = 0; i < 20; i++)
        {
            long t = DateTime.UtcNow.Ticks + i;
            var result = smi.Update(new TValue(t, 100.0 + i));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    // --- Blau vs Chande/Kroll produce different results ---

    [Fact]
    public void BlauVsChandeKroll_ProduceDifferentResults()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 77);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var smiBlau = new Smi(10, 3, 3, blau: true);
        var smiCk = new Smi(10, 3, 3, blau: false);

        for (int i = 0; i < bars.Count; i++)
        {
            smiBlau.Update(bars[i]);
            smiCk.Update(bars[i]);
        }

        // They should produce different K values (different algorithms)
        Assert.NotEqual(smiBlau.K.Value, smiCk.K.Value, 1e-6);
    }

    // --- Static batch ---

    [Fact]
    public void StaticBatch_Works()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (k, d) = Smi.Batch(bars, 10, 3, 3);

        Assert.Equal(50, k.Count);
        Assert.Equal(50, d.Count);
        Assert.True(double.IsFinite(k.Last.Value));
        Assert.True(double.IsFinite(d.Last.Value));
    }

    // --- Calculate factory ---

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (results, indicator) = Smi.Calculate(bars, 10, 3, 3);

        Assert.Equal(50, results.K.Count);
        Assert.Equal(50, results.D.Count);
        Assert.True(indicator.IsHot);
    }
}
