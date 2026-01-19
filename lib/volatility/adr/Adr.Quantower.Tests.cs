using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class AdrIndicatorTests
{
    [Fact]
    public void AdrIndicator_Constructor_SetsDefaults()
    {
        var indicator = new AdrIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.Equal(AdrMethod.Sma, indicator.Method);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("ADR - Average Daily Range", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void AdrIndicator_ShortName_IncludesParameters()
    {
        var indicator = new AdrIndicator { Period = 20, Method = AdrMethod.Ema };
        Assert.Equal("ADR 20 Ema", indicator.ShortName);
    }

    [Fact]
    public void AdrIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new AdrIndicator();

        Assert.Equal(0, AdrIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void AdrIndicator_Initialize_CreatesInternalAdr()
    {
        var indicator = new AdrIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void AdrIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new AdrIndicator { Period = 5 };
        indicator.Initialize();

        // Add historical data with volatility
        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
        Assert.True(val > 0); // ADR should be positive with volatility
    }

    [Fact]
    public void AdrIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new AdrIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(20), 120, 128, 115, 125, 1500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void AdrIndicator_DifferentPeriods_Work()
    {
        int[] periods = { 5, 10, 14, 20, 50 };

        foreach (var period in periods)
        {
            var indicator = new AdrIndicator { Period = period };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 60; i++)
            {
                double basePrice = 100 + i;
                indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double val = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(val), $"Period {period} should produce finite value");
            Assert.True(val > 0, $"Period {period} should produce positive ADR");
        }
    }

    [Fact]
    public void AdrIndicator_DifferentMethods_Work()
    {
        AdrMethod[] methods = { AdrMethod.Sma, AdrMethod.Ema, AdrMethod.Wma };

        foreach (var method in methods)
        {
            var indicator = new AdrIndicator { Period = 14, Method = method };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 30; i++)
            {
                double basePrice = 100 + i;
                indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double val = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(val), $"Method {method} should produce finite value");
            Assert.True(val > 0, $"Method {method} should produce positive ADR");
        }
    }

    [Fact]
    public void AdrIndicator_Period_CanBeChanged()
    {
        var indicator = new AdrIndicator();
        Assert.Equal(14, indicator.Period);

        indicator.Period = 20;
        Assert.Equal(20, indicator.Period);

        indicator.Period = 5;
        Assert.Equal(5, indicator.Period);
    }

    [Fact]
    public void AdrIndicator_Method_CanBeChanged()
    {
        var indicator = new AdrIndicator();
        Assert.Equal(AdrMethod.Sma, indicator.Method);

        indicator.Method = AdrMethod.Ema;
        Assert.Equal(AdrMethod.Ema, indicator.Method);

        indicator.Method = AdrMethod.Wma;
        Assert.Equal(AdrMethod.Wma, indicator.Method);
    }

    [Fact]
    public void AdrIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new AdrIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void AdrIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new AdrIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Adr.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }
}