using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class MeanDevIndicatorTests
{
    [Fact]
    public void MeanDevIndicator_Constructor_SetsDefaults()
    {
        var indicator = new MeanDevIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("MeanDev - Mean Absolute Deviation", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void MeanDevIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new MeanDevIndicator { Period = 14 };

        Assert.Equal(0, MeanDevIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void MeanDevIndicator_Initialize_CreatesInternalMeanDev()
    {
        var indicator = new MeanDevIndicator { Period = 10 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
        Assert.Equal("MeanDev", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void MeanDevIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new MeanDevIndicator { Period = 5 };
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
        Assert.True(value >= 0.0);
    }

    [Fact]
    public void MeanDevIndicator_DifferentSourceTypes()
    {
        var indicator = new MeanDevIndicator { Period = 5, Source = SourceType.Open };
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
    public void MeanDevIndicator_ConstantData_ReturnsZero()
    {
        var indicator = new MeanDevIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100.0, 105.0, 95.0, 100.0);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double value = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(0.0, value, precision: 6);
    }
}
