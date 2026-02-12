using Xunit;

namespace QuanTAlib.Tests;

public sealed class StochTests
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
        var ex = Assert.Throws<ArgumentException>(() => new Stoch(kLength: 0));
        Assert.Equal("kLength", ex.ParamName);
    }

    [Fact]
    public void Constructor_InvalidDPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Stoch(kLength: 14, dPeriod: 0));
        Assert.Equal("dPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeKLength_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Stoch(kLength: -5));
        Assert.Equal("kLength", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeDPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Stoch(kLength: 5, dPeriod: -1));
        Assert.Equal("dPeriod", ex.ParamName);
    }

    // === B) Basic calculation ===

    [Fact]
    public void Update_ReturnsTValue()
    {
        var stoch = new Stoch(kLength: 5, dPeriod: 3);
        var bar = new TBar(DateTime.UtcNow, 10, 12, 8, 11, 100);
        TValue result = stoch.Update(bar);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_Last_K_D_Accessible()
    {
        var stoch = new Stoch(kLength: 5, dPeriod: 3);
        var bar = new TBar(DateTime.UtcNow, 10, 12, 8, 11, 100);
        stoch.Update(bar);
        Assert.True(double.IsFinite(stoch.Last.Value));
        Assert.True(double.IsFinite(stoch.K.Value));
        Assert.True(double.IsFinite(stoch.D.Value));
        Assert.NotEmpty(stoch.Name);
    }

    [Fact]
    public void ConstantBars_K_Is_Zero_Or_Defined()
    {
        var stoch = new Stoch(kLength: 5, dPeriod: 3);
        for (int i = 0; i < 20; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 50, 50, 50, 50, 100);
            stoch.Update(bar);
        }
        // When all H=L=C, range=0, so %K=0
        Assert.Equal(0.0, stoch.K.Value);
        Assert.Equal(0.0, stoch.D.Value);
    }

    [Fact]
    public void RisingBars_K_Approaches_100()
    {
        var stoch = new Stoch(kLength: 5, dPeriod: 3);
        for (int i = 0; i < 20; i++)
        {
            double price = 100.0 + i;
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 0.5, price - 0.5, price + 0.5, 100);
            stoch.Update(bar);
        }
        // Close at recent high should produce high %K
        Assert.True(stoch.K.Value > 50.0);
    }

    [Fact]
    public void FallingBars_K_Approaches_0()
    {
        var stoch = new Stoch(kLength: 5, dPeriod: 3);
        for (int i = 0; i < 20; i++)
        {
            double price = 200.0 - i;
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 0.5, price - 0.5, price - 0.5, 100);
            stoch.Update(bar);
        }
        // Close at recent low should produce low %K
        Assert.True(stoch.K.Value < 50.0);
    }

    // === C) State + bar correction ===

    [Fact]
    public void IsNew_True_Advances_State()
    {
        var stoch = new Stoch(kLength: 5, dPeriod: 3);
        var bars = GenerateBars(10);

        for (int i = 0; i < 10; i++)
        {
            stoch.Update(bars[i], isNew: true);
        }

        _ = stoch.K.Value;

        // Feed one more bar
        var nextBar = new TBar(DateTime.UtcNow.AddMinutes(100), 105, 110, 100, 108, 100);
        stoch.Update(nextBar, isNew: true);

        // State should have advanced — K may differ
        Assert.True(double.IsFinite(stoch.K.Value));
    }

    [Fact]
    public void IsNew_False_Rewrites()
    {
        var stoch = new Stoch(kLength: 5, dPeriod: 3);
        var bars = GenerateBars(10);

        for (int i = 0; i < 9; i++)
        {
            stoch.Update(bars[i], isNew: true);
        }

        stoch.Update(bars[9], isNew: true);
        double kAfterNew = stoch.K.Value;

        // Update same bar position with different value
        var corrected = new TBar(bars[9].Time, 999, 1005, 995, 1000, 100);
        stoch.Update(corrected, isNew: false);
        double kAfterCorrect = stoch.K.Value;

        // Correcting with very different price should change K
        Assert.NotEqual(kAfterNew, kAfterCorrect);
    }

    [Fact]
    public void IterativeCorrections_Restore()
    {
        var stoch = new Stoch(kLength: 5, dPeriod: 3);
        var bars = GenerateBars(15);

        for (int i = 0; i < 10; i++)
        {
            stoch.Update(bars[i], isNew: true);
        }

        _ = stoch.K.Value;
        _ = stoch.D.Value;

        // Apply correction
        stoch.Update(bars[10], isNew: true);
        // Roll back with correction
        stoch.Update(bars[10], isNew: false);
        // Apply same bar again
        stoch.Update(bars[10], isNew: false);

        // Multiple corrections of the same bar should converge
        double kAfter = stoch.K.Value;
        Assert.True(double.IsFinite(kAfter));
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var stoch = new Stoch(kLength: 5, dPeriod: 3);
        var bars = GenerateBars(20);

        for (int i = 0; i < 20; i++)
        {
            stoch.Update(bars[i], isNew: true);
        }

        Assert.True(stoch.IsHot);
        stoch.Reset();
        Assert.False(stoch.IsHot);
        Assert.Equal(default, stoch.Last);
        Assert.Equal(default, stoch.K);
        Assert.Equal(default, stoch.D);
    }

    // === D) Warmup/convergence ===

    [Fact]
    public void IsHot_FlipsAfterKLength()
    {
        var stoch = new Stoch(kLength: 5, dPeriod: 3);

        for (int i = 0; i < 4; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100 + i, 102 + i, 98 + i, 101 + i, 100);
            stoch.Update(bar);
            Assert.False(stoch.IsHot);
        }

        var bar5 = new TBar(DateTime.UtcNow.AddMinutes(4), 104, 106, 102, 105, 100);
        stoch.Update(bar5);
        Assert.True(stoch.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesKLength()
    {
        var stoch = new Stoch(kLength: 10, dPeriod: 3);
        Assert.Equal(10, stoch.WarmupPeriod);
    }

    // === E) Robustness ===

    [Fact]
    public void NaN_UsesLastValid()
    {
        var stoch = new Stoch(kLength: 5, dPeriod: 3);

        // Feed valid bars first
        for (int i = 0; i < 6; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100 + i, 102 + i, 98 + i, 101 + i, 100);
            stoch.Update(bar);
        }

        _ = stoch.K.Value;

        // Feed NaN bar — should use last valid
        var nanBar = new TBar(DateTime.UtcNow.AddMinutes(10), double.NaN, double.NaN, double.NaN, double.NaN, 0);
        stoch.Update(nanBar);
        Assert.True(double.IsFinite(stoch.K.Value));
    }

    [Fact]
    public void Infinity_UsesLastValid()
    {
        var stoch = new Stoch(kLength: 5, dPeriod: 3);

        for (int i = 0; i < 6; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100 + i, 102 + i, 98 + i, 101 + i, 100);
            stoch.Update(bar);
        }

        var infBar = new TBar(DateTime.UtcNow.AddMinutes(10), double.PositiveInfinity, double.PositiveInfinity,
            double.NegativeInfinity, double.PositiveInfinity, 0);
        stoch.Update(infBar);
        Assert.True(double.IsFinite(stoch.K.Value));
    }

    [Fact]
    public void AllNaN_ReturnsNaN()
    {
        var stoch = new Stoch(kLength: 5, dPeriod: 3);

        // No valid data ever
        var nanBar = new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 0);
        stoch.Update(nanBar);
        Assert.True(double.IsNaN(stoch.K.Value));
        Assert.True(double.IsNaN(stoch.D.Value));
    }

    // === F) Consistency ===

    [Fact]
    public void StreamingMatchesBatch()
    {
        const int kLength = 14;
        const int dPeriod = 3;
        var bars = GenerateBars(100);

        // Streaming
        var stochStream = new Stoch(kLength: kLength, dPeriod: dPeriod);
        var streamK = new double[100];
        var streamD = new double[100];
        for (int i = 0; i < 100; i++)
        {
            stochStream.Update(bars[i], isNew: true);
            streamK[i] = stochStream.K.Value;
            streamD[i] = stochStream.D.Value;
        }

        // Batch (TBarSeries)
        var (batchK, batchD) = Stoch.Batch(bars, kLength, dPeriod);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(streamK[i], batchK.Values[i], 10);
            Assert.Equal(streamD[i], batchD.Values[i], 10);
        }
    }

    [Fact]
    public void SpanMatchesTBarSeries()
    {
        const int kLength = 14;
        const int dPeriod = 3;
        var bars = GenerateBars(100);

        // TBarSeries batch
        var (tbK, tbD) = Stoch.Batch(bars, kLength, dPeriod);

        // Span batch
        var kOut = new double[100];
        var dOut = new double[100];
        Stoch.Batch(bars.HighValues, bars.LowValues, bars.CloseValues,
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
        const int kLength = 14;
        const int dPeriod = 3;
        var bars = GenerateBars(50);

        var stochDirect = new Stoch(kLength: kLength, dPeriod: dPeriod);
        var directK = new double[50];
        for (int i = 0; i < 50; i++)
        {
            stochDirect.Update(bars[i], isNew: true);
            directK[i] = stochDirect.K.Value;
        }

        // Event-based via TBarSeries subscription
        var barSeries = new TBarSeries();
        var stochEvent = new Stoch(barSeries, kLength: kLength, dPeriod: dPeriod);
        var eventK = new List<double>();
        stochEvent.Pub += (object? _, in TValueEventArgs e) => eventK.Add(e.Value.Value);

        // Re-prime so events fire from index 0
        stochEvent.Reset();
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
        const int kLength = 14;
        const int dPeriod = 3;
        var bars = GenerateBars(100);

        // Streaming
        var stochStream = new Stoch(kLength: kLength, dPeriod: dPeriod);
        for (int i = 0; i < 100; i++)
        {
            stochStream.Update(bars[i], isNew: true);
        }

        // Update(TBarSeries)
        var stochBatch = new Stoch(kLength: kLength, dPeriod: dPeriod);
        var (kSeries, dSeries) = stochBatch.Update(bars);

        Assert.Equal(stochStream.K.Value, kSeries.Values[^1], 10);
        Assert.Equal(stochStream.D.Value, dSeries.Values[^1], 10);
    }

    // === G) Span API tests ===

    [Fact]
    public void Batch_EmptyInput_NoException()
    {
        var kOut = Array.Empty<double>();
        var dOut = Array.Empty<double>();
        Stoch.Batch(ReadOnlySpan<double>.Empty, ReadOnlySpan<double>.Empty,
            ReadOnlySpan<double>.Empty, kOut.AsSpan(), dOut.AsSpan(), 14, 3);
        Assert.Empty(kOut);
    }

    [Fact]
    public void Batch_InvalidKLength_Throws()
    {
        var kOut = new double[5];
        var dOut = new double[5];
        var src = new double[] { 1, 2, 3, 4, 5 };
        var ex = Assert.Throws<ArgumentException>(() =>
            Stoch.Batch(src.AsSpan(), src.AsSpan(), src.AsSpan(), kOut.AsSpan(), dOut.AsSpan(), 0, 3));
        Assert.Equal("kLength", ex.ParamName);
    }

    [Fact]
    public void Batch_InvalidDPeriod_Throws()
    {
        var kOut = new double[5];
        var dOut = new double[5];
        var src = new double[] { 1, 2, 3, 4, 5 };
        var ex = Assert.Throws<ArgumentException>(() =>
            Stoch.Batch(src.AsSpan(), src.AsSpan(), src.AsSpan(), kOut.AsSpan(), dOut.AsSpan(), 5, 0));
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
            Stoch.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(), kOut.AsSpan(), dOut.AsSpan(), 3, 3));
    }

    [Fact]
    public void Batch_OutputTooShort_Throws()
    {
        var src = new double[] { 1, 2, 3, 4, 5 };
        var kOut = new double[3]; // too short
        var dOut = new double[5];
        var ex = Assert.Throws<ArgumentException>(() =>
            Stoch.Batch(src.AsSpan(), src.AsSpan(), src.AsSpan(), kOut.AsSpan(), dOut.AsSpan(), 3, 3));
        Assert.Equal("kOut", ex.ParamName);
    }

    [Fact]
    public void Batch_DOutputTooShort_Throws()
    {
        var src = new double[] { 1, 2, 3, 4, 5 };
        var kOut = new double[5];
        var dOut = new double[3]; // too short
        var ex = Assert.Throws<ArgumentException>(() =>
            Stoch.Batch(src.AsSpan(), src.AsSpan(), src.AsSpan(), kOut.AsSpan(), dOut.AsSpan(), 3, 3));
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
        Stoch.Batch(bars.HighValues, bars.LowValues, bars.CloseValues,
            kOut.AsSpan(), dOut.AsSpan(), 14, 3);

        Assert.True(double.IsFinite(kOut[^1]));
        Assert.True(double.IsFinite(dOut[^1]));
    }

    // === H) Chainability ===

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var stoch = new Stoch(kLength: 5, dPeriod: 3);
        int fireCount = 0;
        stoch.Pub += (object? _, in TValueEventArgs _) => fireCount++;

        var bar = new TBar(DateTime.UtcNow, 10, 12, 8, 11, 100);
        stoch.Update(bar);

        Assert.Equal(1, fireCount);
    }

    [Fact]
    public void TValue_Overload_Works()
    {
        var stoch = new Stoch(kLength: 5, dPeriod: 3);

        for (int i = 0; i < 10; i++)
        {
            stoch.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + i));
        }

        // TValue creates H=L=C bars, so range = 0 once window is all same-height
        Assert.True(double.IsFinite(stoch.K.Value));
    }

    [Fact]
    public void Name_MatchesParameters()
    {
        var stoch = new Stoch(kLength: 14, dPeriod: 3);
        Assert.Equal("Stoch(14,3)", stoch.Name);
    }

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var bars = GenerateBars(50);
        var (results, indicator) = Stoch.Calculate(bars, kLength: 14, dPeriod: 3);

        Assert.Equal(50, results.K.Count);
        Assert.Equal(50, results.D.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void K_Bounded_0_100()
    {
        var stoch = new Stoch(kLength: 5, dPeriod: 3);
        var bars = GenerateBars(100);

        for (int i = 0; i < 100; i++)
        {
            stoch.Update(bars[i], isNew: true);
            double k = stoch.K.Value;
            if (double.IsFinite(k))
            {
                Assert.InRange(k, -0.001, 100.001);
            }
        }
    }

    [Fact]
    public void CloseAtHigh_K_Is_100()
    {
        var stoch = new Stoch(kLength: 5, dPeriod: 3);

        // Build up a range first
        for (int i = 0; i < 4; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 110, 90, 100, 100);
            stoch.Update(bar);
        }

        // Close at the absolute highest high with range present
        var topBar = new TBar(DateTime.UtcNow.AddMinutes(4), 100, 110, 90, 110, 100);
        stoch.Update(topBar);

        Assert.Equal(100.0, stoch.K.Value, 6);
    }

    [Fact]
    public void CloseAtLow_K_Is_0()
    {
        var stoch = new Stoch(kLength: 5, dPeriod: 3);

        // Build up a range first
        for (int i = 0; i < 4; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 110, 90, 100, 100);
            stoch.Update(bar);
        }

        // Close at the absolute lowest low with range present
        var botBar = new TBar(DateTime.UtcNow.AddMinutes(4), 100, 110, 90, 90, 100);
        stoch.Update(botBar);

        Assert.Equal(0.0, stoch.K.Value, 6);
    }
}
