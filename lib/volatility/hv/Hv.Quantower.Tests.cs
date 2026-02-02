using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class HvIndicatorTests
{
    [Fact]
    public void HvIndicator_Constructor_SetsDefaults()
    {
        var indicator = new HvIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.True(indicator.Annualize);
        Assert.Equal(252, indicator.AnnualPeriods);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("HV - Historical Volatility (Close-to-Close)", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void HvIndicator_ShortName_IncludesParameters()
    {
        var indicator = new HvIndicator { Period = 14 };
        Assert.Contains("HV", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void HvIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new HvIndicator();

        Assert.Equal(0, HvIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void HvIndicator_Initialize_CreatesInternalHv()
    {
        var indicator = new HvIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void HvIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new HvIndicator { Period = 10 };
        indicator.Initialize();

        // Add historical data with trending prices (needed for log returns)
        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double closePrice = 100 + i * 0.5 + Math.Sin(i * 0.3) * 2; // Trending with variation
            indicator.HistoricalData.AddBar(now.AddMinutes(i), closePrice - 1, closePrice + 2, closePrice - 2, closePrice, 1000);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
        Assert.True(val >= 0, "Volatility should be non-negative");
    }

    [Fact]
    public void HvIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new HvIndicator { Period = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double closePrice = 100 + i * 0.3;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), closePrice - 1, closePrice + 2, closePrice - 2, closePrice, 1000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar with price jump
        indicator.HistoricalData.AddBar(now.AddMinutes(30), 115, 120, 110, 118, 1500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void HvIndicator_DifferentPeriods_Work()
    {
        int[] periods = { 5, 10, 14, 20 };

        foreach (var period in periods)
        {
            var indicator = new HvIndicator { Period = period };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 50; i++)
            {
                double closePrice = 100 + i * 0.2 + Math.Sin(i * 0.5) * 3;
                indicator.HistoricalData.AddBar(now.AddMinutes(i), closePrice - 1, closePrice + 2, closePrice - 2, closePrice, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double val = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(val), $"Period {period} should produce finite value");
            Assert.True(val >= 0, $"Period {period} should produce non-negative value");
        }
    }

    [Fact]
    public void HvIndicator_Period_CanBeChanged()
    {
        var indicator = new HvIndicator();
        Assert.Equal(20, indicator.Period);

        indicator.Period = 14;
        Assert.Equal(14, indicator.Period);

        indicator.Period = 10;
        Assert.Equal(10, indicator.Period);
    }

    [Fact]
    public void HvIndicator_Annualize_CanBeToggled()
    {
        var indicator = new HvIndicator();
        Assert.True(indicator.Annualize);

        indicator.Annualize = false;
        Assert.False(indicator.Annualize);

        indicator.Annualize = true;
        Assert.True(indicator.Annualize);
    }

    [Fact]
    public void HvIndicator_AnnualPeriods_CanBeChanged()
    {
        var indicator = new HvIndicator();
        Assert.Equal(252, indicator.AnnualPeriods);

        indicator.AnnualPeriods = 365;
        Assert.Equal(365, indicator.AnnualPeriods);

        indicator.AnnualPeriods = 52;
        Assert.Equal(52, indicator.AnnualPeriods);
    }

    [Fact]
    public void HvIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new HvIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void HvIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new HvIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Hv.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void HvIndicator_HighVolatility_ProducesHigherValue()
    {
        var indicator1 = new HvIndicator { Period = 10, Annualize = false };
        var indicator2 = new HvIndicator { Period = 10, Annualize = false };
        indicator1.Initialize();
        indicator2.Initialize();

        var now = DateTime.UtcNow;

        // Indicator 1: low volatility (small price changes)
        for (int i = 0; i < 30; i++)
        {
            double closePrice = 100 + i * 0.01; // Small consistent changes
            indicator1.HistoricalData.AddBar(now.AddMinutes(i), closePrice - 0.5, closePrice + 0.5, closePrice - 0.5, closePrice, 1000);
            indicator1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Indicator 2: high volatility (large price swings)
        for (int i = 0; i < 30; i++)
        {
            double closePrice = 100 + Math.Sin(i * 0.5) * 10; // Large swings
            indicator2.HistoricalData.AddBar(now.AddMinutes(i), closePrice - 2, closePrice + 2, closePrice - 2, closePrice, 1000);
            indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lowVol = indicator1.LinesSeries[0].GetValue(0);
        double highVol = indicator2.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(lowVol));
        Assert.True(double.IsFinite(highVol));
        Assert.True(highVol > lowVol, "Higher volatility closes should produce higher HV value");
    }

    [Fact]
    public void HvIndicator_AnnualizedValue_IsScaled()
    {
        var indicatorRaw = new HvIndicator { Period = 10, Annualize = false };
        var indicatorAnn = new HvIndicator { Period = 10, Annualize = true, AnnualPeriods = 252 };
        indicatorRaw.Initialize();
        indicatorAnn.Initialize();

        var now = DateTime.UtcNow;

        // Same data for both - trending with variation
        for (int i = 0; i < 30; i++)
        {
            double closePrice = 100 + i * 0.5 + Math.Sin(i * 0.3) * 2;
            indicatorRaw.HistoricalData.AddBar(now.AddMinutes(i), closePrice - 1, closePrice + 2, closePrice - 2, closePrice, 1000);
            indicatorRaw.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            indicatorAnn.HistoricalData.AddBar(now.AddMinutes(i), closePrice - 1, closePrice + 2, closePrice - 2, closePrice, 1000);
            indicatorAnn.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double rawValue = indicatorRaw.LinesSeries[0].GetValue(0);
        double annValue = indicatorAnn.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(rawValue));
        Assert.True(double.IsFinite(annValue));

        // Annualized should be approximately sqrt(252) times larger
        double expectedRatio = Math.Sqrt(252);
        double actualRatio = annValue / rawValue;

        Assert.True(Math.Abs(actualRatio - expectedRatio) < 0.01,
            $"Annualized value should be ~{expectedRatio:F2}× raw, got {actualRatio:F2}×");
    }

    [Fact]
    public void HvIndicator_OnlyUsesClose_IgnoresOpenHighLow()
    {
        // Test that HV only uses Close (not Open-High-Low)
        var indicator1 = new HvIndicator { Period = 10, Annualize = false };
        var indicator2 = new HvIndicator { Period = 10, Annualize = false };
        indicator1.Initialize();
        indicator2.Initialize();

        var now = DateTime.UtcNow;

        // Same close prices but different high/low
        for (int i = 0; i < 30; i++)
        {
            double closePrice = 100 + i * 0.5;
            // Indicator 1: narrow range
            indicator1.HistoricalData.AddBar(now.AddMinutes(i), closePrice, closePrice + 1, closePrice - 1, closePrice, 1000);
            indicator1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            // Indicator 2: wide range (same close)
            indicator2.HistoricalData.AddBar(now.AddMinutes(i), closePrice - 5, closePrice + 10, closePrice - 10, closePrice, 1000);
            indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val1 = indicator1.LinesSeries[0].GetValue(0);
        double val2 = indicator2.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(val1));
        Assert.True(double.IsFinite(val2));
        // HV should be identical since close prices are the same
        Assert.Equal(val1, val2, 10);
    }

    [Fact]
    public void HvIndicator_ConstantPrice_ProducesZeroVolatility()
    {
        var indicator = new HvIndicator { Period = 10, Annualize = false };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Constant close price (no volatility in returns)
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(val));
        Assert.True(val < 0.001, "Constant close price should produce near-zero volatility");
    }

    [Fact]
    public void HvIndicator_VaryingReturns_ProducesNonZeroVolatility()
    {
        var indicator = new HvIndicator { Period = 10, Annualize = false };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Price with varying returns (not constant growth rate) - should have non-zero volatility
        // Alternating +2% and +0.5% returns to ensure variance in returns
        for (int i = 0; i < 30; i++)
        {
            double rate = (i % 2 == 0) ? 1.02 : 1.005;
            double closePrice = 100 * Math.Pow(rate, i / 2 + 1) * (i % 2 == 0 ? 1.0 : rate);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), closePrice - 1, closePrice + 1, closePrice - 1, closePrice, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(val));
        Assert.True(val > 0, "Varying returns should produce non-zero volatility");
    }
}