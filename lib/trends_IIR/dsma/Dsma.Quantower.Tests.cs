using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class DsmaIndicatorTests
{
    [Fact]
    public void DsmaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new DsmaIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(0.5, indicator.ScaleFactor);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("DSMA - Deviation-Scaled Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void DsmaIndicator_MinHistoryDepths_ReturnsZero()
    {
        var indicator = new DsmaIndicator { Period = 20 };

        Assert.Equal(0, DsmaIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void DsmaIndicator_ShortName_IncludesParameters()
    {
        var indicator = new DsmaIndicator { Period = 15, ScaleFactor = 0.6 };

        Assert.Contains("DSMA", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("15", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("0.60", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void DsmaIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new DsmaIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Dsma.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void DsmaIndicator_Initialize_CreatesInternalDsma()
    {
        var indicator = new DsmaIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void DsmaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new DsmaIndicator { Period = 5 };
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
    public void DsmaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new DsmaIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void DsmaIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new DsmaIndicator { Period = 5 };
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
    public void DsmaIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new DsmaIndicator { Period = 5 };
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
    public void DsmaIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new DsmaIndicator { Period = 5, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void DsmaIndicator_Parameters_CanBeChanged()
    {
        var indicator = new DsmaIndicator { Period = 10, ScaleFactor = 0.3 };
        Assert.Equal(10, indicator.Period);
        Assert.Equal(0.3, indicator.ScaleFactor);

        indicator.Period = 20;
        indicator.ScaleFactor = 0.7;

        Assert.Equal(20, indicator.Period);
        Assert.Equal(0.7, indicator.ScaleFactor);
        Assert.Equal(0, DsmaIndicator.MinHistoryDepths);
    }

    [Fact]
    public void DsmaIndicator_ScaleFactorBounds_Work()
    {
        var indicator = new DsmaIndicator();

        // Test minimum bound
        indicator.ScaleFactor = 0.01;
        Assert.Equal(0.01, indicator.ScaleFactor);

        // Test maximum bound
        indicator.ScaleFactor = 0.9;
        Assert.Equal(0.9, indicator.ScaleFactor);

        // Test mid-range
        indicator.ScaleFactor = 0.5;
        Assert.Equal(0.5, indicator.ScaleFactor);
    }

    [Fact]
    public void DsmaIndicator_ProcessUpdate_BarCorrection_HandlesIsNew()
    {
        var indicator = new DsmaIndicator { Period = 5, ScaleFactor = 0.5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 110, 98, 105);

        // Process first bar
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        
        // Process second bar as new
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));
        double afterNewBar = indicator.LinesSeries[0].GetValue(0);

        // Update same bar (bar correction)
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        double afterTick = indicator.LinesSeries[0].GetValue(0);

        // Both should be finite
        Assert.True(double.IsFinite(afterNewBar));
        Assert.True(double.IsFinite(afterTick));
    }
}
