using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Quantower.Tests;

public class AtrnIndicatorTests
{
    [Fact]
    public void AtrnIndicator_Constructor_SetsDefaults()
    {
        var indicator = new AtrnIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("ATRN - Average True Range Normalized", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void AtrnIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new AtrnIndicator();

        Assert.Equal(0, AtrnIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void AtrnIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new AtrnIndicator { Period = 14 };

        Assert.True(indicator.ShortName.Contains("ATRN", StringComparison.Ordinal));
        Assert.True(indicator.ShortName.Contains("14", StringComparison.Ordinal));
    }

    [Fact]
    public void AtrnIndicator_Initialize_CreatesInternalAtrn()
    {
        var indicator = new AtrnIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void AtrnIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new AtrnIndicator { Period = 5 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        // Process update
        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // Line series should have a value
        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void AtrnIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new AtrnIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void AtrnIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new AtrnIndicator { Period = 5 };
        indicator.Initialize();

        // Add initial bar first (NewTick requires at least one bar in historical data)
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Now NewTick should not throw an exception
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        // Assert that the indicator still exists (method completed without exception)
        Assert.NotNull(indicator);
        // NewTick updates the last bar in place or adds a new point depending on implementation
        Assert.True(indicator.LinesSeries[0].Count >= 1);
    }

    [Fact]
    public void AtrnIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new AtrnIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = [100, 102, 105, 103, 107, 110];

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
    public void AtrnIndicator_Period_CanBeChanged()
    {
        var indicator = new AtrnIndicator { Period = 10 };

        Assert.Equal(10, indicator.Period);

        indicator.Period = 20;
        Assert.Equal(20, indicator.Period);
    }

    [Fact]
    public void AtrnIndicator_ShowColdValues_CanBeChanged()
    {
        var indicator = new AtrnIndicator { ShowColdValues = true };

        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);
    }

    [Fact]
    public void AtrnIndicator_ShortName_UpdatesWhenPeriodChanges()
    {
        var indicator = new AtrnIndicator { Period = 10 };
        string initialName = indicator.ShortName;

        Assert.True(initialName.Contains("10", StringComparison.Ordinal));

        indicator.Period = 20;
        string updatedName = indicator.ShortName;

        Assert.True(updatedName.Contains("20", StringComparison.Ordinal));
    }

    [Fact]
    public void AtrnIndicator_LineSeries_HasCorrectProperties()
    {
        var indicator = new AtrnIndicator { Period = 10 };
        indicator.Initialize();

        var lineSeries = indicator.LinesSeries[0];

        Assert.True(lineSeries.Name.Contains("ATRN", StringComparison.Ordinal));
        Assert.Equal(2, lineSeries.Width);
        Assert.Equal(LineStyle.Solid, lineSeries.Style);
    }

    [Fact]
    public void AtrnIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new AtrnIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Atrn.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }
}