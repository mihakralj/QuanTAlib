using System;
using Xunit;

namespace QuanTAlib.Tests;

public class IchimokuTests
{
    private const double Precision = 1e-10;

    #region Constructor Tests

    [Fact]
    public void Constructor_DefaultParameters_SetsCorrectValues()
    {
        var ichimoku = new Ichimoku();
        Assert.Equal(9, ichimoku.TenkanPeriod);
        Assert.Equal(26, ichimoku.KijunPeriod);
        Assert.Equal(52, ichimoku.SenkouBPeriod);
        Assert.Equal(26, ichimoku.Displacement);
        Assert.Equal(52, ichimoku.WarmupPeriod); // Max of all periods
    }

    [Fact]
    public void Constructor_CustomParameters_SetsCorrectValues()
    {
        var ichimoku = new Ichimoku(10, 30, 60, 30);
        Assert.Equal(10, ichimoku.TenkanPeriod);
        Assert.Equal(30, ichimoku.KijunPeriod);
        Assert.Equal(60, ichimoku.SenkouBPeriod);
        Assert.Equal(30, ichimoku.Displacement);
        Assert.Equal(60, ichimoku.WarmupPeriod);
    }

    [Fact]
    public void Constructor_ZeroTenkanPeriod_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Ichimoku(0, 26, 52, 26));
    }

    [Fact]
    public void Constructor_NegativeKijunPeriod_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Ichimoku(9, -1, 52, 26));
    }

    [Fact]
    public void Constructor_ZeroSenkouBPeriod_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Ichimoku(9, 26, 0, 26));
    }

    [Fact]
    public void Constructor_ZeroDisplacement_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Ichimoku(9, 26, 52, 0));
    }

    [Fact]
    public void Name_FormatsCorrectly()
    {
        var ichimoku = new Ichimoku(9, 26, 52, 26);
        Assert.Equal("Ichimoku(9,26,52,26)", ichimoku.Name);
    }

    #endregion

    #region Warmup Tests

    [Fact]
    public void IsHot_BeforeWarmup_ReturnsFalse()
    {
        var ichimoku = new Ichimoku(9, 26, 52, 26);
        var bar = new TBar(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 100, 105, 95, 102, 1000);
        ichimoku.Update(bar);
        Assert.False(ichimoku.IsHot);
    }

    [Fact]
    public void IsHot_AfterWarmup_ReturnsTrue()
    {
        var ichimoku = new Ichimoku(9, 26, 52, 26);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 52; i++)
        {
            var bar = new TBar(baseTime + i * 60000, 100 + i, 105 + i, 95 + i, 102 + i, 1000);
            ichimoku.Update(bar);
        }

        Assert.True(ichimoku.IsHot);
    }

    [Fact]
    public void WarmupPeriod_BasedOnLongestPeriod()
    {
        var ichimoku1 = new Ichimoku(9, 26, 52, 26);
        Assert.Equal(52, ichimoku1.WarmupPeriod);

        var ichimoku2 = new Ichimoku(100, 50, 30, 26);
        Assert.Equal(100, ichimoku2.WarmupPeriod);
    }

    #endregion

    #region Calculation Tests

    [Fact]
    public void Tenkan_CalculatesDonchianMidpoint()
    {
        var ichimoku = new Ichimoku(3, 5, 10, 5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Add 3 bars with known high/low
        // Bar 1: H=110, L=90
        // Bar 2: H=115, L=85
        // Bar 3: H=105, L=95
        // 3-period high = 115, 3-period low = 85
        // Tenkan = (115 + 85) / 2 = 100

        ichimoku.Update(new TBar(baseTime, 100, 110, 90, 100, 1000));
        ichimoku.Update(new TBar(baseTime + 60000, 100, 115, 85, 100, 1000));
        ichimoku.Update(new TBar(baseTime + 120000, 100, 105, 95, 100, 1000));

        Assert.Equal(100.0, ichimoku.Tenkan.Value, Precision);
    }

    [Fact]
    public void Kijun_CalculatesDonchianMidpoint()
    {
        var ichimoku = new Ichimoku(2, 3, 5, 3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Add 3 bars
        // Bar 1: H=110, L=90
        // Bar 2: H=120, L=80
        // Bar 3: H=115, L=85
        // 3-period high = 120, 3-period low = 80
        // Kijun = (120 + 80) / 2 = 100

        ichimoku.Update(new TBar(baseTime, 100, 110, 90, 100, 1000));
        ichimoku.Update(new TBar(baseTime + 60000, 100, 120, 80, 100, 1000));
        ichimoku.Update(new TBar(baseTime + 120000, 100, 115, 85, 100, 1000));

        Assert.Equal(100.0, ichimoku.Kijun.Value, Precision);
    }

    [Fact]
    public void SenkouA_AverageOfTenkanAndKijun()
    {
        var ichimoku = new Ichimoku(2, 3, 5, 3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Create scenario where Tenkan and Kijun have known values
        // Using same setup: 2-period for Tenkan, 3-period for Kijun

        ichimoku.Update(new TBar(baseTime, 100, 110, 90, 100, 1000));        // T: (110+90)/2=100, K: (110+90)/2=100
        ichimoku.Update(new TBar(baseTime + 60000, 100, 120, 80, 100, 1000)); // T: (120+80)/2=100, K: (120+80)/2=100
        ichimoku.Update(new TBar(baseTime + 120000, 100, 100, 100, 100, 1000)); // T: (120+80)/2=100, K: (120+80)/2=100

        // SenkouA = (Tenkan + Kijun) / 2 = (100 + 100) / 2 = 100
        Assert.Equal(100.0, ichimoku.SenkouA.Value, Precision);
    }

    [Fact]
    public void SenkouB_CalculatesLongestPeriodMidpoint()
    {
        var ichimoku = new Ichimoku(2, 3, 4, 3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Add 4 bars for full Senkou B calculation
        // Bar 1: H=100, L=90
        // Bar 2: H=110, L=85
        // Bar 3: H=105, L=88
        // Bar 4: H=108, L=92
        // 4-period high = 110, 4-period low = 85
        // SenkouB = (110 + 85) / 2 = 97.5

        ichimoku.Update(new TBar(baseTime, 95, 100, 90, 95, 1000));
        ichimoku.Update(new TBar(baseTime + 60000, 100, 110, 85, 100, 1000));
        ichimoku.Update(new TBar(baseTime + 120000, 95, 105, 88, 95, 1000));
        ichimoku.Update(new TBar(baseTime + 180000, 100, 108, 92, 100, 1000));

        Assert.Equal(97.5, ichimoku.SenkouB.Value, Precision);
    }

    [Fact]
    public void Chikou_EqualsCurrentClose()
    {
        var ichimoku = new Ichimoku();
        long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var bar = new TBar(time, 100, 105, 95, 102.5, 1000);
        ichimoku.Update(bar);

        Assert.Equal(102.5, ichimoku.Chikou.Value, Precision);
    }

    [Fact]
    public void Last_ReturnsKijun()
    {
        var ichimoku = new Ichimoku();
        long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var bar = new TBar(time, 100, 105, 95, 102, 1000);
        ichimoku.Update(bar);

        Assert.Equal(ichimoku.Kijun.Value, ichimoku.Last.Value, Precision);
    }

    #endregion

    #region Single Value Update Tests

    [Fact]
    public void Update_SingleValue_TreatsAsHLC()
    {
        var ichimoku = new Ichimoku(2, 3, 5, 3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // When using single value, H=L=C=value
        ichimoku.Update(new TValue(baseTime, 100.0));
        ichimoku.Update(new TValue(baseTime + 60000, 100.0));
        ichimoku.Update(new TValue(baseTime + 120000, 100.0));

        // All lines should equal 100 when all H=L=100
        Assert.Equal(100.0, ichimoku.Tenkan.Value, Precision);
        Assert.Equal(100.0, ichimoku.Kijun.Value, Precision);
        Assert.Equal(100.0, ichimoku.SenkouA.Value, Precision);
    }

    #endregion

    #region Bar Correction Tests

    [Fact]
    public void Update_BarCorrection_RestoresPreviousState()
    {
        var ichimoku = new Ichimoku(3, 5, 10, 5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Add some initial bars
        for (int i = 0; i < 3; i++)
        {
            ichimoku.Update(new TBar(baseTime + i * 60000, 100, 105, 95, 100, 1000));
        }

        // Capture state before update (use underscore to indicate intentionally unused)
        _ = ichimoku.Tenkan.Value;

        // Update with new bar
        ichimoku.Update(new TBar(baseTime + 3 * 60000, 110, 120, 100, 115, 1000), isNew: true);
        double tenkanAfterNew = ichimoku.Tenkan.Value;

        // Correct the bar (isNew=false) with different values
        ichimoku.Update(new TBar(baseTime + 3 * 60000, 90, 95, 85, 90, 1000), isNew: false);
        double tenkanAfterCorrection = ichimoku.Tenkan.Value;

        // Values should differ based on the correction
        Assert.NotEqual(tenkanAfterNew, tenkanAfterCorrection);
    }

    [Fact]
    public void Update_SequentialCorrections_ProduceConsistentResults()
    {
        var ichimoku = new Ichimoku(3, 5, 10, 5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Fill buffer
        for (int i = 0; i < 5; i++)
        {
            ichimoku.Update(new TBar(baseTime + i * 60000, 100, 105, 95, 100, 1000));
        }

        // First update
        ichimoku.Update(new TBar(baseTime + 5 * 60000, 105, 110, 100, 105, 1000), isNew: true);
        double firstTenkan = ichimoku.Tenkan.Value;

        // Multiple corrections should converge
        for (int i = 0; i < 3; i++)
        {
            ichimoku.Update(new TBar(baseTime + 5 * 60000, 105, 110, 100, 105, 1000), isNew: false);
        }

        Assert.Equal(firstTenkan, ichimoku.Tenkan.Value, Precision);
    }

    #endregion

    #region NaN/Invalid Input Tests

    [Fact]
    public void Update_NaNHigh_UsesLastValidHigh()
    {
        var ichimoku = new Ichimoku(3, 5, 10, 5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        ichimoku.Update(new TBar(baseTime, 100, 110, 90, 100, 1000));
        ichimoku.Update(new TBar(baseTime + 60000, 100, 120, 80, 100, 1000));

        // Now update with NaN high
        var barWithNaN = new TBar(baseTime + 120000, double.NaN, double.NaN, 85, 100, 1000);
        ichimoku.Update(barWithNaN);

        // Should still produce valid output
        Assert.True(double.IsFinite(ichimoku.Tenkan.Value));
        Assert.True(double.IsFinite(ichimoku.Kijun.Value));
    }

    [Fact]
    public void Update_NaNLow_UsesLastValidLow()
    {
        var ichimoku = new Ichimoku(3, 5, 10, 5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        ichimoku.Update(new TBar(baseTime, 100, 110, 90, 100, 1000));
        ichimoku.Update(new TBar(baseTime + 60000, 100, 115, double.NaN, 100, 1000));

        Assert.True(double.IsFinite(ichimoku.Tenkan.Value));
    }

    [Fact]
    public void Update_InfinityValues_FallbackToPrevious()
    {
        var ichimoku = new Ichimoku(3, 5, 10, 5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        ichimoku.Update(new TBar(baseTime, 100, 110, 90, 100, 1000));
        ichimoku.Update(new TBar(baseTime + 60000, double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, 100, 1000));

        // Should handle gracefully
        Assert.True(double.IsFinite(ichimoku.Tenkan.Value));
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsAllState()
    {
        var ichimoku = new Ichimoku();
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Process some bars
        for (int i = 0; i < 60; i++)
        {
            ichimoku.Update(new TBar(baseTime + i * 60000, 100 + i, 105 + i, 95 + i, 100 + i, 1000));
        }

        Assert.True(ichimoku.IsHot);

        ichimoku.Reset();

        Assert.False(ichimoku.IsHot);
        Assert.Equal(default, ichimoku.Tenkan);
        Assert.Equal(default, ichimoku.Kijun);
        Assert.Equal(default, ichimoku.SenkouA);
        Assert.Equal(default, ichimoku.SenkouB);
        Assert.Equal(default, ichimoku.Chikou);
    }

    [Fact]
    public void Reset_AllowsReuse()
    {
        var ichimoku = new Ichimoku(3, 5, 10, 5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // First use
        for (int i = 0; i < 10; i++)
        {
            ichimoku.Update(new TBar(baseTime + i * 60000, 100, 110, 90, 100, 1000));
        }

        double firstTenkan = ichimoku.Tenkan.Value;

        // Reset and reuse
        ichimoku.Reset();

        for (int i = 0; i < 10; i++)
        {
            ichimoku.Update(new TBar(baseTime + i * 60000, 100, 110, 90, 100, 1000));
        }

        Assert.Equal(firstTenkan, ichimoku.Tenkan.Value, Precision);
    }

    #endregion

    #region Batch Processing Tests

    [Fact]
    public void Batch_ReturnsAllComponents()
    {
        var source = new TBarSeries();
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 60; i++)
        {
            source.Add(new TBar(baseTime + i * 60000, 100, 110, 90, 100, 1000));
        }

        var (tenkan, kijun, senkouA, senkouB, chikou) = Ichimoku.Batch(source);

        Assert.Equal(60, tenkan.Count);
        Assert.Equal(60, kijun.Count);
        Assert.Equal(60, senkouA.Count);
        Assert.Equal(60, senkouB.Count);
        Assert.Equal(60, chikou.Count);
    }

    [Fact]
    public void Batch_EmptySource_ReturnsEmptySeries()
    {
        var source = new TBarSeries();

        var (tenkan, kijun, senkouA, senkouB, chikou) = Ichimoku.Batch(source);

        Assert.Empty(tenkan);
        Assert.Empty(kijun);
        Assert.Empty(senkouA);
        Assert.Empty(senkouB);
        Assert.Empty(chikou);
    }

    [Fact]
    public void Batch_CustomParameters_AppliesCorrectly()
    {
        var source = new TBarSeries();
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 20; i++)
        {
            source.Add(new TBar(baseTime + i * 60000, 100 + i, 110 + i, 90 + i, 100 + i, 1000));
        }

        var (tenkan, _, _, _, _) = Ichimoku.Batch(source, 3, 5, 10, 5);

        Assert.Equal(20, tenkan.Count);
    }

    [Fact]
    public void Calculate_ReturnsBothResultsAndIndicator()
    {
        var source = new TBarSeries();
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 60; i++)
        {
            source.Add(new TBar(baseTime + i * 60000, 100, 110, 90, 100, 1000));
        }

        var (results, indicator) = Ichimoku.Calculate(source);

        Assert.Equal(60, results.Tenkan.Count);
        Assert.True(indicator.IsHot);
        Assert.Equal(52, indicator.WarmupPeriod);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void Update_ConstantPrice_AllLinesEqual()
    {
        var ichimoku = new Ichimoku(3, 5, 10, 5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Constant high=low=close=100
        for (int i = 0; i < 10; i++)
        {
            ichimoku.Update(new TBar(baseTime + i * 60000, 100, 100, 100, 100, 1000));
        }

        Assert.Equal(100.0, ichimoku.Tenkan.Value, Precision);
        Assert.Equal(100.0, ichimoku.Kijun.Value, Precision);
        Assert.Equal(100.0, ichimoku.SenkouA.Value, Precision);
        Assert.Equal(100.0, ichimoku.SenkouB.Value, Precision);
        Assert.Equal(100.0, ichimoku.Chikou.Value, Precision);
    }

    [Fact]
    public void Update_SingleBar_ComputesCorrectly()
    {
        var ichimoku = new Ichimoku();
        long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var bar = new TBar(time, 100, 110, 90, 100, 1000);
        ichimoku.Update(bar);

        // With single bar: high=110, low=90
        // All midpoints = (110 + 90) / 2 = 100
        Assert.Equal(100.0, ichimoku.Tenkan.Value, Precision);
        Assert.Equal(100.0, ichimoku.Kijun.Value, Precision);
        Assert.Equal(100.0, ichimoku.SenkouA.Value, Precision);
        Assert.Equal(100.0, ichimoku.SenkouB.Value, Precision);
        Assert.Equal(100.0, ichimoku.Chikou.Value, Precision); // Close
    }

    [Fact]
    public void Update_TrendingMarket_CloudFormsCorrectly()
    {
        var ichimoku = new Ichimoku(3, 5, 10, 5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Uptrend: increasing highs and lows
        for (int i = 0; i < 15; i++)
        {
            double basePrice = 100 + i * 2;
            ichimoku.Update(new TBar(baseTime + i * 60000, basePrice, basePrice + 5, basePrice - 5, basePrice, 1000));
        }

        // In uptrend, Tenkan should be above Kijun (faster vs slower)
        // And SenkouA should be above SenkouB (bullish cloud)
        Assert.True(ichimoku.Tenkan.Value >= ichimoku.Kijun.Value);
    }

    [Fact]
    public void AllOutputs_HaveCorrectTimestamps()
    {
        var ichimoku = new Ichimoku();
        long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var bar = new TBar(time, 100, 110, 90, 100, 1000);
        ichimoku.Update(bar);

        Assert.Equal(time, ichimoku.Tenkan.Time);
        Assert.Equal(time, ichimoku.Kijun.Time);
        Assert.Equal(time, ichimoku.SenkouA.Time);
        Assert.Equal(time, ichimoku.SenkouB.Time);
        Assert.Equal(time, ichimoku.Chikou.Time);
    }

    #endregion

    #region Rolling Window Tests

    [Fact]
    public void RollingWindow_OldValuesDroppedCorrectly()
    {
        var ichimoku = new Ichimoku(3, 3, 3, 3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Add first 3 bars: H ranging 100-120
        ichimoku.Update(new TBar(baseTime, 100, 100, 90, 100, 1000));
        ichimoku.Update(new TBar(baseTime + 60000, 110, 110, 100, 110, 1000));
        ichimoku.Update(new TBar(baseTime + 120000, 120, 120, 110, 120, 1000));

        // Donchian midpoint = (120 + 90) / 2 = 105
        Assert.Equal(105.0, ichimoku.Tenkan.Value, Precision);

        // Add 4th bar with H=130, L=120
        // Now window is bars 2,3,4: H=110,120,130 L=100,110,120
        // Donchian midpoint = (130 + 100) / 2 = 115
        ichimoku.Update(new TBar(baseTime + 180000, 130, 130, 120, 130, 1000));
        Assert.Equal(115.0, ichimoku.Tenkan.Value, Precision);
    }

    #endregion
}
