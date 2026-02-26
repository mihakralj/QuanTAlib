using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class CrsiIndicatorTests
{
    [Fact]
    public void CrsiIndicator_Constructor_SetsDefaults()
    {
        var indicator = new CrsiIndicator();

        Assert.Equal(3, indicator.RsiPeriod);
        Assert.Equal(2, indicator.StreakPeriod);
        Assert.Equal(100, indicator.RankPeriod);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("CRSI - Connors RSI", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void CrsiIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new CrsiIndicator { RsiPeriod = 3 };

        Assert.Equal(0, CrsiIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void CrsiIndicator_ShortName_IncludesParameters()
    {
        var indicator = new CrsiIndicator { RsiPeriod = 5, StreakPeriod = 3, RankPeriod = 50 };
        indicator.Initialize();

        Assert.Contains("CRSI", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("5", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("3", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("50", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void CrsiIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new CrsiIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Crsi.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void CrsiIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new CrsiIndicator { RsiPeriod = 3, StreakPeriod = 2, RankPeriod = 10 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void CrsiIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new CrsiIndicator { RsiPeriod = 3, StreakPeriod = 2, RankPeriod = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double value = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(value));
        Assert.True(value >= 0.0 && value <= 100.0);
    }

    [Fact]
    public void CrsiIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new CrsiIndicator { RsiPeriod = 3, StreakPeriod = 2, RankPeriod = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        indicator.HistoricalData.AddBar(now.AddMinutes(20), 120, 130, 110, 125);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void CrsiIndicator_Parameters_CanBeChanged()
    {
        var indicator = new CrsiIndicator();

        indicator.RsiPeriod = 5;
        indicator.StreakPeriod = 3;
        indicator.RankPeriod = 50;
        indicator.Source = SourceType.Open;

        Assert.Equal(5, indicator.RsiPeriod);
        Assert.Equal(3, indicator.StreakPeriod);
        Assert.Equal(50, indicator.RankPeriod);
        Assert.Equal(SourceType.Open, indicator.Source);
        Assert.Equal(0, CrsiIndicator.MinHistoryDepths);
    }

    [Fact]
    public void CrsiIndicator_DifferentSources_Work()
    {
        foreach (var source in new[] { SourceType.Close, SourceType.Open, SourceType.High, SourceType.Low })
        {
            var indicator = new CrsiIndicator { RsiPeriod = 3, StreakPeriod = 2, RankPeriod = 5, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 15; i++)
            {
                indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double value = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(value));
        }
    }
}
