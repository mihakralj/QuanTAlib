using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class VhfIndicatorTests
{
    [Fact]
    public void VhfIndicator_Constructor_SetsDefaults()
    {
        var indicator = new VhfIndicator();

        Assert.Equal(28, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("VHF - Vertical Horizontal Filter", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void VhfIndicator_ShortName_IncludesParameters()
    {
        var indicator = new VhfIndicator { Period = 14 };
        indicator.Initialize();

        Assert.Contains("VHF", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void VhfIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new VhfIndicator();

        Assert.Equal(0, VhfIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void VhfIndicator_Initialize_CreatesInternalVhf()
    {
        var indicator = new VhfIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (single VHF line)
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void VhfIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new VhfIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double vhfVal = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(vhfVal));
        Assert.True(vhfVal >= 0);
    }

    [Fact]
    public void VhfIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new VhfIndicator { Period = 5 };
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
    public void VhfIndicator_DifferentPeriods_Work()
    {
        int[] periods = { 5, 10, 28 };

        foreach (int period in periods)
        {
            var indicator = new VhfIndicator { Period = period };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 100; i++)
            {
                double basePrice = 100 + i;
                indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double vhfVal = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(vhfVal), $"Period {period} should produce finite VHF");
        }
    }

    [Fact]
    public void VhfIndicator_Period_CanBeChanged()
    {
        var indicator = new VhfIndicator();
        Assert.Equal(28, indicator.Period);

        indicator.Period = 14;
        Assert.Equal(14, indicator.Period);
    }

    [Fact]
    public void VhfIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new VhfIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void VhfIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new VhfIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Vhf.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void VhfIndicator_HasOneLineSeries_WithCorrectName()
    {
        var indicator = new VhfIndicator();
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
        Assert.Equal("VHF", indicator.LinesSeries[0].Name);
    }
}
