using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class MinusDiIndicatorTests
{
    [Fact]
    public void MinusDiIndicator_Constructor_SetsDefaults()
    {
        var indicator = new MinusDiIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("-DI - Minus Directional Indicator", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void MinusDiIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new MinusDiIndicator { Period = 20 };

        Assert.Equal(0, MinusDiIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void MinusDiIndicator_Initialize_CreatesInternal()
    {
        var indicator = new MinusDiIndicator { Period = 14 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void MinusDiIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new MinusDiIndicator { Period = 5 };
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
    public void MinusDiIndicator_ShortName_IsCorrect()
    {
        var indicator = new MinusDiIndicator { Period = 20 };
        Assert.Equal("-DI 20", indicator.ShortName);
    }

    [Fact]
    public void MinusDiIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new MinusDiIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MinusDi.Quantower.cs", indicator.SourceCodeLink, StringComparison.OrdinalIgnoreCase);
    }
}
