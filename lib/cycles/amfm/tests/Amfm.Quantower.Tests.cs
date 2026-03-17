using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Quantower.Tests;

public class AmfmIndicatorTests
{
    [Fact]
    public void AmfmIndicator_Constructor_SetsDefaults()
    {
        var indicator = new AmfmIndicator();

        Assert.Equal(30, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("AMFM - Ehlers AM Detector / FM Demodulator", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void AmfmIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new AmfmIndicator();

        Assert.Equal(0, AmfmIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void AmfmIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new AmfmIndicator { Period = 20 };

        Assert.True(indicator.ShortName.Contains("AMFM", StringComparison.Ordinal));
        Assert.True(indicator.ShortName.Contains("20", StringComparison.Ordinal));
    }

    [Fact]
    public void AmfmIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new AmfmIndicator { Period = 30 };
        indicator.Initialize();

        // Should have 2 line series: AM and FM
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void AmfmIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new AmfmIndicator { Period = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void AmfmIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new AmfmIndicator { Period = 10 };
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
    public void AmfmIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new AmfmIndicator { Period = 10 };
        indicator.Initialize();

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.NotNull(indicator);
    }

    [Fact]
    public void AmfmIndicator_MultipleUpdates_ProducesFiniteValues()
    {
        var indicator = new AmfmIndicator { Period = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 105, 103, 107, 110 };

        foreach (var close in closes)
        {
            indicator.HistoricalData.AddBar(now, close - 1, close + 2, close - 2, close);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        for (int i = 0; i < closes.Length; i++)
        {
            int idx = closes.Length - 1 - i;
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(idx)));
            Assert.True(double.IsFinite(indicator.LinesSeries[1].GetValue(idx)));
        }
    }

    [Fact]
    public void AmfmIndicator_DualOutput_BothSeriesPopulated()
    {
        var indicator = new AmfmIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now, 100 + i, 105 + i, 95 + i, 102 + i);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        // Both AM and FM line series should have values
        Assert.Equal(20, indicator.LinesSeries[0].Count);
        Assert.Equal(20, indicator.LinesSeries[1].Count);
    }

    [Fact]
    public void AmfmIndicator_Period_CanBeChanged()
    {
        var indicator = new AmfmIndicator { Period = 30 };

        Assert.Equal(30, indicator.Period);

        indicator.Period = 50;
        Assert.Equal(50, indicator.Period);
    }
}
