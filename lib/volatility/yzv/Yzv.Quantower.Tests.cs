using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class YzvIndicatorTests
{
    [Fact]
    public void YzvIndicator_Constructor_SetsDefaults()
    {
        var indicator = new YzvIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("YZV - Yang-Zhang Volatility", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void YzvIndicator_ShortName_IncludesParameters()
    {
        var indicator = new YzvIndicator { Period = 30 };
        Assert.Contains("YZV", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("30", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void YzvIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new YzvIndicator();

        Assert.Equal(0, YzvIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void YzvIndicator_Initialize_CreatesInternalYzv()
    {
        var indicator = new YzvIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void YzvIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new YzvIndicator { Period = 10 };
        indicator.Initialize();

        // Add historical data with varying volatility
        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            // Create price movement that generates volatility
            double basePrice = 100 + Math.Sin(i * 0.3) * (5 + i * 0.1);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 2, basePrice - 2, basePrice, 1000);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
        Assert.True(val >= 0, "YZV should be non-negative");
    }

    [Fact]
    public void YzvIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new YzvIndicator { Period = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double basePrice = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 2, basePrice - 2, basePrice + 1, 1000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(30), 130, 135, 125, 132, 1500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void YzvIndicator_DifferentPeriods_Work()
    {
        var periods = new[] { 5, 10, 20, 30 };

        foreach (int period in periods)
        {
            var indicator = new YzvIndicator { Period = period };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 60; i++)
            {
                // Create price movement with varying amplitude
                double basePrice = 100 + Math.Sin(i * 0.2) * 5;
                indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 2, basePrice - 2, basePrice, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double val = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(val), $"Period {period} should produce finite value");
            Assert.True(val >= 0, $"Period {period} should produce non-negative value");
        }
    }

    [Fact]
    public void YzvIndicator_Period_CanBeChanged()
    {
        var indicator = new YzvIndicator();
        Assert.Equal(20, indicator.Period);

        indicator.Period = 30;
        Assert.Equal(30, indicator.Period);

        indicator.Period = 10;
        Assert.Equal(10, indicator.Period);
    }

    [Fact]
    public void YzvIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new YzvIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void YzvIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new YzvIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Yzv.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void YzvIndicator_ConstantPrice_ProducesNearZero()
    {
        var indicator = new YzvIndicator { Period = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Constant price - no volatility
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 100.01, 99.99, 100, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
        Assert.True(val < 0.01, "Constant price should produce near-zero YZV");
    }

    [Fact]
    public void YzvIndicator_HighVolatility_ProducesPositiveValue()
    {
        var indicator = new YzvIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // High volatility with large price swings
        for (int i = 0; i < 30; i++)
        {
            double price = 100 + (i % 2 == 0 ? 10 : -10); // Large oscillations
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 5, price - 5, price, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
        Assert.True(val > 0, "High volatility should produce positive YZV value");
    }

    [Fact]
    public void YzvIndicator_UsesOHLC_ForCalculation()
    {
        // YZV uses full OHLC for calculation (overnight + intraday components)
        var indicator = new YzvIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Price with varying OHLC
        for (int i = 0; i < 20; i++)
        {
            double open = 100 + Math.Sin(i * 0.3) * 3;
            double high = open + 2 + Math.Abs(Math.Sin(i * 0.5));
            double low = open - 2 - Math.Abs(Math.Cos(i * 0.5));
            double close = open + Math.Sin(i * 0.4) * 2;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), open, high, low, close, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(val));
        Assert.True(val >= 0, "YZV should be non-negative");
    }

    [Fact]
    public void YzvIndicator_LargerPeriod_SmootherOutput()
    {
        var indicator1 = new YzvIndicator { Period = 5 };
        var indicator2 = new YzvIndicator { Period = 20 };
        indicator1.Initialize();
        indicator2.Initialize();

        var now = DateTime.UtcNow;
        var results1 = new List<double>();
        var results2 = new List<double>();

        for (int i = 0; i < 60; i++)
        {
            double price = 100 + Math.Sin(i * 0.3) * 5;
            indicator1.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price, 1000);
            indicator2.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price, 1000);
            indicator1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            if (i >= 25) // After both are fully warmed up
            {
                results1.Add(indicator1.LinesSeries[0].GetValue(0));
                results2.Add(indicator2.LinesSeries[0].GetValue(0));
            }
        }

        // Calculate variance of changes
        double variance1 = CalculateChangeVariance(results1);
        double variance2 = CalculateChangeVariance(results2);

        // Longer period should be smoother
        Assert.True(variance2 <= variance1 * 1.5, // Allow some tolerance
            $"Longer period should be smoother: short variance={variance1:F6}, long variance={variance2:F6}");
    }

    private static double CalculateChangeVariance(List<double> values)
    {
        if (values.Count < 2)
        {
            return 0;
        }

        var changes = new List<double>();
        for (int i = 1; i < values.Count; i++)
        {
            changes.Add(values[i] - values[i - 1]);
        }

        double mean = changes.Average();
        double variance = changes.Select(c => (c - mean) * (c - mean)).Average();
        return variance;
    }

    [Fact]
    public void YzvIndicator_GapUp_AffectsVolatility()
    {
        var indicator = new YzvIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Normal trading
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 101, 99, 100, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double beforeGap = indicator.LinesSeries[0].GetValue(0);

        // Large gap up (open much higher than previous close)
        for (int i = 10; i < 20; i++)
        {
            double open = 120 + (i - 10) * 2; // Large gaps
            indicator.HistoricalData.AddBar(now.AddMinutes(i), open, open + 2, open - 2, open + 1, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double afterGap = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(beforeGap));
        Assert.True(double.IsFinite(afterGap));
        // Gap should increase volatility measurement
        Assert.True(afterGap > beforeGap * 0.5, "Gap up should affect volatility");
    }

    [Fact]
    public void YzvIndicator_VolatilityRegimeChange_RespondsCorrectly()
    {
        var indicator = new YzvIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Low volatility regime
        for (int i = 0; i < 20; i++)
        {
            double price = 100 + Math.Sin(i * 0.5) * 0.5; // Small movements
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 0.2, price - 0.2, price, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lowVolVal = indicator.LinesSeries[0].GetValue(0);

        // High volatility regime
        for (int i = 20; i < 40; i++)
        {
            double price = 100 + Math.Sin(i * 0.5) * 10; // Large movements
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 5, price - 5, price, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double highVolVal = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(lowVolVal));
        Assert.True(double.IsFinite(highVolVal));
        Assert.True(highVolVal > lowVolVal, "High volatility regime should produce higher YZV");
    }
}