using TradingPlatform.BusinessLayer;
using Xunit;

namespace QuanTAlib.Tests;

public class QstickIndicatorTests
{
    [Fact]
    public void Constructor_CreatesValidIndicator()
    {
        var indicator = new QstickIndicator();
        Assert.NotNull(indicator);
        Assert.Equal("Qstick Indicator", indicator.Name);
    }

    [Fact]
    public void DefaultPeriod_Is14()
    {
        var indicator = new QstickIndicator();
        Assert.Equal(14, indicator.Period);
    }

    [Fact]
    public void DefaultMaType_IsSMA()
    {
        var indicator = new QstickIndicator();
        Assert.Equal("SMA", indicator.MaType);
    }

    [Fact]
    public void ShortName_IncludesParameters()
    {
        var indicator = new QstickIndicator { Period = 20, MaType = "EMA" };
        Assert.Equal("QSTICK(20,EMA)", indicator.ShortName);
    }

    [Fact]
    public void MinHistoryDepths_EqualsZero()
    {
        var indicator = new QstickIndicator { Period = 10 };
        Assert.Equal(0, QstickIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void CalculationIntegration_ProducesCorrectValues()
    {
        var qstickCore = new Qstick(3);
        var time = DateTime.UtcNow;

        // Simulate bar data
        var bar1 = new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000);
        var bar2 = new TBar(time.AddMinutes(1).Ticks, 100.0, 105.0, 95.0, 103.0, 1000);
        var bar3 = new TBar(time.AddMinutes(2).Ticks, 100.0, 108.0, 95.0, 106.0, 1000);

        qstickCore.Update(bar1);
        qstickCore.Update(bar2);
        var result = qstickCore.Update(bar3);

        // SMA of (5, 3, 6) = 14/3 ≈ 4.667
        Assert.Equal(14.0 / 3.0, result.Value, 10);
    }

    [Fact]
    public void EmaMode_CalculatesCorrectly()
    {
        var qstickCore = new Qstick(3, useEma: true);
        var time = DateTime.UtcNow;

        // Bar 1: diff = 5
        qstickCore.Update(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));

        // Bar 2: diff = -3, EMA with alpha = 0.5
        var result = qstickCore.Update(new TBar(time.AddMinutes(1).Ticks, 100.0, 105.0, 95.0, 97.0, 1000));

        // EMA = 0.5 * -3 + 0.5 * 5 = 1.0
        Assert.Equal(1.0, result.Value, 10);
    }

    [Fact]
    public void BullishBars_ProducePositiveQstick()
    {
        var qstick = new Qstick(5);
        var time = DateTime.UtcNow;

        // All bullish bars (close > open)
        for (int i = 0; i < 5; i++)
        {
            qstick.Update(new TBar(time.AddMinutes(i).Ticks, 100.0, 110.0, 95.0, 105.0, 1000));
        }

        Assert.True(qstick.Last.Value > 0);
        Assert.Equal(5.0, qstick.Last.Value, 10);
    }

    [Fact]
    public void BearishBars_ProduceNegativeQstick()
    {
        var qstick = new Qstick(5);
        var time = DateTime.UtcNow;

        // All bearish bars (close < open)
        for (int i = 0; i < 5; i++)
        {
            qstick.Update(new TBar(time.AddMinutes(i).Ticks, 100.0, 105.0, 90.0, 95.0, 1000));
        }

        Assert.True(qstick.Last.Value < 0);
        Assert.Equal(-5.0, qstick.Last.Value, 10);
    }

    [Fact]
    public void DojiBars_ProduceZeroQstick()
    {
        var qstick = new Qstick(5);
        var time = DateTime.UtcNow;

        // All doji bars (close = open)
        for (int i = 0; i < 5; i++)
        {
            qstick.Update(new TBar(time.AddMinutes(i).Ticks, 100.0, 105.0, 95.0, 100.0, 1000));
        }

        Assert.Equal(0.0, qstick.Last.Value, 10);
    }

    [Fact]
    public void CoreIndicator_ResetsCorrectly()
    {
        var qstick = new Qstick(3);
        var time = DateTime.UtcNow;

        qstick.Update(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));
        qstick.Update(new TBar(time.AddMinutes(1).Ticks, 100.0, 105.0, 95.0, 103.0, 1000));

        Assert.NotEqual(default, qstick.Last);

        qstick.Reset();

        Assert.False(qstick.IsHot);
        Assert.Equal(default, qstick.Last);
    }
}
