using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class VossIndicatorTests
{
    [Fact]
    public void VossIndicator_Constructor_SetsDefaults()
    {
        var indicator = new VossIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(3, indicator.Predict);
        Assert.Equal(0.25, indicator.Bandwidth);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("VOSS - Ehlers Voss Predictive Filter", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void VossIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new VossIndicator { Period = 20, Predict = 3 };

        Assert.Equal(0, VossIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void VossIndicator_ShortName_IncludesParameters()
    {
        var indicator = new VossIndicator { Period = 20, Predict = 3, Bandwidth = 0.25 };

        Assert.Contains("VOSS", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("3", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void VossIndicator_Initialize_CreatesInternalVoss()
    {
        var indicator = new VossIndicator { Period = 20, Predict = 3, Bandwidth = 0.25 };

        indicator.Initialize();

        // Voss has two line series: Voss predictor + Bandpass (Filt)
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void VossIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new VossIndicator { Period = 20, Predict = 3, Bandwidth = 0.25 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
        Assert.Equal(1, indicator.LinesSeries[1].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[1].GetValue(0)));
    }

    [Fact]
    public void VossIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new VossIndicator { Period = 20, Predict = 3, Bandwidth = 0.25 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
        Assert.Equal(2, indicator.LinesSeries[1].Count);
    }

    [Fact]
    public void VossIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new VossIndicator { Period = 20, Predict = 3, Bandwidth = 0.25 };
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
    public void VossIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new VossIndicator { Period = 20, Predict = 3, Bandwidth = 0.25, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite Voss value");
            Assert.True(double.IsFinite(indicator.LinesSeries[1].GetValue(0)),
                $"Source {source} should produce finite Filt value");
        }
    }

    [Fact]
    public void VossIndicator_Parameters_CanBeChanged()
    {
        var indicator = new VossIndicator { Period = 20, Predict = 3, Bandwidth = 0.25 };
        Assert.Equal(20, indicator.Period);
        Assert.Equal(3, indicator.Predict);
        Assert.Equal(0.25, indicator.Bandwidth);

        indicator.Period = 40;
        indicator.Predict = 5;
        indicator.Bandwidth = 0.15;
        Assert.Equal(40, indicator.Period);
        Assert.Equal(5, indicator.Predict);
        Assert.Equal(0.15, indicator.Bandwidth);
    }
}
