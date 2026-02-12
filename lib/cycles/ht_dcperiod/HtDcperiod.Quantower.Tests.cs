using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

/// <summary>
/// Tests for HtDcperiodIndicator Quantower adapter.
/// Covers: constructor, properties (Source, ShowColdValues, ShortName, MinHistoryDepths),
/// OnInit, OnUpdate (HistoricalBar, NewBar, NewTick filtered), multiple bars,
/// ShowColdValues false, reinitialize, source variants.
/// </summary>
public class HtDcperiodIndicatorTests
{
    // ═══════════════════════════════ Constructor ═══════════════════════════════

    [Fact]
    public void Constructor_InitializesName()
    {
        var indicator = new HtDcperiodIndicator();
        Assert.Contains("HT_DCPERIOD", indicator.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_InitializesDescription()
    {
        var indicator = new HtDcperiodIndicator();
        Assert.False(string.IsNullOrEmpty(indicator.Description));
        Assert.Contains("Hilbert", indicator.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_SeparateWindowTrue()
    {
        var indicator = new HtDcperiodIndicator();
        Assert.True(indicator.SeparateWindow);
    }

    [Fact]
    public void Constructor_HasLineSeries()
    {
        var indicator = new HtDcperiodIndicator();
        Assert.True(indicator.LinesSeries.Count >= 1);
    }

    // ═══════════════════════════════ Properties ═══════════════════════════════

    [Fact]
    public void Source_DefaultsToClose()
    {
        var indicator = new HtDcperiodIndicator();
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void ShowColdValues_DefaultsToTrue()
    {
        var indicator = new HtDcperiodIndicator();
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void ShortName_IsHtDcperiod()
    {
        var indicator = new HtDcperiodIndicator();
        Assert.Equal("HT_DCPERIOD", indicator.ShortName);
    }

    [Fact]
    public void MinHistoryDepths_Static_Is32()
    {
        Assert.Equal(32, HtDcperiodIndicator.MinHistoryDepths);
    }

    [Fact]
    public void MinHistoryDepths_Interface_Is32()
    {
        IWatchlistIndicator indicator = new HtDcperiodIndicator();
        Assert.Equal(32, indicator.MinHistoryDepths);
    }

    [Fact]
    public void SourceCodeLink_IsNotEmpty()
    {
        var indicator = new HtDcperiodIndicator();
        Assert.False(string.IsNullOrEmpty(indicator.SourceCodeLink));
        Assert.Contains("HtDcperiod", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    // ═══════════════════════════════ OnInit ═══════════════════════════════════

    [Fact]
    public void OnInit_CreatesInternalIndicator()
    {
        var indicator = new HtDcperiodIndicator();
        indicator.Initialize();
        // Should not throw - internal indicator created successfully
        Assert.True(true);
    }

    // ═══════════════════════════════ OnUpdate ═════════════════════════════════

    [Fact]
    public void OnUpdate_HistoricalBar_Processes()
    {
        var indicator = new HtDcperiodIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 40; i++)
        {
            indicator.HistoricalData.AddBar(
                time: now.AddMinutes(i),
                open: 100 + i,
                high: 105 + i,
                low: 95 + i,
                close: 102 + i,
                volume: 1000);

            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        Assert.Equal(40, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void OnUpdate_NewBar_Processes()
    {
        var indicator = new HtDcperiodIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Feed historical bars first
        for (int i = 0; i < 35; i++)
        {
            indicator.HistoricalData.AddBar(
                time: now.AddMinutes(i),
                open: 100 + i,
                high: 105 + i,
                low: 95 + i,
                close: 102 + i,
                volume: 1000);

            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Then process a new bar
        indicator.HistoricalData.AddBar(
            time: now.AddMinutes(35),
            open: 135, high: 140, low: 130, close: 137, volume: 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(36, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void OnUpdate_NewTick_DoesNotThrow()
    {
        var indicator = new HtDcperiodIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // NewTick should be filtered (early return) - no exception
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        Assert.True(true);
    }

    // ═══════════════════════════════ Multiple Bars ════════════════════════════

    [Fact]
    public void OnUpdate_MultipleBars_ProducesFiniteValues()
    {
        var indicator = new HtDcperiodIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // HT_DCPERIOD needs significant warmup - feed sinusoidal data
        for (int i = 0; i < 100; i++)
        {
            double price = 100 + 10 * Math.Sin(i * 0.3);
            indicator.HistoricalData.AddBar(
                time: now.AddMinutes(i),
                open: price - 1,
                high: price + 2,
                low: price - 2,
                close: price,
                volume: 1000);

            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        Assert.Equal(100, indicator.LinesSeries[0].Count);

        // After warmup, values should be finite
        double lastValue = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(lastValue));
    }

    [Fact]
    public void OnUpdate_SingleBar_ProducesValue()
    {
        var indicator = new HtDcperiodIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.Equal(1, indicator.LinesSeries[0].Count);
    }

    // ═══════════════════════════════ ShowColdValues ═══════════════════════════

    [Fact]
    public void ShowColdValues_CanBeSetFalse()
    {
        var indicator = new HtDcperiodIndicator();
        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);
    }

    [Fact]
    public void ShowColdValues_False_ProcessesWithoutError()
    {
        var indicator = new HtDcperiodIndicator();
        indicator.ShowColdValues = false;
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 40; i++)
        {
            indicator.HistoricalData.AddBar(
                time: now.AddMinutes(i),
                open: 100 + i, high: 105 + i, low: 95 + i,
                close: 102 + i, volume: 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }
        Assert.True(true);
    }

    // ═══════════════════════════════ Reinitialize ═════════════════════════════

    [Fact]
    public void Reinitialize_ResetsState()
    {
        var indicator = new HtDcperiodIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(
                time: now.AddMinutes(i),
                open: 100 + i, high: 105 + i, low: 95 + i, close: 102 + i, volume: 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Reinitialize
        indicator.Initialize();

        // Should process fresh data without error
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(
                time: now.AddMinutes(100 + i),
                open: 200 + i, high: 205 + i, low: 195 + i, close: 202 + i, volume: 2000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }
        Assert.True(true);
    }

    // ═══════════════════════════════ Source Variants ══════════════════════════

    [Fact]
    public void Source_SetToOpen_Accepted()
    {
        var indicator = new HtDcperiodIndicator();
        indicator.Source = SourceType.Open;
        Assert.Equal(SourceType.Open, indicator.Source);
    }

    [Fact]
    public void Source_SetToHigh_Accepted()
    {
        var indicator = new HtDcperiodIndicator();
        indicator.Source = SourceType.High;
        Assert.Equal(SourceType.High, indicator.Source);
    }

    [Fact]
    public void Source_SetToLow_Accepted()
    {
        var indicator = new HtDcperiodIndicator();
        indicator.Source = SourceType.Low;
        Assert.Equal(SourceType.Low, indicator.Source);
    }

    [Fact]
    public void Source_DifferentSources_ProcessWithoutError()
    {
        foreach (var source in new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close })
        {
            var indicator = new HtDcperiodIndicator();
            indicator.Source = source;
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 40; i++)
            {
                indicator.HistoricalData.AddBar(
                    time: now.AddMinutes(i),
                    open: 100 + i, high: 105 + i, low: 95 + i,
                    close: 102 + i, volume: 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }
        }
        Assert.True(true);
    }

    // ═══════════════════════════════ OnBackGround ═════════════════════════════

    [Fact]
    public void OnBackGround_IsTrue()
    {
        var indicator = new HtDcperiodIndicator();
        Assert.True(indicator.OnBackGround);
    }

    // ═══════════════════════════════ Value Assertions ═════════════════════════

    [Fact]
    public void Values_AfterWarmup_ArePositive()
    {
        var indicator = new HtDcperiodIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Feed sinusoidal data with known period (~21 bars)
        for (int i = 0; i < 100; i++)
        {
            double price = 100 + 10 * Math.Sin(2 * Math.PI * i / 21.0);
            indicator.HistoricalData.AddBar(
                time: now.AddMinutes(i),
                open: price - 0.5,
                high: price + 1,
                low: price - 1,
                close: price,
                volume: 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Dominant cycle period should be positive after warmup
        double lastValue = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastValue > 0, $"Expected positive period, got {lastValue}");
    }
}
