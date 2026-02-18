using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class RoofingIndicatorTests
{
    [Fact]
    public void RoofingIndicator_Constructor_SetsDefaults()
    {
        var indicator = new RoofingIndicator();

        Assert.Equal(48, indicator.HpLength);
        Assert.Equal(10, indicator.SsLength);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("ROOFING - Ehlers Roofing Filter", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void RoofingIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new RoofingIndicator { HpLength = 48, SsLength = 10 };

        Assert.Equal(0, RoofingIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void RoofingIndicator_ShortName_IncludesParameters()
    {
        var indicator = new RoofingIndicator { HpLength = 48, SsLength = 10 };

        Assert.Contains("ROOFING", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("48", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void RoofingIndicator_Initialize_CreatesInternalRoofing()
    {
        var indicator = new RoofingIndicator { HpLength = 48, SsLength = 10 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void RoofingIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new RoofingIndicator { HpLength = 48, SsLength = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void RoofingIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new RoofingIndicator { HpLength = 48, SsLength = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void RoofingIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new RoofingIndicator { HpLength = 48, SsLength = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        double firstValue = indicator.LinesSeries[0].GetValue(0);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        double secondValue = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(firstValue));
        Assert.True(double.IsFinite(secondValue));
    }

    [Fact]
    public void RoofingIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new RoofingIndicator { HpLength = 48, SsLength = 10, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void RoofingIndicator_Parameters_CanBeChanged()
    {
        var indicator = new RoofingIndicator { HpLength = 48, SsLength = 10 };
        Assert.Equal(48, indicator.HpLength);
        Assert.Equal(10, indicator.SsLength);

        indicator.HpLength = 80;
        indicator.SsLength = 20;
        Assert.Equal(80, indicator.HpLength);
        Assert.Equal(20, indicator.SsLength);
    }
}
