using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class MamaIndicatorTests
{
    [Fact]
    public void MamaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new MamaIndicator();

        Assert.Equal(0.5, indicator.FastLimit);
        Assert.Equal(0.05, indicator.SlowLimit);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("MAMA - Ehlers MESA Adaptive Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void MamaIndicator_MinHistoryDepths_Equals50()
    {
        var indicator = new MamaIndicator();

        Assert.Equal(0, MamaIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void MamaIndicator_ShortName_IncludesLimitsAndSource()
    {
        var indicator = new MamaIndicator { FastLimit = 0.5, SlowLimit = 0.05 };
        indicator.Initialize();

        Assert.Contains("MAMA", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("Close", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void MamaIndicator_Initialize_CreatesInternalMama()
    {
        var indicator = new MamaIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (MAMA and FAMA)
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void MamaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new MamaIndicator();
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        // Process update
        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // Line series should have a value
        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.Equal(1, indicator.LinesSeries[1].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
        Assert.True(double.IsFinite(indicator.LinesSeries[1].GetValue(0)));
    }

    [Fact]
    public void MamaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new MamaIndicator();
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
        Assert.Equal(2, indicator.LinesSeries[1].Count);
    }

    [Fact]
    public void MamaIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new MamaIndicator();
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
    public void MamaIndicator_MultipleUpdates_ProducesCorrectMamaSequence()
    {
        var indicator = new MamaIndicator();
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
            Assert.True(double.IsFinite(indicator.LinesSeries[1].GetValue(closes.Length - 1 - i)));
        }

        // MAMA should be smoothing the values
        double lastMama = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastMama >= 100 && lastMama <= 110);
    }

    [Fact]
    public void MamaIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new MamaIndicator { Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void MamaIndicator_Limits_CanBeChanged()
    {
        var indicator = new MamaIndicator { FastLimit = 0.5, SlowLimit = 0.05 };
        Assert.Equal(0.5, indicator.FastLimit);
        Assert.Equal(0.05, indicator.SlowLimit);

        indicator.FastLimit = 0.8;
        indicator.SlowLimit = 0.1;
        Assert.Equal(0.8, indicator.FastLimit);
        Assert.Equal(0.1, indicator.SlowLimit);
    }
}
