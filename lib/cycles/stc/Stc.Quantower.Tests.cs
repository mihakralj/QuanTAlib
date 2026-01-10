using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class StcIndicatorTests
{
    [Fact]
    public void StcIndicator_Constructor_SetsDefaults()
    {
        var indicator = new StcIndicator();

        Assert.Equal(12, indicator.CycleLength);
        Assert.Equal(26, indicator.FastLength);
        Assert.Equal(50, indicator.SlowLength);
        Assert.Equal(StcSmoothing.Sigmoid, indicator.Smoothing);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("STC - Schaff Trend Cycle", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void StcIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new StcIndicator();

        Assert.Equal(0, StcIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void StcIndicator_ShortName_IncludesParameters()
    {
        var indicator = new StcIndicator
        {
            CycleLength = 10,
            FastLength = 23,
            SlowLength = 50,
            Smoothing = StcSmoothing.Ema
        };

        // Format is "STC {CycleLength}:{FastLength}:{SlowLength}:{Smoothing}:{Source}"
        // e.g. "STC 10:23:50:Ema:Close"
        string shortName = indicator.ShortName;

        Assert.Contains("STC", shortName, StringComparison.Ordinal);
        Assert.Contains("10", shortName, StringComparison.Ordinal);
        Assert.Contains("23", shortName, StringComparison.Ordinal);
        Assert.Contains("50", shortName, StringComparison.Ordinal);
        Assert.Contains("Ema", shortName, StringComparison.Ordinal);
    }

    [Fact]
    public void StcIndicator_Initialize_CreatesInternalStc()
    {
        var indicator = new StcIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
        Assert.Equal("STC", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void StcIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new StcIndicator { CycleLength = 5, FastLength = 10, SlowLength = 20 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;

        // We must feed bars one by one to simulate history for stateful indicators
        for (int i = 0; i < 50; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have values
        Assert.Equal(50, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0))); // GetValue(0) is the most recent
    }

    [Fact]
    public void StcIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new StcIndicator { CycleLength = 5, FastLength = 10, SlowLength = 20 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Feed enough history to warm up
        for (int i = 0; i < 50; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        indicator.HistoricalData.AddBar(now.AddMinutes(50), 102, 108, 100, 106);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.True(indicator.LinesSeries[0].Count > 0);
    }

    [Fact]
    public void StcIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new StcIndicator { CycleLength = 5, FastLength = 10, SlowLength = 20 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Feed warmup bars
        for (int i = 0; i < 50; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double firstValue = indicator.LinesSeries[0].GetValue(0);

        // Update with NewTick (same bar, new price potentially, but reusing last bar in this mock)
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        double secondValue = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(firstValue));
        Assert.True(double.IsFinite(secondValue));
    }

    [Fact]
    public void StcIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new StcIndicator { CycleLength = 10, FastLength = 12, SlowLength = 26 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Generate enough price action to clear warmup (SlowLength + 2*CycleLength = 26 + 20 = 46)
        // We'll generate 100 bars to be safe
        double[] closes = new double[100];
        for (int i = 0; i < 100; i++)
        {
            closes[i] = 100 + Math.Sin(i * 0.1) * 10;
        }

        foreach (var close in closes)
        {
            indicator.HistoricalData.AddBar(now, close, close + 2, close - 2, close);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        // The last value should be finite (we are well past 46)
        double lastVal = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(lastVal));
    }

    [Fact]
    public void StcIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new StcIndicator
            {
                CycleLength = 10,
                FastLength = 23,
                SlowLength = 50,
                Source = source
            };
            indicator.Initialize();

            var now = DateTime.UtcNow;

            // Feed enough bars to produce a value
            // Warmup = 50 + 20 = 70 approx
            for (int i = 0; i < 80; i++)
            {
                indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 105);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void StcIndicator_Parameters_CanBeChanged()
    {
        var indicator = new StcIndicator();

        indicator.CycleLength = 20;
        Assert.Equal(20, indicator.CycleLength);

        indicator.FastLength = 12;
        Assert.Equal(12, indicator.FastLength);

        indicator.SlowLength = 26;
        Assert.Equal(26, indicator.SlowLength);

        indicator.Smoothing = StcSmoothing.Digital;
        Assert.Equal(StcSmoothing.Digital, indicator.Smoothing);
    }
}
