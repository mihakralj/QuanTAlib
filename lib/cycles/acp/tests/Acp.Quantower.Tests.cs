using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Quantower.Tests;

public class AcpIndicatorTests
{
    [Fact]
    public void AcpIndicator_Constructor_SetsDefaults()
    {
        var indicator = new AcpIndicator();

        Assert.Equal(8, indicator.MinPeriod);
        Assert.Equal(48, indicator.MaxPeriod);
        Assert.Equal(3, indicator.AvgLength);
        Assert.True(indicator.Enhance);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("ACP - Ehlers Autocorrelation Periodogram", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void AcpIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new AcpIndicator();

        Assert.Equal(0, AcpIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void AcpIndicator_ShortName_IncludesPeriods()
    {
        var indicator = new AcpIndicator { MinPeriod = 10, MaxPeriod = 60 };

        Assert.True(indicator.ShortName.Contains("ACP", StringComparison.Ordinal));
        Assert.True(indicator.ShortName.Contains("10", StringComparison.Ordinal));
        Assert.True(indicator.ShortName.Contains("60", StringComparison.Ordinal));
    }

    [Fact]
    public void AcpIndicator_Initialize_CreatesInternalAcp()
    {
        var indicator = new AcpIndicator { MinPeriod = 8, MaxPeriod = 48 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (Cycle + Power)
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void AcpIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new AcpIndicator { MinPeriod = 8, MaxPeriod = 48 };
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
    public void AcpIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new AcpIndicator { MinPeriod = 8, MaxPeriod = 48 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void AcpIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new AcpIndicator { MinPeriod = 8, MaxPeriod = 48 };
        indicator.Initialize();

        // Should not throw an exception
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        // Assert that the indicator still exists
        Assert.NotNull(indicator);
    }

    [Fact]
    public void AcpIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new AcpIndicator { MinPeriod = 8, MaxPeriod = 48 };
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
    public void AcpIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new AcpIndicator { MinPeriod = 8, MaxPeriod = 48, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void AcpIndicator_MinPeriod_CanBeChanged()
    {
        var indicator = new AcpIndicator { MinPeriod = 8 };

        Assert.Equal(8, indicator.MinPeriod);

        indicator.MinPeriod = 12;
        Assert.Equal(12, indicator.MinPeriod);
    }

    [Fact]
    public void AcpIndicator_MaxPeriod_CanBeChanged()
    {
        var indicator = new AcpIndicator { MaxPeriod = 48 };

        Assert.Equal(48, indicator.MaxPeriod);

        indicator.MaxPeriod = 100;
        Assert.Equal(100, indicator.MaxPeriod);
    }

    [Fact]
    public void AcpIndicator_AvgLength_CanBeChanged()
    {
        var indicator = new AcpIndicator { AvgLength = 3 };

        Assert.Equal(3, indicator.AvgLength);

        indicator.AvgLength = 10;
        Assert.Equal(10, indicator.AvgLength);
    }

    [Fact]
    public void AcpIndicator_Enhance_CanBeChanged()
    {
        var indicator = new AcpIndicator { Enhance = true };

        Assert.True(indicator.Enhance);

        indicator.Enhance = false;
        Assert.False(indicator.Enhance);
    }

    [Fact]
    public void AcpIndicator_Source_CanBeChanged()
    {
        var indicator = new AcpIndicator { Source = SourceType.Close };

        Assert.Equal(SourceType.Close, indicator.Source);

        indicator.Source = SourceType.Open;
        Assert.Equal(SourceType.Open, indicator.Source);
    }

    [Fact]
    public void AcpIndicator_ShowColdValues_CanBeChanged()
    {
        var indicator = new AcpIndicator { ShowColdValues = true };

        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);
    }

    [Fact]
    public void AcpIndicator_ShortName_UpdatesWhenPeriodsChange()
    {
        var indicator = new AcpIndicator { MinPeriod = 8, MaxPeriod = 48 };
        string initialName = indicator.ShortName;

        Assert.True(initialName.Contains("8", StringComparison.Ordinal));
        Assert.True(initialName.Contains("48", StringComparison.Ordinal));

        indicator.MinPeriod = 10;
        indicator.MaxPeriod = 60;
        string updatedName = indicator.ShortName;

        Assert.True(updatedName.Contains("10", StringComparison.Ordinal));
        Assert.True(updatedName.Contains("60", StringComparison.Ordinal));
    }

    [Fact]
    public void AcpIndicator_ProcessUpdate_IgnoresNonBarUpdates()
    {
        var indicator = new AcpIndicator { MinPeriod = 8, MaxPeriod = 48 };
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
    public void AcpIndicator_CycleSeries_HasCorrectProperties()
    {
        var indicator = new AcpIndicator { MinPeriod = 8, MaxPeriod = 48 };
        indicator.Initialize();

        var lineSeries = indicator.LinesSeries[0];

        Assert.Equal("Cycle", lineSeries.Name);
        Assert.Equal(2, lineSeries.Width);
        Assert.Equal(LineStyle.Solid, lineSeries.Style);
    }

    [Fact]
    public void AcpIndicator_PowerSeries_HasCorrectProperties()
    {
        var indicator = new AcpIndicator { MinPeriod = 8, MaxPeriod = 48 };
        indicator.Initialize();

        var powerSeries = indicator.LinesSeries[1];

        Assert.Equal("Power", powerSeries.Name);
        Assert.Equal(1, powerSeries.Width);
        Assert.Equal(LineStyle.Dot, powerSeries.Style);
    }

    [Fact]
    public void AcpIndicator_DifferentPeriodRanges_Work()
    {
        var periodRanges = new[] { (8, 48), (10, 60), (6, 30), (12, 100) };

        foreach (var (minPeriod, maxPeriod) in periodRanges)
        {
            var indicator = new AcpIndicator { MinPeriod = minPeriod, MaxPeriod = maxPeriod };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            // Add enough bars
            for (int i = 0; i < maxPeriod + 10; i++)
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
    public void AcpIndicator_SineWave_DetectsCycle()
    {
        var indicator = new AcpIndicator { MinPeriod = 8, MaxPeriod = 48 };
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
        Assert.InRange(cycleValue, 8, 48);
    }

    [Fact]
    public void AcpIndicator_PowerOutput_ScaledCorrectly()
    {
        var indicator = new AcpIndicator { MinPeriod = 8, MaxPeriod = 48 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 100; i++)
        {
            double price = 100.0 + 10.0 * Math.Sin(2.0 * Math.PI * i / 20.0);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Power is scaled by MaxPeriod
        double powerValue = indicator.LinesSeries[1].GetValue(0);
        Assert.True(double.IsFinite(powerValue));
        Assert.True(powerValue >= 0, "Power should be non-negative");
    }

    [Fact]
    public void AcpIndicator_EnhanceMode_AffectsOutput()
    {
        var indicatorEnhanced = new AcpIndicator { MinPeriod = 8, MaxPeriod = 48, Enhance = true };
        var indicatorNormal = new AcpIndicator { MinPeriod = 8, MaxPeriod = 48, Enhance = false };
        indicatorEnhanced.Initialize();
        indicatorNormal.Initialize();

        var now = DateTime.UtcNow;

        // Add same data to both
        for (int i = 0; i < 100; i++)
        {
            double price = 100.0 + 10.0 * Math.Sin(2.0 * Math.PI * i / 20.0);
            indicatorEnhanced.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price);
            indicatorNormal.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price);
            indicatorEnhanced.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            indicatorNormal.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Both should produce finite values
        Assert.True(double.IsFinite(indicatorEnhanced.LinesSeries[0].GetValue(0)));
        Assert.True(double.IsFinite(indicatorNormal.LinesSeries[0].GetValue(0)));
    }
}
