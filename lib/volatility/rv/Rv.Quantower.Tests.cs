using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class RvIndicatorTests
{
    [Fact]
    public void RvIndicator_Constructor_SetsDefaults()
    {
        var indicator = new RvIndicator();

        Assert.Equal(5, indicator.Period);
        Assert.Equal(20, indicator.SmoothingPeriod);
        Assert.True(indicator.Annualize);
        Assert.Equal(252, indicator.AnnualPeriods);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("RV - Realized Volatility", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void RvIndicator_ShortName_IncludesParameters()
    {
        var indicator = new RvIndicator { Period = 10, SmoothingPeriod = 15 };
        Assert.Contains("RV", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("15", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void RvIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new RvIndicator();

        Assert.Equal(0, RvIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void RvIndicator_Initialize_CreatesInternalRv()
    {
        var indicator = new RvIndicator();

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void RvIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new RvIndicator { Period = 5, SmoothingPeriod = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double closePrice = 100 + i * 0.5 + Math.Sin(i * 0.3) * 2;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), closePrice - 1, closePrice + 2, closePrice - 2, closePrice, 1000);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
        Assert.True(val >= 0, "Volatility should be non-negative");
    }

    [Fact]
    public void RvIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new RvIndicator { Period = 5, SmoothingPeriod = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double closePrice = 100 + i * 0.3;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), closePrice - 1, closePrice + 2, closePrice - 2, closePrice, 1000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        indicator.HistoricalData.AddBar(now.AddMinutes(30), 115, 120, 110, 118, 1500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void RvIndicator_DifferentPeriods_Work()
    {
        int[] periods = { 3, 5, 10 };

        foreach (var period in periods)
        {
            var indicator = new RvIndicator { Period = period, SmoothingPeriod = 10 };
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
    public void RvIndicator_Period_CanBeChanged()
    {
        var indicator = new RvIndicator();
        Assert.Equal(5, indicator.Period);

        indicator.Period = 10;
        Assert.Equal(10, indicator.Period);
    }

    [Fact]
    public void RvIndicator_SmoothingPeriod_CanBeChanged()
    {
        var indicator = new RvIndicator();
        Assert.Equal(20, indicator.SmoothingPeriod);

        indicator.SmoothingPeriod = 30;
        Assert.Equal(30, indicator.SmoothingPeriod);
    }

    [Fact]
    public void RvIndicator_Annualize_CanBeToggled()
    {
        var indicator = new RvIndicator();
        Assert.True(indicator.Annualize);

        indicator.Annualize = false;
        Assert.False(indicator.Annualize);

        indicator.Annualize = true;
        Assert.True(indicator.Annualize);
    }

    [Fact]
    public void RvIndicator_AnnualPeriods_CanBeChanged()
    {
        var indicator = new RvIndicator();
        Assert.Equal(252, indicator.AnnualPeriods);

        indicator.AnnualPeriods = 365;
        Assert.Equal(365, indicator.AnnualPeriods);
    }

    [Fact]
    public void RvIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new RvIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);
    }

    [Fact]
    public void RvIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new RvIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Rv.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void RvIndicator_HighVolatility_ProducesHigherValue()
    {
        var indicator1 = new RvIndicator { Period = 5, SmoothingPeriod = 10, Annualize = false };
        var indicator2 = new RvIndicator { Period = 5, SmoothingPeriod = 10, Annualize = false };
        indicator1.Initialize();
        indicator2.Initialize();

        var now = DateTime.UtcNow;

        // Low volatility
        for (int i = 0; i < 30; i++)
        {
            double closePrice = 100 + i * 0.01;
            indicator1.HistoricalData.AddBar(now.AddMinutes(i), closePrice - 0.5, closePrice + 0.5, closePrice - 0.5, closePrice, 1000);
            indicator1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // High volatility
        for (int i = 0; i < 30; i++)
        {
            double closePrice = 100 + Math.Sin(i * 0.5) * 10;
            indicator2.HistoricalData.AddBar(now.AddMinutes(i), closePrice - 2, closePrice + 2, closePrice - 2, closePrice, 1000);
            indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lowVol = indicator1.LinesSeries[0].GetValue(0);
        double highVol = indicator2.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(lowVol));
        Assert.True(double.IsFinite(highVol));
        Assert.True(highVol > lowVol, "Higher volatility closes should produce higher RV value");
    }

    [Fact]
    public void RvIndicator_AnnualizedValue_IsScaled()
    {
        var indicatorRaw = new RvIndicator { Period = 5, SmoothingPeriod = 10, Annualize = false };
        var indicatorAnn = new RvIndicator { Period = 5, SmoothingPeriod = 10, Annualize = true, AnnualPeriods = 252 };
        indicatorRaw.Initialize();
        indicatorAnn.Initialize();

        var now = DateTime.UtcNow;

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

        double expectedRatio = Math.Sqrt(252);
        double actualRatio = annValue / rawValue;

        Assert.True(Math.Abs(actualRatio - expectedRatio) < 0.01,
            $"Annualized value should be ~{expectedRatio:F2}× raw, got {actualRatio:F2}×");
    }

    [Fact]
    public void RvIndicator_OnlyUsesClose_IgnoresOpenHighLow()
    {
        var indicator1 = new RvIndicator { Period = 5, SmoothingPeriod = 10, Annualize = false };
        var indicator2 = new RvIndicator { Period = 5, SmoothingPeriod = 10, Annualize = false };
        indicator1.Initialize();
        indicator2.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 30; i++)
        {
            double closePrice = 100 + i * 0.5;
            // Narrow range
            indicator1.HistoricalData.AddBar(now.AddMinutes(i), closePrice, closePrice + 1, closePrice - 1, closePrice, 1000);
            indicator1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            // Wide range (same close)
            indicator2.HistoricalData.AddBar(now.AddMinutes(i), closePrice - 5, closePrice + 10, closePrice - 10, closePrice, 1000);
            indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val1 = indicator1.LinesSeries[0].GetValue(0);
        double val2 = indicator2.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(val1));
        Assert.True(double.IsFinite(val2));
        Assert.Equal(val1, val2, 10);
    }

    [Fact]
    public void RvIndicator_ConstantPrice_ProducesZeroVolatility()
    {
        var indicator = new RvIndicator { Period = 5, SmoothingPeriod = 10, Annualize = false };
        indicator.Initialize();

        var now = DateTime.UtcNow;

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
    public void RvIndicator_VaryingReturns_ProducesNonZeroVolatility()
    {
        var indicator = new RvIndicator { Period = 5, SmoothingPeriod = 10, Annualize = false };
        indicator.Initialize();

        var now = DateTime.UtcNow;

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

    [Fact]
    public void RvIndicator_DifferentSmoothingPeriods_ProduceDifferentResults()
    {
        var indicator1 = new RvIndicator { Period = 5, SmoothingPeriod = 5, Annualize = false };
        var indicator2 = new RvIndicator { Period = 5, SmoothingPeriod = 20, Annualize = false };
        indicator1.Initialize();
        indicator2.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 50; i++)
        {
            double closePrice = 100 + Math.Sin(i * 0.3) * 5;
            indicator1.HistoricalData.AddBar(now.AddMinutes(i), closePrice - 1, closePrice + 1, closePrice - 1, closePrice, 1000);
            indicator1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            indicator2.HistoricalData.AddBar(now.AddMinutes(i), closePrice - 1, closePrice + 1, closePrice - 1, closePrice, 1000);
            indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val1 = indicator1.LinesSeries[0].GetValue(0);
        double val2 = indicator2.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(val1));
        Assert.True(double.IsFinite(val2));
        // Different smoothing periods should produce different results
        Assert.NotEqual(val1, val2);
    }
}