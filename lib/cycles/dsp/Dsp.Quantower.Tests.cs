using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Quantower.Tests;

public class DspIndicatorTests
{
    [Fact]
    public void DspIndicator_Constructor_SetsDefaults()
    {
        var indicator = new DspIndicator();

        Assert.Equal(40, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("DSP - Detrended Synthetic Price", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void DspIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new DspIndicator();

        Assert.Equal(0, DspIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void DspIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new DspIndicator { Period = 20 };

        Assert.True(indicator.ShortName.Contains("DSP", StringComparison.Ordinal));
        Assert.True(indicator.ShortName.Contains("20", StringComparison.Ordinal));
    }

    [Fact]
    public void DspIndicator_Initialize_CreatesInternalDsp()
    {
        var indicator = new DspIndicator { Period = 40 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (DSP + Zero line)
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void DspIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new DspIndicator { Period = 20 };
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
    public void DspIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new DspIndicator { Period = 20 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void DspIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new DspIndicator { Period = 20 };
        indicator.Initialize();

        // Should not throw an exception
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        // Assert that the indicator still exists (method completed without exception)
        Assert.NotNull(indicator);
    }

    [Fact]
    public void DspIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new DspIndicator { Period = 20 };
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
    public void DspIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new DspIndicator { Period = 20, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void DspIndicator_Period_CanBeChanged()
    {
        var indicator = new DspIndicator { Period = 40 };

        Assert.Equal(40, indicator.Period);

        indicator.Period = 20;
        Assert.Equal(20, indicator.Period);
    }

    [Fact]
    public void DspIndicator_Source_CanBeChanged()
    {
        var indicator = new DspIndicator { Source = SourceType.Close };

        Assert.Equal(SourceType.Close, indicator.Source);

        indicator.Source = SourceType.Open;
        Assert.Equal(SourceType.Open, indicator.Source);
    }

    [Fact]
    public void DspIndicator_ShowColdValues_CanBeChanged()
    {
        var indicator = new DspIndicator { ShowColdValues = true };

        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);
    }

    [Fact]
    public void DspIndicator_ShortName_UpdatesWhenPeriodChanges()
    {
        var indicator = new DspIndicator { Period = 40 };
        string initialName = indicator.ShortName;

        Assert.True(initialName.Contains("40", StringComparison.Ordinal));

        indicator.Period = 20;
        string updatedName = indicator.ShortName;

        Assert.True(updatedName.Contains("20", StringComparison.Ordinal));
    }

    [Fact]
    public void DspIndicator_ProcessUpdate_IgnoresNonBarUpdates()
    {
        var indicator = new DspIndicator { Period = 20 };
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
    public void DspIndicator_LineSeries_HasCorrectProperties()
    {
        var indicator = new DspIndicator { Period = 40 };
        indicator.Initialize();

        var lineSeries = indicator.LinesSeries[0];

        Assert.Equal("DSP", lineSeries.Name);
        Assert.Equal(2, lineSeries.Width);
        Assert.Equal(LineStyle.Solid, lineSeries.Style);
    }

    [Fact]
    public void DspIndicator_ZeroLine_HasCorrectProperties()
    {
        var indicator = new DspIndicator { Period = 40 };
        indicator.Initialize();

        var zeroLine = indicator.LinesSeries[1];

        Assert.Equal("Zero", zeroLine.Name);
        Assert.Equal(1, zeroLine.Width);
        Assert.Equal(LineStyle.Dash, zeroLine.Style);
    }

    [Fact]
    public void DspIndicator_DifferentPeriods_Work()
    {
        var periods = new[] { 8, 20, 40, 80 };

        foreach (var period in periods)
        {
            var indicator = new DspIndicator { Period = period };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            // Add enough bars to fill the buffer
            for (int i = 0; i < period + 10; i++)
            {
                double close = 100 + (i % 10);
                indicator.HistoricalData.AddBar(now.AddMinutes(i), close, close + 2, close - 2, close);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            // Last value should be finite
            double dspValue = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(dspValue), $"Period {period} should produce finite value");
        }
    }

    [Fact]
    public void DspIndicator_ConstantPrice_ProducesZeroDsp()
    {
        var indicator = new DspIndicator { Period = 20 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Add constant price bars - need enough for EMAs to converge
        for (int i = 0; i < 500; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 100, 100, 100);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // DSP should be approximately zero for constant price after convergence
        // Tolerance allows for floating-point rounding in EMA bias correction
        double dspValue = indicator.LinesSeries[0].GetValue(0);
        Assert.True(Math.Abs(dspValue) < 0.01, $"Constant price should produce near-zero DSP, got {dspValue}");
    }

    [Fact]
    public void DspIndicator_Uptrend_ProducesPositiveDsp()
    {
        var indicator = new DspIndicator { Period = 20 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Add uptrending price bars
        for (int i = 0; i < 50; i++)
        {
            double price = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // DSP should be positive for uptrend (fast EMA > slow EMA)
        double dspValue = indicator.LinesSeries[0].GetValue(0);
        Assert.True(dspValue > 0, $"Uptrend should produce positive DSP, got {dspValue}");
    }

    [Fact]
    public void DspIndicator_Downtrend_ProducesNegativeDsp()
    {
        var indicator = new DspIndicator { Period = 20 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Add downtrending price bars
        for (int i = 0; i < 50; i++)
        {
            double price = 200 - i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // DSP should be negative for downtrend (fast EMA < slow EMA)
        double dspValue = indicator.LinesSeries[0].GetValue(0);
        Assert.True(dspValue < 0, $"Downtrend should produce negative DSP, got {dspValue}");
    }

    [Fact]
    public void DspIndicator_OscillatesAroundZero_ForSineWave()
    {
        var indicator = new DspIndicator { Period = 20 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        var values = new List<double>();

        // Generate sine wave price pattern
        for (int i = 0; i < 100; i++)
        {
            double price = 100.0 + 10.0 * Math.Sin(i * 0.1);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            values.Add(indicator.LinesSeries[0].GetValue(0));
        }

        // Should have both positive and negative values
        int positiveCount = values.Count(v => v > 0);
        int negativeCount = values.Count(v => v < 0);

        Assert.True(positiveCount > 0, "Should have positive DSP values");
        Assert.True(negativeCount > 0, "Should have negative DSP values");
    }
}