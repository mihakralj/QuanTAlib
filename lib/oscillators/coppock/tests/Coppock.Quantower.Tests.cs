using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class CoppockIndicatorTests
{
    [Fact]
    public void CoppockIndicator_Constructor_SetsDefaults()
    {
        var indicator = new CoppockIndicator();

        Assert.Equal(14, indicator.LongRoc);
        Assert.Equal(11, indicator.ShortRoc);
        Assert.Equal(10, indicator.WmaPeriod);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("COPPOCK - Coppock Curve", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void CoppockIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new CoppockIndicator();

        Assert.Equal(0, CoppockIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void CoppockIndicator_ShortName_IncludesParameters()
    {
        var indicator = new CoppockIndicator { LongRoc = 14, ShortRoc = 11, WmaPeriod = 10 };
        indicator.Initialize();

        Assert.Contains("COPPOCK", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void CoppockIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new CoppockIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Coppock", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void CoppockIndicator_Initialize_CreatesOneSeries()
    {
        var indicator = new CoppockIndicator { LongRoc = 5, ShortRoc = 4, WmaPeriod = 4 };
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void CoppockIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new CoppockIndicator { LongRoc = 5, ShortRoc = 4, WmaPeriod = 4 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void CoppockIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new CoppockIndicator { LongRoc = 5, ShortRoc = 4, WmaPeriod = 4 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 15; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        indicator.HistoricalData.AddBar(now.AddMinutes(15), 115, 125, 105, 120);
        var newArgs = new UpdateArgs(UpdateReason.NewBar);
        indicator.ProcessUpdate(newArgs);

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void CoppockIndicator_DifferentSourceTypes_ProcessCorrectly()
    {
        foreach (var sourceType in new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close })
        {
            var indicator = new CoppockIndicator
            {
                LongRoc = 5,
                ShortRoc = 4,
                WmaPeriod = 4,
                Source = sourceType
            };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 25; i++)
            {
                indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i * 0.5, 110 + i * 0.5, 90 + i * 0.5, 105 + i * 0.5);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
        }
    }
}
