using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Quantower.Tests;

public class EbswIndicatorTests
{
    [Fact]
    public void EbswIndicator_Constructor_SetsDefaults()
    {
        var indicator = new EbswIndicator();

        Assert.Equal(40, indicator.HpLength);
        Assert.Equal(10, indicator.SsfLength);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("EBSW - Ehlers Even Better Sinewave", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void EbswIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new EbswIndicator();

        Assert.Equal(0, EbswIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void EbswIndicator_ShortName_IncludesParameters()
    {
        var indicator = new EbswIndicator { HpLength = 20, SsfLength = 5 };

        Assert.True(indicator.ShortName.Contains("EBSW", StringComparison.Ordinal));
        Assert.True(indicator.ShortName.Contains("20", StringComparison.Ordinal));
        Assert.True(indicator.ShortName.Contains("5", StringComparison.Ordinal));
    }

    [Fact]
    public void EbswIndicator_Initialize_CreatesInternalEbsw()
    {
        var indicator = new EbswIndicator { HpLength = 40, SsfLength = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (EBSW + Zero + Upper + Lower lines)
        Assert.Equal(4, indicator.LinesSeries.Count);
    }

    [Fact]
    public void EbswIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new EbswIndicator { HpLength = 20, SsfLength = 5 };
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
    public void EbswIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new EbswIndicator { HpLength = 20, SsfLength = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void EbswIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new EbswIndicator { HpLength = 20, SsfLength = 5 };
        indicator.Initialize();

        // Should not throw an exception
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        // Assert that the indicator still exists (method completed without exception)
        Assert.NotNull(indicator);
    }

    [Fact]
    public void EbswIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new EbswIndicator { HpLength = 20, SsfLength = 5 };
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
    public void EbswIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new EbswIndicator { HpLength = 20, SsfLength = 5, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void EbswIndicator_HpLength_CanBeChanged()
    {
        var indicator = new EbswIndicator { HpLength = 40 };

        Assert.Equal(40, indicator.HpLength);

        indicator.HpLength = 20;
        Assert.Equal(20, indicator.HpLength);
    }

    [Fact]
    public void EbswIndicator_SsfLength_CanBeChanged()
    {
        var indicator = new EbswIndicator { SsfLength = 10 };

        Assert.Equal(10, indicator.SsfLength);

        indicator.SsfLength = 5;
        Assert.Equal(5, indicator.SsfLength);
    }

    [Fact]
    public void EbswIndicator_Source_CanBeChanged()
    {
        var indicator = new EbswIndicator { Source = SourceType.Close };

        Assert.Equal(SourceType.Close, indicator.Source);

        indicator.Source = SourceType.Open;
        Assert.Equal(SourceType.Open, indicator.Source);
    }

    [Fact]
    public void EbswIndicator_ShowColdValues_CanBeChanged()
    {
        var indicator = new EbswIndicator { ShowColdValues = true };

        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);
    }

    [Fact]
    public void EbswIndicator_ShortName_UpdatesWhenParametersChange()
    {
        var indicator = new EbswIndicator { HpLength = 40, SsfLength = 10 };
        string initialName = indicator.ShortName;

        Assert.True(initialName.Contains("40", StringComparison.Ordinal));
        Assert.True(initialName.Contains("10", StringComparison.Ordinal));

        indicator.HpLength = 20;
        indicator.SsfLength = 5;
        string updatedName = indicator.ShortName;

        Assert.True(updatedName.Contains("20", StringComparison.Ordinal));
        Assert.True(updatedName.Contains("5", StringComparison.Ordinal));
    }

    [Fact]
    public void EbswIndicator_ProcessUpdate_IgnoresNonBarUpdates()
    {
        var indicator = new EbswIndicator { HpLength = 20, SsfLength = 5 };
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
    public void EbswIndicator_LineSeries_HasCorrectProperties()
    {
        var indicator = new EbswIndicator { HpLength = 40, SsfLength = 10 };
        indicator.Initialize();

        var lineSeries = indicator.LinesSeries[0];

        Assert.Equal("EBSW", lineSeries.Name);
        Assert.Equal(2, lineSeries.Width);
        Assert.Equal(LineStyle.Solid, lineSeries.Style);
    }

    [Fact]
    public void EbswIndicator_ZeroLine_HasCorrectProperties()
    {
        var indicator = new EbswIndicator { HpLength = 40, SsfLength = 10 };
        indicator.Initialize();

        var zeroLine = indicator.LinesSeries[1];

        Assert.Equal("Zero", zeroLine.Name);
        Assert.Equal(1, zeroLine.Width);
        Assert.Equal(LineStyle.Dash, zeroLine.Style);
    }

    [Fact]
    public void EbswIndicator_BoundaryLines_HasCorrectProperties()
    {
        var indicator = new EbswIndicator { HpLength = 40, SsfLength = 10 };
        indicator.Initialize();

        var upperLine = indicator.LinesSeries[2];
        var lowerLine = indicator.LinesSeries[3];

        Assert.Equal("+1", upperLine.Name);
        Assert.Equal("-1", lowerLine.Name);
        Assert.Equal(LineStyle.Dot, upperLine.Style);
        Assert.Equal(LineStyle.Dot, lowerLine.Style);
    }

    [Fact]
    public void EbswIndicator_DifferentParameters_Work()
    {
        var paramSets = new[] { (10, 3), (20, 5), (40, 10), (80, 20) };

        foreach (var (hpLength, ssfLength) in paramSets)
        {
            var indicator = new EbswIndicator { HpLength = hpLength, SsfLength = ssfLength };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            // Add enough bars to fill the buffer
            for (int i = 0; i < hpLength + 10; i++)
            {
                double close = 100 + (i % 10);
                indicator.HistoricalData.AddBar(now.AddMinutes(i), close, close + 2, close - 2, close);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            // Last value should be finite
            double ebswValue = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(ebswValue), $"HP {hpLength}, SSF {ssfLength} should produce finite value");
        }
    }

    [Fact]
    public void EbswIndicator_ConstantPrice_ProducesBoundedOutput()
    {
        var indicator = new EbswIndicator { HpLength = 20, SsfLength = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Add constant price bars
        for (int i = 0; i < 500; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 100, 100, 100);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // AGC normalizes output to [-1, +1] even for constant input
        // (high-pass filter → 0, but AGC normalizes tiny residuals to ±1)
        double ebswValue = indicator.LinesSeries[0].GetValue(0);
        Assert.True(ebswValue >= -1.0 && ebswValue <= 1.0,
            $"EBSW value {ebswValue} should be in [-1, +1]");
    }

    [Fact]
    public void EbswIndicator_OutputBounded_BetweenNegativeOneAndOne()
    {
        var indicator = new EbswIndicator { HpLength = 20, SsfLength = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Add varying price bars
        for (int i = 0; i < 100; i++)
        {
            double price = 100 + (20 * Math.Sin(i * 0.2));
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            double ebswValue = indicator.LinesSeries[0].GetValue(0);
            Assert.True(ebswValue >= -1.0 && ebswValue <= 1.0,
                $"EBSW value {ebswValue} should be in [-1, +1]");
        }
    }

    [Fact]
    public void EbswIndicator_OscillatesAroundZero_ForSineWave()
    {
        var indicator = new EbswIndicator { HpLength = 40, SsfLength = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        var values = new List<double>();

        // Generate sine wave price pattern
        for (int i = 0; i < 200; i++)
        {
            double price = 100.0 + (10.0 * Math.Sin(i * 0.1));
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            values.Add(indicator.LinesSeries[0].GetValue(0));
        }

        // Should have both positive and negative values
        int positiveCount = values.Count(v => v > 0);
        int negativeCount = values.Count(v => v < 0);

        Assert.True(positiveCount > 0, "Should have positive EBSW values");
        Assert.True(negativeCount > 0, "Should have negative EBSW values");
    }

    [Fact]
    public void EbswIndicator_ZeroCrossings_IndicateCyclePhase()
    {
        var indicator = new EbswIndicator { HpLength = 20, SsfLength = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        var values = new List<double>();

        // Generate sine wave price pattern
        for (int i = 0; i < 200; i++)
        {
            double price = 100.0 + (10.0 * Math.Sin(i * 0.15));
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            values.Add(indicator.LinesSeries[0].GetValue(0));
        }

        // Count zero crossings
        int crossings = 0;
        for (int i = 1; i < values.Count; i++)
        {
            if (values[i - 1] * values[i] < 0)
            {
                crossings++;
            }
        }

        // Should have multiple zero crossings for oscillating price
        Assert.True(crossings >= 3, $"Should have multiple zero crossings, got {crossings}");
    }
}
