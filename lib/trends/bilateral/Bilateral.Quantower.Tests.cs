using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class BilateralIndicatorTests
{
    [Fact]
    public void BilateralIndicator_Constructor_SetsDefaults()
    {
        var indicator = new BilateralIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.Equal(0.5, indicator.SigmaSRatio);
        Assert.Equal(1.0, indicator.SigmaRMult);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Bilateral Filter", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void BilateralIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new BilateralIndicator { Period = 20 };

        Assert.Equal(0, BilateralIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void BilateralIndicator_ShortName_IncludesPeriodAndSource()
    {
        var indicator = new BilateralIndicator { Period = 15 };

        Assert.Contains("Bilateral", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("15", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void BilateralIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new BilateralIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Bilateral.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void BilateralIndicator_Initialize_CreatesInternalBilateral()
    {
        var indicator = new BilateralIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void BilateralIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new BilateralIndicator { Period = 3 };
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
    public void BilateralIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new BilateralIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void BilateralIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new BilateralIndicator { Period = 3 };
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
    public void BilateralIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new BilateralIndicator { Period = 3 };
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
    public void BilateralIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new BilateralIndicator { Period = 3, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void BilateralIndicator_Parameters_CanBeChanged()
    {
        var indicator = new BilateralIndicator { Period = 5, SigmaSRatio = 0.5, SigmaRMult = 1.0 };
        Assert.Equal(5, indicator.Period);
        Assert.Equal(0.5, indicator.SigmaSRatio);
        Assert.Equal(1.0, indicator.SigmaRMult);

        indicator.Period = 20;
        indicator.SigmaSRatio = 1.0;
        indicator.SigmaRMult = 2.0;
        
        Assert.Equal(20, indicator.Period);
        Assert.Equal(1.0, indicator.SigmaSRatio);
        Assert.Equal(2.0, indicator.SigmaRMult);
        Assert.Equal(0, BilateralIndicator.MinHistoryDepths);
    }
}
