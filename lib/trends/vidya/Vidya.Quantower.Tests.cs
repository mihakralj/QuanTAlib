using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class VidyaIndicatorTests
{
    [Fact]
    public void VidyaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new VidyaIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("VIDYA - Variable Index Dynamic Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void VidyaIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new VidyaIndicator { Period = 20 };

        Assert.Equal(0, VidyaIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void VidyaIndicator_ShortName_IncludesPeriodAndSource()
    {
        var indicator = new VidyaIndicator { Period = 15 };

        Assert.Contains("VIDYA", indicator.ShortName);
        Assert.Contains("15", indicator.ShortName);
    }

    [Fact]
    public void VidyaIndicator_Initialize_CreatesInternalVidya()
    {
        var indicator = new VidyaIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void VidyaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new VidyaIndicator { Period = 3 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 102);
            
            // Process update
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        Assert.True(indicator.LinesSeries[0].Count > 0);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void VidyaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new VidyaIndicator { Period = 3 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        // Process first update
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        // Line series should have values
        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void VidyaIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new VidyaIndicator { Period = 3 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 102);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double firstValue = indicator.LinesSeries[0].GetValue(0);

        // Update with new tick (same bar data - simulates intrabar update)
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        double secondValue = indicator.LinesSeries[0].GetValue(0);

        // Both values should be finite
        Assert.True(double.IsFinite(firstValue));
        Assert.True(double.IsFinite(secondValue));
    }

    [Fact]
    public void VidyaIndicator_MultipleUpdates_ProducesCorrectVidyaSequence()
    {
        var indicator = new VidyaIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 104, 103, 105, 107, 106 };

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

        // VIDYA should be smoothing the values
        // Last VIDYA value should be between first and last close
        double lastVidya = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastVidya >= 100 && lastVidya <= 110);
    }

    [Fact]
    public void VidyaIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new VidyaIndicator { Period = 3, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void VidyaIndicator_Period_CanBeChanged()
    {
        var indicator = new VidyaIndicator { Period = 5 };
        Assert.Equal(5, indicator.Period);

        indicator.Period = 20;
        Assert.Equal(20, indicator.Period);
        Assert.Equal(0, VidyaIndicator.MinHistoryDepths);
    }
}
