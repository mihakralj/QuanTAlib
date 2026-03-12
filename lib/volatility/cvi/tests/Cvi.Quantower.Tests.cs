using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class CviIndicatorTests
{
    [Fact]
    public void CviIndicator_Constructor_SetsDefaults()
    {
        var indicator = new CviIndicator();

        Assert.Equal(10, indicator.RocLength);
        Assert.Equal(10, indicator.SmoothLength);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("CVI - Chaikin's Volatility", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void CviIndicator_ShortName_IncludesParameters()
    {
        var indicator = new CviIndicator { RocLength = 14, SmoothLength = 20 };
        Assert.Contains("CVI", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void CviIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new CviIndicator();

        Assert.Equal(0, CviIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void CviIndicator_Initialize_CreatesInternalCvi()
    {
        var indicator = new CviIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void CviIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new CviIndicator { RocLength = 5, SmoothLength = 5 };
        indicator.Initialize();

        // Add historical data with varying high-low ranges
        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double basePrice = 100 + i;
            double range = 2 + (i % 5); // Varying ranges
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + range, basePrice - range, basePrice + 1, 1000);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void CviIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new CviIndicator { RocLength = 5, SmoothLength = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double basePrice = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar with larger range
        indicator.HistoricalData.AddBar(now.AddMinutes(30), 120, 135, 105, 125, 1500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void CviIndicator_DifferentRocLengths_Work()
    {
        int[] rocLengths = { 5, 10, 14, 20 };

        foreach (var rocLength in rocLengths)
        {
            var indicator = new CviIndicator { RocLength = rocLength, SmoothLength = 10 };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 50; i++)
            {
                double basePrice = 100 + i;
                double range = 3 + (i % 4);
                indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + range, basePrice - range, basePrice + 1, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double val = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(val), $"ROC length {rocLength} should produce finite value");
        }
    }

    [Fact]
    public void CviIndicator_DifferentSmoothLengths_Work()
    {
        int[] smoothLengths = { 5, 10, 14, 20 };

        foreach (var smoothLength in smoothLengths)
        {
            var indicator = new CviIndicator { RocLength = 10, SmoothLength = smoothLength };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 50; i++)
            {
                double basePrice = 100 + i;
                double range = 3 + (i % 4);
                indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + range, basePrice - range, basePrice + 1, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double val = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(val), $"Smooth length {smoothLength} should produce finite value");
        }
    }

    [Fact]
    public void CviIndicator_RocLength_CanBeChanged()
    {
        var indicator = new CviIndicator();
        Assert.Equal(10, indicator.RocLength);

        indicator.RocLength = 14;
        Assert.Equal(14, indicator.RocLength);

        indicator.RocLength = 20;
        Assert.Equal(20, indicator.RocLength);
    }

    [Fact]
    public void CviIndicator_SmoothLength_CanBeChanged()
    {
        var indicator = new CviIndicator();
        Assert.Equal(10, indicator.SmoothLength);

        indicator.SmoothLength = 14;
        Assert.Equal(14, indicator.SmoothLength);

        indicator.SmoothLength = 20;
        Assert.Equal(20, indicator.SmoothLength);
    }

    [Fact]
    public void CviIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new CviIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void CviIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new CviIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Cvi.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void CviIndicator_ExpandingVolatility_ProducesPositiveValues()
    {
        var indicator = new CviIndicator { RocLength = 5, SmoothLength = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // First 20 bars: small range
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 1, basePrice - 1, basePrice, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Next 15 bars: expanding range
        for (int i = 20; i < 35; i++)
        {
            double basePrice = 100;
            double range = 1 + (i - 20) * 0.5; // Gradually increasing range
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + range, basePrice - range, basePrice, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val), "Expanding volatility should produce finite value");
        // With expanding ranges, CVI should trend positive
        Assert.True(val > 0, "Expanding volatility should produce positive CVI");
    }

    [Fact]
    public void CviIndicator_ContractingVolatility_ProducesNegativeValues()
    {
        var indicator = new CviIndicator { RocLength = 5, SmoothLength = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // First 20 bars: large range
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 10, basePrice - 10, basePrice, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Next 15 bars: contracting range
        for (int i = 20; i < 35; i++)
        {
            double basePrice = 100;
            double range = Math.Max(1, 10 - (i - 20) * 0.5); // Gradually decreasing range
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + range, basePrice - range, basePrice, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val), "Contracting volatility should produce finite value");
        // With contracting ranges, CVI should trend negative
        Assert.True(val < 0, "Contracting volatility should produce negative CVI");
    }

    [Fact]
    public void CviIndicator_UsesHighLowRange()
    {
        var indicator1 = new CviIndicator { RocLength = 5, SmoothLength = 5 };
        var indicator2 = new CviIndicator { RocLength = 5, SmoothLength = 5 };
        indicator1.Initialize();
        indicator2.Initialize();

        var now = DateTime.UtcNow;

        // Same OHLC structure but different ranges
        for (int i = 0; i < 30; i++)
        {
            // Indicator 1: narrow range
            indicator1.HistoricalData.AddBar(now.AddMinutes(i), 100, 102, 98, 101, 1000);
            indicator1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            // Indicator 2: wide range (same open/close, different high/low)
            indicator2.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 101, 1000);
            indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val1 = indicator1.LinesSeries[0].GetValue(0);
        double val2 = indicator2.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(val1));
        Assert.True(double.IsFinite(val2));
        // With constant but different ranges, the absolute values may differ
        // but both should be close to 0 (no rate of change)
    }
}
