using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class CcvIndicatorTests
{
    [Fact]
    public void CcvIndicator_Constructor_SetsDefaults()
    {
        var indicator = new CcvIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(1, indicator.Method);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("CCV - Close-to-Close Volatility", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void CcvIndicator_ShortName_IncludesParameters()
    {
        var indicator = new CcvIndicator { Period = 14, Method = 2 };
        Assert.Contains("CCV", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("2", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void CcvIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new CcvIndicator();

        Assert.Equal(0, CcvIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void CcvIndicator_Initialize_CreatesInternalCcv()
    {
        var indicator = new CcvIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void CcvIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new CcvIndicator { Period = 5 };
        indicator.Initialize();

        // Add historical data with volatility
        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double basePrice = 100 + i * 2 + (i % 2 == 0 ? 5 : -5); // Add some volatility
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
        Assert.True(val >= 0); // CCV should be non-negative
    }

    [Fact]
    public void CcvIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new CcvIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double basePrice = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(30), 120, 128, 115, 125, 1500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void CcvIndicator_DifferentPeriods_Work()
    {
        int[] periods = { 5, 10, 20, 50 };

        foreach (var period in periods)
        {
            var indicator = new CcvIndicator { Period = period };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 60; i++)
            {
                double basePrice = 100 + i + (i % 3 == 0 ? 10 : -5); // Add volatility
                indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double val = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(val), $"Period {period} should produce finite value");
            Assert.True(val >= 0, $"Period {period} should produce non-negative CCV");
        }
    }

    [Fact]
    public void CcvIndicator_DifferentMethods_Work()
    {
        int[] methods = { 1, 2, 3 }; // SMA, EMA, WMA

        foreach (var method in methods)
        {
            var indicator = new CcvIndicator { Method = method };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 50; i++)
            {
                double basePrice = 100 + i;
                indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double val = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(val), $"Method {method} should produce finite value");
            Assert.True(val >= 0, $"Method {method} should produce non-negative CCV");
        }
    }

    [Fact]
    public void CcvIndicator_DifferentSourceTypes_Work()
    {
        SourceType[] sources = { SourceType.Close, SourceType.High, SourceType.Low, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new CcvIndicator { Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 40; i++)
            {
                double basePrice = 100 + i;
                indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double val = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(val), $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void CcvIndicator_Period_CanBeChanged()
    {
        var indicator = new CcvIndicator();
        Assert.Equal(20, indicator.Period);

        indicator.Period = 14;
        Assert.Equal(14, indicator.Period);

        indicator.Period = 50;
        Assert.Equal(50, indicator.Period);
    }

    [Fact]
    public void CcvIndicator_Method_CanBeChanged()
    {
        var indicator = new CcvIndicator();
        Assert.Equal(1, indicator.Method);

        indicator.Method = 2;
        Assert.Equal(2, indicator.Method);

        indicator.Method = 3;
        Assert.Equal(3, indicator.Method);
    }

    [Fact]
    public void CcvIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new CcvIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void CcvIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new CcvIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Ccv.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }
}