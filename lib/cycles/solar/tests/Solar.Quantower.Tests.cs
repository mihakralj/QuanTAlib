using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Quantower.Tests;

public class SolarIndicatorTests
{
    [Fact]
    public void SolarIndicator_Constructor_SetsDefaults()
    {
        var indicator = new SolarIndicator();

        Assert.True(indicator.ShowColdValues);
        Assert.Equal("SOLAR - Solar Cycle", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void SolarIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new SolarIndicator();

        Assert.Equal(0, SolarIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void SolarIndicator_ShortName_IsSolar()
    {
        var indicator = new SolarIndicator();

        Assert.Equal("SOLAR", indicator.ShortName);
    }

    [Fact]
    public void SolarIndicator_Initialize_CreatesInternalSolar()
    {
        var indicator = new SolarIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (Solar Cycle + 3 reference lines)
        Assert.Equal(4, indicator.LinesSeries.Count);
    }

    [Fact]
    public void SolarIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new SolarIndicator();
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        // Process update
        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // Line series should have a value
        Assert.Equal(1, indicator.LinesSeries[0].Count);
        double value = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(value));
        Assert.True(value >= -1.0 && value <= 1.0, $"Solar cycle should be -1 to 1, got {value}");
    }

    [Fact]
    public void SolarIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new SolarIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void SolarIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new SolarIndicator();
        indicator.Initialize();

        // Should not throw an exception
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.NotNull(indicator);
    }

    [Fact]
    public void SolarIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new SolarIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        int barCount = 30;

        for (int i = 0; i < barCount; i++)
        {
            indicator.HistoricalData.AddBar(now.AddDays(i), 100, 105, 95, 102);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // All values should be in valid range [-1, 1]
        for (int i = 0; i < barCount; i++)
        {
            double value = indicator.LinesSeries[0].GetValue(barCount - 1 - i);
            Assert.True(double.IsFinite(value), $"Value at index {i} should be finite");
            Assert.True(value >= -1.0 && value <= 1.0, $"Value at index {i} should be -1 to 1, got {value}");
        }
    }

    [Fact]
    public void SolarIndicator_ShowColdValues_CanBeChanged()
    {
        var indicator = new SolarIndicator { ShowColdValues = true };

        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);
    }

    [Fact]
    public void SolarIndicator_ProcessUpdate_IgnoresNonBarUpdates()
    {
        var indicator = new SolarIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.NotNull(indicator);
    }

    [Fact]
    public void SolarIndicator_LineSeries_HasCorrectProperties()
    {
        var indicator = new SolarIndicator();
        indicator.Initialize();

        var lineSeries = indicator.LinesSeries[0];

        Assert.Equal("Solar Cycle", lineSeries.Name);
        Assert.Equal(2, lineSeries.Width);
        Assert.Equal(LineStyle.Solid, lineSeries.Style);
    }

    [Fact]
    public void SolarIndicator_ReferenceLines_HaveCorrectValues()
    {
        var indicator = new SolarIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Check reference line values
        Assert.Equal(1.0, indicator.LinesSeries[1].GetValue(0));  // Summer Solstice line
        Assert.Equal(-1.0, indicator.LinesSeries[2].GetValue(0)); // Winter Solstice line
        Assert.Equal(0.0, indicator.LinesSeries[3].GetValue(0));  // Equinox line
    }

    [Fact]
    public void SolarIndicator_CycleVariesOverTime()
    {
        var indicator = new SolarIndicator();
        indicator.Initialize();

        var baseDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
        // Add bars over 6 months to see significant variation
        for (int i = 0; i < 180; i++)
        {
            indicator.HistoricalData.AddBar(baseDate.AddDays(i), 100, 105, 95, 102);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Collect all values
        var cycles = new double[180];
        for (int i = 0; i < 180; i++)
        {
            cycles[i] = indicator.LinesSeries[0].GetValue(179 - i);
        }

        // Verify there's variation in cycles
        double minCycle = cycles.Min();
        double maxCycle = cycles.Max();
        
        Assert.True(maxCycle - minCycle > 1.0, 
            $"Solar cycle should vary significantly over 6 months. Min: {minCycle}, Max: {maxCycle}");
    }

    [Fact]
    public void SolarIndicator_ProducesValidCycle()
    {
        var indicator = new SolarIndicator();
        indicator.Initialize();

        var testDate = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        indicator.HistoricalData.AddBar(testDate, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double cycle = indicator.LinesSeries[0].GetValue(0);
        Assert.True(cycle >= -1.0 && cycle <= 1.0, $"Cycle should be in [-1,1] range, got {cycle}");
    }

    [Fact]
    public void SolarIndicator_CycleVariesWithDate()
    {
        var indicator = new SolarIndicator();
        indicator.Initialize();

        var date1 = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var date2 = new DateTime(2024, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        
        indicator.HistoricalData.AddBar(date1, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        double cycle1 = indicator.LinesSeries[0].GetValue(0);
        
        indicator.HistoricalData.AddBar(date2, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));
        double cycle2 = indicator.LinesSeries[0].GetValue(0);

        // Cycles at opposite ends of year should differ significantly
        Assert.NotEqual(cycle1, cycle2);
    }

    [Fact]
    public void SolarIndicator_HasFourLineSeries()
    {
        var indicator = new SolarIndicator();
        indicator.Initialize();

        Assert.Equal(4, indicator.LinesSeries.Count);
        Assert.Equal("Solar Cycle", indicator.LinesSeries[0].Name);
        Assert.Equal("Summer Solstice", indicator.LinesSeries[1].Name);
        Assert.Equal("Winter Solstice", indicator.LinesSeries[2].Name);
        Assert.Equal("Equinox", indicator.LinesSeries[3].Name);
    }

    [Fact]
    public void SolarIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new SolarIndicator();

        Assert.NotNull(indicator.SourceCodeLink);
        Assert.Contains("Solar.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
    }
}
