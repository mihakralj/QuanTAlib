namespace QuanTAlib.Tests;

public class VwapbandsTests
{
    [Fact]
    public void Vwapbands_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Vwapbands(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Vwapbands(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Vwapbands(0.0001)); // Below MinMultiplier

        var vwapbands = new Vwapbands(1.0);
        Assert.NotNull(vwapbands);
    }

    [Fact]
    public void Vwapbands_DefaultConstructor_UsesDefaultMultiplier()
    {
        var vwapbands = new Vwapbands();
        Assert.NotNull(vwapbands);
        Assert.Contains("Vwapbands", vwapbands.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Vwapbands_Update_ReturnsValue()
    {
        var vwapbands = new Vwapbands(1.0);
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000);
        var result = vwapbands.Update(bar);

        Assert.True(double.IsFinite(result.Value));
        Assert.True(double.IsFinite(vwapbands.Upper1.Value));
        Assert.True(double.IsFinite(vwapbands.Lower1.Value));
        Assert.True(double.IsFinite(vwapbands.Upper2.Value));
        Assert.True(double.IsFinite(vwapbands.Lower2.Value));
        Assert.True(double.IsFinite(vwapbands.Vwap.Value));
        Assert.True(double.IsFinite(vwapbands.StdDev.Value));
    }

    [Fact]
    public void Vwapbands_FirstBar_InitializesCorrectly()
    {
        var vwapbands = new Vwapbands(1.0);
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000);
        _ = vwapbands.Update(bar);

        // First bar: VWAP = HLC3 = (105+95+100)/3 = 100
        double expectedVwap = (105 + 95 + 100) / 3.0;
        Assert.Equal(expectedVwap, vwapbands.Vwap.Value, precision: 10);

        // First bar has zero variance (only 1 point)
        Assert.Equal(0, vwapbands.StdDev.Value, precision: 10);
        Assert.Equal(expectedVwap, vwapbands.Upper1.Value, precision: 10);
        Assert.Equal(expectedVwap, vwapbands.Lower1.Value, precision: 10);
    }

    [Fact]
    public void Vwapbands_Properties_Accessible()
    {
        var vwapbands = new Vwapbands(2.0);

        Assert.False(vwapbands.IsHot);
        Assert.Contains("Vwapbands", vwapbands.Name, StringComparison.Ordinal);
        Assert.Equal(2, vwapbands.WarmupPeriod);
    }

    [Fact]
    public void Vwapbands_Update_IsNew_AcceptsParameter()
    {
        var vwapbands = new Vwapbands(1.0);
        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000);
        var bar2 = new TBar(DateTime.UtcNow, 100, 106, 94, 101, 1100);

        var result1 = vwapbands.Update(bar1, isNew: true);
        var result2 = vwapbands.Update(bar2, isNew: false);

        Assert.True(double.IsFinite(result1.Value));
        Assert.True(double.IsFinite(result2.Value));
    }

    [Fact]
    public void Vwapbands_Update_IsNew_False_UpdatesValue()
    {
        var vwapbands = new Vwapbands(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(15, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Process several bars
        for (int i = 0; i < 15; i++)
        {
            vwapbands.Update(bars[i], isNew: true);
        }

        double beforeCorrection = vwapbands.Vwap.Value;

        // Correct last bar with different value
        var correctionBar = new TBar(DateTime.UtcNow, 200, 210, 190, 200, 5000);
        vwapbands.Update(correctionBar, isNew: false);
        double afterCorrection = vwapbands.Vwap.Value;

        Assert.NotEqual(beforeCorrection, afterCorrection);
    }

    [Fact]
    public void Vwapbands_IterativeCorrections_RestoreToOriginalState()
    {
        var vwapbands = new Vwapbands(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Process all bars
        for (int i = 0; i < bars.Count; i++)
        {
            vwapbands.Update(bars[i]);
        }
        double originalVwap = vwapbands.Vwap.Value;
        double originalUpper1 = vwapbands.Upper1.Value;
        double originalLower1 = vwapbands.Lower1.Value;

        // Make multiple corrections
        for (int i = 0; i < 10; i++)
        {
            var correctionBar = new TBar(DateTime.UtcNow, 150 + i, 160 + i, 140 + i, 155 + i, 2000 + i * 100);
            vwapbands.Update(correctionBar, isNew: false);
        }

        // Restore original
        vwapbands.Update(bars[^1], isNew: false);
        double restoredVwap = vwapbands.Vwap.Value;
        double restoredUpper1 = vwapbands.Upper1.Value;
        double restoredLower1 = vwapbands.Lower1.Value;

        Assert.Equal(originalVwap, restoredVwap, precision: 8);
        Assert.Equal(originalUpper1, restoredUpper1, precision: 8);
        Assert.Equal(originalLower1, restoredLower1, precision: 8);
    }

    [Fact]
    public void Vwapbands_Reset_ClearsState()
    {
        var vwapbands = new Vwapbands(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 20; i++)
        {
            vwapbands.Update(bars[i]);
        }

        Assert.True(vwapbands.IsHot);

        vwapbands.Reset();

        Assert.False(vwapbands.IsHot);
    }

    [Fact]
    public void Vwapbands_IsHot_BecomesTrueAfterWarmup()
    {
        var vwapbands = new Vwapbands(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // WarmupPeriod is 2
        vwapbands.Update(bars[0]);
        Assert.False(vwapbands.IsHot);

        vwapbands.Update(bars[1]);
        Assert.True(vwapbands.IsHot);
    }

    [Fact]
    public void Vwapbands_WarmupPeriod_IsSetCorrectly()
    {
        var vwapbands = new Vwapbands(1.0);
        Assert.Equal(2, vwapbands.WarmupPeriod);
    }

    [Fact]
    public void Vwapbands_NaN_Price_UsesLastValidValue()
    {
        var vwapbands = new Vwapbands(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 10; i++)
        {
            vwapbands.Update(bars[i]);
        }

        vwapbands.Update(new TValue(DateTime.UtcNow, double.NaN), 1000, isNew: true);
        double afterNaN = vwapbands.Vwap.Value;

        Assert.True(double.IsFinite(afterNaN));
    }

    [Fact]
    public void Vwapbands_NaN_Volume_UsesLastValidValue()
    {
        var vwapbands = new Vwapbands(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 10; i++)
        {
            vwapbands.Update(bars[i]);
        }

        vwapbands.Update(new TValue(DateTime.UtcNow, 100), double.NaN, isNew: true);
        double afterNaN = vwapbands.Vwap.Value;

        Assert.True(double.IsFinite(afterNaN));
    }

    [Fact]
    public void Vwapbands_Infinity_Input_UsesLastValidValue()
    {
        var vwapbands = new Vwapbands(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 10; i++)
        {
            vwapbands.Update(bars[i]);
        }

        vwapbands.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity), 1000, isNew: true);
        Assert.True(double.IsFinite(vwapbands.Vwap.Value));

        vwapbands.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity), 1000, isNew: true);
        Assert.True(double.IsFinite(vwapbands.Vwap.Value));
    }

    [Fact]
    public void Vwapbands_BandRelationship_Upper2GreaterThanUpper1GreaterThanLower1GreaterThanLower2()
    {
        var vwapbands = new Vwapbands(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            vwapbands.Update(bars[i]);

            // Skip first bar where StdDev is 0
            if (i > 0)
            {
                Assert.True(vwapbands.Upper2.Value >= vwapbands.Upper1.Value,
                    $"Upper2 ({vwapbands.Upper2.Value}) should be >= Upper1 ({vwapbands.Upper1.Value})");
                Assert.True(vwapbands.Upper1.Value >= vwapbands.Vwap.Value,
                    $"Upper1 ({vwapbands.Upper1.Value}) should be >= Vwap ({vwapbands.Vwap.Value})");
                Assert.True(vwapbands.Vwap.Value >= vwapbands.Lower1.Value,
                    $"Vwap ({vwapbands.Vwap.Value}) should be >= Lower1 ({vwapbands.Lower1.Value})");
                Assert.True(vwapbands.Lower1.Value >= vwapbands.Lower2.Value,
                    $"Lower1 ({vwapbands.Lower1.Value}) should be >= Lower2 ({vwapbands.Lower2.Value})");
            }
        }
    }

    [Fact]
    public void Vwapbands_VwapBetweenBands()
    {
        var vwapbands = new Vwapbands(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            vwapbands.Update(bars[i]);
            Assert.True(vwapbands.Vwap.Value <= vwapbands.Upper1.Value,
                $"Vwap ({vwapbands.Vwap.Value}) should be <= Upper1 ({vwapbands.Upper1.Value})");
            Assert.True(vwapbands.Vwap.Value >= vwapbands.Lower1.Value,
                $"Vwap ({vwapbands.Vwap.Value}) should be >= Lower1 ({vwapbands.Lower1.Value})");
        }
    }

    [Fact]
    public void Vwapbands_Width_EqualsUpper1MinusLower1()
    {
        var vwapbands = new Vwapbands(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            vwapbands.Update(bars[i]);
            double expectedWidth = vwapbands.Upper1.Value - vwapbands.Lower1.Value;
            Assert.Equal(expectedWidth, vwapbands.Width.Value, precision: 10);
        }
    }

    [Fact]
    public void Vwapbands_SessionReset_ResetsVwapCalculation()
    {
        var vwapbands = new Vwapbands(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Process first 10 bars
        for (int i = 0; i < 10; i++)
        {
            vwapbands.Update(bars[i]);
        }
        double vwapBeforeReset = vwapbands.Vwap.Value;

        // Reset and process next bar - should start fresh
        var resetBar = new TBar(DateTime.UtcNow, 200, 210, 190, 200, 1000);
        vwapbands.Update(resetBar, isNew: true, reset: true);

        // After reset, VWAP should be just the new bar's HLC3
        double expectedVwap = (210 + 190 + 200) / 3.0;
        Assert.Equal(expectedVwap, vwapbands.Vwap.Value, precision: 10);
        Assert.NotEqual(vwapBeforeReset, vwapbands.Vwap.Value);
    }

    [Fact]
    public void Vwapbands_SessionReset_ResetsIsHotGating()
    {
        var vwapbands = new Vwapbands(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Process bars until IsHot is true (WarmupPeriod = 2)
        vwapbands.Update(bars[0]);
        Assert.False(vwapbands.IsHot);
        vwapbands.Update(bars[1]);
        Assert.True(vwapbands.IsHot);

        // Process more bars to ensure we're well past warmup
        for (int i = 2; i < 10; i++)
        {
            vwapbands.Update(bars[i]);
        }
        Assert.True(vwapbands.IsHot);

        // Reset session - IsHot should become false
        var resetBar1 = new TBar(DateTime.UtcNow, 200, 210, 190, 200, 1000);
        vwapbands.Update(resetBar1, isNew: true, reset: true);
        Assert.False(vwapbands.IsHot, "IsHot should be false after reset (1 bar accumulated)");

        // Process second bar after reset - IsHot should become true
        var resetBar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 205, 215, 195, 205, 1100);
        vwapbands.Update(resetBar2, isNew: true);
        Assert.True(vwapbands.IsHot, "IsHot should be true after 2 bars accumulated post-reset");
    }

    [Fact]
    public void Vwapbands_VwapFormula_MatchesExpected()
    {
        var vwapbands = new Vwapbands(1.0);

        // Bar 1: price=100, volume=1000
        var bar1 = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 1000);
        vwapbands.Update(bar1);
        Assert.Equal(100.0, vwapbands.Vwap.Value, precision: 10);

        // Bar 2: price=110, volume=2000
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 110, 110, 110, 110, 2000);
        vwapbands.Update(bar2);

        // VWAP = (100*1000 + 110*2000) / (1000+2000) = 320000/3000 = 106.666...
        double expectedVwap = (100.0 * 1000 + 110.0 * 2000) / (1000 + 2000);
        Assert.Equal(expectedVwap, vwapbands.Vwap.Value, precision: 10);
    }

    [Fact]
    public void Vwapbands_StdDevFormula_MatchesExpected()
    {
        var vwapbands = new Vwapbands(1.0);

        // Bar 1: price=100, volume=1
        var bar1 = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 1);
        vwapbands.Update(bar1);

        // Bar 2: price=200, volume=1
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 200, 200, 200, 200, 1);
        vwapbands.Update(bar2);

        // VWAP = (100*1 + 200*1) / 2 = 150
        // MeanP2 = (100²*1 + 200²*1) / 2 = (10000 + 40000) / 2 = 25000
        // Variance = MeanP2 - VWAP² = 25000 - 22500 = 2500
        // StdDev = sqrt(2500) = 50
        Assert.Equal(150.0, vwapbands.Vwap.Value, precision: 10);
        Assert.Equal(50.0, vwapbands.StdDev.Value, precision: 10);
        Assert.Equal(200.0, vwapbands.Upper1.Value, precision: 10); // 150 + 50
        Assert.Equal(100.0, vwapbands.Lower1.Value, precision: 10); // 150 - 50
        Assert.Equal(250.0, vwapbands.Upper2.Value, precision: 10); // 150 + 100
        Assert.Equal(50.0, vwapbands.Lower2.Value, precision: 10);  // 150 - 100
    }

    [Fact]
    public void Vwapbands_BatchCalc_MatchesIterativeCalc()
    {
        var vwapbandsIterative = new Vwapbands(1.0);
        var vwapbandsBatch = new Vwapbands(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Iterative
        var iterativeVwap = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            vwapbandsIterative.Update(bars[i]);
            iterativeVwap.Add(vwapbandsIterative.Vwap.Value);
        }

        // Batch
        var batchResult = vwapbandsBatch.Update(bars);

        // Compare last 50 values
        for (int i = 50; i < 100; i++)
        {
            Assert.Equal(iterativeVwap[i], batchResult[i].Value, precision: 10);
        }
    }

    [Fact]
    public void Vwapbands_StaticCalculate_TBarSeries_Works()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (upper1, lower1, upper2, lower2, vwap, stdev) = Vwapbands.Calculate(bars, 1.0);

        Assert.Equal(50, upper1.Count);
        Assert.Equal(50, lower1.Count);
        Assert.Equal(50, upper2.Count);
        Assert.Equal(50, lower2.Count);
        Assert.Equal(50, vwap.Count);
        Assert.Equal(50, stdev.Count);
        Assert.True(double.IsFinite(vwap.Last.Value));
    }

    [Fact]
    public void Vwapbands_SpanCalculate_ValidatesInput()
    {
        double[] price = [100, 101, 102, 103, 104];
        double[] volume = [1000, 1100, 1200, 1300, 1400];
        double[] upper1 = new double[5];
        double[] lower1 = new double[5];
        double[] upper2 = new double[5];
        double[] lower2 = new double[5];
        double[] vwap = new double[5];
        double[] stdDev = new double[5];
        double[] wrongSize = new double[3];

        // Multiplier must be >= MinMultiplier
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Vwapbands.Calculate(price.AsSpan(), volume.AsSpan(),
                upper1.AsSpan(), lower1.AsSpan(), upper2.AsSpan(), lower2.AsSpan(), vwap.AsSpan(), stdDev.AsSpan(), 0));

        // All arrays must be same length
        Assert.Throws<ArgumentException>(() =>
            Vwapbands.Calculate(price.AsSpan(), volume.AsSpan(),
                wrongSize.AsSpan(), lower1.AsSpan(), upper2.AsSpan(), lower2.AsSpan(), vwap.AsSpan(), stdDev.AsSpan(), 1.0));
    }

    [Fact]
    public void Vwapbands_SpanCalculate_HandlesNaN()
    {
        double[] price = [100, 101, double.NaN, 103, 104];
        double[] volume = [1000, 1100, 1200, 1300, 1400];
        double[] upper1 = new double[5];
        double[] lower1 = new double[5];
        double[] upper2 = new double[5];
        double[] lower2 = new double[5];
        double[] vwap = new double[5];
        double[] stdDev = new double[5];

        Vwapbands.Calculate(price.AsSpan(), volume.AsSpan(),
            upper1.AsSpan(), lower1.AsSpan(), upper2.AsSpan(), lower2.AsSpan(), vwap.AsSpan(), stdDev.AsSpan(), 1.0);

        foreach (var val in vwap)
        {
            Assert.True(double.IsFinite(val), $"VWAP should be finite, got {val}");
        }
    }

    [Fact]
    public void Vwapbands_FlatLine_ReturnsSameValueForVwap()
    {
        var vwapbands = new Vwapbands(1.0);
        for (int i = 0; i < 30; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 100, 100, 100, 1000);
            vwapbands.Update(bar);
        }

        // With constant price, VWAP should equal the price
        Assert.Equal(100.0, vwapbands.Vwap.Value, precision: 6);
        // StdDev of zero variance = 0, so upper = lower = vwap
        Assert.Equal(vwapbands.Vwap.Value, vwapbands.Upper1.Value, precision: 6);
        Assert.Equal(vwapbands.Vwap.Value, vwapbands.Lower1.Value, precision: 6);
    }

    [Fact]
    public void Vwapbands_HigherMultiplier_WiderBands()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var vwapbands1 = new Vwapbands(1.0);
        var vwapbands2 = new Vwapbands(2.0);

        for (int i = 0; i < bars.Count; i++)
        {
            vwapbands1.Update(bars[i]);
            vwapbands2.Update(bars[i]);
        }

        // Same VWAP
        Assert.Equal(vwapbands1.Vwap.Value, vwapbands2.Vwap.Value, precision: 10);

        // Higher multiplier = wider bands
        Assert.True(vwapbands2.Width.Value > vwapbands1.Width.Value,
            $"Width with mult=2 ({vwapbands2.Width.Value}) should be > width with mult=1 ({vwapbands1.Width.Value})");
    }

    [Fact]
    public void Vwapbands_ZeroVolume_DoesNotAffectVwap()
    {
        var vwapbands = new Vwapbands(1.0);

        var bar1 = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 1000);
        vwapbands.Update(bar1);
        double vwapAfterBar1 = vwapbands.Vwap.Value;

        // Zero volume bar should not change VWAP
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 200, 200, 200, 200, 0);
        vwapbands.Update(bar2);
        double vwapAfterBar2 = vwapbands.Vwap.Value;

        Assert.Equal(vwapAfterBar1, vwapAfterBar2, precision: 10);
    }

    [Fact]
    public void Vwapbands_Prime_SetsStateCorrectly()
    {
        var vwapbands = new Vwapbands(1.0);
        double[] history = [100, 101, 102, 103, 104, 105, 106];

        vwapbands.Prime(history);

        Assert.True(vwapbands.IsHot);
        Assert.True(double.IsFinite(vwapbands.Vwap.Value));
    }

    [Fact]
    public void Vwapbands_UpdateTValue_UsesVolumeOfOne()
    {
        var vwapbands = new Vwapbands(1.0);

        // Using Update(TValue) should use volume=1
        vwapbands.Update(new TValue(DateTime.UtcNow, 100.0));
        vwapbands.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 200.0));

        // With equal volume (1 each), VWAP = (100+200)/2 = 150
        Assert.Equal(150.0, vwapbands.Vwap.Value, precision: 10);
    }

    [Fact]
    public void Vwapbands_UpdateTSeries_Works()
    {
        var vwapbands = new Vwapbands(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var result = vwapbands.Update(bars);

        Assert.Equal(50, result.Count);
        Assert.True(double.IsFinite(result.Last.Value));
    }

    [Fact]
    public void Vwapbands_UpdateTSeries_PriceOnly_Works()
    {
        var vwapbands = new Vwapbands(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries priceSeries = bars.Close;

        var result = vwapbands.Update(priceSeries);

        Assert.Equal(50, result.Count);
        Assert.True(double.IsFinite(result.Last.Value));
    }

    [Fact]
    public void Vwapbands_VolumeWeighting_AffectsVwap()
    {
        var vwapbands = new Vwapbands(1.0);

        // High volume at low price
        var bar1 = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 10000);
        vwapbands.Update(bar1);

        // Low volume at high price
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 200, 200, 200, 200, 100);
        vwapbands.Update(bar2);

        // VWAP should be closer to 100 due to higher volume
        // VWAP = (100*10000 + 200*100) / (10000+100) = 1020000/10100 ≈ 100.99
        double expectedVwap = (100.0 * 10000 + 200.0 * 100) / (10000 + 100);
        Assert.Equal(expectedVwap, vwapbands.Vwap.Value, precision: 10);
        Assert.True(vwapbands.Vwap.Value < 110, "VWAP should be heavily weighted toward 100");
    }
}
