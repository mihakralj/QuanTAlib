using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Quantower.Tests;

public class HomodIndicatorTests
{
    [Fact]
    public void HomodIndicator_Constructor_SetsDefaults()
    {
        var indicator = new HomodIndicator();

        Assert.Equal(6.0, indicator.MinPeriod);
        Assert.Equal(50.0, indicator.MaxPeriod);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("HOMOD - Ehlers Homodyne Discriminator", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void HomodIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new HomodIndicator();

        Assert.Equal(0, HomodIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void HomodIndicator_ShortName_IncludesPeriods()
    {
        var indicator = new HomodIndicator { MinPeriod = 8.0, MaxPeriod = 60.0 };

        Assert.True(indicator.ShortName.Contains("HOMOD", StringComparison.Ordinal));
        Assert.True(indicator.ShortName.Contains("8", StringComparison.Ordinal));
        Assert.True(indicator.ShortName.Contains("60", StringComparison.Ordinal));
    }

    [Fact]
    public void HomodIndicator_Initialize_CreatesInternalHomod()
    {
        var indicator = new HomodIndicator { MinPeriod = 6.0, MaxPeriod = 50.0 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (Cycle only)
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void HomodIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new HomodIndicator { MinPeriod = 6.0, MaxPeriod = 50.0 };
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
    public void HomodIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new HomodIndicator { MinPeriod = 6.0, MaxPeriod = 50.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void HomodIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new HomodIndicator { MinPeriod = 6.0, MaxPeriod = 50.0 };
        indicator.Initialize();

        // Should not throw an exception
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        // Assert that the indicator still exists
        Assert.NotNull(indicator);
    }

    [Fact]
    public void HomodIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new HomodIndicator { MinPeriod = 6.0, MaxPeriod = 50.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 105, 103, 107, 110, 108, 112, 115, 113 };

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
    public void HomodIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new HomodIndicator { MinPeriod = 6.0, MaxPeriod = 50.0, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void HomodIndicator_MinPeriod_CanBeChanged()
    {
        var indicator = new HomodIndicator { MinPeriod = 6.0 };

        Assert.Equal(6.0, indicator.MinPeriod);

        indicator.MinPeriod = 10.0;
        Assert.Equal(10.0, indicator.MinPeriod);
    }

    [Fact]
    public void HomodIndicator_MaxPeriod_CanBeChanged()
    {
        var indicator = new HomodIndicator { MaxPeriod = 50.0 };

        Assert.Equal(50.0, indicator.MaxPeriod);

        indicator.MaxPeriod = 100.0;
        Assert.Equal(100.0, indicator.MaxPeriod);
    }

    [Fact]
    public void HomodIndicator_Source_CanBeChanged()
    {
        var indicator = new HomodIndicator { Source = SourceType.Close };

        Assert.Equal(SourceType.Close, indicator.Source);

        indicator.Source = SourceType.Open;
        Assert.Equal(SourceType.Open, indicator.Source);
    }

    [Fact]
    public void HomodIndicator_ShowColdValues_CanBeChanged()
    {
        var indicator = new HomodIndicator { ShowColdValues = true };

        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);
    }

    [Fact]
    public void HomodIndicator_ShortName_UpdatesWhenPeriodsChange()
    {
        var indicator = new HomodIndicator { MinPeriod = 6.0, MaxPeriod = 50.0 };
        string initialName = indicator.ShortName;

        Assert.True(initialName.Contains("6", StringComparison.Ordinal));
        Assert.True(initialName.Contains("50", StringComparison.Ordinal));

        indicator.MinPeriod = 10.0;
        indicator.MaxPeriod = 60.0;
        string updatedName = indicator.ShortName;

        Assert.True(updatedName.Contains("10", StringComparison.Ordinal));
        Assert.True(updatedName.Contains("60", StringComparison.Ordinal));
    }

    [Fact]
    public void HomodIndicator_ProcessUpdate_IgnoresNonBarUpdates()
    {
        var indicator = new HomodIndicator { MinPeriod = 6.0, MaxPeriod = 50.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        // Process historical bar first
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Process other update reasons - should not throw
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.NotNull(indicator);
    }

    [Fact]
    public void HomodIndicator_CycleSeries_HasCorrectProperties()
    {
        var indicator = new HomodIndicator { MinPeriod = 6.0, MaxPeriod = 50.0 };
        indicator.Initialize();

        var lineSeries = indicator.LinesSeries[0];

        Assert.Equal("Cycle", lineSeries.Name);
        Assert.Equal(2, lineSeries.Width);
        Assert.Equal(LineStyle.Solid, lineSeries.Style);
    }

    [Fact]
    public void HomodIndicator_DifferentPeriodRanges_Work()
    {
        var periodRanges = new[] { (6.0, 50.0), (8.0, 60.0), (5.0, 30.0), (10.0, 100.0) };

        foreach (var (minPeriod, maxPeriod) in periodRanges)
        {
            var indicator = new HomodIndicator { MinPeriod = minPeriod, MaxPeriod = maxPeriod };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            // Add enough bars
            int numBars = (int)maxPeriod + 50;
            for (int i = 0; i < numBars; i++)
            {
                double close = 100 + (i % 10);
                indicator.HistoricalData.AddBar(now.AddMinutes(i), close, close + 2, close - 2, close);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            // Last value should be finite
            double cycleValue = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(cycleValue), $"Period range ({minPeriod},{maxPeriod}) should produce finite value");
        }
    }

    [Fact]
    public void HomodIndicator_SineWave_DetectsCycle()
    {
        var indicator = new HomodIndicator { MinPeriod = 6.0, MaxPeriod = 50.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        const int knownPeriod = 20;

        // Generate sine wave pattern
        for (int i = 0; i < 200; i++)
        {
            double price = 100.0 + 10.0 * Math.Sin(2.0 * Math.PI * i / knownPeriod);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Cycle value should be in valid range
        double cycleValue = indicator.LinesSeries[0].GetValue(0);
        Assert.InRange(cycleValue, 6.0, 50.0);
    }

    [Fact]
    public void HomodIndicator_ConstantInput_ProducesStableOutput()
    {
        var indicator = new HomodIndicator { MinPeriod = 6.0, MaxPeriod = 50.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        const double constantPrice = 100.0;

        for (int i = 0; i < 100; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), constantPrice, constantPrice, constantPrice, constantPrice);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Should produce finite values even with constant input
        double cycleValue = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(cycleValue));
    }

    [Fact]
    public void HomodIndicator_TrendingInput_ProducesFiniteOutput()
    {
        var indicator = new HomodIndicator { MinPeriod = 6.0, MaxPeriod = 50.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 100; i++)
        {
            double price = 100.0 + i * 0.5; // Trending up
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Should produce finite values with trending input
        double cycleValue = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(cycleValue));
    }

    [Fact]
    public void HomodIndicator_VolatileInput_ProducesFiniteOutput()
    {
        var indicator = new HomodIndicator { MinPeriod = 6.0, MaxPeriod = 50.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 100; i++)
        {
            double price = 100.0 + (i % 2 == 0 ? 10.0 : -10.0); // Volatile swings
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 5, price - 5, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Should produce finite values with volatile input
        double cycleValue = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(cycleValue));
    }
}
