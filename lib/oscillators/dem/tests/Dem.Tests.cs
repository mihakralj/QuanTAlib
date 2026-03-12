using System.Runtime.CompilerServices;
using Xunit;

namespace QuanTAlib.Tests;

public sealed class DemTests
{
    private readonly GBM _gbm = new(100.0, 0.05, 0.2, seed: 42);
    private const double Tolerance = 1e-9;

    // ───── A) Constructor validation ─────

    [Fact]
    public void Constructor_DefaultPeriod_IsValid()
    {
        var dem = new Dem();
        Assert.Equal("Dem(14)", dem.Name);
        Assert.Equal(15, dem.WarmupPeriod);
    }

    [Fact]
    public void Constructor_ZeroPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Dem(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Dem(period: -1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_Period1_IsValid()
    {
        var dem = new Dem(period: 1);
        Assert.Equal("Dem(1)", dem.Name);
        Assert.Equal(2, dem.WarmupPeriod);
    }

    [Fact]
    public void Constructor_CustomPeriod_SetsCorrectly()
    {
        var dem = new Dem(period: 14);
        Assert.Equal("Dem(14)", dem.Name);
        Assert.Equal(15, dem.WarmupPeriod);
    }

    // ───── B) Basic calculation ─────

    [Fact]
    public void Update_ReturnsTValue()
    {
        var dem = new Dem(period: 5);
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        var result = dem.Update(bar);
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var dem = new Dem(period: 5);
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        dem.Update(bar);
        Assert.True(double.IsFinite(dem.Last.Value));
    }

    [Fact]
    public void Update_KnownValue_Period1_DeMaxOnly()
    {
        // period=1: SMA_DeMax=DeMax, SMA_DeMin=DeMin for that single bar
        // Bar 1: prevH=100, prevL=90; H=110, L=80 → DeMax=10, DeMin=10
        // DEM = 10/(10+10) = 0.5
        var dem = new Dem(period: 1);
        var bar1 = new TBar(DateTime.UtcNow, 100, 100, 90, 95, 1000);
        dem.Update(bar1, isNew: true);  // first bar, prevHigh=High, prevLow=Low → DeMax=DeMin=0
        // bar2 uses bar1 as prev: prevHigh=100, prevLow=90
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 100, 110, 80, 100, 1000);
        var result = dem.Update(bar2, isNew: true);
        // DeMax = max(110-100, 0) = 10; DeMin = max(90-80, 0) = 10 → DEM = 10/20 = 0.5
        Assert.Equal(0.5, result.Value, Tolerance);
    }

    [Fact]
    public void Update_KnownValue_Period1_PureBullish()
    {
        // Bar 2: High much higher than prevHigh, Low same as prevLow → DeMin=0
        // DEM = DeMax / (DeMax + 0) = 1.0
        var dem = new Dem(period: 1);
        var bar1 = new TBar(DateTime.UtcNow, 100, 100, 90, 95, 1000);
        dem.Update(bar1, isNew: true);
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 100, 110, 90, 105, 1000);
        var result = dem.Update(bar2, isNew: true);
        // DeMax = max(110-100, 0) = 10; DeMin = max(90-90, 0) = 0 → DEM = 10/10 = 1.0
        Assert.Equal(1.0, result.Value, Tolerance);
    }

    [Fact]
    public void Update_KnownValue_Period1_PureBearish()
    {
        // Bar 2: Low much lower than prevLow, High same as prevHigh → DeMax=0
        // DEM = 0 / (0 + DeMin) = 0.0
        var dem = new Dem(period: 1);
        var bar1 = new TBar(DateTime.UtcNow, 100, 100, 90, 95, 1000);
        dem.Update(bar1, isNew: true);
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 100, 100, 80, 85, 1000);
        var result = dem.Update(bar2, isNew: true);
        // DeMax = max(100-100, 0) = 0; DeMin = max(90-80, 0) = 10 → DEM = 0/10 = 0.0
        Assert.Equal(0.0, result.Value, Tolerance);
    }

    [Fact]
    public void Update_OutputInRange_0_to_1()
    {
        var dem = new Dem(period: 14);
        for (int i = 0; i < 50; i++)
        {
            var result = dem.Update(_gbm.Next(isNew: true));
            if (dem.IsHot)
            {
                Assert.True(result.Value >= 0.0, $"DEM below 0: {result.Value}");
                Assert.True(result.Value <= 1.0, $"DEM above 1: {result.Value}");
            }
        }
    }

    // ───── C) State + bar correction ─────

    [Fact]
    public void Update_IsNew_True_AdvancesState()
    {
        var dem = new Dem(period: 5);
        for (int i = 0; i < 20; i++)
        {
            dem.Update(_gbm.Next(isNew: true), isNew: true);
        }
        Assert.True(double.IsFinite(dem.Last.Value));
    }

    [Fact]
    public void Update_IsNew_False_RollsBack()
    {
        var dem = new Dem(period: 5);
        for (int i = 0; i < 12; i++)
        {
            dem.Update(_gbm.Next(isNew: true), isNew: true);
        }

        var bar = new TBar(DateTime.UtcNow, 105, 110, 100, 107, 1000);
        dem.Update(bar, isNew: false);
        double corrected1 = dem.Last.Value;

        dem.Update(bar, isNew: false);
        double corrected2 = dem.Last.Value;

        Assert.Equal(corrected1, corrected2, Tolerance);
    }

    [Fact]
    public void Update_IterativeCorrections_Restore()
    {
        var dem = new Dem(period: 5);
        var bars = new TBar[15];
        for (int i = 0; i < bars.Length; i++)
        {
            bars[i] = _gbm.Next(isNew: true);
        }

        foreach (var b in bars)
        {
            dem.Update(b, isNew: true);
        }

        double baseline = dem.Last.Value;

        // Corrupt with wildly different values
        dem.Update(new TBar(DateTime.UtcNow, 200, 250, 150, 220, 5000), isNew: false);
        dem.Update(new TBar(DateTime.UtcNow, 999, 1050, 900, 1000, 9999), isNew: false);
        // Restore with original last bar
        dem.Update(bars[^1], isNew: false);

        Assert.Equal(baseline, dem.Last.Value, Tolerance);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var dem = new Dem(period: 5);
        for (int i = 0; i < 20; i++)
        {
            dem.Update(_gbm.Next(isNew: true), isNew: true);
        }

        dem.Reset();

        Assert.False(dem.IsHot);
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        dem.Update(bar, isNew: true);
        Assert.False(dem.IsHot);
    }

    // ───── D) Warmup / IsHot ─────

    [Fact]
    public void IsHot_BeforeWarmup_False()
    {
        var dem = new Dem(period: 5);
        // period+1 = 6 bars needed; first 6 bars should NOT yet be hot (needs > period bars)
        for (int i = 0; i < 5; i++)
        {
            dem.Update(_gbm.Next(isNew: true), isNew: true);
            Assert.False(dem.IsHot, $"Should not be hot after {i + 1} bar(s)");
        }
    }

    [Fact]
    public void IsHot_AfterWarmup_True()
    {
        var dem = new Dem(period: 5);
        for (int i = 0; i < 6; i++)
        {
            dem.Update(_gbm.Next(isNew: true), isNew: true);
        }
        Assert.True(dem.IsHot);
    }

    [Fact]
    public void WarmupPeriod_IsPeriodPlusOne()
    {
        Assert.Equal(15, new Dem(14).WarmupPeriod);
        Assert.Equal(2, new Dem(1).WarmupPeriod);
        Assert.Equal(27, new Dem(26).WarmupPeriod);
    }

    // ───── E) Robustness ─────

    [Fact]
    public void Update_NaNInput_UsesLastValid()
    {
        var dem = new Dem(period: 5);
        for (int i = 0; i < 20; i++)
        {
            dem.Update(_gbm.Next(isNew: true), isNew: true);
        }

        var nanBar = new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 0);
        var result = dem.Update(nanBar, isNew: true);
        Assert.True(double.IsFinite(result.Value), "NaN input should not produce NaN output");
    }

    [Fact]
    public void Update_InfinityInput_Handled()
    {
        var dem = new Dem(period: 5);
        for (int i = 0; i < 20; i++)
        {
            dem.Update(_gbm.Next(isNew: true), isNew: true);
        }

        var infBar = new TBar(DateTime.UtcNow, 100, double.PositiveInfinity, 90, 100, 0);
        var result = dem.Update(infBar, isNew: true);
        Assert.True(double.IsFinite(result.Value), "Infinity input should not propagate");
    }

    [Fact]
    public void Update_BatchNaN_Safe()
    {
        var dem = new Dem(period: 5);
        for (int i = 0; i < 10; i++)
        {
            dem.Update(_gbm.Next(isNew: true), isNew: true);
        }

        for (int i = 0; i < 5; i++)
        {
            var nanBar = new TBar(DateTime.UtcNow.AddMinutes(i), double.NaN, double.NaN, double.NaN, double.NaN, 0);
            var result = dem.Update(nanBar, isNew: true);
            Assert.True(double.IsFinite(result.Value), $"Batch NaN failed at bar {i}");
        }
    }

    // ───── F) Consistency ─────

    [Fact]
    [SkipLocalsInit]
    public void Consistency_Streaming_Equals_Batch()
    {
        const int N = 200;
        const int period = 14;

        var gbm = new GBM(100.0, 0.05, 0.2, seed: 1234);
        var highs = new double[N];
        var lows = new double[N];
        var bars = new TBar[N];

        for (int i = 0; i < N; i++)
        {
            bars[i] = gbm.Next(isNew: true);
            highs[i] = bars[i].High;
            lows[i] = bars[i].Low;
        }

        // Streaming
        var dem = new Dem(period);
        for (int i = 0; i < N; i++) { dem.Update(bars[i], isNew: true); }
        double streamVal = dem.Last.Value;

        // Batch span
        var batchOut = new double[N];
        Dem.Batch(highs, lows, batchOut, period);

        Assert.Equal(streamVal, batchOut[N - 1], Tolerance);
    }

    [Fact]
    public void Consistency_Deterministic_SameSeed()
    {
        const int period = 14;
        const int N = 100;

        double run1, run2;

        var gbm1 = new GBM(100.0, 0.05, 0.2, seed: 7);
        var dem1 = new Dem(period);
        for (int i = 0; i < N; i++) { dem1.Update(gbm1.Next(isNew: true), isNew: true); }
        run1 = dem1.Last.Value;

        var gbm2 = new GBM(100.0, 0.05, 0.2, seed: 7);
        var dem2 = new Dem(period);
        for (int i = 0; i < N; i++) { dem2.Update(gbm2.Next(isNew: true), isNew: true); }
        run2 = dem2.Last.Value;

        Assert.Equal(run1, run2, Tolerance);
    }

    // ───── G) Span API tests ─────

    [Fact]
    public void Batch_ZeroPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Dem.Batch(new double[10], new double[10], new double[10], period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_MismatchedLow_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Dem.Batch(new double[10], new double[5], new double[10], period: 3));
        Assert.Equal("low", ex.ParamName);
    }

    [Fact]
    public void Batch_MismatchedOutput_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Dem.Batch(new double[10], new double[10], new double[5], period: 3));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_EmptyInput_NoThrow()
    {
        var emptyOut = Array.Empty<double>();
        Dem.Batch([], [], emptyOut, period: 5);
        Assert.Empty(emptyOut);
    }

    [Fact]
    public void Batch_OutputInRange_0_to_1()
    {
        const int N = 100;
        const int period = 14;

        var gbm = new GBM(100.0, 0.05, 0.3, seed: 99);
        var highs = new double[N];
        var lows = new double[N];
        for (int i = 0; i < N; i++)
        {
            var bar = gbm.Next(isNew: true);
            highs[i] = bar.High;
            lows[i] = bar.Low;
        }

        var output = new double[N];
        Dem.Batch(highs, lows, output, period);

        for (int i = period; i < N; i++)
        {
            Assert.True(output[i] >= 0.0, $"Batch DEM[{i}] = {output[i]} below 0");
            Assert.True(output[i] <= 1.0, $"Batch DEM[{i}] = {output[i]} above 1");
        }
    }

    [Fact]
    public void Batch_LargePeriod_UsesArrayPool()
    {
        // period > 256 forces ArrayPool path
        const int N = 500;
        const int period = 300;

        var gbm = new GBM(100.0, 0.05, 0.2, seed: 11);
        var highs = new double[N];
        var lows = new double[N];
        for (int i = 0; i < N; i++)
        {
            var bar = gbm.Next(isNew: true);
            highs[i] = bar.High;
            lows[i] = bar.Low;
        }

        var output = new double[N];
        // Should not throw
        Dem.Batch(highs, lows, output, period);
        Assert.True(double.IsFinite(output[N - 1]));
    }

    // ───── H) Chainability ─────

    [Fact]
    public void PubEvent_Fires_OnUpdate()
    {
        var dem = new Dem(period: 5);
        int count = 0;
        dem.Pub += (object? _, in TValueEventArgs e) => count++;

        for (int i = 0; i < 10; i++)
        {
            dem.Update(_gbm.Next(isNew: true), isNew: true);
        }

        Assert.Equal(10, count);
    }

    [Fact]
    public void TBarSeries_Chaining_Works()
    {
        var source = new TBarSeries();
        var dem = new Dem(source, period: 5);

        var gbm = new GBM(100.0, 0.05, 0.2, seed: 55);
        for (int i = 0; i < 20; i++)
        {
            source.Add(gbm.Next(isNew: true));
        }

        Assert.True(double.IsFinite(dem.Last.Value));
    }
}
