using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class NatrIndicatorTests
{
    [Fact]
    public void NatrIndicator_Constructor_SetsDefaults()
    {
        var indicator = new NatrIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("NATR - Normalized Average True Range", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void NatrIndicator_ShortName_IncludesParameters()
    {
        var indicator = new NatrIndicator { Period = 20 };
        Assert.Equal("NATR 20", indicator.ShortName);
    }

    [Fact]
    public void NatrIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new NatrIndicator();

        Assert.Equal(0, NatrIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void NatrIndicator_Initialize_CreatesInternalNatr()
    {
        var indicator = new NatrIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void NatrIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new NatrIndicator { Period = 5 };
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
        Assert.True(val > 0); // NATR should be positive with volatility
        Assert.True(val < 100); // NATR as percentage should be reasonable
    }

    [Fact]
    public void NatrIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new NatrIndicator { Period = 5 };
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
    public void NatrIndicator_DifferentPeriods_Work()
    {
        int[] periods = { 5, 10, 14, 20, 50 };

        foreach (var period in periods)
        {
            var indicator = new NatrIndicator { Period = period };
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
            Assert.True(val > 0, $"Period {period} should produce positive NATR");
        }
    }

    [Fact]
    public void NatrIndicator_Period_CanBeChanged()
    {
        var indicator = new NatrIndicator();
        Assert.Equal(14, indicator.Period);

        indicator.Period = 20;
        Assert.Equal(20, indicator.Period);

        indicator.Period = 5;
        Assert.Equal(5, indicator.Period);
    }

    [Fact]
    public void NatrIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new NatrIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void NatrIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new NatrIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Natr.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void NatrIndicator_Description_IsSet()
    {
        var indicator = new NatrIndicator();
        Assert.Contains("percentage", indicator.Description, StringComparison.OrdinalIgnoreCase);
    }
}
