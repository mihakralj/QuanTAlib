using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class VaIndicatorTests
{
    [Fact]
    public void VaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new VaIndicator();

        Assert.Equal("VA - Volume Accumulation", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(1, indicator.MinHistoryDepths);
    }

    [Fact]
    public void VaIndicator_ShortName_IsConstant()
    {
        var indicator = new VaIndicator();
        Assert.Equal("VA", indicator.ShortName);
    }

    [Fact]
    public void VaIndicator_MinHistoryDepths_EqualsOne()
    {
        var indicator = new VaIndicator();

        Assert.Equal(1, indicator.MinHistoryDepths);
        Assert.Equal(1, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void VaIndicator_Initialize_CreatesInternalVa()
    {
        var indicator = new VaIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void VaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new VaIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double close = 100 + (i * 0.5);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), close - 2, close + 2, close - 3, close, 100000);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void VaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new VaIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 105, 100000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(30), 105, 115, 100, 112, 80000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void VaIndicator_CloseAboveMidpoint_PositiveAccumulation()
    {
        var indicator = new VaIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Bar: H=110, L=90, C=105, V=1000
        // midpoint = (110 + 90) / 2 = 100
        // va_period = 1000 * (105 - 100) = 5000
        indicator.HistoricalData.AddBar(now, 100, 110, 90, 105, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(5000, val, 1);
    }

    [Fact]
    public void VaIndicator_CloseBelowMidpoint_NegativeAccumulation()
    {
        var indicator = new VaIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Bar: H=110, L=90, C=95, V=1000
        // midpoint = (110 + 90) / 2 = 100
        // va_period = 1000 * (95 - 100) = -5000
        indicator.HistoricalData.AddBar(now, 100, 110, 90, 95, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(-5000, val, 1);
    }

    [Fact]
    public void VaIndicator_CloseAtMidpoint_ZeroAccumulation()
    {
        var indicator = new VaIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Bar: H=110, L=90, C=100, V=1000
        // midpoint = (110 + 90) / 2 = 100
        // va_period = 1000 * (100 - 100) = 0
        indicator.HistoricalData.AddBar(now, 100, 110, 90, 100, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(0, val, 1);
    }

    [Fact]
    public void VaIndicator_MultipleBarAccumulation_CorrectSum()
    {
        var indicator = new VaIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Bar 1: midpoint=100, close=105, vol=1000 -> va=5000
        indicator.HistoricalData.AddBar(now, 100, 110, 90, 105, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Bar 2: midpoint=100, close=95, vol=500 -> va_period=-2500, total=2500
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 110, 90, 95, 500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(2500, val, 1);
    }

    [Fact]
    public void VaIndicator_LargeVolume_LargerImpact()
    {
        var indicator1 = new VaIndicator();
        indicator1.Initialize();

        var indicator2 = new VaIndicator();
        indicator2.Initialize();

        var now = DateTime.UtcNow;

        // Same price action, different volume
        indicator1.HistoricalData.AddBar(now, 100, 110, 90, 105, 1000);
        indicator1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        indicator2.HistoricalData.AddBar(now, 100, 110, 90, 105, 10000);
        indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double val1 = indicator1.LinesSeries[0].GetValue(0);
        double val2 = indicator2.LinesSeries[0].GetValue(0);

        // 10x volume should produce 10x VA
        Assert.Equal(val1 * 10, val2, 1);
    }

    [Fact]
    public void VaIndicator_CumulativeNature_AlwaysAccumulates()
    {
        var indicator = new VaIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double lastVa = 0;

        // Add multiple positive bars - VA should keep increasing
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 108, 1000);
            var args = i == 0
                ? new UpdateArgs(UpdateReason.HistoricalBar)
                : new UpdateArgs(UpdateReason.NewBar);
            indicator.ProcessUpdate(args);

            double currentVa = indicator.LinesSeries[0].GetValue(0);
            Assert.True(currentVa > lastVa, $"VA should increase: {currentVa} > {lastVa}");
            lastVa = currentVa;
        }
    }

    [Fact]
    public void VaIndicator_MixedPressure_CorrectNetEffect()
    {
        var indicator = new VaIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Equal positive and negative with same volume should net to zero
        // Bar 1: +5000 (close above midpoint)
        indicator.HistoricalData.AddBar(now, 100, 110, 90, 105, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Bar 2: -5000 (close below midpoint by same amount)
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 110, 90, 95, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(0, val, 1);
    }
}
