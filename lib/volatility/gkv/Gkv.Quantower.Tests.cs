using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class GkvIndicatorTests
{
    [Fact]
    public void GkvIndicator_Constructor_SetsDefaults()
    {
        var indicator = new GkvIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.True(indicator.Annualize);
        Assert.Equal(252, indicator.AnnualPeriods);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("GKV - Garman-Klass Volatility", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void GkvIndicator_ShortName_IncludesParameters()
    {
        var indicator = new GkvIndicator { Period = 14 };
        Assert.Contains("GKV", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void GkvIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new GkvIndicator();

        Assert.Equal(0, GkvIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void GkvIndicator_Initialize_CreatesInternalGkv()
    {
        var indicator = new GkvIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void GkvIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new GkvIndicator { Period = 10 };
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
    public void GkvIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new GkvIndicator { Period = 10 };
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
    public void GkvIndicator_DifferentPeriods_Work()
    {
        int[] periods = { 5, 10, 14, 20 };

        foreach (var period in periods)
        {
            var indicator = new GkvIndicator { Period = period };
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
    public void GkvIndicator_Period_CanBeChanged()
    {
        var indicator = new GkvIndicator();
        Assert.Equal(20, indicator.Period);

        indicator.Period = 14;
        Assert.Equal(14, indicator.Period);

        indicator.Period = 10;
        Assert.Equal(10, indicator.Period);
    }

    [Fact]
    public void GkvIndicator_Annualize_CanBeToggled()
    {
        var indicator = new GkvIndicator();
        Assert.True(indicator.Annualize);

        indicator.Annualize = false;
        Assert.False(indicator.Annualize);

        indicator.Annualize = true;
        Assert.True(indicator.Annualize);
    }

    [Fact]
    public void GkvIndicator_AnnualPeriods_CanBeChanged()
    {
        var indicator = new GkvIndicator();
        Assert.Equal(252, indicator.AnnualPeriods);

        indicator.AnnualPeriods = 365;
        Assert.Equal(365, indicator.AnnualPeriods);

        indicator.AnnualPeriods = 52;
        Assert.Equal(52, indicator.AnnualPeriods);
    }

    [Fact]
    public void GkvIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new GkvIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void GkvIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new GkvIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Gkv.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void GkvIndicator_HighVolatility_ProducesHigherValue()
    {
        var indicator1 = new GkvIndicator { Period = 10, Annualize = false };
        var indicator2 = new GkvIndicator { Period = 10, Annualize = false };
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
        Assert.True(highVol > lowVol, "Higher volatility bars should produce higher GKV value");
    }

    [Fact]
    public void GkvIndicator_AnnualizedValue_IsScaled()
    {
        var indicatorRaw = new GkvIndicator { Period = 10, Annualize = false };
        var indicatorAnn = new GkvIndicator { Period = 10, Annualize = true, AnnualPeriods = 252 };
        indicatorRaw.Initialize();
        indicatorAnn.Initialize();

        var now = DateTime.UtcNow;

        // Same data for both
        for (int i = 0; i < 30; i++)
        {
            double basePrice = 100 + i * 0.5;
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
    public void GkvIndicator_UsesAllOhlcPrices()
    {
        // Test that GKV uses all 4 prices (OHLC)
        var indicator1 = new GkvIndicator { Period = 10, Annualize = false };
        var indicator2 = new GkvIndicator { Period = 10, Annualize = false };
        indicator1.Initialize();
        indicator2.Initialize();

        var now = DateTime.UtcNow;

        // Same high/low range but different open/close
        for (int i = 0; i < 30; i++)
        {
            // Indicator 1: open = close (doji pattern)
            indicator1.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100, 1000);
            indicator1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            // Indicator 2: open != close (directional move)
            indicator2.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 104, 1000);
            indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val1 = indicator1.LinesSeries[0].GetValue(0);
        double val2 = indicator2.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(val1));
        Assert.True(double.IsFinite(val2));
        // GKV uses close-open term, so values should differ
        Assert.NotEqual(val1, val2);
    }

    [Fact]
    public void GkvIndicator_ConstantPrice_ProducesZeroVolatility()
    {
        var indicator = new GkvIndicator { Period = 10, Annualize = false };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Constant price (no volatility)
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 100, 100, 100, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(val));
        Assert.True(val < 0.001, "Constant price should produce near-zero volatility");
    }
}
