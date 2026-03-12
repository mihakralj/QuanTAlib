using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class EdcfIndicatorTests
{
    [Fact]
    public void EdcfIndicator_Constructor_SetsDefaults()
    {
        var indicator = new EdcfIndicator();

        Assert.Equal(15, indicator.Length);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("EDCF - Ehlers Distance Coefficient Filter", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void EdcfIndicator_MinHistoryDepths_EqualsTwo()
    {
        Assert.Equal(2, EdcfIndicator.MinHistoryDepths);
        var indicator = new EdcfIndicator();
        Assert.Equal(2, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void EdcfIndicator_ShortName_IncludesLengthAndSource()
    {
        var indicator = new EdcfIndicator { Length = 10 };

        Assert.Contains("EDCF", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void EdcfIndicator_Initialize_CreatesInternalEdcf()
    {
        var indicator = new EdcfIndicator { Length = 15 };
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void EdcfIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new EdcfIndicator { Length = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void EdcfIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new EdcfIndicator { Length = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void EdcfIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new EdcfIndicator { Length = 5 };
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
    public void EdcfIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new EdcfIndicator { Length = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = [100, 102, 104, 103, 105, 107, 106, 108, 110, 109];

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
    }

    [Fact]
    public void EdcfIndicator_DifferentSources_Work()
    {
        var sourceTypes = new[] { SourceType.Close, SourceType.Open, SourceType.HL2, SourceType.HLC3 };

        foreach (var sourceType in sourceTypes)
        {
            var indicator = new EdcfIndicator { Length = 5, Source = sourceType };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {sourceType} produced non-finite value");
        }
    }
}
