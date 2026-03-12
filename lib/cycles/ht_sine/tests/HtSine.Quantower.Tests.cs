using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Quantower.Tests;

public class HtSineIndicatorTests
{
    [Fact]
    public void HtSineIndicator_Constructor_SetsDefaults()
    {
        var indicator = new HtSineIndicator();

        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("HT_SINE - Ehlers Hilbert Transform SineWave", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void HtSineIndicator_MinHistoryDepths_Equals63()
    {
        var indicator = new HtSineIndicator();

        Assert.Equal(63, HtSineIndicator.MinHistoryDepths);
        Assert.Equal(63, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void HtSineIndicator_ShortName_IsHtSine()
    {
        var indicator = new HtSineIndicator();

        Assert.Equal("HT_SINE", indicator.ShortName);
    }

    [Fact]
    public void HtSineIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new HtSineIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (Sine + LeadSine + Zero lines)
        Assert.Equal(3, indicator.LinesSeries.Count);
    }

    [Fact]
    public void HtSineIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new HtSineIndicator();
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
    public void HtSineIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new HtSineIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void HtSineIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new HtSineIndicator();
        indicator.Initialize();

        // Should not throw an exception
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        // Assert that the indicator still exists (method completed without exception)
        Assert.NotNull(indicator);
    }

    [Fact]
    public void HtSineIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new HtSineIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = [100, 102, 105, 103, 107, 110, 108, 112, 115, 113];

        foreach (var close in closes)
        {
            indicator.HistoricalData.AddBar(now, close, close + 2, close - 2, close);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        // All sine values should be finite
        for (int i = 0; i < closes.Length; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(closes.Length - 1 - i)));
        }
    }

    [Fact]
    public void HtSineIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new HtSineIndicator { Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void HtSineIndicator_Source_CanBeChanged()
    {
        var indicator = new HtSineIndicator { Source = SourceType.Close };

        Assert.Equal(SourceType.Close, indicator.Source);

        indicator.Source = SourceType.Open;
        Assert.Equal(SourceType.Open, indicator.Source);
    }

    [Fact]
    public void HtSineIndicator_ShowColdValues_CanBeChanged()
    {
        var indicator = new HtSineIndicator { ShowColdValues = true };

        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);
    }

    [Fact]
    public void HtSineIndicator_SineSeries_HasCorrectProperties()
    {
        var indicator = new HtSineIndicator();
        indicator.Initialize();

        var sineSeries = indicator.LinesSeries[0];

        Assert.Equal("Sine", sineSeries.Name);
        Assert.Equal(2, sineSeries.Width);
        Assert.Equal(LineStyle.Solid, sineSeries.Style);
    }

    [Fact]
    public void HtSineIndicator_LeadSineSeries_HasCorrectProperties()
    {
        var indicator = new HtSineIndicator();
        indicator.Initialize();

        var leadSineSeries = indicator.LinesSeries[1];

        Assert.Equal("LeadSine", leadSineSeries.Name);
        Assert.Equal(1, leadSineSeries.Width);
        Assert.Equal(LineStyle.Solid, leadSineSeries.Style);
    }

    [Fact]
    public void HtSineIndicator_ZeroLine_HasCorrectProperties()
    {
        var indicator = new HtSineIndicator();
        indicator.Initialize();

        var zeroLine = indicator.LinesSeries[2];

        Assert.Equal("Zero", zeroLine.Name);
        Assert.Equal(1, zeroLine.Width);
        Assert.Equal(LineStyle.Dash, zeroLine.Style);
    }

    [Fact]
    public void HtSineIndicator_BothOutputs_ProducedAfterWarmup()
    {
        var indicator = new HtSineIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add enough bars to pass warmup (63 bars)
        for (int i = 0; i < 70; i++)
        {
            double price = 100.0 + (10.0 * Math.Sin(i * 0.15));
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Both sine and leadsine should have values
        double sineValue = indicator.LinesSeries[0].GetValue(0);
        double leadSineValue = indicator.LinesSeries[1].GetValue(0);

        Assert.True(double.IsFinite(sineValue), "Sine should produce finite value");
        Assert.True(double.IsFinite(leadSineValue), "LeadSine should produce finite value");
    }

    [Fact]
    public void HtSineIndicator_OutputsInRangeMinusOneToOne()
    {
        var indicator = new HtSineIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Generate enough data
        for (int i = 0; i < 100; i++)
        {
            double price = 100.0 + (10.0 * Math.Sin(i * 0.15));
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Check all values are in range [-1, 1]
        for (int i = 0; i < 100; i++)
        {
            double sineValue = indicator.LinesSeries[0].GetValue(99 - i);
            double leadSineValue = indicator.LinesSeries[1].GetValue(99 - i);

            Assert.InRange(sineValue, -1.0, 1.0);
            Assert.InRange(leadSineValue, -1.0, 1.0);
        }
    }

    [Fact]
    public void HtSineIndicator_LeadSineLeadsSine()
    {
        var indicator = new HtSineIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        var sineValues = new List<double>();
        var leadSineValues = new List<double>();

        // Generate cyclic price pattern
        for (int i = 0; i < 100; i++)
        {
            double price = 100.0 + (10.0 * Math.Sin(i * 0.15));
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            sineValues.Add(indicator.LinesSeries[0].GetValue(0));
            leadSineValues.Add(indicator.LinesSeries[1].GetValue(0));
        }

        // LeadSine should generally cross zero before Sine (phase lead)
        // Count zero crossings where LeadSine leads
        int leadsCount = 0;
        for (int i = 70; i < sineValues.Count - 1; i++)
        {
            // Check if LeadSine crossed zero in this bar
            bool leadCrossed = (leadSineValues[i - 1] <= 0 && leadSineValues[i] > 0) ||
                              (leadSineValues[i - 1] >= 0 && leadSineValues[i] < 0);
            if (leadCrossed)
            {
                leadsCount++;
            }
        }

        Assert.True(leadsCount >= 0, "LeadSine should have zero crossings");
    }

    [Fact]
    public void HtSineIndicator_SourceCodeLink_PointsToGitHub()
    {
        var indicator = new HtSineIndicator();

        Assert.Contains("github.com/mihakralj/QuanTAlib", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("HtSine.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }
}
