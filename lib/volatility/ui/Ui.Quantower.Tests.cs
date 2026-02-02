using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class UiIndicatorTests
{
    [Fact]
    public void UiIndicator_Constructor_SetsDefaults()
    {
        var indicator = new UiIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("UI - Ulcer Index", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void UiIndicator_ShortName_IncludesParameters()
    {
        var indicator = new UiIndicator { Period = 20 };
        Assert.Contains("UI", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void UiIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new UiIndicator();

        Assert.Equal(0, UiIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void UiIndicator_Initialize_CreatesInternalUi()
    {
        var indicator = new UiIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void UiIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new UiIndicator { Period = 10 };
        indicator.Initialize();

        // Add historical data with declining prices (creates drawdown)
        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double basePrice = 110 - (i * 0.5); // Declining from 110
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 1, basePrice - 1, basePrice, 1000);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
        Assert.True(val >= 0, "Ulcer Index should be non-negative");
    }

    [Fact]
    public void UiIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new UiIndicator { Period = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double basePrice = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 2, basePrice - 2, basePrice + 1, 1000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar with price drop
        indicator.HistoricalData.AddBar(now.AddMinutes(30), 100, 105, 95, 100, 1500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void UiIndicator_DifferentPeriods_Work()
    {
        int[] periods = { 5, 10, 14, 20 };

        foreach (var period in periods)
        {
            var indicator = new UiIndicator { Period = period };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 50; i++)
            {
                // Create some price movement with occasional drawdowns
                double basePrice = 100 + (i % 10) - 5;
                indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 2, basePrice - 2, basePrice, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double val = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(val), $"Period {period} should produce finite value");
            Assert.True(val >= 0, $"Period {period} should produce non-negative value");
        }
    }

    [Fact]
    public void UiIndicator_Period_CanBeChanged()
    {
        var indicator = new UiIndicator();
        Assert.Equal(14, indicator.Period);

        indicator.Period = 20;
        Assert.Equal(20, indicator.Period);

        indicator.Period = 10;
        Assert.Equal(10, indicator.Period);
    }

    [Fact]
    public void UiIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new UiIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void UiIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new UiIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Ui.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void UiIndicator_AtPeriodHigh_ProducesZero()
    {
        var indicator = new UiIndicator { Period = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Constantly rising prices = always at new high = no drawdown
        for (int i = 0; i < 30; i++)
        {
            double price = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 0.5, price - 0.5, price, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
        Assert.True(val < 0.5, "Price at period high should produce near-zero UI");
    }

    [Fact]
    public void UiIndicator_Drawdown_ProducesPositiveValue()
    {
        var indicator = new UiIndicator { Period = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Price rises then drops - creates drawdown
        for (int i = 0; i < 10; i++)
        {
            double price = 100 + i * 2; // Rise to 118
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Now drop the price
        for (int i = 10; i < 20; i++)
        {
            double price = 118 - (i - 10) * 3; // Drop from 118 to 88
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
        Assert.True(val > 0, "Drawdown should produce positive UI value");
    }

    [Fact]
    public void UiIndicator_DeeperDrawdown_ProducesHigherValue()
    {
        var indicator1 = new UiIndicator { Period = 10 };
        var indicator2 = new UiIndicator { Period = 10 };
        indicator1.Initialize();
        indicator2.Initialize();

        var now = DateTime.UtcNow;

        // Indicator 1: small drawdown (5%)
        for (int i = 0; i < 10; i++)
        {
            double price = 100 + i;
            indicator1.HistoricalData.AddBar(now.AddMinutes(i), price, price + 0.5, price - 0.5, price, 1000);
            indicator1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }
        for (int i = 10; i < 20; i++)
        {
            double price = 109 - (i - 10) * 0.5; // Small drop
            indicator1.HistoricalData.AddBar(now.AddMinutes(i), price, price + 0.5, price - 0.5, price, 1000);
            indicator1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Indicator 2: large drawdown (20%)
        for (int i = 0; i < 10; i++)
        {
            double price = 100 + i;
            indicator2.HistoricalData.AddBar(now.AddMinutes(i), price, price + 0.5, price - 0.5, price, 1000);
            indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }
        for (int i = 10; i < 20; i++)
        {
            double price = 109 - (i - 10) * 2; // Large drop
            indicator2.HistoricalData.AddBar(now.AddMinutes(i), price, price + 0.5, price - 0.5, price, 1000);
            indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double smallDrawdown = indicator1.LinesSeries[0].GetValue(0);
        double largeDrawdown = indicator2.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(smallDrawdown));
        Assert.True(double.IsFinite(largeDrawdown));
        Assert.True(largeDrawdown > smallDrawdown, "Deeper drawdown should produce higher UI value");
    }

    [Fact]
    public void UiIndicator_UsesClosePrice_NotHighLow()
    {
        // UI uses close price for both the rolling max and drawdown calculation
        var indicator = new UiIndicator { Period = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Price with constant close but varying high/low
        for (int i = 0; i < 30; i++)
        {
            // Close is constant at 100, but high/low varies
            double highRange = 5 + (i % 5);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 100 + highRange, 100 - highRange, 100, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(val));
        // Since close is always 100 (at period high), UI should be near zero
        Assert.True(val < 0.5, "Constant close should produce near-zero UI regardless of high/low range");
    }

    [Fact]
    public void UiIndicator_ConstantPrice_ProducesZero()
    {
        var indicator = new UiIndicator { Period = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Constant price - no drawdown possible
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 100.01, 99.99, 100, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(val));
        Assert.True(val < 0.1, "Constant price should produce near-zero UI");
    }

    [Fact]
    public void UiIndicator_RecoveryFromDrawdown_ReducesValue()
    {
        var indicator = new UiIndicator { Period = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Initial rise to establish a high
        for (int i = 0; i < 10; i++)
        {
            double price = 100 + i; // Rise to 109
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Drawdown - price drops significantly
        for (int i = 10; i < 15; i++)
        {
            double price = 109 - (i - 10) * 4; // Drop from 109 to 89
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double duringDrawdown = indicator.LinesSeries[0].GetValue(0);

        // Full recovery - price rises ABOVE the old high (so no more drawdown)
        // Need at least 10 more bars of rising prices to fully replace the drawdown window
        for (int i = 15; i < 30; i++)
        {
            double price = 89 + (i - 15) * 3; // Rise from 89 to 134 (well past old high of 109)
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double afterRecovery = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(duringDrawdown));
        Assert.True(double.IsFinite(afterRecovery));
        Assert.True(duringDrawdown > 0, "During drawdown, UI should be positive");
        // After 15 bars of rising prices past the old high, UI should be near zero or much lower
        Assert.True(afterRecovery < duringDrawdown, "Recovery from drawdown should reduce UI value");
    }
}