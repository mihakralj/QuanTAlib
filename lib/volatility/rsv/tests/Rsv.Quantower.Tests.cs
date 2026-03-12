using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class RsvIndicatorTests
{
    [Fact]
    public void RsvIndicator_Constructor_SetsDefaults()
    {
        var indicator = new RsvIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.True(indicator.Annualize);
        Assert.Equal(252, indicator.AnnualPeriods);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("RSV - Rogers-Satchell Volatility", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void RsvIndicator_ShortName_IncludesParameters()
    {
        var indicator = new RsvIndicator { Period = 14 };
        Assert.Contains("RSV", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void RsvIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new RsvIndicator();

        Assert.Equal(0, RsvIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void RsvIndicator_Initialize_CreatesInternalRsv()
    {
        var indicator = new RsvIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void RsvIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new RsvIndicator { Period = 10 };
        indicator.Initialize();

        // Add historical data with varying volatility
        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double basePrice = 100 + i;
            double range = 2 + (i % 5); // Varying ranges
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + range, basePrice - range, basePrice + 1, 1000);

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
    public void RsvIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new RsvIndicator { Period = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double basePrice = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar with larger range
        indicator.HistoricalData.AddBar(now.AddMinutes(30), 120, 135, 105, 125, 1500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void RsvIndicator_DifferentPeriods_Work()
    {
        int[] periods = { 5, 10, 14, 20 };

        foreach (var period in periods)
        {
            var indicator = new RsvIndicator { Period = period };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 50; i++)
            {
                double basePrice = 100 + i;
                double range = 3 + (i % 4);
                indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + range, basePrice - range, basePrice + 1, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double val = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(val), $"Period {period} should produce finite value");
            Assert.True(val >= 0, $"Period {period} should produce non-negative value");
        }
    }

    [Fact]
    public void RsvIndicator_Period_CanBeChanged()
    {
        var indicator = new RsvIndicator();
        Assert.Equal(20, indicator.Period);

        indicator.Period = 14;
        Assert.Equal(14, indicator.Period);

        indicator.Period = 10;
        Assert.Equal(10, indicator.Period);
    }

    [Fact]
    public void RsvIndicator_Annualize_CanBeToggled()
    {
        var indicator = new RsvIndicator();
        Assert.True(indicator.Annualize);

        indicator.Annualize = false;
        Assert.False(indicator.Annualize);

        indicator.Annualize = true;
        Assert.True(indicator.Annualize);
    }

    [Fact]
    public void RsvIndicator_AnnualPeriods_CanBeChanged()
    {
        var indicator = new RsvIndicator();
        Assert.Equal(252, indicator.AnnualPeriods);

        indicator.AnnualPeriods = 365;
        Assert.Equal(365, indicator.AnnualPeriods);

        indicator.AnnualPeriods = 52;
        Assert.Equal(52, indicator.AnnualPeriods);
    }

    [Fact]
    public void RsvIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new RsvIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void RsvIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new RsvIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Rsv.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void RsvIndicator_HighVolatility_ProducesHigherValue()
    {
        var indicator1 = new RsvIndicator { Period = 10, Annualize = false };
        var indicator2 = new RsvIndicator { Period = 10, Annualize = false };
        indicator1.Initialize();
        indicator2.Initialize();

        var now = DateTime.UtcNow;

        // Indicator 1: low volatility (narrow range)
        for (int i = 0; i < 30; i++)
        {
            double basePrice = 100;
            indicator1.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 1, basePrice - 1, basePrice + 0.5, 1000);
            indicator1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Indicator 2: high volatility (wide range)
        for (int i = 0; i < 30; i++)
        {
            double basePrice = 100;
            indicator2.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 10, basePrice - 10, basePrice + 2, 1000);
            indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lowVol = indicator1.LinesSeries[0].GetValue(0);
        double highVol = indicator2.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(lowVol));
        Assert.True(double.IsFinite(highVol));
        Assert.True(highVol > lowVol, "Higher volatility bars should produce higher RSV value");
    }

    [Fact]
    public void RsvIndicator_AnnualizedValue_IsScaled()
    {
        var indicatorRaw = new RsvIndicator { Period = 10, Annualize = false };
        var indicatorAnn = new RsvIndicator { Period = 10, Annualize = true, AnnualPeriods = 252 };
        indicatorRaw.Initialize();
        indicatorAnn.Initialize();

        var now = DateTime.UtcNow;

        // Same data for both
        for (int i = 0; i < 30; i++)
        {
            double basePrice = 100 + (i * 0.5);
            indicatorRaw.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 3, basePrice - 3, basePrice + 1, 1000);
            indicatorRaw.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            indicatorAnn.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 3, basePrice - 3, basePrice + 1, 1000);
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
    public void RsvIndicator_UsesAllOhlc_SensitiveToOpenClose()
    {
        // Test that RSV uses all OHLC prices (unlike HLV which only uses H-L)
        var indicator1 = new RsvIndicator { Period = 10, Annualize = false };
        var indicator2 = new RsvIndicator { Period = 10, Annualize = false };
        indicator1.Initialize();
        indicator2.Initialize();

        var now = DateTime.UtcNow;

        // Same high/low range but different open/close
        for (int i = 0; i < 30; i++)
        {
            // Indicator 1: open = close (doji pattern at center)
            indicator1.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100, 1000);
            indicator1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            // Indicator 2: open and close at extremes (strong directional move)
            indicator2.HistoricalData.AddBar(now.AddMinutes(i), 95.5, 105, 95, 104.5, 1000);
            indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val1 = indicator1.LinesSeries[0].GetValue(0);
        double val2 = indicator2.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(val1));
        Assert.True(double.IsFinite(val2));
        // RSV should be different since it uses all OHLC prices
        Assert.NotEqual(val1, val2, 5); // Values should differ significantly
    }

    [Fact]
    public void RsvIndicator_ConstantPrice_ProducesZeroVolatility()
    {
        var indicator = new RsvIndicator { Period = 10, Annualize = false };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Constant price (no volatility) - but need small spread to avoid log(1) issues
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 100.01, 99.99, 100, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(val));
        Assert.True(val < 0.01, "Near-constant price should produce near-zero volatility");
    }

    [Fact]
    public void RsvIndicator_DriftAdjusted_HandlesUptrend()
    {
        // RSV is drift-adjusted, so should handle trending markets well
        var indicator = new RsvIndicator { Period = 10, Annualize = false };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Strong uptrend with consistent volatility
        for (int i = 0; i < 30; i++)
        {
            double basePrice = 100 + (i * 2); // Trending up
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 3, basePrice - 2, basePrice + 1, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(val));
        Assert.True(val > 0, "Trending market with volatility should produce positive RSV");
    }
}
