using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class CmaIndicatorTests
{
    [Fact]
    public void CmaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new CmaIndicator();

        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("CMA - Cumulative Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void CmaIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new CmaIndicator();

        Assert.Equal(0, CmaIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void CmaIndicator_ShortName_IncludesSource()
    {
        var indicator = new CmaIndicator();

        Assert.Contains("CMA", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void CmaIndicator_Initialize_CreatesInternalCma()
    {
        var indicator = new CmaIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void CmaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new CmaIndicator();
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
    public void CmaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new CmaIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void CmaIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new CmaIndicator();
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
    public void CmaIndicator_MultipleUpdates_ProducesCorrectCmaSequence()
    {
        var indicator = new CmaIndicator();
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

        // Last CMA should be average of all values: (100 + 102 + 104 + 103 + 105) / 5 = 102.8
        double lastCma = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(102.8, lastCma, 1e-10);
    }

    [Fact]
    public void CmaIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new CmaIndicator { Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void CmaIndicator_CalculatesRunningAverage()
    {
        var indicator = new CmaIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add bars with known close prices: 10, 20, 30
        indicator.HistoricalData.AddBar(now, 10, 10, 10, 10);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        Assert.Equal(10.0, indicator.LinesSeries[0].GetValue(0), 1e-10); // CMA = 10

        indicator.HistoricalData.AddBar(now.AddMinutes(1), 20, 20, 20, 20);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));
        Assert.Equal(15.0, indicator.LinesSeries[0].GetValue(0), 1e-10); // CMA = (10+20)/2 = 15

        indicator.HistoricalData.AddBar(now.AddMinutes(2), 30, 30, 30, 30);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));
        Assert.Equal(20.0, indicator.LinesSeries[0].GetValue(0), 1e-10); // CMA = (10+20+30)/3 = 20
    }
}
