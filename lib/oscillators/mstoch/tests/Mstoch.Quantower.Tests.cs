using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class MstochIndicatorTests
{
    [Fact]
    public void MstochIndicator_Constructor_SetsDefaults()
    {
        var indicator = new MstochIndicator();

        Assert.Equal(20, indicator.StochLength);
        Assert.Equal(48, indicator.HpLength);
        Assert.Equal(10, indicator.SsLength);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("MSTOCH - Ehlers MESA Stochastic", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void MstochIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new MstochIndicator();

        Assert.Equal(0, MstochIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void MstochIndicator_ShortName_IncludesParameters()
    {
        var indicator = new MstochIndicator { StochLength = 20, HpLength = 48, SsLength = 10 };
        indicator.Initialize();

        Assert.Contains("MSTOCH", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("48", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void MstochIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new MstochIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Mstoch", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void MstochIndicator_Initialize_CreatesOneLineSeries()
    {
        var indicator = new MstochIndicator { StochLength = 10, HpLength = 20, SsLength = 5 };
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void MstochIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new MstochIndicator { StochLength = 5, HpLength = 10, SsLength = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i * 0.5, 110 + i * 0.5, 90 + i * 0.5, 105 + i * 0.5);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
        Assert.True(val >= 0.0 && val <= 1.0);
    }

    [Fact]
    public void MstochIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new MstochIndicator { StochLength = 5, HpLength = 10, SsLength = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Simulate a new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(20), 120, 130, 110, 125);
        var newArgs = new UpdateArgs(UpdateReason.NewBar);
        indicator.ProcessUpdate(newArgs);

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void MstochIndicator_DifferentSourceTypes_ProcessCorrectly()
    {
        foreach (var sourceType in new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close })
        {
            var indicator = new MstochIndicator
            {
                StochLength = 5,
                HpLength = 10,
                SsLength = 3,
                Source = sourceType
            };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 20; i++)
            {
                indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i * 0.5, 110 + i * 0.5, 90 + i * 0.5, 105 + i * 0.5);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
        }
    }
}
