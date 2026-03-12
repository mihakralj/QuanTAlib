using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class TtmSqueezeIndicatorTests
{
    [Fact]
    public void TtmSqueezeIndicator_Constructor_SetsDefaults()
    {
        var indicator = new TtmSqueezeIndicator();

        Assert.Equal(20, indicator.BbPeriod);
        Assert.Equal(2.0, indicator.BbMult);
        Assert.Equal(20, indicator.KcPeriod);
        Assert.Equal(1.5, indicator.KcMult);
        Assert.Equal(20, indicator.MomPeriod);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("TTM Squeeze", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void TtmSqueezeIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new TtmSqueezeIndicator { BbPeriod = 20 };

        Assert.Equal(0, TtmSqueezeIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void TtmSqueezeIndicator_ShortName_IncludesParameters()
    {
        var indicator = new TtmSqueezeIndicator
        {
            BbPeriod = 15,
            BbMult = 1.5,
            KcPeriod = 10,
            KcMult = 2.0,
            MomPeriod = 25
        };
        indicator.Initialize();

        Assert.Contains("TTM_SQZ", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("15", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("25", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void TtmSqueezeIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new TtmSqueezeIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("TtmSqueeze.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void TtmSqueezeIndicator_Initialize_CreatesInternalSqueeze()
    {
        var indicator = new TtmSqueezeIndicator
        {
            BbPeriod = 14,
            KcPeriod = 14,
            MomPeriod = 14
        };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (momentum + squeeze)
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void TtmSqueezeIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new TtmSqueezeIndicator
        {
            BbPeriod = 5,
            KcPeriod = 5,
            MomPeriod = 5
        };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double momentum = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(momentum));
    }

    [Fact]
    public void TtmSqueezeIndicator_TwoLineSeries_Exist()
    {
        var indicator = new TtmSqueezeIndicator();
        indicator.Initialize();

        // Should have momentum + squeeze dot series
        Assert.Equal(2, indicator.LinesSeries.Count);
    }
}
