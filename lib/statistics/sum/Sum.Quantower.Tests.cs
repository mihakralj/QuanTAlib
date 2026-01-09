using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class SumIndicatorTests
{
    [Fact]
    public void SumIndicator_Constructor_SetsDefaults()
    {
        var indicator = new SumIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("SUM - Rolling Sum", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void SumIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new SumIndicator();

        Assert.Equal(0, SumIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void SumIndicator_ShortName_IncludesPeriodAndSource()
    {
        var indicator = new SumIndicator { Period = 20 };

        Assert.Contains("SUM", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void SumIndicator_Initialize_CreatesInternalSum()
    {
        var indicator = new SumIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void SumIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new SumIndicator { Period = 5 };
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
    public void SumIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new SumIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void SumIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new SumIndicator { Period = 5 };
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
    public void SumIndicator_MultipleUpdates_ProducesCorrectSumSequence()
    {
        var indicator = new SumIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 10, 20, 30, 40, 50 };

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

        // Last SUM(3) should be sum of last 3 values: 30 + 40 + 50 = 120
        double lastSum = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(120.0, lastSum, 1e-10);
    }

    [Fact]
    public void SumIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new SumIndicator { Period = 5, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void SumIndicator_CalculatesRollingSum()
    {
        var indicator = new SumIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add bars with known close prices: 10, 20, 30, 40
        indicator.HistoricalData.AddBar(now, 10, 10, 10, 10);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        Assert.Equal(10.0, indicator.LinesSeries[0].GetValue(0), 1e-10); // Sum = 10

        indicator.HistoricalData.AddBar(now.AddMinutes(1), 20, 20, 20, 20);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));
        Assert.Equal(30.0, indicator.LinesSeries[0].GetValue(0), 1e-10); // Sum = 10+20 = 30

        indicator.HistoricalData.AddBar(now.AddMinutes(2), 30, 30, 30, 30);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));
        Assert.Equal(60.0, indicator.LinesSeries[0].GetValue(0), 1e-10); // Sum = 10+20+30 = 60

        indicator.HistoricalData.AddBar(now.AddMinutes(3), 40, 40, 40, 40);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));
        Assert.Equal(90.0, indicator.LinesSeries[0].GetValue(0), 1e-10); // Sum = 20+30+40 = 90 (10 dropped)
    }

    [Fact]
    public void SumIndicator_Period_CanBeChanged()
    {
        var indicator = new SumIndicator { Period = 50 };

        Assert.Equal(50, indicator.Period);

        indicator.Period = 100;
        Assert.Equal(100, indicator.Period);
    }
}
