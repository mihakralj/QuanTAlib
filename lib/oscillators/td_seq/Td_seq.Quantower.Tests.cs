using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class TdSeqIndicatorTests
{
    [Fact]
    public void TdSeqIndicator_Constructor_SetsDefaults()
    {
        var indicator = new TdSeqIndicator();

        Assert.Equal(4, indicator.ComparePeriod);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("TD_SEQ - TD Sequential", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void TdSeqIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new TdSeqIndicator { ComparePeriod = 4 };

        Assert.Equal(0, TdSeqIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void TdSeqIndicator_ShortName_IncludesComparePeriod()
    {
        var indicator = new TdSeqIndicator { ComparePeriod = 6 };
        indicator.Initialize();

        Assert.Contains("TD_SEQ", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("6", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void TdSeqIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new TdSeqIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Td_seq.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void TdSeqIndicator_Initialize_CreatesTwoLineSeries()
    {
        var indicator = new TdSeqIndicator { ComparePeriod = 4 };
        indicator.Initialize();

        // Setup line + Countdown line
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void TdSeqIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new TdSeqIndicator { ComparePeriod = 4 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double setupValue    = indicator.LinesSeries[0].GetValue(0);
        double countdownValue = indicator.LinesSeries[1].GetValue(0);

        Assert.True(double.IsFinite(setupValue));
        Assert.True(double.IsFinite(countdownValue));
    }

    [Fact]
    public void TdSeqIndicator_ProcessUpdate_NewBar_UpdatesValue()
    {
        var indicator = new TdSeqIndicator { ComparePeriod = 4 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        indicator.HistoricalData.AddBar(now.AddMinutes(20), 120, 130, 110, 125);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.True(indicator.LinesSeries[0].Count >= 2);
    }

    [Fact]
    public void TdSeqIndicator_Parameters_CanBeChanged()
    {
        var indicator = new TdSeqIndicator { ComparePeriod = 4 };

        indicator.ComparePeriod = 6;

        Assert.Equal(6, indicator.ComparePeriod);
        Assert.Equal(0, TdSeqIndicator.MinHistoryDepths);
    }

    [Fact]
    public void TdSeqIndicator_RisingPrices_SetupCountPositive()
    {
        var indicator = new TdSeqIndicator { ComparePeriod = 4 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double p = 100.0 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), p, p + 2, p - 2, p);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // After 9+ qualifying bars, setup line should show a positive value
        double setupValue = indicator.LinesSeries[0].GetValue(0);
        Assert.True(setupValue >= 0, $"Expected non-negative setup for rising prices, got {setupValue}");
    }
}
