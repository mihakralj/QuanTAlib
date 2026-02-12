using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class DpoIndicatorTests
{
    [Fact]
    public void DpoIndicator_Constructor_SetsDefaults()
    {
        var indicator = new DpoIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("DPO - Detrended Price Oscillator", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void DpoIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new DpoIndicator { Period = 20 };

        Assert.Equal(0, DpoIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void DpoIndicator_ShortName_IncludesParameters()
    {
        var indicator = new DpoIndicator { Period = 10 };
        indicator.Initialize();

        Assert.Contains("DPO", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void DpoIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new DpoIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Dpo.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void DpoIndicator_Initialize_CreatesInternalDpo()
    {
        var indicator = new DpoIndicator { Period = 10 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void DpoIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new DpoIndicator { Period = 5 };
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
    public void DpoIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new DpoIndicator { Period = 5 };
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
    public void DpoIndicator_Parameters_CanBeChanged()
    {
        var indicator = new DpoIndicator { Period = 20 };

        indicator.Period = 10;
        indicator.Source = SourceType.Open;

        Assert.Equal(10, indicator.Period);
        Assert.Equal(SourceType.Open, indicator.Source);
        Assert.Equal(0, DpoIndicator.MinHistoryDepths);
    }
}
