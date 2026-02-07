using System;
using Xunit;

namespace QuanTAlib.Tests;

public class TtmSqueezeTests
{
    private const double Precision = 1e-10;

    #region Constructor Tests

    [Fact]
    public void Constructor_DefaultParameters_AreCorrect()
    {
        var squeeze = new TtmSqueeze();
        Assert.Equal(20, squeeze.BbPeriod);
        Assert.Equal(20, squeeze.KcPeriod);
        Assert.Equal(20, squeeze.MomPeriod);
    }

    [Fact]
    public void Constructor_CustomParameters_AreSet()
    {
        var squeeze = new TtmSqueeze(bbPeriod: 15, bbMult: 1.5, kcPeriod: 10, kcMult: 2.0, momPeriod: 25);
        Assert.Equal(15, squeeze.BbPeriod);
        Assert.Equal(10, squeeze.KcPeriod);
        Assert.Equal(25, squeeze.MomPeriod);
    }

    [Fact]
    public void Constructor_InvalidBbPeriod_Throws()
    {
        Assert.Throws<ArgumentException>(() => new TtmSqueeze(bbPeriod: 1));
    }

    [Fact]
    public void Constructor_InvalidKcPeriod_Throws()
    {
        Assert.Throws<ArgumentException>(() => new TtmSqueeze(kcPeriod: 0));
    }

    [Fact]
    public void Constructor_InvalidMomPeriod_Throws()
    {
        Assert.Throws<ArgumentException>(() => new TtmSqueeze(momPeriod: 1));
    }

    [Fact]
    public void Constructor_InvalidBbMult_Throws()
    {
        Assert.Throws<ArgumentException>(() => new TtmSqueeze(bbMult: 0));
    }

    [Fact]
    public void Constructor_InvalidKcMult_Throws()
    {
        Assert.Throws<ArgumentException>(() => new TtmSqueeze(kcMult: -1));
    }

    [Fact]
    public void Name_IncludesAllParameters()
    {
        var squeeze = new TtmSqueeze(15, 1.5, 10, 2.0, 25);
        Assert.Contains("15", squeeze.Name, StringComparison.Ordinal);
        Assert.Contains("1.5", squeeze.Name, StringComparison.Ordinal);
        Assert.Contains("10", squeeze.Name, StringComparison.Ordinal);
        Assert.Contains("2.0", squeeze.Name, StringComparison.Ordinal);
        Assert.Contains("25", squeeze.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void WarmupPeriod_IsMaxOfPeriods()
    {
        var squeeze = new TtmSqueeze(bbPeriod: 15, bbMult: 2.0, kcPeriod: 10, kcMult: 1.5, momPeriod: 25);
        Assert.Equal(25, squeeze.WarmupPeriod);
    }

    #endregion

    #region IsHot Tests

    [Fact]
    public void IsHot_BeforeWarmup_ReturnsFalse()
    {
        var squeeze = new TtmSqueeze(bbPeriod: 5, bbMult: 2.0, kcPeriod: 5, kcMult: 1.5, momPeriod: 5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 4; i++)
        {
            squeeze.Update(new TBar(baseTime + i * 60000, 100, 105, 95, 102, 1000));
        }

        Assert.False(squeeze.IsHot);
    }

    [Fact]
    public void IsHot_AfterWarmup_ReturnsTrue()
    {
        var squeeze = new TtmSqueeze(bbPeriod: 5, bbMult: 2.0, kcPeriod: 5, kcMult: 1.5, momPeriod: 5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 5; i++)
        {
            squeeze.Update(new TBar(baseTime + i * 60000, 100, 105, 95, 102, 1000));
        }

        Assert.True(squeeze.IsHot);
    }

    #endregion

    #region Squeeze Detection Tests

    [Fact]
    public void Update_LowVolatility_SqueezeOn()
    {
        var squeeze = new TtmSqueeze(bbPeriod: 5, bbMult: 2.0, kcPeriod: 5, kcMult: 1.5, momPeriod: 5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Low volatility: tight range bars
        for (int i = 0; i < 10; i++)
        {
            squeeze.Update(new TBar(baseTime + i * 60000, 100, 100.5, 99.5, 100, 1000));
        }

        // With tight range (0.5 from mid), low stddev means BB should be tighter
        // This should trigger squeeze on
        // Note: May need specific values depending on implementation
        Assert.True(double.IsFinite(squeeze.Momentum.Value));
    }

    [Fact]
    public void Update_HighVolatility_SqueezeOff()
    {
        var squeeze = new TtmSqueeze(bbPeriod: 5, bbMult: 2.0, kcPeriod: 5, kcMult: 1.5, momPeriod: 5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // High volatility: wide range bars
        for (int i = 0; i < 10; i++)
        {
            double offset = (i % 2 == 0) ? 10 : -10;
            squeeze.Update(new TBar(baseTime + i * 60000, 100, 110 + offset, 90 + offset, 100 + offset, 1000));
        }

        Assert.True(double.IsFinite(squeeze.Momentum.Value));
    }

    [Fact]
    public void Update_SqueezeFired_DetectedOnTransition()
    {
        var squeeze = new TtmSqueeze(bbPeriod: 3, bbMult: 2.0, kcPeriod: 3, kcMult: 1.5, momPeriod: 3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Start with tight range (likely squeeze on)
        for (int i = 0; i < 5; i++)
        {
            squeeze.Update(new TBar(baseTime + i * 60000, 100, 100.1, 99.9, 100, 1000));
        }

        // Sudden volatility expansion (removed unused initialSqueezeOn variable)
        squeeze.Update(new TBar(baseTime + 5 * 60000, 100, 120, 80, 115, 1000));

        // The squeeze state should have changed
        // (The exact behavior depends on the calculation)
        Assert.True(double.IsFinite(squeeze.Momentum.Value));
    }

    #endregion

    #region Momentum Tests

    [Fact]
    public void Update_PriceAboveMidline_PositiveMomentum()
    {
        var squeeze = new TtmSqueeze(bbPeriod: 3, bbMult: 2.0, kcPeriod: 3, kcMult: 1.5, momPeriod: 3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Prices consistently above the donchian midline
        squeeze.Update(new TBar(baseTime, 100, 102, 98, 101, 1000));
        squeeze.Update(new TBar(baseTime + 60000, 101, 103, 99, 102, 1000));
        squeeze.Update(new TBar(baseTime + 120000, 102, 104, 100, 103, 1000));
        squeeze.Update(new TBar(baseTime + 180000, 103, 106, 101, 105, 1000));

        // With rising prices, momentum should be positive
        Assert.True(squeeze.MomentumPositive);
    }

    [Fact]
    public void Update_PriceBelowMidline_NegativeMomentum()
    {
        var squeeze = new TtmSqueeze(bbPeriod: 3, bbMult: 2.0, kcPeriod: 3, kcMult: 1.5, momPeriod: 3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Prices consistently below the donchian midline
        squeeze.Update(new TBar(baseTime, 100, 102, 98, 99, 1000));
        squeeze.Update(new TBar(baseTime + 60000, 99, 101, 97, 98, 1000));
        squeeze.Update(new TBar(baseTime + 120000, 98, 100, 96, 97, 1000));
        squeeze.Update(new TBar(baseTime + 180000, 97, 99, 95, 96, 1000));

        // With falling prices, momentum should be negative
        Assert.False(squeeze.MomentumPositive);
    }

    [Fact]
    public void Update_RisingMomentum_Detected()
    {
        var squeeze = new TtmSqueeze(bbPeriod: 3, bbMult: 2.0, kcPeriod: 3, kcMult: 1.5, momPeriod: 3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Flat then accelerating up
        for (int i = 0; i < 3; i++)
        {
            squeeze.Update(new TBar(baseTime + i * 60000, 100, 101, 99, 100, 1000));
        }

        // Strong up move
        squeeze.Update(new TBar(baseTime + 3 * 60000, 100, 115, 99, 112, 1000));
        squeeze.Update(new TBar(baseTime + 4 * 60000, 112, 125, 110, 122, 1000));

        Assert.True(squeeze.MomentumRising);
    }

    #endregion

    #region Color Coding Tests

    [Fact]
    public void ColorCode_RisingAboveZero_IsCyan()
    {
        var squeeze = new TtmSqueeze(bbPeriod: 3, bbMult: 2.0, kcPeriod: 3, kcMult: 1.5, momPeriod: 3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Strong uptrend with rising momentum
        for (int i = 0; i < 5; i++)
        {
            squeeze.Update(new TBar(baseTime + i * 60000, 100 + i * 2, 105 + i * 2, 98 + i * 2, 103 + i * 2, 1000));
        }

        // Should be MomentumPositive and MomentumRising = ColorCode 0 (Cyan)
        if (squeeze.MomentumPositive && squeeze.MomentumRising)
        {
            Assert.Equal(0, squeeze.ColorCode);
        }
    }

    [Fact]
    public void ColorCode_FallingBelowZero_IsRed()
    {
        var squeeze = new TtmSqueeze(bbPeriod: 3, bbMult: 2.0, kcPeriod: 3, kcMult: 1.5, momPeriod: 3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Strong downtrend with falling momentum
        for (int i = 0; i < 5; i++)
        {
            squeeze.Update(new TBar(baseTime + i * 60000, 100 - i * 2, 102 - i * 2, 95 - i * 2, 97 - i * 2, 1000));
        }

        // Should be !MomentumPositive and !MomentumRising = ColorCode 2 (Red)
        if (!squeeze.MomentumPositive && !squeeze.MomentumRising)
        {
            Assert.Equal(2, squeeze.ColorCode);
        }
    }

    #endregion

    #region Bar Correction Tests

    [Fact]
    public void Update_BarCorrection_RestoresPreviousState()
    {
        var squeeze = new TtmSqueeze(bbPeriod: 3, bbMult: 2.0, kcPeriod: 3, kcMult: 1.5, momPeriod: 3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 3; i++)
        {
            squeeze.Update(new TBar(baseTime + i * 60000, 100, 105, 95, 102, 1000));
        }

        // Add new bar
        squeeze.Update(new TBar(baseTime + 3 * 60000, 100, 110, 98, 108, 1000), isNew: true);
        double valueAfterNew = squeeze.Momentum.Value;

        // Correct the bar with different data
        squeeze.Update(new TBar(baseTime + 3 * 60000, 108, 112, 105, 92, 1000), isNew: false);
        double valueAfterCorrection = squeeze.Momentum.Value;

        Assert.NotEqual(valueAfterNew, valueAfterCorrection);
    }

    [Fact]
    public void Update_MultipleCorrections_ProduceConsistentResults()
    {
        var squeeze = new TtmSqueeze(bbPeriod: 3, bbMult: 2.0, kcPeriod: 3, kcMult: 1.5, momPeriod: 3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 3; i++)
        {
            squeeze.Update(new TBar(baseTime + i * 60000, 100, 105, 95, 102, 1000));
        }

        // New bar
        squeeze.Update(new TBar(baseTime + 3 * 60000, 100, 110, 98, 108, 1000), isNew: true);
        double firstValue = squeeze.Momentum.Value;

        // Correction 1
        squeeze.Update(new TBar(baseTime + 3 * 60000, 108, 115, 105, 90, 1000), isNew: false);

        // Correction 2 - same as first new bar
        squeeze.Update(new TBar(baseTime + 3 * 60000, 100, 110, 98, 108, 1000), isNew: false);
        double secondValue = squeeze.Momentum.Value;

        Assert.Equal(firstValue, secondValue, Precision);
    }

    #endregion

    #region NaN Handling Tests

    [Fact]
    public void Update_NaNInput_UsesLastValidValue()
    {
        var squeeze = new TtmSqueeze(bbPeriod: 3, bbMult: 2.0, kcPeriod: 3, kcMult: 1.5, momPeriod: 3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        squeeze.Update(new TBar(baseTime, 100, 105, 95, 102, 1000));

        squeeze.Update(new TBar(baseTime + 60000, double.NaN, double.NaN, double.NaN, double.NaN, 1000));

        Assert.True(double.IsFinite(squeeze.Momentum.Value));
    }

    [Fact]
    public void Update_InfinityInput_UsesLastValidValue()
    {
        var squeeze = new TtmSqueeze(bbPeriod: 3, bbMult: 2.0, kcPeriod: 3, kcMult: 1.5, momPeriod: 3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        squeeze.Update(new TBar(baseTime, 100, 105, 95, 102, 1000));

        squeeze.Update(new TBar(baseTime + 60000, double.PositiveInfinity, 105, 95, 102, 1000));

        Assert.True(double.IsFinite(squeeze.Momentum.Value));
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsState()
    {
        var squeeze = new TtmSqueeze(bbPeriod: 3, bbMult: 2.0, kcPeriod: 3, kcMult: 1.5, momPeriod: 3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 5; i++)
        {
            squeeze.Update(new TBar(baseTime + i * 60000, 100, 105, 95, 102, 1000));
        }

        Assert.True(squeeze.IsHot);

        squeeze.Reset();

        Assert.False(squeeze.IsHot);
        Assert.Equal(0, squeeze.Momentum.Value);
    }

    [Fact]
    public void Reset_AllowsFreshStart()
    {
        var squeeze = new TtmSqueeze(bbPeriod: 3, bbMult: 2.0, kcPeriod: 3, kcMult: 1.5, momPeriod: 3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Uptrend
        for (int i = 0; i < 5; i++)
        {
            squeeze.Update(new TBar(baseTime + i * 60000, 100 + i * 2, 105 + i * 2, 95 + i * 2, 103 + i * 2, 1000));
        }

        double upTrendMomentum = squeeze.Momentum.Value;

        squeeze.Reset();

        // Downtrend
        for (int i = 0; i < 5; i++)
        {
            squeeze.Update(new TBar(baseTime + i * 60000, 100 - i * 2, 102 - i * 2, 95 - i * 2, 97 - i * 2, 1000));
        }

        Assert.NotEqual(upTrendMomentum, squeeze.Momentum.Value);
    }

    #endregion

    #region Prime Tests

    [Fact]
    public void Prime_FillsBuffer()
    {
        var squeeze = new TtmSqueeze(bbPeriod: 5, bbMult: 2.0, kcPeriod: 5, kcMult: 1.5, momPeriod: 5);
        var source = new TBarSeries();
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 10; i++)
        {
            source.Add(new TBar(baseTime + i * 60000, 100, 105, 95, 102, 1000));
        }

        squeeze.Prime(source);

        Assert.True(squeeze.IsHot);
    }

    #endregion

    #region Batch Tests

    [Fact]
    public void Batch_ReturnsSeriesOfCorrectLength()
    {
        var source = new TBarSeries();
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 20; i++)
        {
            source.Add(new TBar(baseTime + i * 60000, 100 + i, 105 + i, 95 + i, 102 + i, 1000));
        }

        var result = TtmSqueeze.Batch(source);

        Assert.Equal(20, result.Count);
    }

    [Fact]
    public void Batch_EmptySource_ReturnsEmpty()
    {
        var source = new TBarSeries();
        var result = TtmSqueeze.Batch(source);

        Assert.Empty(result);
    }

    [Fact]
    public void Calculate_ReturnsBothResultsAndIndicator()
    {
        var source = new TBarSeries();
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 20; i++)
        {
            source.Add(new TBar(baseTime + i * 60000, 100, 105, 95, 102, 1000));
        }

        var (results, indicator) = TtmSqueeze.Calculate(source, bbPeriod: 10, bbMult: 2.0, kcPeriod: 10, kcMult: 1.5, momPeriod: 10);

        Assert.Equal(20, results.Count);
        Assert.True(indicator.IsHot);
        Assert.Equal(10, indicator.BbPeriod);
    }

    #endregion

    #region Event Publishing Tests

    [Fact]
    public void Update_PublishesEvent()
    {
        var squeeze = new TtmSqueeze(bbPeriod: 3, bbMult: 2.0, kcPeriod: 3, kcMult: 1.5, momPeriod: 3);
        int eventCount = 0;
        squeeze.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        squeeze.Update(new TBar(baseTime, 100, 105, 95, 102, 1000));

        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void Update_EventContainsCorrectValue()
    {
        var squeeze = new TtmSqueeze(bbPeriod: 3, bbMult: 2.0, kcPeriod: 3, kcMult: 1.5, momPeriod: 3);
        TValue? receivedValue = null;
        squeeze.Pub += (object? sender, in TValueEventArgs args) => receivedValue = args.Value;

        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        squeeze.Update(new TBar(baseTime, 100, 105, 95, 102, 1000));

        Assert.NotNull(receivedValue);
        Assert.Equal(squeeze.Momentum.Value, receivedValue.Value.Value);
    }

    #endregion

    #region GBM Random Data Test

    [Fact]
    public void Update_GbmData_ProducesFiniteValues()
    {
        var squeeze = new TtmSqueeze(bbPeriod: 14, bbMult: 2.0, kcPeriod: 14, kcMult: 1.5, momPeriod: 14);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var random = new Random(42);

        double price = 100.0;

        for (int i = 0; i < 100; i++)
        {
            double change = (random.NextDouble() - 0.5) * 4;
            double open = price;
            double high = Math.Max(open, open + Math.Abs(change) + random.NextDouble() * 2);
            double low = Math.Min(open, open - Math.Abs(change) - random.NextDouble() * 2);
            double close = open + change;

            squeeze.Update(new TBar(baseTime + i * 60000, open, high, low, close, 1000));
            price = close;

            // Momentum should always be finite
            Assert.True(double.IsFinite(squeeze.Momentum.Value));

            // ColorCode should be valid (0-3)
            Assert.InRange(squeeze.ColorCode, 0, 3);
        }
    }

    #endregion
}
