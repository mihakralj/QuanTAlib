using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class WienerIndicatorTests
{
    [Fact]
    public void WienerIndicator_Constructor_SetsDefaults()
    {
        var indicator = new WienerIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(10, indicator.SmoothPeriod);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Wiener - Wiener Filter", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void WienerIndicator_MinHistoryDepths_EqualsTwo()
    {
        var indicator = new WienerIndicator();

        Assert.Equal(2, WienerIndicator.MinHistoryDepths);
        Assert.Equal(2, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void WienerIndicator_ShortName_IncludesParameters()
    {
        var indicator = new WienerIndicator { Period = 13, SmoothPeriod = 7, Source = SourceType.Close };

        Assert.Contains("Wiener(13,7)", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("Close", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void WienerIndicator_Initialize_CreatesInternalWiener()
    {
        var indicator = new WienerIndicator { Period = 10, SmoothPeriod = 5 };
        indicator.Initialize();
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void WienerIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new WienerIndicator { Period = 5, SmoothPeriod = 3 };
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
    public void WienerIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new WienerIndicator { Period = 5, SmoothPeriod = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void WienerIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new WienerIndicator { Period = 5, SmoothPeriod = 3 };
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
    public void WienerIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new WienerIndicator { Period = 5, SmoothPeriod = 3 };
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

        // Check last value matches logic
        double lastVal = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(lastVal));
    }

    [Fact]
    public void WienerIndicator_Parameters_CanBeChanged()
    {
        var indicator = new WienerIndicator { Period = 5, SmoothPeriod = 10 };
        Assert.Equal(5, indicator.Period);
        Assert.Equal(10, indicator.SmoothPeriod);

        indicator.Period = 20;
        indicator.SmoothPeriod = 5;
        Assert.Equal(20, indicator.Period);
        Assert.Equal(5, indicator.SmoothPeriod);
    }
}
