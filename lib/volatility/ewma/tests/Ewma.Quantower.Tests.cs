using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class EwmaIndicatorTests
{
    [Fact]
    public void EwmaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new EwmaIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.True(indicator.AnnualizeVol);
        Assert.Equal(252, indicator.AnnualPeriods);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("EWMA - Exponentially Weighted Moving Average Volatility", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void EwmaIndicator_ShortName_IncludesParameters()
    {
        var indicator = new EwmaIndicator { Period = 14, AnnualizeVol = true, AnnualPeriods = 252 };
        Assert.Contains("EWMA", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void EwmaIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new EwmaIndicator();

        Assert.Equal(0, EwmaIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void EwmaIndicator_Initialize_CreatesInternalEwma()
    {
        var indicator = new EwmaIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void EwmaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new EwmaIndicator { Period = 5, AnnualizeVol = false };
        indicator.Initialize();

        // Add historical data with varying prices
        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double basePrice = 100 + i + (i % 5); // Varying prices
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 2, basePrice - 2, basePrice + 1, 1000);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void EwmaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new EwmaIndicator { Period = 5, AnnualizeVol = false };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double basePrice = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar with price change
        indicator.HistoricalData.AddBar(now.AddMinutes(30), 120, 125, 115, 122, 1500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void EwmaIndicator_DifferentPeriods_Work()
    {
        int[] periods = { 5, 10, 14, 20 };

        foreach (var period in periods)
        {
            var indicator = new EwmaIndicator { Period = period, AnnualizeVol = false };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 50; i++)
            {
                double basePrice = 100 + i + (i % 4);
                indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 3, basePrice - 3, basePrice + 1, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double val = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(val), $"Period {period} should produce finite value");
        }
    }

    [Fact]
    public void EwmaIndicator_DifferentAnnualPeriods_Work()
    {
        int[] annualPeriods = { 12, 52, 252, 365 };

        foreach (var annualPeriod in annualPeriods)
        {
            var indicator = new EwmaIndicator { Period = 10, AnnualizeVol = true, AnnualPeriods = annualPeriod };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 50; i++)
            {
                double basePrice = 100 + i + (i % 4);
                indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 3, basePrice - 3, basePrice + 1, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double val = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(val), $"Annual period {annualPeriod} should produce finite value");
        }
    }

    [Fact]
    public void EwmaIndicator_Period_CanBeChanged()
    {
        var indicator = new EwmaIndicator();
        Assert.Equal(20, indicator.Period);

        indicator.Period = 14;
        Assert.Equal(14, indicator.Period);

        indicator.Period = 30;
        Assert.Equal(30, indicator.Period);
    }

    [Fact]
    public void EwmaIndicator_AnnualizeVol_CanBeToggled()
    {
        var indicator = new EwmaIndicator();
        Assert.True(indicator.AnnualizeVol);

        indicator.AnnualizeVol = false;
        Assert.False(indicator.AnnualizeVol);

        indicator.AnnualizeVol = true;
        Assert.True(indicator.AnnualizeVol);
    }

    [Fact]
    public void EwmaIndicator_AnnualPeriods_CanBeChanged()
    {
        var indicator = new EwmaIndicator();
        Assert.Equal(252, indicator.AnnualPeriods);

        indicator.AnnualPeriods = 52;
        Assert.Equal(52, indicator.AnnualPeriods);

        indicator.AnnualPeriods = 365;
        Assert.Equal(365, indicator.AnnualPeriods);
    }

    [Fact]
    public void EwmaIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new EwmaIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void EwmaIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new EwmaIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Ewma.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void EwmaIndicator_ConstantPrices_ProducesZeroVolatility()
    {
        var indicator = new EwmaIndicator { Period = 5, AnnualizeVol = false };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 100, 100, 100, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val), "Constant prices should produce finite value");
        Assert.Equal(0.0, val, 1e-10);
    }

    [Fact]
    public void EwmaIndicator_VolatilePrices_ProducesPositiveVolatility()
    {
        var indicator = new EwmaIndicator { Period = 5, AnnualizeVol = false };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            // Alternating prices to create volatility
            double price = (i % 2 == 0) ? 100 : 110;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val), "Volatile prices should produce finite value");
        Assert.True(val > 0, "Volatile prices should produce positive volatility");
    }

    [Fact]
    public void EwmaIndicator_AnnualizationMultipliesVolatility()
    {
        var indicatorNoAnn = new EwmaIndicator { Period = 10, AnnualizeVol = false };
        var indicatorAnn = new EwmaIndicator { Period = 10, AnnualizeVol = true, AnnualPeriods = 252 };
        indicatorNoAnn.Initialize();
        indicatorAnn.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            double price = 100 + i + (i % 5);
            indicatorNoAnn.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price, 1000);
            indicatorAnn.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price, 1000);
            indicatorNoAnn.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            indicatorAnn.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double valNoAnn = indicatorNoAnn.LinesSeries[0].GetValue(0);
        double valAnn = indicatorAnn.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(valNoAnn));
        Assert.True(double.IsFinite(valAnn));

        // Annualized should be approximately sqrt(252) times larger
        if (valNoAnn > 1e-10)
        {
            double ratio = valAnn / valNoAnn;
            double expectedRatio = Math.Sqrt(252);
            Assert.True(Math.Abs(ratio - expectedRatio) < 0.01,
                $"Annualized volatility ratio should be ~{expectedRatio}, got {ratio}");
        }
    }

    [Fact]
    public void EwmaIndicator_ShorterPeriod_MoreResponsive()
    {
        var indicatorShort = new EwmaIndicator { Period = 5, AnnualizeVol = false };
        var indicatorLong = new EwmaIndicator { Period = 50, AnnualizeVol = false };
        indicatorShort.Initialize();
        indicatorLong.Initialize();

        var now = DateTime.UtcNow;

        // Build up history with low volatility
        for (int i = 0; i < 60; i++)
        {
            indicatorShort.HistoricalData.AddBar(now.AddMinutes(i), 100, 101, 99, 100, 1000);
            indicatorLong.HistoricalData.AddBar(now.AddMinutes(i), 100, 101, 99, 100, 1000);
            indicatorShort.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            indicatorLong.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double shortBefore = indicatorShort.LinesSeries[0].GetValue(0);
        double longBefore = indicatorLong.LinesSeries[0].GetValue(0);

        // Inject shock
        indicatorShort.HistoricalData.AddBar(now.AddMinutes(60), 100, 120, 80, 110, 1500);
        indicatorLong.HistoricalData.AddBar(now.AddMinutes(60), 100, 120, 80, 110, 1500);
        indicatorShort.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));
        indicatorLong.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double shortAfter = indicatorShort.LinesSeries[0].GetValue(0);
        double longAfter = indicatorLong.LinesSeries[0].GetValue(0);

        double shortIncrease = shortAfter - shortBefore;
        double longIncrease = longAfter - longBefore;

        Assert.True(shortIncrease > longIncrease,
            "Shorter period should respond more strongly to shocks");
    }
}
