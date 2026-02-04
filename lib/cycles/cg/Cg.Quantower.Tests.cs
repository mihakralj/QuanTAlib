using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Quantower.Tests;

public class CgIndicatorTests
{
    [Fact]
    public void CgIndicator_Constructor_SetsDefaults()
    {
        var indicator = new CgIndicator();

        Assert.Equal(10, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("CG - Center of Gravity", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void CgIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new CgIndicator();

        Assert.Equal(0, CgIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void CgIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new CgIndicator { Period = 14 };

        Assert.True(indicator.ShortName.Contains("CG", StringComparison.Ordinal));
        Assert.True(indicator.ShortName.Contains("14", StringComparison.Ordinal));
    }

    [Fact]
    public void CgIndicator_Initialize_CreatesInternalCg()
    {
        var indicator = new CgIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (CG + Zero line)
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void CgIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new CgIndicator { Period = 5 };
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
    public void CgIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new CgIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void CgIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new CgIndicator { Period = 5 };
        indicator.Initialize();

        // Should not throw an exception
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        // Assert that the indicator still exists (method completed without exception)
        Assert.NotNull(indicator);
    }

    [Fact]
    public void CgIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new CgIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 105, 103, 107, 110 };

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
    public void CgIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new CgIndicator { Period = 5, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void CgIndicator_Period_CanBeChanged()
    {
        var indicator = new CgIndicator { Period = 10 };

        Assert.Equal(10, indicator.Period);

        indicator.Period = 20;
        Assert.Equal(20, indicator.Period);
    }

    [Fact]
    public void CgIndicator_Source_CanBeChanged()
    {
        var indicator = new CgIndicator { Source = SourceType.Close };

        Assert.Equal(SourceType.Close, indicator.Source);

        indicator.Source = SourceType.Open;
        Assert.Equal(SourceType.Open, indicator.Source);
    }

    [Fact]
    public void CgIndicator_ShowColdValues_CanBeChanged()
    {
        var indicator = new CgIndicator { ShowColdValues = true };

        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);
    }

    [Fact]
    public void CgIndicator_ShortName_UpdatesWhenPeriodChanges()
    {
        var indicator = new CgIndicator { Period = 10 };
        string initialName = indicator.ShortName;

        Assert.True(initialName.Contains("10", StringComparison.Ordinal));

        indicator.Period = 20;
        string updatedName = indicator.ShortName;

        Assert.True(updatedName.Contains("20", StringComparison.Ordinal));
    }

    [Fact]
    public void CgIndicator_ProcessUpdate_IgnoresNonBarUpdates()
    {
        var indicator = new CgIndicator { Period = 5 };
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
    public void CgIndicator_LineSeries_HasCorrectProperties()
    {
        var indicator = new CgIndicator { Period = 10 };
        indicator.Initialize();

        var lineSeries = indicator.LinesSeries[0];

        Assert.Equal("CG", lineSeries.Name);
        Assert.Equal(2, lineSeries.Width);
        Assert.Equal(LineStyle.Solid, lineSeries.Style);
    }

    [Fact]
    public void CgIndicator_ZeroLine_HasCorrectProperties()
    {
        var indicator = new CgIndicator { Period = 10 };
        indicator.Initialize();

        var zeroLine = indicator.LinesSeries[1];

        Assert.Equal("Zero", zeroLine.Name);
        Assert.Equal(1, zeroLine.Width);
        Assert.Equal(LineStyle.Dash, zeroLine.Style);
    }

    [Fact]
    public void CgIndicator_DifferentPeriods_Work()
    {
        var periods = new[] { 5, 10, 20, 50 };

        foreach (var period in periods)
        {
            var indicator = new CgIndicator { Period = period };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            // Add enough bars to fill the buffer
            for (int i = 0; i < period + 5; i++)
            {
                double close = 100 + (i % 10);
                indicator.HistoricalData.AddBar(now.AddMinutes(i), close, close + 2, close - 2, close);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            // Last value should be finite
            double cgValue = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(cgValue), $"Period {period} should produce finite value");
        }
    }

    [Fact]
    public void CgIndicator_CgValuesAreBounded()
    {
        var indicator = new CgIndicator { Period = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 98, 105, 97, 110, 95, 108, 92, 115, 90, 120 };
        double maxExpectedBound = (10 - 1) / 2.0 + 1.0; // Period-based bound with margin

        foreach (var close in closes)
        {
            indicator.HistoricalData.AddBar(now, close, close + 5, close - 5, close);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        // All CG values should be bounded based on period
        for (int i = 0; i < closes.Length; i++)
        {
            double value = indicator.LinesSeries[0].GetValue(closes.Length - 1 - i);
            Assert.True(Math.Abs(value) <= maxExpectedBound, 
                $"CG value at index {i} should be bounded ±{maxExpectedBound}, got {value}");
        }
    }

    [Fact]
    public void CgIndicator_ConstantPrice_ProducesZeroCg()
    {
        var indicator = new CgIndicator { Period = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Add constant price bars
        for (int i = 0; i < 15; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 100, 100, 100);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // CG should be approximately zero for constant price
        double cgValue = indicator.LinesSeries[0].GetValue(0);
        Assert.True(Math.Abs(cgValue) < 1e-9, $"Constant price should produce zero CG, got {cgValue}");
    }

    [Fact]
    public void CgIndicator_Uptrend_ProducesPositiveCg()
    {
        var indicator = new CgIndicator { Period = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Add uptrending price bars
        for (int i = 0; i < 15; i++)
        {
            double price = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // CG should be positive for uptrend
        double cgValue = indicator.LinesSeries[0].GetValue(0);
        Assert.True(cgValue > 0, $"Uptrend should produce positive CG, got {cgValue}");
    }

    [Fact]
    public void CgIndicator_Downtrend_ProducesNegativeCg()
    {
        var indicator = new CgIndicator { Period = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Add downtrending price bars
        for (int i = 0; i < 15; i++)
        {
            double price = 200 - i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // CG should be negative for downtrend
        double cgValue = indicator.LinesSeries[0].GetValue(0);
        Assert.True(cgValue < 0, $"Downtrend should produce negative CG, got {cgValue}");
    }
}