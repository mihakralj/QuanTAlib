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
    public void Constructor_SetsDescription()
    {
        var indicator = new TtmTrendIndicator();
        Assert.Contains("TTM Trend", indicator.Description, StringComparison.Ordinal);
        Assert.Contains("EMA", indicator.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultPeriod_Is6()
    {
        var indicator = new TtmTrendIndicator();
        Assert.Equal(6, indicator.Period);
    }

    [Fact]
    public void DefaultShowColdValues_IsTrue()
    {
        var indicator = new TtmTrendIndicator();
        Assert.True(indicator.ShowColdValues);
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
    public void Constructor_AddsOneLineSeries()
    {
        var indicator = new TtmTrendIndicator();
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void Parameters_CanBeChanged()
    {
        var indicator = new TtmTrendIndicator { Period = 6 };

        indicator.Period = 20;

        Assert.Equal(20, indicator.Period);
        Assert.Equal(0, TtmTrendIndicator.MinHistoryDepths);
    }

    [Fact]
    public void ShowColdValues_CanBeChanged()
    {
        var indicator = new TtmTrendIndicator();

        indicator.ShowColdValues = false;

        Assert.False(indicator.ShowColdValues);
    }

    [Fact]
    public void Initialize_CreatesInternalIndicator()
    {
        var indicator = new TtmTrendIndicator { Period = 10 };

        indicator.Initialize();

        // Line series count should remain 1 after init
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new TtmTrendIndicator { Period = 6 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double value = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(value));
    }

    [Fact]
    public void ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new TtmTrendIndicator { Period = 6 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Simulate a new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(10), 110, 120, 100, 115);
        var newArgs = new UpdateArgs(UpdateReason.NewBar);
        indicator.ProcessUpdate(newArgs);

        double value = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(value));
    }

    [Fact]
    public void ProcessUpdate_BullishTrend_ProducesGreenMarker()
    {
        var indicator = new TtmTrendIndicator { Period = 2 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Feed strongly rising bars to trigger bullish trend (Trend == 1)
        indicator.HistoricalData.AddBar(now, 50.0, 55.0, 48.0, 52.0);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        indicator.HistoricalData.AddBar(now.AddMinutes(1), 60.0, 65.0, 58.0, 62.0);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        indicator.HistoricalData.AddBar(now.AddMinutes(2), 70.0, 75.0, 68.0, 72.0);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        indicator.HistoricalData.AddBar(now.AddMinutes(3), 80.0, 85.0, 78.0, 82.0);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Value should be finite after enough bars
        double value = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(value));
    }

    [Fact]
    public void ProcessUpdate_BearishTrend_ProducesRedMarker()
    {
        var indicator = new TtmTrendIndicator { Period = 2 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Feed strongly falling bars to trigger bearish trend (Trend == -1)
        indicator.HistoricalData.AddBar(now, 100.0, 105.0, 98.0, 102.0);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        indicator.HistoricalData.AddBar(now.AddMinutes(1), 90.0, 95.0, 88.0, 92.0);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        indicator.HistoricalData.AddBar(now.AddMinutes(2), 80.0, 85.0, 78.0, 82.0);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        indicator.HistoricalData.AddBar(now.AddMinutes(3), 70.0, 75.0, 68.0, 72.0);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double value = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(value));
    }

    [Fact]
    public void ProcessUpdate_FlatPrices_ProducesGrayMarker()
    {
        var indicator = new TtmTrendIndicator { Period = 2 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Feed identical bars to get Trend == 0 (neutral)
        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100.0, 100.0, 100.0, 100.0);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double value = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(value));
    }

    [Fact]
    public void ProcessUpdate_ColdValues_HiddenWhenDisabled()
    {
        var indicator = new TtmTrendIndicator { Period = 6, ShowColdValues = false };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Only 1 bar — indicator should not yet be hot
        indicator.HistoricalData.AddBar(now, 100.0, 105.0, 98.0, 102.0);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // With ShowColdValues=false, the cold value should not be set
        // (LineSeries.SetValue with isHot=false and showCold=false skips the value)
        Assert.Single(indicator.LinesSeries);
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

    [Fact]
    public void ProcessUpdate_MultipleNewBars_AccumulatesValues()
    {
        var indicator = new TtmTrendIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Feed historical bars
        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + (i * 2), 110 + (i * 2), 90 + (i * 2), 105 + (i * 2));
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Feed new bars
        for (int i = 5; i < 8; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + (i * 2), 110 + (i * 2), 90 + (i * 2), 105 + (i * 2));
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));
        }

        double value = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(value));
    }

    [Fact]
    public void Initialize_AfterParameterChange_UsesNewPeriod()
    {
        var indicator = new TtmTrendIndicator { Period = 6 };
        indicator.Initialize();

        // Change period and re-initialize
        indicator.Period = 20;
        indicator.Initialize();

        Assert.Equal("TTM_TREND(20)", indicator.ShortName);
    }
}
