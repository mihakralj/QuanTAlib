using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Quantower.Tests;

public class SsfdspIndicatorTests
{
    [Fact]
    public void SsfdspIndicator_Constructor_SetsDefaults()
    {
        var indicator = new SsfdspIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("SSFDSP - Ehlers SSF Detrended Synthetic Price", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void SsfdspIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new SsfdspIndicator();

        Assert.Equal(0, SsfdspIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void SsfdspIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new SsfdspIndicator { Period = 30 };

        Assert.True(indicator.ShortName.Contains("SSFDSP", StringComparison.Ordinal));
        Assert.True(indicator.ShortName.Contains("30", StringComparison.Ordinal));
    }

    [Fact]
    public void SsfdspIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new SsfdspIndicator { Period = 20 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (SSFDSP + Zero lines)
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void SsfdspIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new SsfdspIndicator { Period = 20 };
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
    public void SsfdspIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new SsfdspIndicator { Period = 20 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void SsfdspIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new SsfdspIndicator { Period = 20 };
        indicator.Initialize();

        // Should not throw an exception
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        // Assert that the indicator still exists (method completed without exception)
        Assert.NotNull(indicator);
    }

    [Fact]
    public void SsfdspIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new SsfdspIndicator { Period = 20 };
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
    public void SsfdspIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new SsfdspIndicator { Period = 20, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void SsfdspIndicator_Period_CanBeChanged()
    {
        var indicator = new SsfdspIndicator { Period = 20 };

        Assert.Equal(20, indicator.Period);

        indicator.Period = 40;
        Assert.Equal(40, indicator.Period);
    }

    [Fact]
    public void SsfdspIndicator_Source_CanBeChanged()
    {
        var indicator = new SsfdspIndicator { Source = SourceType.Close };

        Assert.Equal(SourceType.Close, indicator.Source);

        indicator.Source = SourceType.Open;
        Assert.Equal(SourceType.Open, indicator.Source);
    }

    [Fact]
    public void SsfdspIndicator_ShowColdValues_CanBeChanged()
    {
        var indicator = new SsfdspIndicator { ShowColdValues = true };

        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);
    }

    [Fact]
    public void SsfdspIndicator_ShortName_UpdatesWhenParametersChange()
    {
        var indicator = new SsfdspIndicator { Period = 20 };
        string initialName = indicator.ShortName;

        Assert.True(initialName.Contains("20", StringComparison.Ordinal));

        indicator.Period = 40;
        string updatedName = indicator.ShortName;

        Assert.True(updatedName.Contains("40", StringComparison.Ordinal));
    }

    [Fact]
    public void SsfdspIndicator_LineSeries_HasCorrectProperties()
    {
        var indicator = new SsfdspIndicator { Period = 20 };
        indicator.Initialize();

        var lineSeries = indicator.LinesSeries[0];

        Assert.Equal("SSFDSP", lineSeries.Name);
        Assert.Equal(2, lineSeries.Width);
        Assert.Equal(LineStyle.Solid, lineSeries.Style);
    }

    [Fact]
    public void SsfdspIndicator_ZeroLine_HasCorrectProperties()
    {
        var indicator = new SsfdspIndicator { Period = 20 };
        indicator.Initialize();

        var zeroLine = indicator.LinesSeries[1];

        Assert.Equal("Zero", zeroLine.Name);
        Assert.Equal(1, zeroLine.Width);
        Assert.Equal(LineStyle.Dash, zeroLine.Style);
    }

    [Fact]
    public void SsfdspIndicator_DifferentPeriods_Work()
    {
        var periods = new[] { 8, 20, 40, 100 };

        foreach (var period in periods)
        {
            var indicator = new SsfdspIndicator { Period = period };
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
            double ssfdspValue = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(ssfdspValue), $"Period {period} should produce finite value");
        }
    }

    [Fact]
    public void SsfdspIndicator_OscillatesAroundZero()
    {
        var indicator = new SsfdspIndicator { Period = 20 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        var values = new List<double>();

        // Generate trending then ranging price pattern
        for (int i = 0; i < 100; i++)
        {
            double price = 100.0 + 10.0 * Math.Sin(i * 0.15);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            values.Add(indicator.LinesSeries[0].GetValue(0));
        }

        // Should have both positive and negative values (oscillates around zero)
        int positiveCount = values.Count(v => v > 0);
        int negativeCount = values.Count(v => v < 0);

        Assert.True(positiveCount > 0, "Should have positive SSFDSP values");
        Assert.True(negativeCount > 0, "Should have negative SSFDSP values");
    }

    [Fact]
    public void SsfdspIndicator_SourceCodeLink_PointsToGitHub()
    {
        var indicator = new SsfdspIndicator();

        Assert.Contains("github.com/mihakralj/QuanTAlib", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Ssfdsp.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }
}
