using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class GwmaIndicatorTests
{
    [Fact]
    public void GwmaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new GwmaIndicator();

        Assert.Equal(10, indicator.Period);
        Assert.Equal(0.4, indicator.Sigma);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("GWMA - Gaussian-Weighted Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void GwmaIndicator_MinHistoryDepths_ReturnsZero()
    {
        var indicator = new GwmaIndicator { Period = 20 };

        Assert.Equal(0, GwmaIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void GwmaIndicator_ShortName_IncludesPeriodAndSource()
    {
        var indicator = new GwmaIndicator { Period = 15 };

        Assert.Contains("GWMA", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("15", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void GwmaIndicator_Initialize_CreatesInternalGwma()
    {
        var indicator = new GwmaIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void GwmaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new GwmaIndicator { Period = 3 };
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
    public void GwmaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new GwmaIndicator { Period = 3 };
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
    public void GwmaIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new GwmaIndicator { Period = 3 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        // Process historical bar first
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        double firstValue = indicator.LinesSeries[0].GetValue(0);

        // Update with new tick (same bar data - simulates intrabar update)
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        double secondValue = indicator.LinesSeries[0].GetValue(0);

        // Both values should be finite
        Assert.True(double.IsFinite(firstValue));
        Assert.True(double.IsFinite(secondValue));
    }

    [Fact]
    public void GwmaIndicator_MultipleUpdates_ProducesCorrectGwmaSequence()
    {
        var indicator = new GwmaIndicator { Period = 3 };
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

        // GWMA should be smoothing the values
        double lastGwma = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastGwma >= 100 && lastGwma <= 110);
    }

    [Fact]
    public void GwmaIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new GwmaIndicator { Period = 3, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void GwmaIndicator_Period_CanBeChanged()
    {
        var indicator = new GwmaIndicator { Period = 5 };
        Assert.Equal(5, indicator.Period);

        indicator.Period = 20;
        Assert.Equal(20, indicator.Period);
        Assert.Equal(0, GwmaIndicator.MinHistoryDepths);
    }

    [Fact]
    public void GwmaIndicator_Sigma_CanBeChanged()
    {
        var indicator = new GwmaIndicator { Sigma = 0.3 };
        Assert.Equal(0.3, indicator.Sigma);

        indicator.Sigma = 0.7;
        Assert.Equal(0.7, indicator.Sigma);
    }

    [Fact]
    public void GwmaIndicator_DifferentSigmaValues_ProduceDifferentResults()
    {
        var indicatorNarrow = new GwmaIndicator { Period = 5, Sigma = 0.2 };
        var indicatorWide = new GwmaIndicator { Period = 5, Sigma = 0.8 };

        indicatorNarrow.Initialize();
        indicatorWide.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 104, 103, 101 };

        foreach (var close in closes)
        {
            indicatorNarrow.HistoricalData.AddBar(now, close, close + 2, close - 2, close);
            indicatorWide.HistoricalData.AddBar(now, close, close + 2, close - 2, close);
            indicatorNarrow.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            indicatorWide.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        double narrowResult = indicatorNarrow.LinesSeries[0].GetValue(0);
        double wideResult = indicatorWide.LinesSeries[0].GetValue(0);

        // Different sigma should produce different results
        Assert.NotEqual(narrowResult, wideResult);
    }
}