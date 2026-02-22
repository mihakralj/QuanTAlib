using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class AgcIndicatorTests
{
    [Fact]
    public void AgcIndicator_Constructor_SetsDefaults()
    {
        var indicator = new AgcIndicator();

        Assert.Equal(0.991, indicator.Decay);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("AGC - Ehlers Automatic Gain Control", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void AgcIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new AgcIndicator { Decay = 0.991 };

        Assert.Equal(0, AgcIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void AgcIndicator_ShortName_IncludesParameters()
    {
        var indicator = new AgcIndicator { Decay = 0.991 };

        Assert.Contains("AGC", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("0.991", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void AgcIndicator_Initialize_CreatesInternalAgc()
    {
        var indicator = new AgcIndicator { Decay = 0.991 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void AgcIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new AgcIndicator { Decay = 0.991 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void AgcIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new AgcIndicator { Decay = 0.991 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void AgcIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new AgcIndicator { Decay = 0.991 };
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
    public void AgcIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new AgcIndicator { Decay = 0.991, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void AgcIndicator_Parameters_CanBeChanged()
    {
        var indicator = new AgcIndicator { Decay = 0.991 };
        Assert.Equal(0.991, indicator.Decay);

        indicator.Decay = 0.95;
        Assert.Equal(0.95, indicator.Decay);
    }
}
