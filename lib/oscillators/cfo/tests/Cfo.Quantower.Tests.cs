using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class CfoIndicatorTests
{
    [Fact]
    public void CfoIndicator_Constructor_SetsDefaults()
    {
        var indicator = new CfoIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("CFO - Chande Forecast Oscillator", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void CfoIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new CfoIndicator { Period = 14 };

        Assert.Equal(0, CfoIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void CfoIndicator_ShortName_IncludesParameters()
    {
        var indicator = new CfoIndicator { Period = 20 };
        indicator.Initialize();

        Assert.Contains("CFO", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void CfoIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new CfoIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Cfo.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void CfoIndicator_Initialize_CreatesInternalCfo()
    {
        var indicator = new CfoIndicator { Period = 10 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void CfoIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new CfoIndicator { Period = 5 };
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
    public void CfoIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new CfoIndicator { Period = 5 };
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
    public void CfoIndicator_Parameters_CanBeChanged()
    {
        var indicator = new CfoIndicator { Period = 14 };

        indicator.Period = 20;
        indicator.Source = SourceType.Open;

        Assert.Equal(20, indicator.Period);
        Assert.Equal(SourceType.Open, indicator.Source);
        Assert.Equal(0, CfoIndicator.MinHistoryDepths);
    }
}
