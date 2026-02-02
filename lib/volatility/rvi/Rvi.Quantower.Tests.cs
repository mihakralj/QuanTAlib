using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class RviIndicatorTests
{
    [Fact]
    public void RviIndicator_Constructor_SetsDefaults()
    {
        var indicator = new RviIndicator();

        Assert.Equal(10, indicator.StdevLength);
        Assert.Equal(14, indicator.RmaLength);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("RVI - Relative Volatility Index", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void RviIndicator_ShortName_IncludesParameters()
    {
        var indicator = new RviIndicator { StdevLength = 10, RmaLength = 14 };
        Assert.Contains("RVI", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void RviIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new RviIndicator();

        Assert.Equal(0, RviIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void RviIndicator_Initialize_CreatesInternalRvi()
    {
        var indicator = new RviIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void RviIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new RviIndicator { StdevLength = 10, RmaLength = 14 };
        indicator.Initialize();

        // Add historical data with trending prices
        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            double closePrice = 100 + i * 0.5 + Math.Sin(i * 0.3) * 2;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), closePrice - 1, closePrice + 2, closePrice - 2, closePrice, 1000);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
        Assert.True(val >= 0 && val <= 100, "RVI should be in range [0,100]");
    }

    [Fact]
    public void RviIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new RviIndicator { StdevLength = 10, RmaLength = 14 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            double closePrice = 100 + i * 0.3;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), closePrice - 1, closePrice + 2, closePrice - 2, closePrice, 1000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(50), 115, 120, 110, 118, 1500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void RviIndicator_DifferentStdevLengths_Work()
    {
        int[] lengths = { 5, 10, 14, 20 };

        foreach (var length in lengths)
        {
            var indicator = new RviIndicator { StdevLength = length, RmaLength = 14 };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 60; i++)
            {
                double closePrice = 100 + i * 0.2 + Math.Sin(i * 0.5) * 3;
                indicator.HistoricalData.AddBar(now.AddMinutes(i), closePrice - 1, closePrice + 2, closePrice - 2, closePrice, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double val = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(val), $"StdevLength {length} should produce finite value");
            Assert.True(val >= 0 && val <= 100, $"StdevLength {length} should produce value in [0,100]");
        }
    }

    [Fact]
    public void RviIndicator_DifferentRmaLengths_Work()
    {
        int[] lengths = { 7, 14, 20, 28 };

        foreach (var length in lengths)
        {
            var indicator = new RviIndicator { StdevLength = 10, RmaLength = length };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 60; i++)
            {
                double closePrice = 100 + i * 0.2 + Math.Sin(i * 0.5) * 3;
                indicator.HistoricalData.AddBar(now.AddMinutes(i), closePrice - 1, closePrice + 2, closePrice - 2, closePrice, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double val = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(val), $"RmaLength {length} should produce finite value");
            Assert.True(val >= 0 && val <= 100, $"RmaLength {length} should produce value in [0,100]");
        }
    }

    [Fact]
    public void RviIndicator_StdevLength_CanBeChanged()
    {
        var indicator = new RviIndicator();
        Assert.Equal(10, indicator.StdevLength);

        indicator.StdevLength = 14;
        Assert.Equal(14, indicator.StdevLength);

        indicator.StdevLength = 20;
        Assert.Equal(20, indicator.StdevLength);
    }

    [Fact]
    public void RviIndicator_RmaLength_CanBeChanged()
    {
        var indicator = new RviIndicator();
        Assert.Equal(14, indicator.RmaLength);

        indicator.RmaLength = 10;
        Assert.Equal(10, indicator.RmaLength);

        indicator.RmaLength = 21;
        Assert.Equal(21, indicator.RmaLength);
    }

    [Fact]
    public void RviIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new RviIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void RviIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new RviIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Rvi.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void RviIndicator_Uptrend_ProducesHighValue()
    {
        var indicator = new RviIndicator { StdevLength = 10, RmaLength = 14 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Strong uptrend: price consistently rising
        for (int i = 0; i < 60; i++)
        {
            double closePrice = 100 + i * 1.5; // Strong consistent uptrend
            indicator.HistoricalData.AddBar(now.AddMinutes(i), closePrice - 1, closePrice + 2, closePrice - 2, closePrice, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(val));
        Assert.True(val > 50, $"Strong uptrend should produce RVI > 50, got {val}");
    }

    [Fact]
    public void RviIndicator_Downtrend_ProducesLowValue()
    {
        var indicator = new RviIndicator { StdevLength = 10, RmaLength = 14 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Strong downtrend: price consistently falling
        for (int i = 0; i < 60; i++)
        {
            double closePrice = 200 - i * 1.5; // Strong consistent downtrend
            indicator.HistoricalData.AddBar(now.AddMinutes(i), closePrice - 1, closePrice + 2, closePrice - 2, closePrice, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(val));
        Assert.True(val < 50, $"Strong downtrend should produce RVI < 50, got {val}");
    }

    [Fact]
    public void RviIndicator_ValueRange_IsBounded()
    {
        var indicator = new RviIndicator { StdevLength = 10, RmaLength = 14 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Mixed data with various price movements
        for (int i = 0; i < 100; i++)
        {
            double closePrice = 100 + Math.Sin(i * 0.2) * 20 + (i % 3 == 0 ? 5 : -3);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), closePrice - 2, closePrice + 3, closePrice - 3, closePrice, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            if (indicator.LinesSeries[0].Count > 0)
            {
                double val = indicator.LinesSeries[0].GetValue(0);
                if (double.IsFinite(val))
                {
                    Assert.True(val >= 0, $"RVI should be >= 0, got {val} at bar {i}");
                    Assert.True(val <= 100, $"RVI should be <= 100, got {val} at bar {i}");
                }
            }
        }
    }

    [Fact]
    public void RviIndicator_UsesClosePrice()
    {
        // RVI should use close prices for direction determination
        var indicator1 = new RviIndicator { StdevLength = 10, RmaLength = 14 };
        var indicator2 = new RviIndicator { StdevLength = 10, RmaLength = 14 };
        indicator1.Initialize();
        indicator2.Initialize();

        var now = DateTime.UtcNow;

        // Same close prices, different open/high/low
        for (int i = 0; i < 60; i++)
        {
            double closePrice = 100 + i * 0.5;
            // Indicator 1: narrow range
            indicator1.HistoricalData.AddBar(now.AddMinutes(i), closePrice, closePrice + 1, closePrice - 1, closePrice, 1000);
            indicator1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            // Indicator 2: wide range (same close)
            indicator2.HistoricalData.AddBar(now.AddMinutes(i), closePrice - 5, closePrice + 10, closePrice - 10, closePrice, 1000);
            indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val1 = indicator1.LinesSeries[0].GetValue(0);
        double val2 = indicator2.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(val1));
        Assert.True(double.IsFinite(val2));
        // RVI primarily depends on close-to-close direction, so values should be similar
        Assert.True(Math.Abs(val1 - val2) < 5, $"RVI values should be similar for same closes: {val1} vs {val2}");
    }

    [Fact]
    public void RviIndicator_NeutralMarket_ProducesNearFifty()
    {
        var indicator = new RviIndicator { StdevLength = 10, RmaLength = 14 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Alternating up/down with equal magnitude
        for (int i = 0; i < 100; i++)
        {
            double closePrice = 100 + (i % 2 == 0 ? 2 : -2);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), closePrice - 1, closePrice + 1, closePrice - 1, closePrice, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(val));
        // In a neutral market, RVI should be near 50
        Assert.True(val >= 30 && val <= 70, $"Neutral market should produce RVI near 50, got {val}");
    }
}