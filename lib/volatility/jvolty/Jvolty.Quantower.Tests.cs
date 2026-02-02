using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class JvoltyIndicatorTests
{
    [Fact]
    public void JvoltyIndicator_Constructor_SetsDefaults()
    {
        var indicator = new JvoltyIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("JVOLTY - Jurik Volatility", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void JvoltyIndicator_ShortName_IncludesParameters()
    {
        var indicator = new JvoltyIndicator { Period = 20 };
        Assert.Contains("JVOLTY", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void JvoltyIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new JvoltyIndicator();

        Assert.Equal(0, JvoltyIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void JvoltyIndicator_Initialize_CreatesInternalJvolty()
    {
        var indicator = new JvoltyIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void JvoltyIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new JvoltyIndicator { Period = 5 };
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
        Assert.True(val >= 1.0); // Jvolty minimum is 1.0
    }

    [Fact]
    public void JvoltyIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new JvoltyIndicator { Period = 5 };
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
    public void JvoltyIndicator_DifferentPeriods_Work()
    {
        int[] periods = { 5, 10, 14, 20, 50 };

        foreach (var period in periods)
        {
            var indicator = new JvoltyIndicator { Period = period };
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
            Assert.True(val >= 1.0, $"Period {period} should produce Jvolty >= 1.0");
        }
    }

    [Fact]
    public void JvoltyIndicator_DifferentSourceTypes_Work()
    {
        SourceType[] sources = { SourceType.Close, SourceType.High, SourceType.Low, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new JvoltyIndicator { Source = source };
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
    public void JvoltyIndicator_Period_CanBeChanged()
    {
        var indicator = new JvoltyIndicator();
        Assert.Equal(14, indicator.Period);

        indicator.Period = 20;
        Assert.Equal(20, indicator.Period);

        indicator.Period = 50;
        Assert.Equal(50, indicator.Period);
    }

    [Fact]
    public void JvoltyIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new JvoltyIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void JvoltyIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new JvoltyIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Jvolty.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }
}