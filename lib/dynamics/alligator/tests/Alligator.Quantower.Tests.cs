using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class AlligatorIndicatorTests
{
    [Fact]
    public void AlligatorIndicator_Constructor_SetsDefaults()
    {
        var indicator = new AlligatorIndicator();

        Assert.Equal(13, indicator.JawPeriod);
        Assert.Equal(8, indicator.JawOffset);
        Assert.Equal(8, indicator.TeethPeriod);
        Assert.Equal(5, indicator.TeethOffset);
        Assert.Equal(5, indicator.LipsPeriod);
        Assert.Equal(3, indicator.LipsOffset);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Alligator", indicator.Name);
        Assert.False(indicator.SeparateWindow); // Overlay on price chart
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void AlligatorIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new AlligatorIndicator { JawPeriod = 20 };

        Assert.Equal(0, AlligatorIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void AlligatorIndicator_ShortName_IncludesParameters()
    {
        var indicator = new AlligatorIndicator { JawPeriod = 13, TeethPeriod = 8, LipsPeriod = 5 };
        indicator.Initialize();

        Assert.Contains("Alligator", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("13", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("8", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("5", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void AlligatorIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new AlligatorIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Alligator.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void AlligatorIndicator_Initialize_CreatesInternalAlligator()
    {
        var indicator = new AlligatorIndicator { JawPeriod = 13, TeethPeriod = 8, LipsPeriod = 5 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (Jaw, Teeth, Lips)
        Assert.Equal(3, indicator.LinesSeries.Count);
    }

    [Fact]
    public void AlligatorIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new AlligatorIndicator { JawPeriod = 13, TeethPeriod = 8, LipsPeriod = 5 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        // Need enough bars for longest period (Jaw = 13)
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double jaw = indicator.LinesSeries[0].GetValue(0);
        double teeth = indicator.LinesSeries[1].GetValue(0);
        double lips = indicator.LinesSeries[2].GetValue(0);

        Assert.True(double.IsFinite(jaw));
        Assert.True(double.IsFinite(teeth));
        Assert.True(double.IsFinite(lips));
    }

    [Fact]
    public void AlligatorIndicator_ThreeLineSeries_HaveCorrectNames()
    {
        var indicator = new AlligatorIndicator();
        indicator.Initialize();

        Assert.Equal(3, indicator.LinesSeries.Count);
        Assert.Equal("Jaw", indicator.LinesSeries[0].Name);
        Assert.Equal("Teeth", indicator.LinesSeries[1].Name);
        Assert.Equal("Lips", indicator.LinesSeries[2].Name);
    }
}
