using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public sealed class BbsIndicatorTests
{
    [Fact]
    public void BbsIndicator_Constructor_SetsDefaults()
    {
        var indicator = new BbsIndicator();

        Assert.Equal(20, indicator.BbPeriod);
        Assert.Equal(2.0, indicator.BbMult);
        Assert.Equal(20, indicator.KcPeriod);
        Assert.Equal(1.5, indicator.KcMult);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("BBS - Bollinger Band Squeeze", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void BbsIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new BbsIndicator { BbPeriod = 20 };

        Assert.Equal(0, BbsIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void BbsIndicator_ShortName_IncludesParameters()
    {
        var indicator = new BbsIndicator
        {
            BbPeriod = 15,
            BbMult = 1.5,
            KcPeriod = 10,
            KcMult = 2.0
        };
        indicator.Initialize();

        Assert.Contains("BBS", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("15", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void BbsIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new BbsIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Bbs.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void BbsIndicator_Initialize_CreatesInternalBbs()
    {
        var indicator = new BbsIndicator
        {
            BbPeriod = 20,
            KcPeriod = 20
        };

        indicator.Initialize();

        // Should have bandwidth + squeeze dot series
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void BbsIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new BbsIndicator
        {
            BbPeriod = 5,
            KcPeriod = 5
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double bandwidth = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(bandwidth));
    }

    [Fact]
    public void BbsIndicator_TwoLineSeries_Exist()
    {
        var indicator = new BbsIndicator();
        indicator.Initialize();

        // Should have bandwidth + squeeze dot series
        Assert.Equal(2, indicator.LinesSeries.Count);
    }
}
