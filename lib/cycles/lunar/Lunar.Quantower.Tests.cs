using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Quantower.Tests;

public class LunarIndicatorTests
{
    [Fact]
    public void LunarIndicator_Constructor_SetsDefaults()
    {
        var indicator = new LunarIndicator();

        Assert.True(indicator.ShowColdValues);
        Assert.Equal("LUNAR - Lunar Phase", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void LunarIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new LunarIndicator();

        Assert.Equal(0, LunarIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void LunarIndicator_ShortName_IsLunar()
    {
        var indicator = new LunarIndicator();

        Assert.Equal("LUNAR", indicator.ShortName);
    }

    [Fact]
    public void LunarIndicator_Initialize_CreatesInternalLunar()
    {
        var indicator = new LunarIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (Lunar Phase + 3 reference lines)
        Assert.Equal(4, indicator.LinesSeries.Count);
    }

    [Fact]
    public void LunarIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new LunarIndicator();
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
        Assert.True(value >= 0.0 && value <= 1.0, $"Lunar phase should be 0-1, got {value}");
    }

    [Fact]
    public void LunarIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new LunarIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void LunarIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new LunarIndicator();
        indicator.Initialize();

        // Should not throw an exception
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        // Assert that the indicator still exists (method completed without exception)
        Assert.NotNull(indicator);
    }

    [Fact]
    public void LunarIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new LunarIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        int barCount = 30; // Cover roughly one lunar month

        for (int i = 0; i < barCount; i++)
        {
            indicator.HistoricalData.AddBar(now.AddDays(i), 100, 105, 95, 102);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // All values should be in valid range [0, 1]
        for (int i = 0; i < barCount; i++)
        {
            double value = indicator.LinesSeries[0].GetValue(barCount - 1 - i);
            Assert.True(double.IsFinite(value), $"Value at index {i} should be finite");
            Assert.True(value >= 0.0 && value <= 1.0, $"Value at index {i} should be 0-1, got {value}");
        }
    }

    [Fact]
    public void LunarIndicator_ShowColdValues_CanBeChanged()
    {
        var indicator = new LunarIndicator { ShowColdValues = true };

        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);
    }

    [Fact]
    public void LunarIndicator_ProcessUpdate_IgnoresNonBarUpdates()
    {
        var indicator = new LunarIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        // Process historical bar first
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Process other update reasons - should not throw
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        // Assert that the indicator still exists (method completed without exception)
        Assert.NotNull(indicator);
    }

    [Fact]
    public void LunarIndicator_LineSeries_HasCorrectProperties()
    {
        var indicator = new LunarIndicator();
        indicator.Initialize();

        var lineSeries = indicator.LinesSeries[0];

        Assert.Equal("Lunar Phase", lineSeries.Name);
        Assert.Equal(2, lineSeries.Width);
        Assert.Equal(LineStyle.Solid, lineSeries.Style);
    }

    [Fact]
    public void LunarIndicator_ReferenceLines_HaveCorrectValues()
    {
        var indicator = new LunarIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Check reference line values
        Assert.Equal(0.0, indicator.LinesSeries[1].GetValue(0)); // New Moon line
        Assert.Equal(1.0, indicator.LinesSeries[2].GetValue(0)); // Full Moon line
        Assert.Equal(0.5, indicator.LinesSeries[3].GetValue(0)); // Quarter line
    }

    [Fact]
    public void LunarIndicator_PhaseVariesOverTime()
    {
        var indicator = new LunarIndicator();
        indicator.Initialize();

        var baseDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
        // Add bars over a lunar month
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(baseDate.AddDays(i), 100, 105, 95, 102);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Collect all values
        var phases = new double[30];
        for (int i = 0; i < 30; i++)
        {
            phases[i] = indicator.LinesSeries[0].GetValue(29 - i);
        }

        // Verify there's variation in phases (not all same value)
        double minPhase = phases.Min();
        double maxPhase = phases.Max();
        
        Assert.True(maxPhase - minPhase > 0.5, 
            $"Lunar phase should vary significantly over a month. Min: {minPhase}, Max: {maxPhase}");
    }

    [Fact]
    public void LunarIndicator_ProducesValidPhase()
    {
        var indicator = new LunarIndicator();
        indicator.Initialize();

        // Use any date - the phase should be in valid range
        var testDate = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        indicator.HistoricalData.AddBar(testDate, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double phase = indicator.LinesSeries[0].GetValue(0);
        Assert.True(phase >= 0.0 && phase <= 1.0, $"Phase should be in [0,1] range, got {phase}");
    }

    [Fact]
    public void LunarIndicator_PhaseVariesWithDate()
    {
        var indicator = new LunarIndicator();
        indicator.Initialize();

        // Add bars at different dates and verify phases vary
        var date1 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var date2 = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        
        indicator.HistoricalData.AddBar(date1, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        double phase1 = indicator.LinesSeries[0].GetValue(0);
        
        indicator.HistoricalData.AddBar(date2, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));
        double phase2 = indicator.LinesSeries[0].GetValue(0);

        // Phases at different dates should differ (14 days apart = significant lunar change)
        Assert.NotEqual(phase1, phase2);
    }

    [Fact]
    public void LunarIndicator_HasFourLineSeries()
    {
        var indicator = new LunarIndicator();
        indicator.Initialize();

        Assert.Equal(4, indicator.LinesSeries.Count);
        Assert.Equal("Lunar Phase", indicator.LinesSeries[0].Name);
        Assert.Equal("New Moon", indicator.LinesSeries[1].Name);
        Assert.Equal("Full Moon", indicator.LinesSeries[2].Name);
        Assert.Equal("Quarter", indicator.LinesSeries[3].Name);
    }

    [Fact]
    public void LunarIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new LunarIndicator();

        Assert.NotNull(indicator.SourceCodeLink);
        Assert.Contains("Lunar.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
    }
}