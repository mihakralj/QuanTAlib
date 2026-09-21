using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class VovIndicatorTests
{
    [Fact]
    public void VovIndicator_Constructor_SetsDefaults()
    {
        var indicator = new VovIndicator();

        Assert.Equal(20, indicator.VolatilityPeriod);
        Assert.Equal(10, indicator.VovPeriod);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("VOV - Volatility of Volatility", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void VovIndicator_ShortName_IncludesParameters()
    {
        var indicator = new VovIndicator { VolatilityPeriod = 30, VovPeriod = 15 };
        Assert.Contains("VOV", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("30", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("15", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void VovIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new VovIndicator();

        Assert.Equal(0, VovIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void VovIndicator_Initialize_CreatesInternalVov()
    {
        var indicator = new VovIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void VovIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new VovIndicator { VolatilityPeriod = 10, VovPeriod = 5 };
        indicator.Initialize();

        // Add historical data with varying volatility
        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            // Create price movement that generates volatility
            double basePrice = 100 + (Math.Sin(i * 0.3) * (5 + (i * 0.1)));
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 2, basePrice - 2, basePrice, 1000);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
        Assert.True(val >= 0, "VOV should be non-negative");
    }

    [Fact]
    public void VovIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new VovIndicator { VolatilityPeriod = 10, VovPeriod = 5 };
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
    public void VovIndicator_DifferentPeriods_Work()
    {
        var periodCombos = new[] { (5, 3), (10, 5), (20, 10), (30, 15) };

        foreach (var (volPeriod, vovPeriod) in periodCombos)
        {
            var indicator = new VovIndicator { VolatilityPeriod = volPeriod, VovPeriod = vovPeriod };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 60; i++)
            {
                // Create price movement with varying amplitude
                double basePrice = 100 + (Math.Sin(i * 0.2) * 5);
                indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 2, basePrice - 2, basePrice, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double val = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(val), $"Periods ({volPeriod},{vovPeriod}) should produce finite value");
            Assert.True(val >= 0, $"Periods ({volPeriod},{vovPeriod}) should produce non-negative value");
        }
    }

    [Fact]
    public void VovIndicator_VolatilityPeriod_CanBeChanged()
    {
        var indicator = new VovIndicator();
        Assert.Equal(20, indicator.VolatilityPeriod);

        indicator.VolatilityPeriod = 30;
        Assert.Equal(30, indicator.VolatilityPeriod);

        indicator.VolatilityPeriod = 10;
        Assert.Equal(10, indicator.VolatilityPeriod);
    }

    [Fact]
    public void VovIndicator_VovPeriod_CanBeChanged()
    {
        var indicator = new VovIndicator();
        Assert.Equal(10, indicator.VovPeriod);

        indicator.VovPeriod = 15;
        Assert.Equal(15, indicator.VovPeriod);

        indicator.VovPeriod = 5;
        Assert.Equal(5, indicator.VovPeriod);
    }

    [Fact]
    public void VovIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new VovIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void VovIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new VovIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Vov.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void VovIndicator_ConstantPrice_ProducesZero()
    {
        var indicator = new VovIndicator { VolatilityPeriod = 10, VovPeriod = 5 };
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
        Assert.True(val < 0.1, "Constant price should produce near-zero VOV");
    }

    [Fact]
    public void VovIndicator_ChangingVolatility_ProducesPositiveValue()
    {
        var indicator = new VovIndicator { VolatilityPeriod = 5, VovPeriod = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Low volatility period
        for (int i = 0; i < 15; i++)
        {
            double price = 100 + ((i % 2) * 0.5); // Small oscillations
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 0.5, price - 0.5, price, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // High volatility period
        for (int i = 15; i < 30; i++)
        {
            double price = 100 + ((i % 2) * 10); // Large oscillations
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 5, price - 5, price, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
        Assert.True(val > 0, "Changing volatility should produce positive VOV value");
    }

    [Fact]
    public void VovIndicator_UsesClosePrice_ForCalculation()
    {
        // VOV uses close price for volatility calculation
        var indicator = new VovIndicator { VolatilityPeriod = 5, VovPeriod = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Price with varying close but constant OHLC range
        for (int i = 0; i < 20; i++)
        {
            double close = 100 + (Math.Sin(i * 0.5) * 5); // Varying close
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, close, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(val));
        Assert.True(val >= 0, "VOV should be non-negative");
    }

    [Fact]
    public void VovIndicator_LargerVolatilityPeriod_SmootherOutput()
    {
        var indicator1 = new VovIndicator { VolatilityPeriod = 5, VovPeriod = 5 };
        var indicator2 = new VovIndicator { VolatilityPeriod = 20, VovPeriod = 5 };
        indicator1.Initialize();
        indicator2.Initialize();

        var now = DateTime.UtcNow;
        var results1 = new List<double>();
        var results2 = new List<double>();

        for (int i = 0; i < 60; i++)
        {
            double price = 100 + (Math.Sin(i * 0.3) * 5);
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

        // Longer volatility period should be smoother
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
    public void VovIndicator_VolatilityRegimeChange_RespondsCorrectly()
    {
        var indicator = new VovIndicator { VolatilityPeriod = 5, VovPeriod = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Stable volatility regime
        for (int i = 0; i < 20; i++)
        {
            double price = 100 + (Math.Sin(i * 0.5) * 2);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double stableVal = indicator.LinesSeries[0].GetValue(0);

        // Transition to variable volatility
        for (int i = 20; i < 40; i++)
        {
            double amplitude = 2 + ((i - 20) * 0.5); // Increasing amplitude
            double price = 100 + (Math.Sin(i * 0.5) * amplitude);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + amplitude, price - amplitude, price, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double transitionVal = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(stableVal));
        Assert.True(double.IsFinite(transitionVal));
        // During volatility regime change, VOV should typically increase
        Assert.True(transitionVal > 0, "Changing volatility regime should produce positive VOV");
    }
}
