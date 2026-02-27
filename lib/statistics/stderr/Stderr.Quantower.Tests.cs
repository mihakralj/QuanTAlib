using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class StderrIndicatorTests
{
    [Fact]
    public void StderrIndicator_Constructor_SetsDefaults()
    {
        var indicator = new StderrIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Stderr - Standard Error of Regression", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void StderrIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new StderrIndicator { Period = 14 };

        Assert.Equal(0, StderrIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void StderrIndicator_Initialize_CreatesInternalStderr()
    {
        var indicator = new StderrIndicator { Period = 10 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Stderr", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void StderrIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new StderrIndicator { Period = 5 };
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
    public void StderrIndicator_DifferentSourceTypes()
    {
        var indicator = new StderrIndicator { Period = 5, Source = SourceType.Open };
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
    public void StderrIndicator_LinearData_ReturnsNearZero()
    {
        var indicator = new StderrIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Perfectly linear close prices → residuals = 0 → Stderr ≈ 0
        for (int i = 0; i < 20; i++)
        {
            double price = 100.0 + i * 2.0;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price - 1, price + 1, price - 2, price);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double value = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(value));
        Assert.Equal(0.0, value, precision: 6);
    }
}
