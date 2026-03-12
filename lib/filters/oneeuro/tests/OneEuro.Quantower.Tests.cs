using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class OneEuroIndicatorTests
{
    [Fact]
    public void OneEuroIndicator_Constructor_SetsDefaults()
    {
        var indicator = new OneEuroIndicator();

        Assert.Equal(1.0, indicator.MinCutoff);
        Assert.Equal(0.007, indicator.Beta);
        Assert.Equal(1.0, indicator.DCutoff);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("ONEEURO - One Euro Filter", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void OneEuroIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new OneEuroIndicator();

        Assert.Equal(0, OneEuroIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void OneEuroIndicator_ShortName_IncludesParameters()
    {
        var indicator = new OneEuroIndicator { MinCutoff = 1.0, Beta = 0.007 };

        Assert.Contains("1€", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("1", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void OneEuroIndicator_Initialize_CreatesInternalFilter()
    {
        var indicator = new OneEuroIndicator();

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void OneEuroIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new OneEuroIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void OneEuroIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new OneEuroIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void OneEuroIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new OneEuroIndicator();
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
    public void OneEuroIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new OneEuroIndicator { Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void OneEuroIndicator_Parameters_CanBeChanged()
    {
        var indicator = new OneEuroIndicator();
        Assert.Equal(1.0, indicator.MinCutoff);
        Assert.Equal(0.007, indicator.Beta);
        Assert.Equal(1.0, indicator.DCutoff);

        indicator.MinCutoff = 2.0;
        indicator.Beta = 0.01;
        indicator.DCutoff = 0.5;
        Assert.Equal(2.0, indicator.MinCutoff);
        Assert.Equal(0.01, indicator.Beta);
        Assert.Equal(0.5, indicator.DCutoff);
    }
}
