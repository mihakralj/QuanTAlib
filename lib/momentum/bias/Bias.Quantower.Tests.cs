using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class BiasIndicatorTests
{
    [Fact]
    public void BiasIndicator_Constructor_SetsDefaults()
    {
        var indicator = new BiasIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("BIAS - Price Deviation from SMA", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void BiasIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new BiasIndicator();

        Assert.Equal(0, BiasIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void BiasIndicator_ShortName_IncludesPeriodAndSource()
    {
        var indicator = new BiasIndicator { Period = 20 };

        Assert.Contains("BIAS", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void BiasIndicator_Initialize_CreatesInternalBias()
    {
        var indicator = new BiasIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void BiasIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new BiasIndicator { Period = 5 };
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
    public void BiasIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new BiasIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void BiasIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new BiasIndicator { Period = 5 };
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
    public void BiasIndicator_MultipleUpdates_ProducesCorrectBiasSequence()
    {
        var indicator = new BiasIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 100, 100, 110, 100 };

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
    public void BiasIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new BiasIndicator { Period = 5, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void BiasIndicator_CalculatesBiasCorrectly()
    {
        var indicator = new BiasIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add 3 bars with close = 100, then one with close = 110
        // SMA(3) of [100, 100, 100] = 100
        // BIAS when price = 100, SMA = 100 → 0%
        indicator.HistoricalData.AddBar(now, 100, 100, 100, 100);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        Assert.Equal(0.0, indicator.LinesSeries[0].GetValue(0), 1e-10);

        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 100, 100, 100);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));
        Assert.Equal(0.0, indicator.LinesSeries[0].GetValue(0), 1e-10);

        indicator.HistoricalData.AddBar(now.AddMinutes(2), 100, 100, 100, 100);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));
        Assert.Equal(0.0, indicator.LinesSeries[0].GetValue(0), 1e-10);

        // Now add a bar with close = 110
        // SMA(3) of [100, 100, 110] = 310/3 ≈ 103.333
        // BIAS = (110 / 103.333) - 1 ≈ 0.0645 (6.45%)
        indicator.HistoricalData.AddBar(now.AddMinutes(3), 110, 110, 110, 110);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));
        double expectedSma = (100.0 + 100.0 + 110.0) / 3.0;
        double expectedBias = (110.0 / expectedSma) - 1.0;
        Assert.Equal(expectedBias, indicator.LinesSeries[0].GetValue(0), 1e-10);
    }

    [Fact]
    public void BiasIndicator_ConstantPrice_ZeroBias()
    {
        var indicator = new BiasIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // All bars at same price should produce zero bias
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 100, 100, 100);
            indicator.ProcessUpdate(i == 0 ? new UpdateArgs(UpdateReason.HistoricalBar) : new UpdateArgs(UpdateReason.NewBar));
            Assert.Equal(0.0, indicator.LinesSeries[0].GetValue(0), 1e-10);
        }
    }

    [Fact]
    public void BiasIndicator_UpTrend_PositiveBias()
    {
        var indicator = new BiasIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 101, 102, 103, 104, 105 };

        for (int i = 0; i < closes.Length; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), closes[i], closes[i], closes[i], closes[i]);
            indicator.ProcessUpdate(i == 0 ? new UpdateArgs(UpdateReason.HistoricalBar) : new UpdateArgs(UpdateReason.NewBar));
        }

        // In uptrend, price should be above SMA, so bias > 0
        Assert.True(indicator.LinesSeries[0].GetValue(0) > 0, "Bias should be positive in uptrend");
    }

    [Fact]
    public void BiasIndicator_DownTrend_NegativeBias()
    {
        var indicator = new BiasIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 105, 104, 103, 102, 101, 100 };

        for (int i = 0; i < closes.Length; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), closes[i], closes[i], closes[i], closes[i]);
            indicator.ProcessUpdate(i == 0 ? new UpdateArgs(UpdateReason.HistoricalBar) : new UpdateArgs(UpdateReason.NewBar));
        }

        // In downtrend, price should be below SMA, so bias < 0
        Assert.True(indicator.LinesSeries[0].GetValue(0) < 0, "Bias should be negative in downtrend");
    }

    [Fact]
    public void BiasIndicator_Period_CanBeChanged()
    {
        var indicator = new BiasIndicator { Period = 50 };

        Assert.Equal(50, indicator.Period);

        indicator.Period = 100;
        Assert.Equal(100, indicator.Period);
    }
}
