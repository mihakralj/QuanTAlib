namespace QuanTAlib.Tests;

public class VwapsdTests
{
    [Fact]
    public void Vwapsd_Constructor_ValidatesNumDevs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Vwapsd(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Vwapsd(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Vwapsd(0.05)); // Below MinNumDevs (0.1)
        Assert.Throws<ArgumentOutOfRangeException>(() => new Vwapsd(5.1));  // Above MaxNumDevs (5.0)
        Assert.Throws<ArgumentOutOfRangeException>(() => new Vwapsd(10));   // Above MaxNumDevs (5.0)

        var vwapsd = new Vwapsd(1.0);
        Assert.NotNull(vwapsd);
    }

    [Fact]
    public void Vwapsd_Constructor_AcceptsValidRange()
    {
        // Test boundary values
        var vwapsdMin = new Vwapsd(0.1);
        Assert.NotNull(vwapsdMin);

        var vwapsdMax = new Vwapsd(5.0);
        Assert.NotNull(vwapsdMax);

        var vwapsdMid = new Vwapsd(2.5);
        Assert.NotNull(vwapsdMid);
    }

    [Fact]
    public void Vwapsd_DefaultConstructor_UsesDefaultNumDevs()
    {
        var vwapsd = new Vwapsd();
        Assert.NotNull(vwapsd);
        Assert.Contains("Vwapsd", vwapsd.Name, StringComparison.Ordinal);
        Assert.Contains("2.0", vwapsd.Name, StringComparison.Ordinal); // Default is 2.0
    }

    [Fact]
    public void Vwapsd_Update_ReturnsValue()
    {
        var vwapsd = new Vwapsd(1.0);
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000);
        var result = vwapsd.Update(bar);

        Assert.True(double.IsFinite(result.Value));
        Assert.True(double.IsFinite(vwapsd.Upper.Value));
        Assert.True(double.IsFinite(vwapsd.Lower.Value));
        Assert.True(double.IsFinite(vwapsd.Vwap.Value));
        Assert.True(double.IsFinite(vwapsd.StdDev.Value));
        Assert.True(double.IsFinite(vwapsd.Width.Value));
    }

    [Fact]
    public void Vwapsd_FirstBar_InitializesCorrectly()
    {
        var vwapsd = new Vwapsd(1.0);
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000);
        _ = vwapsd.Update(bar);

        // First bar: VWAP = HLC3 = (105+95+100)/3 = 100
        double expectedVwap = (105 + 95 + 100) / 3.0;
        Assert.Equal(expectedVwap, vwapsd.Vwap.Value, precision: 10);

        // First bar has zero variance (only 1 point)
        Assert.Equal(0, vwapsd.StdDev.Value, precision: 10);
        Assert.Equal(expectedVwap, vwapsd.Upper.Value, precision: 10);
        Assert.Equal(expectedVwap, vwapsd.Lower.Value, precision: 10);
    }

    [Fact]
    public void Vwapsd_Properties_Accessible()
    {
        var vwapsd = new Vwapsd(2.0);

        Assert.False(vwapsd.IsHot);
        Assert.Contains("Vwapsd", vwapsd.Name, StringComparison.Ordinal);
        Assert.Equal(2, vwapsd.WarmupPeriod);
    }

    [Fact]
    public void Vwapsd_Update_IsNew_AcceptsParameter()
    {
        var vwapsd = new Vwapsd(1.0);
        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000);
        var bar2 = new TBar(DateTime.UtcNow, 100, 106, 94, 101, 1100);

        var result1 = vwapsd.Update(bar1, isNew: true);
        var result2 = vwapsd.Update(bar2, isNew: false);

        Assert.True(double.IsFinite(result1.Value));
        Assert.True(double.IsFinite(result2.Value));
    }

    [Fact]
    public void Vwapsd_Update_IsNew_False_UpdatesValue()
    {
        var vwapsd = new Vwapsd(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(15, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Process several bars
        for (int i = 0; i < 15; i++)
        {
            vwapsd.Update(bars[i], isNew: true);
        }

        double beforeCorrection = vwapsd.Vwap.Value;

        // Correct last bar with different value
        var correctionBar = new TBar(DateTime.UtcNow, 200, 210, 190, 200, 5000);
        vwapsd.Update(correctionBar, isNew: false);
        double afterCorrection = vwapsd.Vwap.Value;

        Assert.NotEqual(beforeCorrection, afterCorrection);
    }

    [Fact]
    public void Vwapsd_IterativeCorrections_RestoreToOriginalState()
    {
        var vwapsd = new Vwapsd(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Process all bars
        for (int i = 0; i < bars.Count; i++)
        {
            vwapsd.Update(bars[i]);
        }
        double originalVwap = vwapsd.Vwap.Value;
        double originalUpper = vwapsd.Upper.Value;
        double originalLower = vwapsd.Lower.Value;

        // Make multiple corrections
        for (int i = 0; i < 10; i++)
        {
            var correctionBar = new TBar(DateTime.UtcNow, 150 + i, 160 + i, 140 + i, 155 + i, 2000 + i * 100);
            vwapsd.Update(correctionBar, isNew: false);
        }

        // Restore original
        vwapsd.Update(bars[^1], isNew: false);
        double restoredVwap = vwapsd.Vwap.Value;
        double restoredUpper = vwapsd.Upper.Value;
        double restoredLower = vwapsd.Lower.Value;

        Assert.Equal(originalVwap, restoredVwap, precision: 8);
        Assert.Equal(originalUpper, restoredUpper, precision: 8);
        Assert.Equal(originalLower, restoredLower, precision: 8);
    }

    [Fact]
    public void Vwapsd_Reset_ClearsState()
    {
        var vwapsd = new Vwapsd(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 20; i++)
        {
            vwapsd.Update(bars[i]);
        }

        Assert.True(vwapsd.IsHot);

        vwapsd.Reset();

        Assert.False(vwapsd.IsHot);
    }

    [Fact]
    public void Vwapsd_IsHot_BecomesTrueAfterWarmup()
    {
        var vwapsd = new Vwapsd(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // WarmupPeriod is 2
        vwapsd.Update(bars[0]);
        Assert.False(vwapsd.IsHot);

        vwapsd.Update(bars[1]);
        Assert.True(vwapsd.IsHot);
    }

    [Fact]
    public void Vwapsd_WarmupPeriod_IsSetCorrectly()
    {
        var vwapsd = new Vwapsd(1.0);
        Assert.Equal(2, vwapsd.WarmupPeriod);
    }

    [Fact]
    public void Vwapsd_NaN_Price_UsesLastValidValue()
    {
        var vwapsd = new Vwapsd(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 10; i++)
        {
            vwapsd.Update(bars[i]);
        }

        vwapsd.Update(new TValue(DateTime.UtcNow, double.NaN), 1000, isNew: true);
        double afterNaN = vwapsd.Vwap.Value;

        Assert.True(double.IsFinite(afterNaN));
    }

    [Fact]
    public void Vwapsd_NaN_Volume_UsesLastValidValue()
    {
        var vwapsd = new Vwapsd(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 10; i++)
        {
            vwapsd.Update(bars[i]);
        }

        vwapsd.Update(new TValue(DateTime.UtcNow, 100), double.NaN, isNew: true);
        double afterNaN = vwapsd.Vwap.Value;

        Assert.True(double.IsFinite(afterNaN));
    }

    [Fact]
    public void Vwapsd_Infinity_Input_UsesLastValidValue()
    {
        var vwapsd = new Vwapsd(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 10; i++)
        {
            vwapsd.Update(bars[i]);
        }

        vwapsd.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity), 1000, isNew: true);
        Assert.True(double.IsFinite(vwapsd.Vwap.Value));

        vwapsd.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity), 1000, isNew: true);
        Assert.True(double.IsFinite(vwapsd.Vwap.Value));
    }

    [Fact]
    public void Vwapsd_BandRelationship_UpperGreaterThanVwapGreaterThanLower()
    {
        var vwapsd = new Vwapsd(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            vwapsd.Update(bars[i]);

            // Skip first bar where StdDev is 0
            if (i > 0)
            {
                Assert.True(vwapsd.Upper.Value >= vwapsd.Vwap.Value,
                    $"Upper ({vwapsd.Upper.Value}) should be >= Vwap ({vwapsd.Vwap.Value})");
                Assert.True(vwapsd.Vwap.Value >= vwapsd.Lower.Value,
                    $"Vwap ({vwapsd.Vwap.Value}) should be >= Lower ({vwapsd.Lower.Value})");
            }
        }
    }

    [Fact]
    public void Vwapsd_VwapBetweenBands()
    {
        var vwapsd = new Vwapsd(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            vwapsd.Update(bars[i]);
            Assert.True(vwapsd.Vwap.Value <= vwapsd.Upper.Value,
                $"Vwap ({vwapsd.Vwap.Value}) should be <= Upper ({vwapsd.Upper.Value})");
            Assert.True(vwapsd.Vwap.Value >= vwapsd.Lower.Value,
                $"Vwap ({vwapsd.Vwap.Value}) should be >= Lower ({vwapsd.Lower.Value})");
        }
    }

    [Fact]
    public void Vwapsd_Width_EqualsUpperMinusLower()
    {
        var vwapsd = new Vwapsd(1.5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            vwapsd.Update(bars[i]);
            double expectedWidth = vwapsd.Upper.Value - vwapsd.Lower.Value;
            Assert.Equal(expectedWidth, vwapsd.Width.Value, precision: 10);
        }
    }

    [Fact]
    public void Vwapsd_SessionReset_ResetsVwapCalculation()
    {
        var vwapsd = new Vwapsd(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Process first 10 bars
        for (int i = 0; i < 10; i++)
        {
            vwapsd.Update(bars[i]);
        }
        double vwapBeforeReset = vwapsd.Vwap.Value;

        // Reset and process next bar - should start fresh
        var resetBar = new TBar(DateTime.UtcNow, 200, 210, 190, 200, 1000);
        vwapsd.Update(resetBar, isNew: true, reset: true);

        // After reset, VWAP should be just the new bar's HLC3
        double expectedVwap = (210 + 190 + 200) / 3.0;
        Assert.Equal(expectedVwap, vwapsd.Vwap.Value, precision: 10);
        Assert.NotEqual(vwapBeforeReset, vwapsd.Vwap.Value);
    }

    [Fact]
    public void Vwapsd_SessionReset_ResetsIsHotGating()
    {
        var vwapsd = new Vwapsd(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Process bars until IsHot is true (WarmupPeriod = 2)
        vwapsd.Update(bars[0]);
        Assert.False(vwapsd.IsHot);
        vwapsd.Update(bars[1]);
        Assert.True(vwapsd.IsHot);

        // Process more bars to ensure we're well past warmup
        for (int i = 2; i < 10; i++)
        {
            vwapsd.Update(bars[i]);
        }
        Assert.True(vwapsd.IsHot);

        // Reset session - IsHot should become false
        var resetBar1 = new TBar(DateTime.UtcNow, 200, 210, 190, 200, 1000);
        vwapsd.Update(resetBar1, isNew: true, reset: true);
        Assert.False(vwapsd.IsHot, "IsHot should be false after reset (1 bar accumulated)");

        // Process second bar after reset - IsHot should become true
        var resetBar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 205, 215, 195, 205, 1100);
        vwapsd.Update(resetBar2, isNew: true);
        Assert.True(vwapsd.IsHot, "IsHot should be true after 2 bars accumulated post-reset");
    }

    [Fact]
    public void Vwapsd_VwapFormula_MatchesExpected()
    {
        var vwapsd = new Vwapsd(1.0);

        // Bar 1: price=100, volume=1000
        var bar1 = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 1000);
        vwapsd.Update(bar1);
        Assert.Equal(100.0, vwapsd.Vwap.Value, precision: 10);

        // Bar 2: price=110, volume=2000
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 110, 110, 110, 110, 2000);
        vwapsd.Update(bar2);

        // VWAP = (100*1000 + 110*2000) / (1000+2000) = 320000/3000 = 106.666...
        double expectedVwap = (100.0 * 1000 + 110.0 * 2000) / (1000 + 2000);
        Assert.Equal(expectedVwap, vwapsd.Vwap.Value, precision: 10);
    }

    [Fact]
    public void Vwapsd_StdDevFormula_MatchesExpected()
    {
        var vwapsd = new Vwapsd(1.0);

        // Bar 1: price=100, volume=1
        var bar1 = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 1);
        vwapsd.Update(bar1);

        // Bar 2: price=200, volume=1
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 200, 200, 200, 200, 1);
        vwapsd.Update(bar2);

        // VWAP = (100*1 + 200*1) / 2 = 150
        // MeanP2 = (100²*1 + 200²*1) / 2 = (10000 + 40000) / 2 = 25000
        // Variance = MeanP2 - VWAP² = 25000 - 22500 = 2500
        // StdDev = sqrt(2500) = 50
        Assert.Equal(150.0, vwapsd.Vwap.Value, precision: 10);
        Assert.Equal(50.0, vwapsd.StdDev.Value, precision: 10);
        Assert.Equal(200.0, vwapsd.Upper.Value, precision: 10); // 150 + 1*50
        Assert.Equal(100.0, vwapsd.Lower.Value, precision: 10); // 150 - 1*50
    }

    [Fact]
    public void Vwapsd_NumDevs_AffectsBands()
    {
        // Test with 2 standard deviations
        var vwapsd2 = new Vwapsd(2.0);

        var bar1 = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 1);
        vwapsd2.Update(bar1);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 200, 200, 200, 200, 1);
        vwapsd2.Update(bar2);

        // VWAP = 150, StdDev = 50
        // With numDevs=2: Upper = 150 + 2*50 = 250, Lower = 150 - 2*50 = 50
        Assert.Equal(150.0, vwapsd2.Vwap.Value, precision: 10);
        Assert.Equal(50.0, vwapsd2.StdDev.Value, precision: 10);
        Assert.Equal(250.0, vwapsd2.Upper.Value, precision: 10);
        Assert.Equal(50.0, vwapsd2.Lower.Value, precision: 10);
    }

    [Fact]
    public void Vwapsd_BatchCalc_MatchesIterativeCalc()
    {
        var vwapsdIterative = new Vwapsd(1.5);
        var vwapsdBatch = new Vwapsd(1.5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Iterative
        var iterativeVwap = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            vwapsdIterative.Update(bars[i]);
            iterativeVwap.Add(vwapsdIterative.Vwap.Value);
        }

        // Batch
        var batchResult = vwapsdBatch.Update(bars);

        // Compare last 50 values
        for (int i = 50; i < 100; i++)
        {
            Assert.Equal(iterativeVwap[i], batchResult[i].Value, precision: 10);
        }
    }

    [Fact]
    public void Vwapsd_StaticCalculate_TBarSeries_Works()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (upper, lower, vwap, stdev) = Vwapsd.Calculate(bars, 1.5);

        Assert.Equal(50, upper.Count);
        Assert.Equal(50, lower.Count);
        Assert.Equal(50, vwap.Count);
        Assert.Equal(50, stdev.Count);
        Assert.True(double.IsFinite(vwap.Last.Value));
    }

    [Fact]
    public void Vwapsd_SpanCalculate_ValidatesInput()
    {
        double[] price = [100, 101, 102, 103, 104];
        double[] volume = [1000, 1100, 1200, 1300, 1400];
        double[] upper = new double[5];
        double[] lower = new double[5];
        double[] vwap = new double[5];
        double[] wrongSize = new double[3];

        // NumDevs must be >= MinNumDevs
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Vwapsd.Calculate(price.AsSpan(), volume.AsSpan(),
                upper.AsSpan(), lower.AsSpan(), vwap.AsSpan(), 0));

        // NumDevs must be <= MaxNumDevs
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Vwapsd.Calculate(price.AsSpan(), volume.AsSpan(),
                upper.AsSpan(), lower.AsSpan(), vwap.AsSpan(), 6.0));

        // All arrays must be same length
        Assert.Throws<ArgumentException>(() =>
            Vwapsd.Calculate(price.AsSpan(), volume.AsSpan(),
                wrongSize.AsSpan(), lower.AsSpan(), vwap.AsSpan(), 1.0));
    }

    [Fact]
    public void Vwapsd_SpanCalculate_HandlesNaN()
    {
        double[] price = [100, 101, double.NaN, 103, 104];
        double[] volume = [1000, 1100, 1200, 1300, 1400];
        double[] upper = new double[5];
        double[] lower = new double[5];
        double[] vwap = new double[5];

        Vwapsd.Calculate(price.AsSpan(), volume.AsSpan(),
            upper.AsSpan(), lower.AsSpan(), vwap.AsSpan(), 1.0);

        foreach (var val in vwap)
        {
            Assert.True(double.IsFinite(val), $"VWAP should be finite, got {val}");
        }
    }

    [Fact]
    public void Vwapsd_FlatLine_ReturnsSameValueForVwap()
    {
        var vwapsd = new Vwapsd(1.0);
        for (int i = 0; i < 30; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 100, 100, 100, 1000);
            vwapsd.Update(bar);
        }

        // With constant price, VWAP should equal the price
        Assert.Equal(100.0, vwapsd.Vwap.Value, precision: 6);
        // StdDev of zero variance = 0, so upper = lower = vwap
        Assert.Equal(vwapsd.Vwap.Value, vwapsd.Upper.Value, precision: 6);
        Assert.Equal(vwapsd.Vwap.Value, vwapsd.Lower.Value, precision: 6);
    }

    [Fact]
    public void Vwapsd_HigherNumDevs_WiderBands()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var vwapsd1 = new Vwapsd(1.0);
        var vwapsd2 = new Vwapsd(2.0);
        var vwapsd3 = new Vwapsd(3.0);

        for (int i = 0; i < bars.Count; i++)
        {
            vwapsd1.Update(bars[i]);
            vwapsd2.Update(bars[i]);
            vwapsd3.Update(bars[i]);
        }

        // Same VWAP for all
        Assert.Equal(vwapsd1.Vwap.Value, vwapsd2.Vwap.Value, precision: 10);
        Assert.Equal(vwapsd2.Vwap.Value, vwapsd3.Vwap.Value, precision: 10);

        // Higher numDevs = wider bands
        Assert.True(vwapsd2.Width.Value > vwapsd1.Width.Value,
            $"Width with numDevs=2 ({vwapsd2.Width.Value}) should be > width with numDevs=1 ({vwapsd1.Width.Value})");
        Assert.True(vwapsd3.Width.Value > vwapsd2.Width.Value,
            $"Width with numDevs=3 ({vwapsd3.Width.Value}) should be > width with numDevs=2 ({vwapsd2.Width.Value})");
    }

    [Fact]
    public void Vwapsd_ZeroVolume_DoesNotAffectVwap()
    {
        var vwapsd = new Vwapsd(1.0);

        var bar1 = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 1000);
        vwapsd.Update(bar1);
        double vwapAfterBar1 = vwapsd.Vwap.Value;

        // Zero volume bar should not change VWAP
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 200, 200, 200, 200, 0);
        vwapsd.Update(bar2);
        double vwapAfterBar2 = vwapsd.Vwap.Value;

        Assert.Equal(vwapAfterBar1, vwapAfterBar2, precision: 10);
    }

    [Fact]
    public void Vwapsd_Prime_SetsStateCorrectly()
    {
        var vwapsd = new Vwapsd(1.0);
        double[] history = [100, 101, 102, 103, 104, 105, 106];

        vwapsd.Prime(history);

        Assert.True(vwapsd.IsHot);
        Assert.True(double.IsFinite(vwapsd.Vwap.Value));
    }

    [Fact]
    public void Vwapsd_UpdateTValue_UsesVolumeOfOne()
    {
        var vwapsd = new Vwapsd(1.0);

        // Using Update(TValue) should use volume=1
        vwapsd.Update(new TValue(DateTime.UtcNow, 100.0));
        vwapsd.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 200.0));

        // With equal volume (1 each), VWAP = (100+200)/2 = 150
        Assert.Equal(150.0, vwapsd.Vwap.Value, precision: 10);
    }

    [Fact]
    public void Vwapsd_UpdateTSeries_Works()
    {
        var vwapsd = new Vwapsd(1.5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var result = vwapsd.Update(bars);

        Assert.Equal(50, result.Count);
        Assert.True(double.IsFinite(result.Last.Value));
    }

    [Fact]
    public void Vwapsd_UpdateTSeries_PriceOnly_Works()
    {
        var vwapsd = new Vwapsd(1.5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries priceSeries = bars.Close;

        var result = vwapsd.Update(priceSeries);

        Assert.Equal(50, result.Count);
        Assert.True(double.IsFinite(result.Last.Value));
    }

    [Fact]
    public void Vwapsd_VolumeWeighting_AffectsVwap()
    {
        var vwapsd = new Vwapsd(1.0);

        // High volume at low price
        var bar1 = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 10000);
        vwapsd.Update(bar1);

        // Low volume at high price
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 200, 200, 200, 200, 100);
        vwapsd.Update(bar2);

        // VWAP should be closer to 100 due to higher volume
        // VWAP = (100*10000 + 200*100) / (10000+100) = 1020000/10100 ≈ 100.99
        double expectedVwap = (100.0 * 10000 + 200.0 * 100) / (10000 + 100);
        Assert.Equal(expectedVwap, vwapsd.Vwap.Value, precision: 10);
        Assert.True(vwapsd.Vwap.Value < 110, "VWAP should be heavily weighted toward 100");
    }

    [Fact]
    public void Vwapsd_FractionalNumDevs_Works()
    {
        var vwapsd = new Vwapsd(1.5);

        var bar1 = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 1);
        vwapsd.Update(bar1);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 200, 200, 200, 200, 1);
        vwapsd.Update(bar2);

        // VWAP = 150, StdDev = 50
        // With numDevs=1.5: Upper = 150 + 1.5*50 = 225, Lower = 150 - 1.5*50 = 75
        Assert.Equal(150.0, vwapsd.Vwap.Value, precision: 10);
        Assert.Equal(50.0, vwapsd.StdDev.Value, precision: 10);
        Assert.Equal(225.0, vwapsd.Upper.Value, precision: 10);
        Assert.Equal(75.0, vwapsd.Lower.Value, precision: 10);
        Assert.Equal(150.0, vwapsd.Width.Value, precision: 10); // 225 - 75
    }

    [Fact]
    public void Vwapsd_BoundaryNumDevs_Min_Works()
    {
        var vwapsd = new Vwapsd(0.1); // Minimum allowed

        var bar1 = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 1);
        vwapsd.Update(bar1);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 200, 200, 200, 200, 1);
        vwapsd.Update(bar2);

        // VWAP = 150, StdDev = 50
        // With numDevs=0.1: Upper = 150 + 0.1*50 = 155, Lower = 150 - 0.1*50 = 145
        Assert.Equal(150.0, vwapsd.Vwap.Value, precision: 10);
        Assert.Equal(50.0, vwapsd.StdDev.Value, precision: 10);
        Assert.Equal(155.0, vwapsd.Upper.Value, precision: 10);
        Assert.Equal(145.0, vwapsd.Lower.Value, precision: 10);
    }

    [Fact]
    public void Vwapsd_BoundaryNumDevs_Max_Works()
    {
        var vwapsd = new Vwapsd(5.0); // Maximum allowed

        var bar1 = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 1);
        vwapsd.Update(bar1);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 200, 200, 200, 200, 1);
        vwapsd.Update(bar2);

        // VWAP = 150, StdDev = 50
        // With numDevs=5.0: Upper = 150 + 5.0*50 = 400, Lower = 150 - 5.0*50 = -100
        Assert.Equal(150.0, vwapsd.Vwap.Value, precision: 10);
        Assert.Equal(50.0, vwapsd.StdDev.Value, precision: 10);
        Assert.Equal(400.0, vwapsd.Upper.Value, precision: 10);
        Assert.Equal(-100.0, vwapsd.Lower.Value, precision: 10);
    }
}