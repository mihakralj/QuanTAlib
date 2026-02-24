using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class ImiIndicatorTests
{
    [Fact]
    public void ImiIndicator_Constructor_SetsDefaults()
    {
        var indicator = new ImiIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Intraday Momentum Index", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void ImiIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new ImiIndicator { Period = 20 };

        Assert.Equal(0, ImiIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void ImiIndicator_ShortName_IncludesParameters()
    {
        var indicator = new ImiIndicator { Period = 20 };
        indicator.Initialize();

        Assert.Contains("IMI", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void ImiIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new ImiIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Imi.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void ImiIndicator_Initialize_CreatesInternalImi()
    {
        var indicator = new ImiIndicator { Period = 14 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (single IMI line)
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void ImiIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new ImiIndicator { Period = 5 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        // Need enough bars for Period
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double imi = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(imi));
        Assert.InRange(imi, 0.0, 100.0);
    }

    [Fact]
    public void ImiIndicator_AllUpBars_Returns100()
    {
        var indicator = new ImiIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 3; i++)
        {
            // Up bars: close > open
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 115, 99, 110);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // All up bars should result in 100
        Assert.Equal(100.0, indicator.LinesSeries[0].GetValue(0), 0.0001);
    }

    [Fact]
    public void ImiIndicator_AllDownBars_Returns0()
    {
        var indicator = new ImiIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 3; i++)
        {
            // Down bars: close < open
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 110, 115, 99, 100);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // All down bars should result in 0
        Assert.Equal(0.0, indicator.LinesSeries[0].GetValue(0), 0.0001);
    }

    [Fact]
    public void ImiIndicator_MixedBars_Returns50()
    {
        var indicator = new ImiIndicator { Period = 2 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Up bar: gain = 10
        indicator.HistoricalData.AddBar(now, 100, 115, 99, 110);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Down bar: loss = 10
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 110, 115, 99, 100);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Equal gains and losses should result in 50
        Assert.Equal(50.0, indicator.LinesSeries[0].GetValue(0), 0.0001);
    }
}
