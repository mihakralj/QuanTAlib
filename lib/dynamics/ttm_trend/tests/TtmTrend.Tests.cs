// TTM_TREND Tests - John Carter's TTM Trend Indicator

using Xunit;

namespace QuanTAlib.Tests;

// ═══════════════════════════════════════════════════════════════════════════
// Constructor Tests
// ═══════════════════════════════════════════════════════════════════════════

public class TtmTrendConstructorTests
{
    [Fact]
    public void Constructor_DefaultPeriod_Is6()
    {
        var ttm = new TtmTrend();
        Assert.Equal(6, ttm.Period);
    }

    [Fact]
    public void Constructor_CustomPeriod_IsSet()
    {
        var ttm = new TtmTrend(period: 10);
        Assert.Equal(10, ttm.Period);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Constructor_InvalidPeriod_Throws(int period)
    {
        Assert.Throws<ArgumentException>(() => new TtmTrend(period));
    }

    [Fact]
    public void Constructor_MinPeriod_IsValid()
    {
        var ttm = new TtmTrend(period: 1);
        Assert.Equal(1, ttm.Period);
    }

    [Fact]
    public void Name_ContainsPeriod()
    {
        var ttm = new TtmTrend(period: 10);
        Assert.Contains("10", ttm.Name, StringComparison.Ordinal);
        Assert.Contains("TTM_TREND", ttm.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void WarmupPeriod_Is2()
    {
        Assert.Equal(2, TtmTrend.WarmupPeriod);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// Basic Operation Tests
// ═══════════════════════════════════════════════════════════════════════════

public class TtmTrendBasicTests
{
    [Fact]
    public void Update_FirstBar_ReturnsValue()
    {
        var ttm = new TtmTrend();
        var result = ttm.Update(new TValue(DateTime.UtcNow.Ticks, 100.0));
        Assert.Equal(100.0, result.Value);
    }

    [Fact]
    public void Update_SecondBar_CalculatesEma()
    {
        var ttm = new TtmTrend(period: 6);  // alpha = 2/7 ≈ 0.2857
        var time = DateTime.UtcNow;

        ttm.Update(new TValue(time.Ticks, 100.0));
        var result = ttm.Update(new TValue(time.AddMinutes(1).Ticks, 107.0));

        // EMA = alpha * value + (1 - alpha) * prevEMA
        // EMA = 0.2857 * 107 + 0.7143 * 100 = 30.57 + 71.43 = 102.0
        double alpha = 2.0 / 7.0;
        double expected = (alpha * 107.0) + ((1 - alpha) * 100.0);
        Assert.Equal(expected, result.Value, 10);
    }

    [Fact]
    public void IsHot_AfterFirstBar_IsFalse()
    {
        var ttm = new TtmTrend();
        ttm.Update(new TValue(DateTime.UtcNow.Ticks, 100.0));
        Assert.False(ttm.IsHot);
    }

    [Fact]
    public void IsHot_AfterSecondBar_IsTrue()
    {
        var ttm = new TtmTrend();
        var time = DateTime.UtcNow;
        ttm.Update(new TValue(time.Ticks, 100.0));
        ttm.Update(new TValue(time.AddMinutes(1).Ticks, 101.0));
        Assert.True(ttm.IsHot);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// Trend Direction Tests
// ═══════════════════════════════════════════════════════════════════════════

public class TtmTrendDirectionTests
{
    [Fact]
    public void Trend_RisingValues_IsBullish()
    {
        var ttm = new TtmTrend(period: 6);
        var time = DateTime.UtcNow;

        ttm.Update(new TValue(time.Ticks, 100.0));
        ttm.Update(new TValue(time.AddMinutes(1).Ticks, 110.0));

        Assert.Equal(1, ttm.Trend);
    }

    [Fact]
    public void Trend_FallingValues_IsBearish()
    {
        var ttm = new TtmTrend(period: 6);
        var time = DateTime.UtcNow;

        ttm.Update(new TValue(time.Ticks, 100.0));
        ttm.Update(new TValue(time.AddMinutes(1).Ticks, 90.0));

        Assert.Equal(-1, ttm.Trend);
    }

    [Fact]
    public void Trend_SameValue_IsNeutral()
    {
        var ttm = new TtmTrend(period: 6);
        var time = DateTime.UtcNow;

        ttm.Update(new TValue(time.Ticks, 100.0));
        ttm.Update(new TValue(time.AddMinutes(1).Ticks, 100.0));

        Assert.Equal(0, ttm.Trend);
    }

    [Fact]
    public void Trend_CanChangeDirection()
    {
        var ttm = new TtmTrend(period: 2);  // Fast EMA
        var time = DateTime.UtcNow;

        ttm.Update(new TValue(time.Ticks, 100.0));
        ttm.Update(new TValue(time.AddMinutes(1).Ticks, 110.0));
        Assert.Equal(1, ttm.Trend);

        // Drop significantly to reverse trend
        ttm.Update(new TValue(time.AddMinutes(2).Ticks, 90.0));
        Assert.Equal(-1, ttm.Trend);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// Strength Tests
// ═══════════════════════════════════════════════════════════════════════════

public class TtmTrendStrengthTests
{
    [Fact]
    public void Strength_IsPositive()
    {
        var ttm = new TtmTrend(period: 6);
        var time = DateTime.UtcNow;

        ttm.Update(new TValue(time.Ticks, 100.0));
        ttm.Update(new TValue(time.AddMinutes(1).Ticks, 110.0));

        Assert.True(ttm.Strength > 0);
    }

    [Fact]
    public void Strength_ZeroOnFirstBar()
    {
        var ttm = new TtmTrend(period: 6);
        var time = DateTime.UtcNow;

        ttm.Update(new TValue(time.Ticks, 100.0));

        Assert.Equal(0, ttm.Strength);
    }

    [Fact]
    public void Strength_LargerMoves_HigherStrength()
    {
        var ttm1 = new TtmTrend(period: 6);
        var ttm2 = new TtmTrend(period: 6);
        var time = DateTime.UtcNow;

        // Small move
        ttm1.Update(new TValue(time.Ticks, 100.0));
        ttm1.Update(new TValue(time.AddMinutes(1).Ticks, 101.0));

        // Large move
        ttm2.Update(new TValue(time.Ticks, 100.0));
        ttm2.Update(new TValue(time.AddMinutes(1).Ticks, 110.0));

        Assert.True(ttm2.Strength > ttm1.Strength);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// Bar Input Tests
// ═══════════════════════════════════════════════════════════════════════════

public class TtmTrendBarInputTests
{
    [Fact]
    public void Update_Bar_UsesTypicalPrice()
    {
        var ttm = new TtmTrend(period: 6);
        var bar = new TBar(DateTime.UtcNow.Ticks, 100.0, 105.0, 98.0, 102.0, 1000);

        var result = ttm.Update(bar);

        // Typical price = (H + L + C) / 3 = (105 + 98 + 102) / 3 = 101.67
        double typical = (105.0 + 98.0 + 102.0) / 3.0;
        Assert.Equal(typical, result.Value, 10);
    }

    [Fact]
    public void Update_BarSeries_ReturnsCorrectLength()
    {
        var ttm = new TtmTrend(period: 6);
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            bars.Add(new TBar(time.AddMinutes(i).Ticks, 100.0, 105.0, 95.0, 102.0, 1000));
        }

        var result = ttm.Update(bars);
        Assert.Equal(10, result.Count);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// Edge Case Tests
// ═══════════════════════════════════════════════════════════════════════════

public class TtmTrendEdgeCaseTests
{
    [Fact]
    public void Update_NaN_ReturnsLastValue()
    {
        var ttm = new TtmTrend(period: 6);
        var time = DateTime.UtcNow;

        var result1 = ttm.Update(new TValue(time.Ticks, 100.0));
        var result2 = ttm.Update(new TValue(time.AddMinutes(1).Ticks, double.NaN));

        Assert.Equal(result1.Value, result2.Value);
    }

    [Fact]
    public void Update_Infinity_ReturnsLastValue()
    {
        var ttm = new TtmTrend(period: 6);
        var time = DateTime.UtcNow;

        var result1 = ttm.Update(new TValue(time.Ticks, 100.0));
        var result2 = ttm.Update(new TValue(time.AddMinutes(1).Ticks, double.PositiveInfinity));

        Assert.Equal(result1.Value, result2.Value);
    }

    [Fact]
    public void Update_LargeValues_CalculatesCorrectly()
    {
        var ttm = new TtmTrend(period: 6);
        var time = DateTime.UtcNow;

        var result = ttm.Update(new TValue(time.Ticks, 1e10));
        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(1e10, result.Value);
    }

    [Fact]
    public void Update_SmallValues_CalculatesCorrectly()
    {
        var ttm = new TtmTrend(period: 6);
        var time = DateTime.UtcNow;

        var result = ttm.Update(new TValue(time.Ticks, 1e-10));
        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(1e-10, result.Value);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// Reset Tests
// ═══════════════════════════════════════════════════════════════════════════

public class TtmTrendResetTests
{
    [Fact]
    public void Reset_ClearsState()
    {
        var ttm = new TtmTrend(period: 6);
        var time = DateTime.UtcNow;

        ttm.Update(new TValue(time.Ticks, 100.0));
        ttm.Update(new TValue(time.AddMinutes(1).Ticks, 110.0));

        Assert.True(ttm.IsHot);

        ttm.Reset();

        Assert.False(ttm.IsHot);
        Assert.Equal(default, ttm.Last);
        Assert.Equal(0, ttm.Trend);
        Assert.Equal(0, ttm.Strength);
    }

    [Fact]
    public void Reset_CanReuseAfterReset()
    {
        var ttm = new TtmTrend(period: 6);
        var time = DateTime.UtcNow;

        ttm.Update(new TValue(time.Ticks, 100.0));
        ttm.Update(new TValue(time.AddMinutes(1).Ticks, 110.0));
        ttm.Reset();

        var result = ttm.Update(new TValue(time.AddMinutes(2).Ticks, 200.0));

        Assert.Equal(200.0, result.Value);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// Bar Correction Tests
// ═══════════════════════════════════════════════════════════════════════════

public class TtmTrendBarCorrectionTests
{
    [Fact]
    public void Update_IsNewFalse_CorrectsPreviousValue()
    {
        var ttm = new TtmTrend(period: 6);
        var time = DateTime.UtcNow;

        ttm.Update(new TValue(time.Ticks, 100.0));
        ttm.Update(new TValue(time.AddMinutes(1).Ticks, 110.0), isNew: true);

        // Correct the bar with different value
        var corrected = ttm.Update(new TValue(time.AddMinutes(1).Ticks, 105.0), isNew: false);

        // Should use 105 instead of 110
        double alpha = 2.0 / 7.0;
        double expected = (alpha * 105.0) + ((1 - alpha) * 100.0);
        Assert.Equal(expected, corrected.Value, 10);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// Batch Processing Tests
// ═══════════════════════════════════════════════════════════════════════════

public class TtmTrendBatchTests
{
    [Fact]
    public void Batch_ReturnsCorrectResults()
    {
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            bars.Add(new TBar(time.AddMinutes(i).Ticks, 100.0 + i, 105.0 + i, 95.0 + i, 102.0 + i, 1000));
        }

        var result = TtmTrend.Batch(bars, period: 6);

        Assert.Equal(10, result.Count);
    }

    [Fact]
    public void Calculate_ReturnsIndicatorAndResults()
    {
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            bars.Add(new TBar(time.AddMinutes(i).Ticks, 100.0 + i, 105.0 + i, 95.0 + i, 102.0 + i, 1000));
        }

        var (results, indicator) = TtmTrend.Calculate(bars, period: 6);

        Assert.Equal(10, results.Count);
        Assert.True(indicator.IsHot);
        Assert.Equal(6, indicator.Period);
    }

    [Fact]
    public void Update_EmptyBarSeries_ReturnsEmpty()
    {
        var ttm = new TtmTrend(period: 6);
        var bars = new TBarSeries();

        var result = ttm.Update(bars);

        Assert.True(result.Count == 0);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// Event Tests
// ═══════════════════════════════════════════════════════════════════════════

public class TtmTrendEventTests
{
    [Fact]
    public void Update_RaisesPubEvent()
    {
        var ttm = new TtmTrend(period: 6);
        var eventRaised = false;
        TValue receivedValue = default;

        ttm.Pub += (object? sender, in TValueEventArgs args) =>
        {
            eventRaised = true;
            receivedValue = args.Value;
        };

        var result = ttm.Update(new TValue(DateTime.UtcNow.Ticks, 100.0));

        Assert.True(eventRaised);
        Assert.Equal(result.Value, receivedValue.Value);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// Prime Tests
// ═══════════════════════════════════════════════════════════════════════════

public class TtmTrendPrimeTests
{
    [Fact]
    public void Prime_WarmUpIndicator()
    {
        var ttm = new TtmTrend(period: 6);
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            bars.Add(new TBar(time.AddMinutes(i).Ticks, 100.0 + i, 105.0 + i, 95.0 + i, 102.0 + i, 1000));
        }

        ttm.Prime(bars);

        Assert.True(ttm.IsHot);
        Assert.NotEqual(default, ttm.Last);
    }
}
