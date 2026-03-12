using TradingPlatform.BusinessLayer;
using Xunit;

namespace QuanTAlib.Tests;

public class PchannelIndicatorTests
{
    [Fact]
    public void Constructor_SetsDefaults()
    {
        var ind = new PchannelIndicator();

        Assert.Equal(20, ind.Period);
        Assert.True(ind.ShowColdValues);
        Assert.Equal("Pchannel - Price Channel", ind.Name);
        Assert.False(ind.SeparateWindow);
        Assert.True(ind.OnBackGround);
    }

    [Fact]
    public void MinHistoryDepths_EqualsPeriod()
    {
        var ind = new PchannelIndicator { Period = 15 };
        Assert.Equal(15, ind.MinHistoryDepths);
    }

    [Fact]
    public void ShortName_ReflectsParameters()
    {
        var ind = new PchannelIndicator { Period = 12 };
        Assert.Contains("12", ind.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void Initialize_AddsThreeLineSeries()
    {
        var ind = new PchannelIndicator { Period = 14 };
        ind.Initialize();

        Assert.Equal(3, ind.LinesSeries.Count);
        Assert.Equal("Middle", ind.LinesSeries[0].Name);
        Assert.Equal("Upper", ind.LinesSeries[1].Name);
        Assert.Equal("Lower", ind.LinesSeries[2].Name);
    }

    [Fact]
    public void ProcessUpdate_Historical_ComputesValues()
    {
        var ind = new PchannelIndicator { Period = 3 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        ind.HistoricalData.AddBar(now, 100, 110, 90, 102);

        ind.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.Equal(1, ind.LinesSeries[0].Count);
        Assert.True(double.IsFinite(ind.LinesSeries[0].GetValue(0)));
        Assert.True(double.IsFinite(ind.LinesSeries[1].GetValue(0)));
        Assert.True(double.IsFinite(ind.LinesSeries[2].GetValue(0)));
    }

    [Fact]
    public void ProcessUpdate_NewBar_Appends()
    {
        var ind = new PchannelIndicator { Period = 3 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        ind.HistoricalData.AddBar(now, 100, 110, 90, 102);
        ind.HistoricalData.AddBar(now.AddMinutes(1), 102, 112, 92, 104);

        ind.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        ind.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, ind.LinesSeries[0].Count);
    }

    [Fact]
    public void ProcessUpdate_NewTick_DoesNotThrow()
    {
        var ind = new PchannelIndicator { Period = 5 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        ind.HistoricalData.AddBar(now, 100, 105, 95, 102);

        ind.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        ind.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.Equal(2, ind.LinesSeries[0].Count);
    }

    [Fact]
    public void MultipleUpdates_ProducesFiniteSeries()
    {
        var ind = new PchannelIndicator { Period = 5 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            ind.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i);
            ind.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        Assert.Equal(10, ind.LinesSeries[0].Count);
        Assert.Equal(10, ind.LinesSeries[1].Count);
        Assert.Equal(10, ind.LinesSeries[2].Count);

        for (int i = 0; i < 10; i++)
        {
            Assert.True(double.IsFinite(ind.LinesSeries[0].GetValue(i)));
            Assert.True(double.IsFinite(ind.LinesSeries[1].GetValue(i)));
            Assert.True(double.IsFinite(ind.LinesSeries[2].GetValue(i)));
        }
    }

    [Fact]
    public void Bands_Order_Correct()
    {
        var ind = new PchannelIndicator { Period = 3 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 6; i++)
        {
            ind.HistoricalData.AddBar(now.AddMinutes(i), 100, 110 + i, 90 - i, 100, 1000);
            ind.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        double middle = ind.LinesSeries[0].GetValue(0);
        double upper = ind.LinesSeries[1].GetValue(0);
        double lower = ind.LinesSeries[2].GetValue(0);

        Assert.True(upper >= middle, $"Upper ({upper}) should be >= Middle ({middle})");
        Assert.True(lower <= middle, $"Lower ({lower}) should be <= Middle ({middle})");
    }
}
