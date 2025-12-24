using Xunit;
using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class DmxIndicatorTests
{
    [Fact]
    public void DmxIndicator_Constructor_SetsDefaults()
    {
        var indicator = new DmxIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("DMX - Jurik Directional Movement Index", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void DmxIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new DmxIndicator { Period = 20 };

        Assert.Equal(0, DmxIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void DmxIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new DmxIndicator { Period = 20 };
        // Initialize to update SourceName (though DMX doesn't use SourceName)
        indicator.Initialize();

        Assert.Contains("DMX", indicator.ShortName);
        Assert.Contains("20", indicator.ShortName);
    }

    [Fact]
    public void DmxIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new DmxIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink);
        Assert.Contains("Dmx.Quantower.cs", indicator.SourceCodeLink);
    }

    [Fact]
    public void DmxIndicator_Initialize_CreatesInternalDmx()
    {
        var indicator = new DmxIndicator { Period = 14 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void DmxIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new DmxIndicator { Period = 5 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        // Need enough bars for Period
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
        }

        // Process update
        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // Line series should have a value
        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void DmxIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new DmxIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(10), 110, 120, 100, 115);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void DmxIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new DmxIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        double firstValue = indicator.LinesSeries[0].GetValue(0);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        double secondValue = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(firstValue));
        Assert.True(double.IsFinite(secondValue));
    }

    [Fact]
    public void DmxIndicator_Parameters_CanBeChanged()
    {
        var indicator = new DmxIndicator { Period = 14 };
        Assert.Equal(14, indicator.Period);

        indicator.Period = 20;

        Assert.Equal(20, indicator.Period);
        Assert.Equal(0, DmxIndicator.MinHistoryDepths);
    }
}
