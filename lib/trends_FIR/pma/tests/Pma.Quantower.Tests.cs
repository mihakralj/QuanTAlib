using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class PmaIndicatorTests
{
    [Fact]
    public void PmaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new PmaIndicator();

        Assert.Equal(7, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("PMA - Predictive Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void PmaIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new PmaIndicator();

        Assert.Equal(0, PmaIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void PmaIndicator_ShortName_IncludesPeriodAndSource()
    {
        var indicator = new PmaIndicator { Period = 14 };

        Assert.Contains("PMA", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void PmaIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new PmaIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Pma.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void PmaIndicator_Initialize_CreatesInternalPma()
    {
        var indicator = new PmaIndicator { Period = 7 };

        indicator.Initialize();

        // After init, two line series should exist (PMA and Trigger)
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void PmaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new PmaIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.Equal(1, indicator.LinesSeries[1].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
        Assert.True(double.IsFinite(indicator.LinesSeries[1].GetValue(0)));
    }

    [Fact]
    public void PmaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new PmaIndicator { Period = 3 };
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
    public void PmaIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new PmaIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        double pmaFirst = indicator.LinesSeries[0].GetValue(0);
        double trigFirst = indicator.LinesSeries[1].GetValue(0);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        double pmaSecond = indicator.LinesSeries[0].GetValue(0);
        double trigSecond = indicator.LinesSeries[1].GetValue(0);

        Assert.True(double.IsFinite(pmaFirst));
        Assert.True(double.IsFinite(trigFirst));
        Assert.True(double.IsFinite(pmaSecond));
        Assert.True(double.IsFinite(trigSecond));
    }

    [Fact]
    public void PmaIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new PmaIndicator { Period = 3 };
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
            Assert.True(double.IsFinite(indicator.LinesSeries[1].GetValue(closes.Length - 1 - i)));
        }

        double lastPma = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastPma >= 95 && lastPma <= 115);
    }

    [Fact]
    public void PmaIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new PmaIndicator { Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite PMA value");
            Assert.True(double.IsFinite(indicator.LinesSeries[1].GetValue(0)),
                $"Source {source} should produce finite Trigger value");
        }
    }

    [Fact]
    public void PmaIndicator_Period_CanBeChanged()
    {
        var indicator = new PmaIndicator { Period = 7 };
        Assert.Equal(7, indicator.Period);

        indicator.Period = 14;
        Assert.Equal(14, indicator.Period);
    }
}
