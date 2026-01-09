using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class BpfIndicatorTests
{
    [Fact]
    public void BpfIndicator_Constructor_SetsDefaults()
    {
        var indicator = new BpfIndicator();

        Assert.Equal(40, indicator.LowerPeriod);
        Assert.Equal(10, indicator.UpperPeriod);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("BPF - Bandpass Filter", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void BpfIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new BpfIndicator { LowerPeriod = 20, UpperPeriod = 5 };

        Assert.Equal(0, BpfIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void BpfIndicator_ShortName_IncludesParameters()
    {
        var indicator = new BpfIndicator { LowerPeriod = 40, UpperPeriod = 10 };

        Assert.Contains("BPF", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("40", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void BpfIndicator_Initialize_CreatesInternalBpf()
    {
        var indicator = new BpfIndicator { LowerPeriod = 40, UpperPeriod = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void BpfIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new BpfIndicator { LowerPeriod = 40, UpperPeriod = 10 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        // Process update
        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // Line series should have a value
        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void BpfIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new BpfIndicator { LowerPeriod = 40, UpperPeriod = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void BpfIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new BpfIndicator { LowerPeriod = 40, UpperPeriod = 10 };
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
    public void BpfIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new BpfIndicator { LowerPeriod = 40, UpperPeriod = 10, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void BpfIndicator_Periods_CanBeChanged()
    {
        var indicator = new BpfIndicator { LowerPeriod = 40, UpperPeriod = 10 };
        Assert.Equal(40, indicator.LowerPeriod);
        Assert.Equal(10, indicator.UpperPeriod);

        indicator.LowerPeriod = 60;
        indicator.UpperPeriod = 20;
        Assert.Equal(60, indicator.LowerPeriod);
        Assert.Equal(20, indicator.UpperPeriod);
    }
}