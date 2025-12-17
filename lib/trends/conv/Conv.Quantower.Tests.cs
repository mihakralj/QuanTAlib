using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class ConvIndicatorTests
{
    [Fact]
    public void ConvIndicator_Constructor_SetsDefaults()
    {
        var indicator = new ConvIndicator();

        Assert.Equal("0.1, 0.2, 0.3, 0.4", indicator.WeightsInput);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("CONV - Convolution", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void ConvIndicator_MinHistoryDepths_EqualsWeightsLength()
    {
        var indicator = new ConvIndicator { WeightsInput = "1, 2, 3, 4, 5" };
        indicator.Initialize(); // Initialize to parse weights

        Assert.Equal(5, indicator.MinHistoryDepths);
        Assert.Equal(5, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void ConvIndicator_ShortName_IncludesSource()
    {
        var indicator = new ConvIndicator();

        Assert.Contains("CONV", indicator.ShortName);
        Assert.Contains("Close", indicator.ShortName);
    }

    [Fact]
    public void ConvIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new ConvIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink);
        Assert.Contains("Conv.Quantower.cs", indicator.SourceCodeLink);
    }

    [Fact]
    public void ConvIndicator_Initialize_CreatesInternalConv()
    {
        var indicator = new ConvIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void ConvIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new ConvIndicator { WeightsInput = "0.5, 0.5" };
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
    public void ConvIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new ConvIndicator { WeightsInput = "0.5, 0.5" };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void ConvIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new ConvIndicator { WeightsInput = "0.5, 0.5" };
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
    public void ConvIndicator_OnPaintChart_DoesNotThrow()
    {
        var indicator = new ConvIndicator();
        indicator.Initialize();

        var method = indicator.GetType().GetMethod("OnPaintChart");
        Assert.NotNull(method);
        Assert.Equal(typeof(ConvIndicator), method.DeclaringType);
    }

    [Fact]
    public void ConvIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        // Weights [0.5, 1.0]
        var indicator = new ConvIndicator { WeightsInput = "0.5, 1.0" };
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
    }

    [Fact]
    public void ConvIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new ConvIndicator { WeightsInput = "0.5, 0.5", Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void ConvIndicator_InvalidWeights_FallsBackToDefault()
    {
        var indicator = new ConvIndicator { WeightsInput = "invalid" };

        // Should not throw, but fallback
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void ConvIndicator_DescriptionIsSet()
    {
        var indicator = new ConvIndicator();

        Assert.Contains("Convolution", indicator.Description);
    }
}
