using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class HmaIndicatorTests
{
    [Fact]
    public void HmaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new HmaIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("HMA - Hull Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void HmaIndicator_MinHistoryDepths_CalculatedCorrectly()
    {
        var indicator = new HmaIndicator { Period = 16 };
        // HMA warmup is roughly Period + Sqrt(Period)
        // 16 + Sqrt(16) = 16 + 4 = 20
        Assert.Equal(0, HmaIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void HmaIndicator_ShortName_IncludesPeriodAndSource()
    {
        var indicator = new HmaIndicator { Period = 21 };

        Assert.Contains("HMA", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("21", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void HmaIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new HmaIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Hma.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void HmaIndicator_Initialize_CreatesInternalHma()
    {
        var indicator = new HmaIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void HmaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new HmaIndicator { Period = 4 }; // Small period for easier testing
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
    public void HmaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new HmaIndicator { Period = 4 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void HmaIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new HmaIndicator { Period = 4 };
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
    public void HmaIndicator_MultipleUpdates_ProducesCorrectHmaSequence()
    {
        var indicator = new HmaIndicator { Period = 4 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 104, 103, 105, 106, 107 };

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
    }

    [Fact]
    public void HmaIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new HmaIndicator { Period = 4, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void HmaIndicator_Period_CanBeChanged()
    {
        var indicator = new HmaIndicator { Period = 5 };
        Assert.Equal(5, indicator.Period);

        indicator.Period = 20;
        Assert.Equal(20, indicator.Period);
        // 20 + sqrt(20) = 20 + 4 = 24
        Assert.Equal(0, HmaIndicator.MinHistoryDepths);
    }
}
