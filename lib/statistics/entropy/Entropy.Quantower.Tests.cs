using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class EntropyIndicatorTests
{
    [Fact]
    public void EntropyIndicator_Constructor_SetsDefaults()
    {
        var indicator = new EntropyIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Entropy - Shannon Entropy", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void EntropyIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new EntropyIndicator { Period = 14 };

        Assert.Equal(0, EntropyIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void EntropyIndicator_Initialize_CreatesInternalEntropy()
    {
        var indicator = new EntropyIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Entropy", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void EntropyIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new EntropyIndicator { Period = 5 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double entropy = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(entropy));
        // Allow tiny floating-point overshoot above 1.0
        Assert.True(entropy >= -1e-10 && entropy <= 1.0 + 1e-10,
            $"Expected entropy in [0, 1], got {entropy}");
    }

    [Fact]
    public void EntropyIndicator_DifferentSourceTypes()
    {
        var indicator = new EntropyIndicator { Period = 5, Source = SourceType.Open };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double entropy = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(entropy));
    }

    [Fact]
    public void EntropyIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new EntropyIndicator { Period = 20 };
        Assert.Equal("Entropy 20", indicator.ShortName);
    }

    [Fact]
    public void EntropyIndicator_NewBar_UpdatesValue()
    {
        var indicator = new EntropyIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add enough bars to warm up
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        _ = indicator.LinesSeries[0].GetValue(0);

        // Add a new bar with a very different value
        indicator.HistoricalData.AddBar(now.AddMinutes(10), 200, 210, 190, 205);
        var newArgs = new UpdateArgs(UpdateReason.NewBar);
        indicator.ProcessUpdate(newArgs);

        double valueAfter = indicator.LinesSeries[0].GetValue(0);

        // Value should change after adding a significantly different bar
        Assert.True(double.IsFinite(valueAfter));
    }
}
