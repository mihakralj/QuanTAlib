using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class DecyclerIndicatorTests
{
    [Fact]
    public void DecyclerIndicator_Constructor_SetsDefaults()
    {
        var indicator = new DecyclerIndicator();

        Assert.Equal(60, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Decycler - Ehlers Decycler", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void DecyclerIndicator_MinHistoryDepths_ReturnsCorrectValue()
    {
        var indicator = new DecyclerIndicator();

        Assert.Equal(60, DecyclerIndicator.MinHistoryDepths);
        Assert.Equal(60, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void DecyclerIndicator_ShortName_ContainsPeriod()
    {
        var indicator = new DecyclerIndicator { Period = 30 };

        // Initialize to set _sourceName
        indicator.Initialize();

        Assert.Contains("Decycler", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("30", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("Close", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void DecyclerIndicator_Initialize_CreatesIndicator()
    {
        var indicator = new DecyclerIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void DecyclerIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new DecyclerIndicator { Period = 20 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void DecyclerIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new DecyclerIndicator { Period = 20 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void DecyclerIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new DecyclerIndicator { Period = 20 };
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
    public void DecyclerIndicator_MultipleUpdates_ProduceResults()
    {
        var indicator = new DecyclerIndicator { Period = 20 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 104, 103, 105, 106, 107 };

        foreach (var close in closes)
        {
            indicator.HistoricalData.AddBar(now, close, close + 2, close - 2, close);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        // All values should be finite
        for (int i = 0; i < closes.Length; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(closes.Length - 1 - i)));
        }

        // Check last value
        double lastVal = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(lastVal));
    }

    [Fact]
    public void DecyclerIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new DecyclerIndicator { Period = 20, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }
}
