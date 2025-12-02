using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class SmaIndicatorTests
{
    [Fact]
    public void SmaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new SmaIndicator();

        Assert.Equal(10, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("SMA - Simple Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void SmaIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new SmaIndicator { Period = 20 };

        Assert.Equal(20, indicator.MinHistoryDepths);
        Assert.Equal(20, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void SmaIndicator_ShortName_IncludesPeriodAndSource()
    {
        var indicator = new SmaIndicator { Period = 15 };

        Assert.Contains("SMA", indicator.ShortName);
        Assert.Contains("15", indicator.ShortName);
    }

    [Fact]
    public void SmaIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new SmaIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink);
        Assert.Contains("Sma.Quantower.cs", indicator.SourceCodeLink);
    }

    [Fact]
    public void SmaIndicator_Initialize_CreatesInternalSma()
    {
        var indicator = new SmaIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void SmaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new SmaIndicator { Period = 3 };
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
    public void SmaIndicator_MultipleUpdates_ProducesCorrectSmaSequence()
    {
        var indicator = new SmaIndicator { Period = 3 };
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

        // Last SMA(3) should be average of last 3 values: (103 + 105 + 104) / 3 ≈ 104
        // Actually: (104 + 103 + 105) / 3 = 104
        double lastSma = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastSma >= 103 && lastSma <= 105);
    }

    [Fact]
    public void SmaIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new SmaIndicator { Period = 3, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void SmaIndicator_Period_CanBeChanged()
    {
        var indicator = new SmaIndicator { Period = 5 };
        Assert.Equal(5, indicator.Period);

        indicator.Period = 20;
        Assert.Equal(20, indicator.Period);
        Assert.Equal(20, indicator.MinHistoryDepths);
    }
}
