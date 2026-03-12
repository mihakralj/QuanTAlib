using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class EthermIndicatorTests
{
    [Fact]
    public void EthermIndicator_Constructor_SetsDefaults()
    {
        var indicator = new EthermIndicator();

        Assert.Equal(22, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("ETHERM - Elder's Thermometer", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void EthermIndicator_ShortName_IncludesParameters()
    {
        var indicator = new EthermIndicator { Period = 14 };
        Assert.Equal("ETHERM 14", indicator.ShortName);
    }

    [Fact]
    public void EthermIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new EthermIndicator();

        Assert.Equal(0, EthermIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void EthermIndicator_Initialize_CreatesInternalEtherm()
    {
        var indicator = new EthermIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (2: temperature + signal)
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void EthermIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new EthermIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Temperature line series should have a value
        double tempVal = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(tempVal));

        // Signal line series should have a value
        double sigVal = indicator.LinesSeries[1].GetValue(0);
        Assert.True(double.IsFinite(sigVal));
    }

    [Fact]
    public void EthermIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new EthermIndicator { Period = 5 };
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
    public void EthermIndicator_DifferentPeriods_Work()
    {
        int[] periods = { 5, 10, 14, 22, 50 };

        foreach (var period in periods)
        {
            var indicator = new EthermIndicator { Period = period };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 60; i++)
            {
                double basePrice = 100 + i;
                indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double tempVal = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(tempVal), $"Period {period} should produce finite temperature");

            double sigVal = indicator.LinesSeries[1].GetValue(0);
            Assert.True(double.IsFinite(sigVal), $"Period {period} should produce finite signal");
        }
    }

    [Fact]
    public void EthermIndicator_Period_CanBeChanged()
    {
        var indicator = new EthermIndicator();
        Assert.Equal(22, indicator.Period);

        indicator.Period = 14;
        Assert.Equal(14, indicator.Period);

        indicator.Period = 5;
        Assert.Equal(5, indicator.Period);
    }

    [Fact]
    public void EthermIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new EthermIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void EthermIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new EthermIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Etherm.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void EthermIndicator_HasTwoLineSeries_WithCorrectNames()
    {
        var indicator = new EthermIndicator();
        indicator.Initialize();

        Assert.Equal(2, indicator.LinesSeries.Count);
        Assert.Equal("Temperature", indicator.LinesSeries[0].Name);
        Assert.Equal("Signal", indicator.LinesSeries[1].Name);
    }
}
