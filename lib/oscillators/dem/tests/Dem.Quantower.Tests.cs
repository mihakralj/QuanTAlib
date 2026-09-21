using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class DemIndicatorTests
{
    [Fact]
    public void DemIndicator_Constructor_SetsDefaults()
    {
        var indicator = new DemIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("DEM - DeMarker Oscillator", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void DemIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new DemIndicator { Period = 14 };

        Assert.Equal(0, DemIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void DemIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new DemIndicator { Period = 14 };
        indicator.Initialize();

        Assert.Contains("DEM", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void DemIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new DemIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Dem.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void DemIndicator_Initialize_CreatesOneLineSeries()
    {
        var indicator = new DemIndicator { Period = 14 };
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void DemIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new DemIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double demValue = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(demValue));
    }

    [Fact]
    public void DemIndicator_ProcessUpdate_NewBar_UpdatesValue()
    {
        var indicator = new DemIndicator { Period = 5 };
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
    public void DemIndicator_Parameters_CanBeChanged()
    {
        var indicator = new DemIndicator { Period = 21 };
        indicator.Initialize();

        Assert.Equal(21, indicator.Period);
    }

    [Fact]
    public void DemIndicator_OhlcInput_ComputesFiniteValues()
    {
        var indicator = new DemIndicator { Period = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double basePrice = 100.0 + (i * 0.5);
            indicator.HistoricalData.AddBar(
                now.AddMinutes(i),
                open: basePrice,
                high: basePrice + 3.0,
                low: basePrice - 2.0,
                close: basePrice + 1.0);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }
}
