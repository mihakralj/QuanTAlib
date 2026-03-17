using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class DmhIndicatorTests
{
    [Fact]
    public void DmhIndicator_Constructor_SetsDefaults()
    {
        var indicator = new DmhIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("DMH - Ehlers Directional Movement with Hann", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void DmhIndicator_MinHistoryDepths_IsZero()
    {
        var indicator = new DmhIndicator { Period = 20 };

        Assert.Equal(0, DmhIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void DmhIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new DmhIndicator { Period = 20 };
        indicator.Initialize();

        Assert.Contains("DMH", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void DmhIndicator_Name_ContainsEhlers()
    {
        var indicator = new DmhIndicator();
        Assert.Contains("Ehlers", indicator.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void DmhIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new DmhIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Dmh.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void DmhIndicator_Initialize_CreatesInternalDmh()
    {
        var indicator = new DmhIndicator { Period = 14 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void DmhIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new DmhIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
        }

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void DmhIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new DmhIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        indicator.HistoricalData.AddBar(now.AddMinutes(10), 110, 120, 100, 115);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void DmhIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new DmhIndicator { Period = 5 };
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
    public void DmhIndicator_Parameters_CanBeChanged()
    {
        var indicator = new DmhIndicator { Period = 14 };
        Assert.Equal(14, indicator.Period);

        indicator.Period = 20;

        Assert.Equal(20, indicator.Period);
        Assert.Equal(0, DmhIndicator.MinHistoryDepths);
    }
}
