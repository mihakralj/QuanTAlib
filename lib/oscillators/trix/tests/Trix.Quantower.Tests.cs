using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class TrixIndicatorTests
{
    [Fact]
    public void TrixIndicator_Constructor_SetsDefaults()
    {
        var indicator = new TrixIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("TRIX - Triple Exponential Average Oscillator", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void TrixIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new TrixIndicator { Period = 14 };

        Assert.Equal(0, TrixIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void TrixIndicator_ShortName_IncludesParameters()
    {
        var indicator = new TrixIndicator { Period = 10 };
        indicator.Initialize();

        Assert.Contains("TRIX", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void TrixIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new TrixIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Trix.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void TrixIndicator_Initialize_CreatesInternalTrix()
    {
        var indicator = new TrixIndicator { Period = 10 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void TrixIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new TrixIndicator { Period = 5 };
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
    public void TrixIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new TrixIndicator { Period = 5 };
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
    public void TrixIndicator_Parameters_CanBeChanged()
    {
        var indicator = new TrixIndicator { Period = 14 };

        indicator.Period = 10;
        indicator.Source = SourceType.Open;

        Assert.Equal(10, indicator.Period);
        Assert.Equal(SourceType.Open, indicator.Source);
        Assert.Equal(0, TrixIndicator.MinHistoryDepths);
    }

    [Fact]
    public void TrixIndicator_ProcessUpdate_DifferentSources()
    {
        var indicator = new TrixIndicator { Period = 5, Source = SourceType.High };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double value = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(value));
    }
}
