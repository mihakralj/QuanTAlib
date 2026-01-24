using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class BbandsIndicatorTests
{
    [Fact]
    public void BbandsIndicator_Constructor_SetsDefaults()
    {
        var indicator = new BbandsIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(2.0, indicator.Multiplier);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("BBANDS - Bollinger Bands", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void BbandsIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new BbandsIndicator { Period = 20 };

        Assert.Equal(20, indicator.MinHistoryDepths);
        Assert.Equal(20, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void BbandsIndicator_ShortName_IncludesPeriodAndMultiplier()
    {
        var indicator = new BbandsIndicator { Period = 15, Multiplier = 2.5 };

        Assert.Contains("BBANDS", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("15", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("2.5", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void BbandsIndicator_Initialize_CreatesInternalBbands()
    {
        var indicator = new BbandsIndicator { Period = 10, Multiplier = 2.0 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Equal(5, indicator.LinesSeries.Count); // Middle, Upper, Lower, Width, %B
    }

    [Fact]
    public void BbandsIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new BbandsIndicator { Period = 3, Multiplier = 2.0 };
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
    public void BbandsIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new BbandsIndicator { Period = 3, Multiplier = 2.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void BbandsIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new BbandsIndicator { Period = 3, Multiplier = 2.0 };
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
    public void BbandsIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new BbandsIndicator { Period = 3, Multiplier = 2.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 104, 103, 105 };

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

        // Middle band should be close to the average of last 3 values
        double lastMiddle = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastMiddle >= 102 && lastMiddle <= 106);
    }

    [Fact]
    public void BbandsIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new BbandsIndicator { Period = 3, Multiplier = 2.0, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void BbandsIndicator_Parameters_CanBeChanged()
    {
        var indicator = new BbandsIndicator { Period = 5, Multiplier = 1.5 };
        Assert.Equal(5, indicator.Period);
        Assert.Equal(1.5, indicator.Multiplier);

        indicator.Period = 20;
        indicator.Multiplier = 2.5;
        Assert.Equal(20, indicator.Period);
        Assert.Equal(2.5, indicator.Multiplier);
        Assert.Equal(20, indicator.MinHistoryDepths);
    }

    [Fact]
    public void BbandsIndicator_AllBandsUpdate_Correctly()
    {
        var indicator = new BbandsIndicator { Period = 3, Multiplier = 2.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Verify all 5 line series have values
        Assert.Equal(5, indicator.LinesSeries.Count);
        foreach (var series in indicator.LinesSeries)
        {
            Assert.Equal(5, series.Count);
            Assert.True(double.IsFinite(series.GetValue(0)));
        }
    }
}