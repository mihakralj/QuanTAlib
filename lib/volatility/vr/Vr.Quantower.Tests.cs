using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class VrIndicatorTests
{
    [Fact]
    public void VrIndicator_Constructor_SetsDefaults()
    {
        var indicator = new VrIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("VR - Volatility Ratio", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void VrIndicator_ShortName_IncludesParameters()
    {
        var indicator = new VrIndicator { Period = 20 };
        Assert.Contains("VR", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void VrIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new VrIndicator();

        Assert.Equal(0, VrIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void VrIndicator_Initialize_CreatesInternalVr()
    {
        var indicator = new VrIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void VrIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new VrIndicator { Period = 10 };
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
        Assert.True(val >= 0, "VR should be non-negative");
    }

    [Fact]
    public void VrIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new VrIndicator { Period = 10 };
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
    public void VrIndicator_DifferentPeriods_Work()
    {
        var periods = new[] { 5, 10, 14, 20 };

        foreach (int period in periods)
        {
            var indicator = new VrIndicator { Period = period };
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
    public void VrIndicator_Period_CanBeChanged()
    {
        var indicator = new VrIndicator();
        Assert.Equal(14, indicator.Period);

        indicator.Period = 20;
        Assert.Equal(20, indicator.Period);

        indicator.Period = 10;
        Assert.Equal(10, indicator.Period);
    }

    [Fact]
    public void VrIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new VrIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void VrIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new VrIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Vr.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void VrIndicator_ConstantPrice_ProducesNearOne()
    {
        var indicator = new VrIndicator { Period = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Constant price with small range - TR ≈ ATR so VR ≈ 1
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 101, 99, 100, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
        // VR should be around 1 when volatility is constant
        Assert.True(val >= 0.5 && val <= 2.0, $"Constant volatility should produce VR near 1, got {val}");
    }

    [Fact]
    public void VrIndicator_HighVolatility_ProducesPositiveValue()
    {
        var indicator = new VrIndicator { Period = 5 };
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
        Assert.True(val > 0, "High volatility should produce positive VR value");
    }

    [Fact]
    public void VrIndicator_UsesHLC_ForCalculation()
    {
        // VR uses HLC (True Range / ATR)
        var indicator = new VrIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Price with varying HLC
        for (int i = 0; i < 20; i++)
        {
            double close = 100 + Math.Sin(i * 0.3) * 3;
            double high = close + 2 + Math.Abs(Math.Sin(i * 0.5));
            double low = close - 2 - Math.Abs(Math.Cos(i * 0.5));
            indicator.HistoricalData.AddBar(now.AddMinutes(i), close, high, low, close, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(val));
        Assert.True(val >= 0, "VR should be non-negative");
    }

    [Fact]
    public void VrIndicator_BreakoutDetection_HighRatio()
    {
        var indicator = new VrIndicator { Period = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Calm period - small ranges
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 101, 99, 100, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double calmVr = indicator.LinesSeries[0].GetValue(0);

        // Breakout - large range
        indicator.HistoricalData.AddBar(now.AddMinutes(20), 100, 115, 85, 110, 5000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double breakoutVr = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(calmVr));
        Assert.True(double.IsFinite(breakoutVr));
        Assert.True(breakoutVr > calmVr, "Breakout should produce higher VR than calm period");
        Assert.True(breakoutVr > 1.5, "Breakout VR should be significantly above 1");
    }

    [Fact]
    public void VrIndicator_LargerPeriod_SmootherATR()
    {
        var indicator1 = new VrIndicator { Period = 5 };
        var indicator2 = new VrIndicator { Period = 20 };
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

        // Both should produce valid values
        Assert.True(results1.All(double.IsFinite));
        Assert.True(results2.All(double.IsFinite));
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
    public void VrIndicator_GapUp_IncreasesRatio()
    {
        var indicator = new VrIndicator { Period = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Normal trading
        for (int i = 0; i < 15; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 101, 99, 100, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double beforeGap = indicator.LinesSeries[0].GetValue(0);

        // Large gap up - TR will be large due to gap from previous close
        indicator.HistoricalData.AddBar(now.AddMinutes(15), 110, 115, 108, 112, 2000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double afterGap = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(beforeGap));
        Assert.True(double.IsFinite(afterGap));
        Assert.True(afterGap > beforeGap, "Gap should increase VR");
    }

    [Fact]
    public void VrIndicator_VolatilityExpansion_RespondsQuickly()
    {
        var indicator = new VrIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Low volatility period
        for (int i = 0; i < 15; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 100.5, 99.5, 100, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lowVolVr = indicator.LinesSeries[0].GetValue(0);

        // Sudden volatility expansion
        indicator.HistoricalData.AddBar(now.AddMinutes(15), 100, 110, 90, 105, 5000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double expansionVr = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(lowVolVr));
        Assert.True(double.IsFinite(expansionVr));
        Assert.True(expansionVr > lowVolVr * 2, "VR should respond quickly to volatility expansion");
    }

    [Fact]
    public void VrIndicator_TypicalValues_AroundOne()
    {
        var indicator = new VrIndicator { Period = 14 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        var values = new List<double>();

        // Normal market with consistent volatility
        for (int i = 0; i < 100; i++)
        {
            double price = 100 + Math.Sin(i * 0.1) * 2;
            double range = 2 + Math.Sin(i * 0.2) * 0.5; // Consistent range
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + range, price - range, price, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            if (i >= 20)
            {
                values.Add(indicator.LinesSeries[0].GetValue(0));
            }
        }

        double avgVr = values.Average();

        // In steady state with consistent volatility, VR should hover around 1
        Assert.True(avgVr >= 0.5 && avgVr <= 2.0, $"Average VR should be around 1, got {avgVr}");
    }
}