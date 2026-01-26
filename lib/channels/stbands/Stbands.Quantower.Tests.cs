using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class StbandsIndicatorTests
{
    [Fact]
    public void StbandsIndicator_Constructor_SetsDefaults()
    {
        var indicator = new StbandsIndicator();

        Assert.Equal(10, indicator.Period);
        Assert.Equal(3.0, indicator.Multiplier);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("STBANDS - Super Trend Bands", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void StbandsIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new StbandsIndicator { Period = 14 };

        Assert.Equal(14, indicator.MinHistoryDepths);
        Assert.Equal(14, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void StbandsIndicator_ShortName_IncludesPeriodAndMultiplier()
    {
        var indicator = new StbandsIndicator { Period = 10, Multiplier = 3.0 };

        Assert.Contains("STBANDS", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("3.0", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void StbandsIndicator_Initialize_CreatesInternalStbands()
    {
        var indicator = new StbandsIndicator { Period = 10, Multiplier = 3.0 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Equal(4, indicator.LinesSeries.Count); // Upper, Lower, Trend, Width
    }

    [Fact]
    public void StbandsIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new StbandsIndicator { Period = 3, Multiplier = 2.0 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        // Process update
        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // Line series should have values
        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void StbandsIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new StbandsIndicator { Period = 3, Multiplier = 2.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void StbandsIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new StbandsIndicator { Period = 3, Multiplier = 2.0 };
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
    public void StbandsIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new StbandsIndicator { Period = 3, Multiplier = 2.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 104, 103, 105 };

        foreach (var close in closes)
        {
            indicator.HistoricalData.AddBar(now, close, close + 5, close - 5, close);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        // All values should be finite
        for (int i = 0; i < closes.Length; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(closes.Length - 1 - i)));
        }
    }

    [Fact]
    public void StbandsIndicator_Parameters_CanBeChanged()
    {
        var indicator = new StbandsIndicator { Period = 5, Multiplier = 1.5 };
        Assert.Equal(5, indicator.Period);
        Assert.Equal(1.5, indicator.Multiplier);

        indicator.Period = 20;
        indicator.Multiplier = 2.5;
        Assert.Equal(20, indicator.Period);
        Assert.Equal(2.5, indicator.Multiplier);
        Assert.Equal(20, indicator.MinHistoryDepths);
    }

    [Fact]
    public void StbandsIndicator_AllSeriesUpdate_Correctly()
    {
        var indicator = new StbandsIndicator { Period = 3, Multiplier = 2.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Verify all 4 line series have values
        Assert.Equal(4, indicator.LinesSeries.Count);
        foreach (var series in indicator.LinesSeries)
        {
            Assert.Equal(5, series.Count);
            Assert.True(double.IsFinite(series.GetValue(0)));
        }
    }

    [Fact]
    public void StbandsIndicator_UpperGreaterThanLower()
    {
        var indicator = new StbandsIndicator { Period = 3, Multiplier = 2.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 100 + i);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Upper should be >= Lower for all bars
        for (int i = 0; i < 5; i++)
        {
            double upper = indicator.LinesSeries[0].GetValue(4 - i); // Upper is first series
            double lower = indicator.LinesSeries[1].GetValue(4 - i); // Lower is second series
            Assert.True(upper >= lower, $"Upper ({upper}) should be >= Lower ({lower}) at index {i}");
        }
    }

    [Fact]
    public void StbandsIndicator_TrendValues_AreValidDirections()
    {
        var indicator = new StbandsIndicator { Period = 3, Multiplier = 2.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 100 + i);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Trend should be +1 or -1
        for (int i = 0; i < 5; i++)
        {
            double trend = indicator.LinesSeries[2].GetValue(4 - i); // Trend is third series
            Assert.True(trend == 1 || trend == -1, $"Trend should be +1 or -1, got {trend}");
        }
    }
}
