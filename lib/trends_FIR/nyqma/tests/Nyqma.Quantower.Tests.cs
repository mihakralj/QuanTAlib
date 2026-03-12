using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class NyqmaIndicatorTests
{
    [Fact]
    public void NyqmaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new NyqmaIndicator();

        Assert.Equal(89, indicator.Period);
        Assert.Equal(21, indicator.NyquistPeriod);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("NYQMA - Nyquist Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void NyqmaIndicator_MinHistoryDepths_IsZero()
    {
        var indicator = new NyqmaIndicator { Period = 14, NyquistPeriod = 5 };

        Assert.Equal(0, NyqmaIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void NyqmaIndicator_ShortName_IncludesPeriodsAndSource()
    {
        var indicator = new NyqmaIndicator { Period = 14, NyquistPeriod = 5 };

        Assert.Contains("NYQMA", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("5", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void NyqmaIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new NyqmaIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Nyqma.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void NyqmaIndicator_Initialize_CreatesInternalNyqma()
    {
        var indicator = new NyqmaIndicator { Period = 10, NyquistPeriod = 4 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void NyqmaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new NyqmaIndicator { Period = 5, NyquistPeriod = 2 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void NyqmaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new NyqmaIndicator { Period = 5, NyquistPeriod = 2 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void NyqmaIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new NyqmaIndicator { Period = 5, NyquistPeriod = 2 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        double first = indicator.LinesSeries[0].GetValue(0);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        double second = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(first));
        Assert.True(double.IsFinite(second));
    }

    [Fact]
    public void NyqmaIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new NyqmaIndicator { Period = 5, NyquistPeriod = 2 };
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

        double lastValue = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastValue >= 95 && lastValue <= 115);
    }

    [Fact]
    public void NyqmaIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new NyqmaIndicator { Source = source, Period = 5, NyquistPeriod = 2 };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite NYQMA value");
        }
    }

    [Fact]
    public void NyqmaIndicator_Periods_CanBeChanged()
    {
        var indicator = new NyqmaIndicator { Period = 10, NyquistPeriod = 4 };
        Assert.Equal(10, indicator.Period);
        Assert.Equal(4, indicator.NyquistPeriod);

        indicator.Period = 21;
        indicator.NyquistPeriod = 8;
        Assert.Equal(21, indicator.Period);
        Assert.Equal(8, indicator.NyquistPeriod);
    }
}
