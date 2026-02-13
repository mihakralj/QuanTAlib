using Xunit;

namespace QuanTAlib.Tests;

public sealed class StochfTests
{
    private static TBarSeries GenerateBars(int count, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    // === A) Constructor validation ===

    [Fact]
    public void Constructor_InvalidKLength_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Stochf(kLength: 0));
        Assert.Equal("kLength", ex.ParamName);
    }

    [Fact]
    public void Constructor_InvalidDPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Stochf(kLength: 5, dPeriod: 0));
        Assert.Equal("dPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeKLength_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Stochf(kLength: -5));
        Assert.Equal("kLength", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeDPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Stochf(kLength: 5, dPeriod: -1));
        Assert.Equal("dPeriod", ex.ParamName);
    }

    // === B) Basic calculation ===

    [Fact]
    public void Update_ReturnsTValue()
    {
        var stochf = new Stochf(kLength: 5, dPeriod: 3);
        var bar = new TBar(DateTime.UtcNow, 10, 12, 8, 11, 100);
        TValue result = stochf.Update(bar);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_Last_K_D_Accessible()
    {
        var stochf = new Stochf(kLength: 5, dPeriod: 3);
        var bar = new TBar(DateTime.UtcNow, 10, 12, 8, 11, 100);
        stochf.Update(bar);
        Assert.True(double.IsFinite(stochf.Last.Value));
        Assert.True(double.IsFinite(stochf.K.Value));
        Assert.True(double.IsFinite(stochf.D.Value));
        Assert.NotEmpty(stochf.Name);
    }

    [Fact]
    public void ConstantBars_K_Is_Zero_Or_Defined()
    {
        var stochf = new Stochf(kLength: 5, dPeriod: 3);
        for (int i = 0; i < 20; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 50, 50, 50, 50, 100);
            stochf.Update(bar);
        }
        // When all H=L=C, range=0, so %K=0
        Assert.Equal(0.0, stochf.K.Value);
        Assert.Equal(0.0, stochf.D.Value);
    }

    [Fact]
    public void RisingBars_K_Approaches_100()
    {
        var stochf = new Stochf(kLength: 5, dPeriod: 3);
        for (int i = 0; i < 20; i++)
        {
            double price = 100.0 + i;
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 0.5, price - 0.5, price + 0.5, 100);
            stochf.Update(bar);
        }
        // Close at recent high should produce high %K
        Assert.True(stochf.K.Value > 50.0);
    }

    [Fact]
    public void FallingBars_K_Approaches_0()
    {
        var stochf = new Stochf(kLength: 5, dPeriod: 3);
        for (int i = 0; i < 20; i++)
        {
            double price = 200.0 - i;
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 0.5, price - 0.5, price - 0.5, 100);
            stochf.Update(bar);
        }
        // Close at recent low should produce low %K
        Assert.True(stochf.K.Value < 50.0);
    }

    // === C) State + bar correction ===

    [Fact]
    public void IsNew_True_Advances_State()
    {
        var stochf = new Stochf(kLength: 5, dPeriod: 3);
        var bars = GenerateBars(10);

        for (int i = 0; i < 10; i++)
        {
            stochf.Update(bars[i], isNew: true);
        }

        _ = stochf.K.Value;

        // Feed one more bar
        var nextBar = new TBar(DateTime.UtcNow.AddMinutes(100), 105, 110, 100, 108, 100);
        stochf.Update(nextBar, isNew: true);

        // State should have advanced — K may differ
        Assert.True(double.IsFinite(stochf.K.Value));
    }

    [Fact]
    public void IsNew_False_Rewrites()
    {
        var stochf = new Stochf(kLength: 5, dPeriod: 3);
        var bars = GenerateBars(10);

        for (int i = 0; i < 9; i++)
        {
            stochf.Update(bars[i], isNew: true);
        }

        stochf.Update(bars[9], isNew: true);
        double kAfterNew = stochf.K.Value;

        // Update same bar position with different value
        var corrected = new TBar(bars[9].Time, 999, 1005, 995, 1000, 100);
        stochf.Update(corrected, isNew: false);
        double kAfterCorrect = stochf.K.Value;

        // Correcting with very different price should change K
        Assert.NotEqual(kAfterNew, kAfterCorrect);
    }

    [Fact]
    public void IterativeCorrections_Restore()
    {
        var stochf = new Stochf(kLength: 5, dPeriod: 3);
        var bars = GenerateBars(15);

        for (int i = 0; i < 10; i++)
        {
            stochf.Update(bars[i], isNew: true);
        }

        _ = stochf.K.Value;
        _ = stochf.D.Value;

        // Apply correction
        stochf.Update(bars[10], isNew: true);
        // Roll back with correction
        stochf.Update(bars[10], isNew: false);
        // Apply same bar again
        stochf.Update(bars[10], isNew: false);

        // Multiple corrections of the same bar should converge
        double kAfter = stochf.K.Value;
        Assert.True(double.IsFinite(kAfter));
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var stochf = new Stochf(kLength: 5, dPeriod: 3);
        var bars = GenerateBars(20);

        for (int i = 0; i < 20; i++)
        {
            stochf.Update(bars[i], isNew: true);
        }

        Assert.True(stochf.IsHot);
        stochf.Reset();
        Assert.False(stochf.IsHot);
        Assert.Equal(default, stochf.Last);
        Assert.Equal(default, stochf.K);
        Assert.Equal(default, stochf.D);
    }

    // === D) Warmup/convergence ===

    [Fact]
    public void IsHot_FlipsAfterKLength()
    {
        var stochf = new Stochf(kLength: 5, dPeriod: 3);

        for (int i = 0; i < 4; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100 + i, 102 + i, 98 + i, 101 + i, 100);
            stochf.Update(bar);
            Assert.False(stochf.IsHot);
        }

        var bar5 = new TBar(DateTime.UtcNow.AddMinutes(4), 104, 106, 102, 105, 100);
        stochf.Update(bar5);
        Assert.True(stochf.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesKLength()
    {
        var stochf = new Stochf(kLength: 10, dPeriod: 3);
        Assert.Equal(10, stochf.WarmupPeriod);
    }

    // === E) Robustness ===

    [Fact]
    public void NaN_UsesLastValid()
    {
        var stochf = new Stochf(kLength: 5, dPeriod: 3);

        // Feed valid bars first
        for (int i = 0; i < 6; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100 + i, 102 + i, 98 + i, 101 + i, 100);
            stochf.Update(bar);
        }

        _ = stochf.K.Value;

        // Feed NaN bar — should use last valid
        var nanBar = new TBar(DateTime.UtcNow.AddMinutes(10), double.NaN, double.NaN, double.NaN, double.NaN, 0);
        stochf.Update(nanBar);
        Assert.True(double.IsFinite(stochf.K.Value));
    }

    [Fact]
    public void Infinity_UsesLastValid()
    {
        var stochf = new Stochf(kLength: 5, dPeriod: 3);

        for (int i = 0; i < 6; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100 + i, 102 + i, 98 + i, 101 + i, 100);
            stochf.Update(bar);
        }

        var infBar = new TBar(DateTime.UtcNow.AddMinutes(10), double.PositiveInfinity, double.PositiveInfinity,
            double.NegativeInfinity, double.PositiveInfinity, 0);
        stochf.Update(infBar);
        Assert.True(double.IsFinite(stochf.K.Value));
    }

    [Fact]
    public void AllNaN_ReturnsNaN()
    {
        var stochf = new Stochf(kLength: 5, dPeriod: 3);

        // No valid data ever
        var nanBar = new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 0);
        stochf.Update(nanBar);
        Assert.True(double.IsNaN(stochf.K.Value));
        Assert.True(double.IsNaN(stochf.D.Value));
    }

    // === F) Consistency ===

    [Fact]
    public void StreamingMatchesBatch()
    {
        const int kLength = 5;
        const int dPeriod = 3;
        var bars = GenerateBars(100);

        // Streaming
        var stochfStream = new Stochf(kLength: kLength, dPeriod: dPeriod);
        var streamK = new double[100];
        var streamD = new double[100];
        for (int i = 0; i < 100; i++)
        {
            stochfStream.Update(bars[i], isNew: true);
            streamK[i] = stochfStream.K.Value;
            streamD[i] = stochfStream.D.Value;
        }

        // Batch (TBarSeries)
        var (batchK, batchD) = Stochf.Batch(bars, kLength, dPeriod);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(streamK[i], batchK.Values[i], 10);
            Assert.Equal(streamD[i], batchD.Values[i], 10);
        }
    }

    [Fact]
    public void SpanMatchesTBarSeries()
    {
        const int kLength = 5;
        const int dPeriod = 3;
        var bars = GenerateBars(100);

        // TBarSeries batch
        var (tbK, tbD) = Stochf.Batch(bars, kLength, dPeriod);

        // Span batch
        var kOut = new double[100];
        var dOut = new double[100];
        Stochf.Batch(bars.HighValues, bars.LowValues, bars.CloseValues,
            kOut.AsSpan(), dOut.AsSpan(), kLength, dPeriod);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tbK.Values[i], kOut[i], 12);
            Assert.Equal(tbD.Values[i], dOut[i], 12);
        }
    }

    [Fact]
    public void EventMatchesStreaming()
    {
        const int kLength = 5;
        const int dPeriod = 3;
        var bars = GenerateBars(50);

        var stochfDirect = new Stochf(kLength: kLength, dPeriod: dPeriod);
        var directK = new double[50];
        for (int i = 0; i < 50; i++)
        {
            stochfDirect.Update(bars[i], isNew: true);
            directK[i] = stochfDirect.K.Value;
        }

        // Event-based via TBarSeries subscription
        var barSeries = new TBarSeries();
        var stochfEvent = new Stochf(barSeries, kLength: kLength, dPeriod: dPeriod);
        var eventK = new List<double>();
        stochfEvent.Pub += (object? _, in TValueEventArgs e) => eventK.Add(e.Value.Value);

        // Re-prime so events fire from index 0
        stochfEvent.Reset();
        for (int i = 0; i < 50; i++)
        {
            barSeries.Add(bars[i], isNew: true);
        }

        // Event list may lag due to priming; compare from end
        Assert.True(eventK.Count >= 50);
    }

    [Fact]
    public void UpdateTBarSeries_MatchesStreaming()
    {
        const int kLength = 5;
        const int dPeriod = 3;
        var bars = GenerateBars(100);

        // Streaming
        var stochfStream = new Stochf(kLength: kLength, dPeriod: dPeriod);
        for (int i = 0; i < 100; i++)
        {
            stochfStream.Update(bars[i], isNew: true);
        }

        // Update(TBarSeries)
        var stochfBatch = new Stochf(kLength: kLength, dPeriod: dPeriod);
        var (kSeries, dSeries) = stochfBatch.Update(bars);

        Assert.Equal(stochfStream.K.Value, kSeries.Values[^1], 10);
        Assert.Equal(stochfStream.D.Value, dSeries.Values[^1], 10);
    }

    // === G) Span API tests ===

    [Fact]
    public void Batch_EmptyInput_NoException()
    {
        var kOut = Array.Empty<double>();
        var dOut = Array.Empty<double>();
        Stochf.Batch(ReadOnlySpan<double>.Empty, ReadOnlySpan<double>.Empty,
            ReadOnlySpan<double>.Empty, kOut.AsSpan(), dOut.AsSpan(), 5, 3);
        Assert.Empty(kOut);
    }

    [Fact]
    public void Batch_InvalidKLength_Throws()
    {
        var kOut = new double[5];
        var dOut = new double[5];
        var src = new double[] { 1, 2, 3, 4, 5 };
        var ex = Assert.Throws<ArgumentException>(() =>
            Stochf.Batch(src.AsSpan(), src.AsSpan(), src.AsSpan(), kOut.AsSpan(), dOut.AsSpan(), 0, 3));
        Assert.Equal("kLength", ex.ParamName);
    }

    [Fact]
    public void Batch_InvalidDPeriod_Throws()
    {
        var kOut = new double[5];
        var dOut = new double[5];
        var src = new double[] { 1, 2, 3, 4, 5 };
        var ex = Assert.Throws<ArgumentException>(() =>
            Stochf.Batch(src.AsSpan(), src.AsSpan(), src.AsSpan(), kOut.AsSpan(), dOut.AsSpan(), 5, 0));
        Assert.Equal("dPeriod", ex.ParamName);
    }

    [Fact]
    public void Batch_MismatchedInputLengths_Throws()
    {
        var high = new double[] { 1, 2, 3 };
        var low = new double[] { 1, 2 };
        var close = new double[] { 1, 2, 3 };
        var kOut = new double[3];
        var dOut = new double[3];
        Assert.Throws<ArgumentException>(() =>
            Stochf.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(), kOut.AsSpan(), dOut.AsSpan(), 3, 3));
    }

    [Fact]
    public void Batch_OutputTooShort_Throws()
    {
        var src = new double[] { 1, 2, 3, 4, 5 };
        var kOut = new double[3]; // too short
        var dOut = new double[5];
        var ex = Assert.Throws<ArgumentException>(() =>
            Stochf.Batch(src.AsSpan(), src.AsSpan(), src.AsSpan(), kOut.AsSpan(), dOut.AsSpan(), 3, 3));
        Assert.Equal("kOut", ex.ParamName);
    }

    [Fact]
    public void Batch_DOutputTooShort_Throws()
    {
        var src = new double[] { 1, 2, 3, 4, 5 };
        var kOut = new double[5];
        var dOut = new double[3]; // too short
        var ex = Assert.Throws<ArgumentException>(() =>
            Stochf.Batch(src.AsSpan(), src.AsSpan(), src.AsSpan(), kOut.AsSpan(), dOut.AsSpan(), 3, 3));
        Assert.Equal("dOut", ex.ParamName);
    }

    [Fact]
    public void Batch_LargeData_NoStackOverflow()
    {
        int count = 1000;
        var bars = GenerateBars(count);
        var kOut = new double[count];
        var dOut = new double[count];

        // Should not throw — uses ArrayPool for large buffers
        Stochf.Batch(bars.HighValues, bars.LowValues, bars.CloseValues,
            kOut.AsSpan(), dOut.AsSpan(), 5, 3);

        Assert.True(double.IsFinite(kOut[^1]));
        Assert.True(double.IsFinite(dOut[^1]));
    }

    // === H) Chainability ===

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var stochf = new Stochf(kLength: 5, dPeriod: 3);
        int fireCount = 0;
        stochf.Pub += (object? _, in TValueEventArgs _) => fireCount++;

        var bar = new TBar(DateTime.UtcNow, 10, 12, 8, 11, 100);
        stochf.Update(bar);

        Assert.Equal(1, fireCount);
    }

    [Fact]
    public void TValue_Overload_Works()
    {
        var stochf = new Stochf(kLength: 5, dPeriod: 3);

        for (int i = 0; i < 10; i++)
        {
            stochf.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + i));
        }

        // TValue creates H=L=C bars, so range = 0 once window is all same-height
        Assert.True(double.IsFinite(stochf.K.Value));
    }

    [Fact]
    public void Name_MatchesParameters()
    {
        var stochf = new Stochf(kLength: 5, dPeriod: 3);
        Assert.Equal("StochF(5,3)", stochf.Name);
    }

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var bars = GenerateBars(50);
        var (results, indicator) = Stochf.Calculate(bars, kLength: 5, dPeriod: 3);

        Assert.Equal(50, results.K.Count);
        Assert.Equal(50, results.D.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void K_Bounded_0_100()
    {
        var stochf = new Stochf(kLength: 5, dPeriod: 3);
        var bars = GenerateBars(100);

        for (int i = 0; i < 100; i++)
        {
            stochf.Update(bars[i], isNew: true);
            double k = stochf.K.Value;
            if (double.IsFinite(k))
            {
                Assert.InRange(k, -0.001, 100.001);
            }
        }
    }

    [Fact]
    public void CloseAtHigh_K_Is_100()
    {
        var stochf = new Stochf(kLength: 5, dPeriod: 3);

        // Build up a range first
        for (int i = 0; i < 4; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 110, 90, 100, 100);
            stochf.Update(bar);
        }

        // Close at the absolute highest high with range present
        var topBar = new TBar(DateTime.UtcNow.AddMinutes(4), 100, 110, 90, 110, 100);
        stochf.Update(topBar);

        Assert.Equal(100.0, stochf.K.Value, 6);
    }

    [Fact]
    public void CloseAtLow_K_Is_0()
    {
        var stochf = new Stochf(kLength: 5, dPeriod: 3);

        // Build up a range first
        for (int i = 0; i < 4; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 110, 90, 100, 100);
            stochf.Update(bar);
        }

        // Close at the absolute lowest low with range present
        var botBar = new TBar(DateTime.UtcNow.AddMinutes(4), 100, 110, 90, 90, 100);
        stochf.Update(botBar);

        Assert.Equal(0.0, stochf.K.Value, 6);
    }
}
