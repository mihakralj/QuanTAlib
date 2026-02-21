using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class MavpIndicatorTests
{
    [Fact]
    public void MavpIndicator_Constructor_SetsDefaults()
    {
        var indicator = new MavpIndicator();

        Assert.Equal(10, indicator.Period);
        Assert.Equal(2, indicator.MinPeriod);
        Assert.Equal(30, indicator.MaxPeriod);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("MAVP - Moving Average Variable Period", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void MavpIndicator_MinHistoryDepths_IsZero()
    {
        var indicator = new MavpIndicator { Period = 20 };

        Assert.Equal(0, MavpIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void MavpIndicator_ShortName_IncludesPeriodAndSource()
    {
        var indicator = new MavpIndicator { Period = 15 };

        Assert.Contains("MAVP", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("15", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void MavpIndicator_Initialize_CreatesInternalMavp()
    {
        var indicator = new MavpIndicator { Period = 10 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void MavpIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new MavpIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void MavpIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new MavpIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void MavpIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new MavpIndicator { Period = 5 };
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
    public void MavpIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new MavpIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 104, 103, 105, 107, 106 };

        foreach (var close in closes)
        {
            indicator.HistoricalData.AddBar(now, close, close + 2, close - 2, close);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        for (int i = 0; i < closes.Length; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(closes.Length - 1 - i)));
        }

        double lastMavp = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastMavp >= 99 && lastMavp <= 110);
    }

    [Fact]
    public void MavpIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new MavpIndicator { Period = 5, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void MavpIndicator_Period_CanBeChanged()
    {
        var indicator = new MavpIndicator { Period = 5 };
        Assert.Equal(5, indicator.Period);

        indicator.Period = 20;
        Assert.Equal(20, indicator.Period);
        Assert.Equal(0, MavpIndicator.MinHistoryDepths);
    }

    [Fact]
    public void MavpIndicator_MinMaxPeriod_CanBeChanged()
    {
        var indicator = new MavpIndicator { MinPeriod = 3, MaxPeriod = 50 };
        Assert.Equal(3, indicator.MinPeriod);
        Assert.Equal(50, indicator.MaxPeriod);
    }
}
