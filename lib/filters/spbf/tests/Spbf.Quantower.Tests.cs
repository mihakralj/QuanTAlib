using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class SpbfIndicatorTests
{
    [Fact]
    public void SpbfIndicator_Constructor_SetsDefaults()
    {
        var indicator = new SpbfIndicator();

        Assert.Equal(40, indicator.ShortPeriod);
        Assert.Equal(60, indicator.LongPeriod);
        Assert.Equal(50, indicator.RmsPeriod);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("SPBF - Ehlers Super Passband Filter", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void SpbfIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new SpbfIndicator { ShortPeriod = 40, LongPeriod = 60, RmsPeriod = 50 };

        Assert.Equal(0, SpbfIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void SpbfIndicator_ShortName_IncludesParameters()
    {
        var indicator = new SpbfIndicator { ShortPeriod = 40, LongPeriod = 60, RmsPeriod = 50 };

        Assert.Contains("SPBF", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("40", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("60", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("50", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void SpbfIndicator_Initialize_CreatesInternalSpbf()
    {
        var indicator = new SpbfIndicator { ShortPeriod = 40, LongPeriod = 60, RmsPeriod = 50 };

        indicator.Initialize();

        Assert.Equal(3, indicator.LinesSeries.Count); // Passband, positive RMS, negative RMS
    }

    [Fact]
    public void SpbfIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new SpbfIndicator { ShortPeriod = 40, LongPeriod = 60, RmsPeriod = 50 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void SpbfIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new SpbfIndicator { ShortPeriod = 40, LongPeriod = 60, RmsPeriod = 50 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void SpbfIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new SpbfIndicator { ShortPeriod = 40, LongPeriod = 60, RmsPeriod = 50 };
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
    public void SpbfIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new SpbfIndicator { ShortPeriod = 40, LongPeriod = 60, RmsPeriod = 50, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void SpbfIndicator_Parameters_CanBeChanged()
    {
        var indicator = new SpbfIndicator { ShortPeriod = 40, LongPeriod = 60, RmsPeriod = 50 };
        Assert.Equal(40, indicator.ShortPeriod);
        Assert.Equal(60, indicator.LongPeriod);
        Assert.Equal(50, indicator.RmsPeriod);

        indicator.ShortPeriod = 20;
        indicator.LongPeriod = 80;
        indicator.RmsPeriod = 30;
        Assert.Equal(20, indicator.ShortPeriod);
        Assert.Equal(80, indicator.LongPeriod);
        Assert.Equal(30, indicator.RmsPeriod);
    }
}
