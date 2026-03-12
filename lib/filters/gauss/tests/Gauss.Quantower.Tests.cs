using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class GaussIndicatorTests
{
    [Fact]
    public void GaussIndicator_Constructor_SetsDefaults()
    {
        var indicator = new GaussIndicator();

        Assert.Equal(1.0, indicator.Sigma);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Gauss - Gaussian Filter", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void GaussIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new GaussIndicator { Sigma = 2.0 };

        Assert.Equal(0, GaussIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void GaussIndicator_ShortName_IncludesSigmaAndSource()
    {
        var indicator = new GaussIndicator { Sigma = 1.5 };

        Assert.Contains("Gauss", indicator.ShortName, StringComparison.Ordinal);
        // "1.50" because of F2 format
        Assert.Contains("1.50", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void GaussIndicator_Initialize_CreatesInternalGauss()
    {
        var indicator = new GaussIndicator { Sigma = 1.0 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void GaussIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new GaussIndicator { Sigma = 1.0 };
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
    public void GaussIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new GaussIndicator { Sigma = 1.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void GaussIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new GaussIndicator { Sigma = 1.0 };
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
    public void GaussIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        // Sigma 1.0 -> Kernel Size = 7
        var indicator = new GaussIndicator { Sigma = 1.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 104, 103, 105, 106, 107 };

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

        // Check last value
        double lastVal = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(lastVal));
    }

    [Fact]
    public void GaussIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new GaussIndicator { Sigma = 1.0, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void GaussIndicator_Sigma_CanBeChanged()
    {
        var indicator = new GaussIndicator { Sigma = 0.5 };
        Assert.Equal(0.5, indicator.Sigma);

        indicator.Sigma = 2.0;
        Assert.Equal(2.0, indicator.Sigma);
    }
}
