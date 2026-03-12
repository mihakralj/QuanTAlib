using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class WillrIndicatorTests
{
    [Fact]
    public void WillrIndicator_Constructor_SetsDefaults()
    {
        var indicator = new WillrIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Contains("WILLR", indicator.Name, StringComparison.Ordinal);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void WillrIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new WillrIndicator { Period = 14 };

        Assert.Equal(0, WillrIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void WillrIndicator_ShortName_IncludesParameters()
    {
        var indicator = new WillrIndicator { Period = 14 };
        indicator.Initialize();

        Assert.Contains("WILLR", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void WillrIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new WillrIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Willr", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void WillrIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new WillrIndicator { Period = 14 };

        indicator.Initialize();

        // After init, line series should exist (WillR + overbought + oversold)
        Assert.Equal(3, indicator.LinesSeries.Count);
    }

    [Fact]
    public void WillrIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new WillrIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double willr = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(willr));
        Assert.True(willr >= -100.0 && willr <= 0.0);
    }

    [Fact]
    public void WillrIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new WillrIndicator { Period = 5 };
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

        double willr = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(willr));
        Assert.True(willr >= -100.0 && willr <= 0.0);
    }

    [Fact]
    public void WillrIndicator_ReferenceLines_AreSet()
    {
        var indicator = new WillrIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Overbought reference line at -20
        double overbought = indicator.LinesSeries[1].GetValue(0);
        Assert.Equal(-20.0, overbought, 1e-10);

        // Oversold reference line at -80
        double oversold = indicator.LinesSeries[2].GetValue(0);
        Assert.Equal(-80.0, oversold, 1e-10);
    }

    [Fact]
    public void WillrIndicator_CustomPeriod_IsUsed()
    {
        var indicator = new WillrIndicator { Period = 7 };
        indicator.Initialize();

        Assert.Contains("7", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void WillrIndicator_Description_IsSet()
    {
        var indicator = new WillrIndicator();

        Assert.NotNull(indicator.Description);
        Assert.NotEmpty(indicator.Description);
        Assert.Contains("Williams", indicator.Description, StringComparison.OrdinalIgnoreCase);
    }
}
