using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class RemaIndicatorTests
{
    [Fact]
    public void RemaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new RemaIndicator();

        Assert.Equal(10, indicator.Period);
        Assert.Equal(0.5, indicator.Lambda);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("REMA - Regularized Exponential Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void RemaIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new RemaIndicator { Period = 20 };

        Assert.Equal(0, RemaIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void RemaIndicator_ShortName_IncludesPeriodLambdaAndSource()
    {
        var indicator = new RemaIndicator { Period = 15, Lambda = 0.7 };

        Assert.Contains("REMA", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("15", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("0.70", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void RemaIndicator_Initialize_CreatesInternalRema()
    {
        var indicator = new RemaIndicator { Period = 10, Lambda = 0.5 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void RemaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new RemaIndicator { Period = 3, Lambda = 0.5 };
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
    public void RemaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new RemaIndicator { Period = 3, Lambda = 0.5 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        // Process first update
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        // Line series should have values
        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void RemaIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new RemaIndicator { Period = 3, Lambda = 0.5 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        // Process historical bar first
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        double firstValue = indicator.LinesSeries[0].GetValue(0);

        // Update with new tick (same bar data - simulates intrabar update)
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        double secondValue = indicator.LinesSeries[0].GetValue(0);

        // Both values should be finite
        Assert.True(double.IsFinite(firstValue));
        Assert.True(double.IsFinite(secondValue));
    }

    [Fact]
    public void RemaIndicator_MultipleUpdates_ProducesCorrectRemaSequence()
    {
        var indicator = new RemaIndicator { Period = 3, Lambda = 0.5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 104, 103, 105, 107, 106 };

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

        // REMA should be smoothing the values
        // Last REMA value should be between first and last close
        double lastRema = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastRema >= 100 && lastRema <= 110);
    }

    [Fact]
    public void RemaIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new RemaIndicator { Period = 3, Lambda = 0.5, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void RemaIndicator_Period_CanBeChanged()
    {
        var indicator = new RemaIndicator { Period = 5 };
        Assert.Equal(5, indicator.Period);

        indicator.Period = 20;
        Assert.Equal(20, indicator.Period);
        Assert.Equal(0, RemaIndicator.MinHistoryDepths);
    }

    [Fact]
    public void RemaIndicator_Lambda_CanBeChanged()
    {
        var indicator = new RemaIndicator { Lambda = 0.5 };
        Assert.Equal(0.5, indicator.Lambda);

        indicator.Lambda = 0.8;
        Assert.Equal(0.8, indicator.Lambda);
    }

    [Fact]
    public void RemaIndicator_DifferentLambdaValues_ProduceDifferentResults()
    {
        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 104, 103, 105, 107, 106 };

        var indicator1 = new RemaIndicator { Period = 3, Lambda = 0.3 };
        var indicator2 = new RemaIndicator { Period = 3, Lambda = 0.7 };
        indicator1.Initialize();
        indicator2.Initialize();

        foreach (var close in closes)
        {
            indicator1.HistoricalData.AddBar(now, close, close + 2, close - 2, close);
            indicator2.HistoricalData.AddBar(now, close, close + 2, close - 2, close);
            indicator1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        // Different lambda values should produce different results
        double result1 = indicator1.LinesSeries[0].GetValue(0);
        double result2 = indicator2.LinesSeries[0].GetValue(0);

        Assert.NotEqual(result1, result2);
    }
}