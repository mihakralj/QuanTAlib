using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class JvoltynIndicatorTests
{
    [Fact]
    public void JvoltynIndicator_Constructor_SetsDefaults()
    {
        var indicator = new JvoltynIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("JVOLTYN - Normalized Jurik Volatility", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void JvoltynIndicator_ShortName_IncludesParameters()
    {
        var indicator = new JvoltynIndicator { Period = 20 };
        Assert.Contains("JVOLTYN", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void JvoltynIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new JvoltynIndicator();

        Assert.Equal(0, JvoltynIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void JvoltynIndicator_Initialize_CreatesInternalJvoltyn()
    {
        var indicator = new JvoltynIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void JvoltynIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new JvoltynIndicator { Period = 5 };
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
        Assert.True(val >= 0.0); // Jvoltyn minimum is 0
        Assert.True(val <= 100.0); // Jvoltyn maximum is 100
    }

    [Fact]
    public void JvoltynIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new JvoltynIndicator { Period = 5 };
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
    public void JvoltynIndicator_DifferentPeriods_Work()
    {
        int[] periods = { 5, 10, 14, 20, 50 };

        foreach (var period in periods)
        {
            var indicator = new JvoltynIndicator { Period = period };
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
            Assert.True(val >= 0.0, $"Period {period} should produce Jvoltyn >= 0");
            Assert.True(val <= 100.0, $"Period {period} should produce Jvoltyn <= 100");
        }
    }

    [Fact]
    public void JvoltynIndicator_DifferentSourceTypes_Work()
    {
        SourceType[] sources = { SourceType.Close, SourceType.High, SourceType.Low, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new JvoltynIndicator { Source = source };
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
    public void JvoltynIndicator_Period_CanBeChanged()
    {
        var indicator = new JvoltynIndicator();
        Assert.Equal(14, indicator.Period);

        indicator.Period = 20;
        Assert.Equal(20, indicator.Period);

        indicator.Period = 50;
        Assert.Equal(50, indicator.Period);
    }

    [Fact]
    public void JvoltynIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new JvoltynIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void JvoltynIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new JvoltynIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Jvoltyn.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void JvoltynIndicator_OutputRange_IsZeroToHundred()
    {
        var indicator = new JvoltynIndicator { Period = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 100; i++)
        {
            // Create varying volatility patterns
            double basePrice = 100 + (i % 10) * 5 + (i % 2 == 0 ? 20 : -15);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 10, basePrice - 10, basePrice + 3, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            double val = indicator.LinesSeries[0].GetValue(0);
            Assert.True(val >= 0.0, $"Bar {i}: value {val} should be >= 0");
            Assert.True(val <= 100.0, $"Bar {i}: value {val} should be <= 100");
        }
    }
}