using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class UbandsIndicatorTests
{
    [Fact]
    public void UbandsIndicator_Constructor_SetsDefaults()
    {
        var indicator = new UbandsIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(1.0, indicator.Multiplier);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("UBANDS - Ehlers Ultimate Bands", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void UbandsIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new UbandsIndicator { Period = 20 };

        Assert.Equal(20, indicator.MinHistoryDepths);
        Assert.Equal(20, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void UbandsIndicator_ShortName_IncludesPeriodAndMultiplier()
    {
        var indicator = new UbandsIndicator { Period = 15, Multiplier = 2.5 };

        Assert.Contains("UBANDS", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("15", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("2.5", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void UbandsIndicator_Initialize_CreatesInternalUbands()
    {
        var indicator = new UbandsIndicator { Period = 10, Multiplier = 1.0 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Equal(4, indicator.LinesSeries.Count); // Middle, Upper, Lower, Width
    }

    [Fact]
    public void UbandsIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new UbandsIndicator { Period = 3, Multiplier = 1.0 };
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
    public void UbandsIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new UbandsIndicator { Period = 3, Multiplier = 1.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void UbandsIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new UbandsIndicator { Period = 3, Multiplier = 1.0 };
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
    public void UbandsIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new UbandsIndicator { Period = 3, Multiplier = 1.0 };
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

        // Middle band (USF) should smooth the values
        double lastMiddle = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastMiddle >= 100 && lastMiddle <= 106);
    }

    [Fact]
    public void UbandsIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new UbandsIndicator { Period = 3, Multiplier = 1.0, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void UbandsIndicator_Parameters_CanBeChanged()
    {
        var indicator = new UbandsIndicator { Period = 5, Multiplier = 1.5 };
        Assert.Equal(5, indicator.Period);
        Assert.Equal(1.5, indicator.Multiplier);

        indicator.Period = 20;
        indicator.Multiplier = 2.5;
        Assert.Equal(20, indicator.Period);
        Assert.Equal(2.5, indicator.Multiplier);
        Assert.Equal(20, indicator.MinHistoryDepths);
    }

    [Fact]
    public void UbandsIndicator_AllBandsUpdate_Correctly()
    {
        var indicator = new UbandsIndicator { Period = 3, Multiplier = 1.0 };
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
    public void UbandsIndicator_BandRelationships_AreCorrect()
    {
        var indicator = new UbandsIndicator { Period = 5, Multiplier = 1.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Add varied data to generate band width
        double[] closes = { 100, 105, 95, 110, 90, 105, 100, 108, 92, 103 };

        foreach (var close in closes)
        {
            indicator.HistoricalData.AddBar(now, close, close + 3, close - 3, close);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        // Get last values: Middle is index 0, Upper is index 1, Lower is index 2, Width is index 3
        double middle = indicator.LinesSeries[0].GetValue(0);
        double upper = indicator.LinesSeries[1].GetValue(0);
        double lower = indicator.LinesSeries[2].GetValue(0);
        double width = indicator.LinesSeries[3].GetValue(0);

        // Upper > Middle > Lower
        Assert.True(upper >= middle, $"Upper ({upper}) should be >= Middle ({middle})");
        Assert.True(middle >= lower, $"Middle ({middle}) should be >= Lower ({lower})");

        // Width = Upper - Lower (approximately)
        Assert.True(Math.Abs(width - (upper - lower)) < 0.0001,
            $"Width ({width}) should equal Upper - Lower ({upper - lower})");
    }
}