using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class BbiIndicatorTests
{
    [Fact]
    public void BbiIndicator_Constructor_SetsDefaults()
    {
        var indicator = new BbiIndicator();

        Assert.Equal(3, indicator.Period1);
        Assert.Equal(6, indicator.Period2);
        Assert.Equal(12, indicator.Period3);
        Assert.Equal(24, indicator.Period4);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("BBI - Bulls Bears Index", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void BbiIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new BbiIndicator();

        Assert.Equal(0, BbiIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void BbiIndicator_ShortName_IncludesParameters()
    {
        var indicator = new BbiIndicator { Period1 = 3, Period2 = 6, Period3 = 12, Period4 = 24 };
        indicator.Initialize();

        Assert.Contains("BBI", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("3", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("24", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void BbiIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new BbiIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Bbi", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void BbiIndicator_Initialize_CreatesOneLineSeries()
    {
        var indicator = new BbiIndicator();
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void BbiIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new BbiIndicator { Period1 = 3, Period2 = 6, Period3 = 12, Period4 = 24 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double bbi = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(bbi));
    }

    [Fact]
    public void BbiIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new BbiIndicator { Period1 = 3, Period2 = 6, Period3 = 12, Period4 = 24 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 25; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        indicator.HistoricalData.AddBar(now.AddMinutes(25), 125, 135, 115, 130);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double bbi = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(bbi));
    }

    [Fact]
    public void BbiIndicator_DifferentSourceTypes_ProcessCorrectly()
    {
        foreach (var sourceType in new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close })
        {
            var indicator = new BbiIndicator
            {
                Period1 = 3,
                Period2 = 6,
                Period3 = 12,
                Period4 = 24,
                Source = sourceType
            };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 30; i++)
            {
                indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i * 0.5, 110 + i * 0.5, 90 + i * 0.5, 105 + i * 0.5);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
        }
    }

    [Fact]
    public void BbiIndicator_CustomPeriods_SetsNameCorrectly()
    {
        var indicator = new BbiIndicator { Period1 = 5, Period2 = 10, Period3 = 20, Period4 = 40 };
        indicator.Initialize();

        Assert.Contains("5", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("40", indicator.ShortName, StringComparison.Ordinal);
    }
}
