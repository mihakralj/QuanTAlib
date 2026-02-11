using TradingPlatform.BusinessLayer;
using Xunit;

namespace QuanTAlib.Tests;

public class TtmTrendIndicatorTests
{
    [Fact]
    public void Constructor_CreatesValidIndicator()
    {
        var indicator = new TtmTrendIndicator();
        Assert.NotNull(indicator);
        Assert.Equal("TTM Trend", indicator.Name);
    }

    [Fact]
    public void DefaultPeriod_Is6()
    {
        var indicator = new TtmTrendIndicator();
        Assert.Equal(6, indicator.Period);
    }

    [Fact]
    public void ShortName_IncludesParameters()
    {
        var indicator = new TtmTrendIndicator { Period = 10 };
        Assert.Equal("TTM_TREND(10)", indicator.ShortName);
    }

    [Fact]
    public void MinHistoryDepths_EqualsZero()
    {
        var indicator = new TtmTrendIndicator { Period = 10 };
        Assert.Equal(0, TtmTrendIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void SeparateWindow_IsFalse()
    {
        var indicator = new TtmTrendIndicator();
        Assert.False(indicator.SeparateWindow);
    }

    [Fact]
    public void OnBackGround_IsTrue()
    {
        var indicator = new TtmTrendIndicator();
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void CalculationIntegration_ProducesCorrectValues()
    {
        var ttmCore = new TtmTrend(6);
        var time = DateTime.UtcNow;

        var bar1 = new TBar(time.Ticks, 100.0, 105.0, 98.0, 102.0, 1000);
        var bar2 = new TBar(time.AddMinutes(1).Ticks, 102.0, 108.0, 100.0, 106.0, 1000);

        ttmCore.Update(bar1);
        var result = ttmCore.Update(bar2);

        // After 2 bars, should be hot and have valid value
        Assert.True(ttmCore.IsHot);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void TrendDirection_Bullish_WhenRising()
    {
        var ttmCore = new TtmTrend(6);
        var time = DateTime.UtcNow;

        ttmCore.Update(new TBar(time.Ticks, 100.0, 105.0, 98.0, 102.0, 1000));
        ttmCore.Update(new TBar(time.AddMinutes(1).Ticks, 110.0, 115.0, 108.0, 112.0, 1000));

        Assert.Equal(1, ttmCore.Trend);
    }

    [Fact]
    public void TrendDirection_Bearish_WhenFalling()
    {
        var ttmCore = new TtmTrend(6);
        var time = DateTime.UtcNow;

        ttmCore.Update(new TBar(time.Ticks, 100.0, 105.0, 98.0, 102.0, 1000));
        ttmCore.Update(new TBar(time.AddMinutes(1).Ticks, 90.0, 95.0, 88.0, 92.0, 1000));

        Assert.Equal(-1, ttmCore.Trend);
    }

    [Fact]
    public void CoreIndicator_ResetsCorrectly()
    {
        var ttm = new TtmTrend(6);
        var time = DateTime.UtcNow;

        ttm.Update(new TBar(time.Ticks, 100.0, 105.0, 98.0, 102.0, 1000));
        ttm.Update(new TBar(time.AddMinutes(1).Ticks, 102.0, 108.0, 100.0, 106.0, 1000));

        Assert.True(ttm.IsHot);

        ttm.Reset();

        Assert.False(ttm.IsHot);
        Assert.Equal(default, ttm.Last);
        Assert.Equal(0, ttm.Trend);
    }
}
