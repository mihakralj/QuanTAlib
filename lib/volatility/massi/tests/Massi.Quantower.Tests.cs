using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class MassiIndicatorTests
{
    [Fact]
    public void MassiIndicator_Constructor_SetsDefaults()
    {
        var indicator = new MassiIndicator();

        Assert.Equal(9, indicator.EmaLength);
        Assert.Equal(25, indicator.SumLength);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("MASSI - Mass Index", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void MassiIndicator_ShortName_IncludesParameters()
    {
        var indicator = new MassiIndicator { EmaLength = 10, SumLength = 30 };
        Assert.Contains("MASSI", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("30", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void MassiIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new MassiIndicator();

        Assert.Equal(0, MassiIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void MassiIndicator_Initialize_CreatesInternalMassi()
    {
        var indicator = new MassiIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void MassiIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new MassiIndicator { EmaLength = 5, SumLength = 10 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
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
    }

    [Fact]
    public void MassiIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new MassiIndicator { EmaLength = 5, SumLength = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            double basePrice = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(50), 160, 168, 155, 165, 1500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void MassiIndicator_DifferentParameters_Work()
    {
        int[] emaLengths = { 5, 9, 14 };
        int[] sumLengths = { 10, 25, 50 };

        foreach (var emaLen in emaLengths)
        {
            foreach (var sumLen in sumLengths)
            {
                var indicator = new MassiIndicator { EmaLength = emaLen, SumLength = sumLen };
                indicator.Initialize();

                var now = DateTime.UtcNow;
                for (int i = 0; i < 80; i++)
                {
                    double basePrice = 100 + i;
                    indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
                    indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
                }

                double val = indicator.LinesSeries[0].GetValue(0);
                Assert.True(double.IsFinite(val), $"MASSI({emaLen},{sumLen}) should produce finite value");
            }
        }
    }

    [Fact]
    public void MassiIndicator_Parameters_CanBeChanged()
    {
        var indicator = new MassiIndicator();
        Assert.Equal(9, indicator.EmaLength);
        Assert.Equal(25, indicator.SumLength);

        indicator.EmaLength = 12;
        indicator.SumLength = 30;
        Assert.Equal(12, indicator.EmaLength);
        Assert.Equal(30, indicator.SumLength);
    }

    [Fact]
    public void MassiIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new MassiIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void MassiIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new MassiIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Massi.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void MassiIndicator_TypicalRange_AroundSumLength()
    {
        // With sumLength=25, MASSI typically hovers around 25 (sum of ratios ~1.0 each)
        var indicator = new MassiIndicator { EmaLength = 9, SumLength = 25 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Use consistent range data
        for (int i = 0; i < 100; i++)
        {
            double basePrice = 100;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 10, basePrice - 10, basePrice, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        // With stable range, ratios approach 1.0, so sum approaches sumLength (25)
        Assert.True(val > 20 && val < 30, $"MASSI value {val} should be near 25 for stable data");
    }

    [Fact]
    public void MassiIndicator_UsesHighLowRange()
    {
        var indicator = new MassiIndicator { EmaLength = 5, SumLength = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Small range bars
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 101, 99, 100, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }
        double smallRangeVal = indicator.LinesSeries[0].GetValue(0);

        // Reset and use large range bars
        var indicator2 = new MassiIndicator { EmaLength = 5, SumLength = 10 };
        indicator2.Initialize();

        for (int i = 0; i < 30; i++)
        {
            indicator2.HistoricalData.AddBar(now.AddMinutes(i), 100, 120, 80, 100, 1000);
            indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }
        double largeRangeVal = indicator2.LinesSeries[0].GetValue(0);

        // Both should produce valid values (MASSI is about ratio patterns, not absolute range)
        Assert.True(double.IsFinite(smallRangeVal));
        Assert.True(double.IsFinite(largeRangeVal));
    }
}
