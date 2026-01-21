using TradingPlatform.BusinessLayer;
using Xunit;

namespace QuanTAlib.Tests;

public class MmchannelIndicatorTests
{
    [Fact]
    public void Constructor_SetsDefaults()
    {
        var ind = new MmchannelIndicator();

        Assert.Equal(20, ind.Period);
        Assert.True(ind.ShowColdValues);
        Assert.Equal("Mmchannel - Min-Max Channel", ind.Name);
        Assert.False(ind.SeparateWindow);
        Assert.True(ind.OnBackGround);
    }

    [Fact]
    public void MinHistoryDepths_EqualsPeriod()
    {
        var ind = new MmchannelIndicator { Period = 15 };
        Assert.Equal(15, ind.MinHistoryDepths);
    }

    [Fact]
    public void ShortName_ReflectsParameters()
    {
        var ind = new MmchannelIndicator { Period = 12 };
        Assert.Contains("12", ind.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void Initialize_AddsTwoLineSeries()
    {
        var ind = new MmchannelIndicator { Period = 14 };
        ind.Initialize();

        Assert.Equal(2, ind.LinesSeries.Count);
        Assert.Equal("Upper", ind.LinesSeries[0].Name);
        Assert.Equal("Lower", ind.LinesSeries[1].Name);
    }

    [Fact]
    public void ProcessUpdate_Historical_ComputesValues()
    {
        var ind = new MmchannelIndicator { Period = 3 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        ind.HistoricalData.AddBar(now, 100, 110, 90, 102);

        ind.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.Equal(1, ind.LinesSeries[0].Count);
        Assert.True(double.IsFinite(ind.LinesSeries[0].GetValue(0)));
        Assert.True(double.IsFinite(ind.LinesSeries[1].GetValue(0)));
    }

    [Fact]
    public void ProcessUpdate_NewBar_Appends()
    {
        var ind = new MmchannelIndicator { Period = 3 };
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
        var ind = new MmchannelIndicator { Period = 5 };
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
        var ind = new MmchannelIndicator { Period = 5 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            ind.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i);
            ind.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        Assert.Equal(10, ind.LinesSeries[0].Count);
        Assert.Equal(10, ind.LinesSeries[1].Count);

        for (int i = 0; i < 10; i++)
        {
            Assert.True(double.IsFinite(ind.LinesSeries[0].GetValue(i)));
            Assert.True(double.IsFinite(ind.LinesSeries[1].GetValue(i)));
        }
    }

    [Fact]
    public void Bands_Order_Correct()
    {
        var ind = new MmchannelIndicator { Period = 3 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 6; i++)
        {
            ind.HistoricalData.AddBar(now.AddMinutes(i), 100, 110 + i, 90 - i, 100, 1000);
            ind.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        double upper = ind.LinesSeries[0].GetValue(0);
        double lower = ind.LinesSeries[1].GetValue(0);

        Assert.True(upper >= lower, $"Upper ({upper}) should be >= Lower ({lower})");
    }

    [Fact]
    public void Bands_TrackExtremes()
    {
        var ind = new MmchannelIndicator { Period = 3 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        // Bar 0: H=110, L=90
        ind.HistoricalData.AddBar(now, 100, 110, 90, 100, 1000);
        ind.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Bar 1: H=115, L=95 (new high)
        ind.HistoricalData.AddBar(now.AddMinutes(1), 100, 115, 95, 100, 1000);
        ind.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        // Bar 2: H=105, L=85 (new low)
        ind.HistoricalData.AddBar(now.AddMinutes(2), 100, 105, 85, 100, 1000);
        ind.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double upper = ind.LinesSeries[0].GetValue(0);
        double lower = ind.LinesSeries[1].GetValue(0);

        // Upper should be max(110, 115, 105) = 115
        // Lower should be min(90, 95, 85) = 85
        Assert.Equal(115, upper, 1e-10);
        Assert.Equal(85, lower, 1e-10);
    }
}
