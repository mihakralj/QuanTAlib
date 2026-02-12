using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class InertiaIndicatorTests
{
    [Fact]
    public void InertiaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new InertiaIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("INERTIA - Inertia Oscillator", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void InertiaIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new InertiaIndicator { Period = 20 };

        Assert.Equal(0, InertiaIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void InertiaIndicator_ShortName_IncludesParameters()
    {
        var indicator = new InertiaIndicator { Period = 14 };
        indicator.Initialize();

        Assert.Contains("INERTIA", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void InertiaIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new InertiaIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Inertia.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void InertiaIndicator_Initialize_CreatesInternalInertia()
    {
        var indicator = new InertiaIndicator { Period = 10 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void InertiaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new InertiaIndicator { Period = 5 };
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
    public void InertiaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new InertiaIndicator { Period = 5 };
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
    public void InertiaIndicator_Parameters_CanBeChanged()
    {
        var indicator = new InertiaIndicator { Period = 20 };

        indicator.Period = 14;
        indicator.Source = SourceType.Open;

        Assert.Equal(14, indicator.Period);
        Assert.Equal(SourceType.Open, indicator.Source);
        Assert.Equal(0, InertiaIndicator.MinHistoryDepths);
    }
}
