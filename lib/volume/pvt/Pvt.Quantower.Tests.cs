using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class PvtIndicatorTests
{
    [Fact]
    public void PvtIndicator_Constructor_SetsDefaults()
    {
        var indicator = new PvtIndicator();

        Assert.Equal("PVT - Price Volume Trend", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(2, indicator.MinHistoryDepths);
    }

    [Fact]
    public void PvtIndicator_ShortName_IsConstant()
    {
        var indicator = new PvtIndicator();
        Assert.Equal("PVT", indicator.ShortName);
    }

    [Fact]
    public void PvtIndicator_MinHistoryDepths_EqualsTwo()
    {
        var indicator = new PvtIndicator();

        Assert.Equal(2, indicator.MinHistoryDepths);
        Assert.Equal(2, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void PvtIndicator_Initialize_CreatesInternalPvt()
    {
        var indicator = new PvtIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void PvtIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new PvtIndicator();
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            // Varying close prices to trigger PVT changes
            double close = 100 + (i % 2 == 0 ? i : -i / 2);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, close, 100000);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void PvtIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new PvtIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 105, 100000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar with higher close to increase PVT
        indicator.HistoricalData.AddBar(now.AddMinutes(30), 105, 115, 100, 112, 80000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void PvtIndicator_UpClose_IncreasesPvt()
    {
        var indicator = new PvtIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // First bar
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 100000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double firstVal = indicator.LinesSeries[0].GetValue(0);

        // Second bar with higher close - PVT should increase
        // PVT += volume * (price_change / prev_price) = 50000 * (108-100)/100 = 4000
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 110, 98, 108, 50000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double secondVal = indicator.LinesSeries[0].GetValue(0);

        Assert.True(secondVal > firstVal, $"PVT should increase when close rises: {secondVal} vs {firstVal}");
        Assert.Equal(4000, secondVal - firstVal, 1); // Volume * (price_change / prev_price)
    }

    [Fact]
    public void PvtIndicator_DownClose_DecreasesPvt()
    {
        var indicator = new PvtIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // First bar
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 100000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double firstVal = indicator.LinesSeries[0].GetValue(0);

        // Second bar with lower close - PVT should decrease
        // PVT += volume * (price_change / prev_price) = 50000 * (92-100)/100 = -4000
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 102, 90, 92, 50000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double secondVal = indicator.LinesSeries[0].GetValue(0);

        Assert.True(secondVal < firstVal, $"PVT should decrease when close falls: {secondVal} vs {firstVal}");
        Assert.Equal(-4000, secondVal - firstVal, 1); // Volume * (price_change / prev_price)
    }

    [Fact]
    public void PvtIndicator_EqualClose_PvtUnchanged()
    {
        var indicator = new PvtIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // First bar
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 100000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double firstVal = indicator.LinesSeries[0].GetValue(0);

        // Second bar with same close - PVT should not change (price_change = 0)
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 110, 90, 100, 200000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double secondVal = indicator.LinesSeries[0].GetValue(0);

        Assert.Equal(firstVal, secondVal);
    }

    [Fact]
    public void PvtIndicator_Cumulative_CorrectAccumulation()
    {
        var indicator = new PvtIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Bar 1: close=100, volume=10000 -> PVT=0 (first bar)
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 10000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Bar 2: close=110 (up from 100), volume=20000 -> PVT += 20000 * (10/100) = 2000
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 115, 98, 110, 20000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        // Bar 3: close=105 (down from 110), volume=15000 -> PVT += 15000 * (-5/110) ≈ -681.82
        indicator.HistoricalData.AddBar(now.AddMinutes(2), 110, 112, 100, 105, 15000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        // Bar 4: close=108 (up from 105), volume=10000 -> PVT += 10000 * (3/105) ≈ 285.71
        indicator.HistoricalData.AddBar(now.AddMinutes(3), 105, 110, 104, 108, 10000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        // Expected: 0 + 2000 - 681.82 + 285.71 ≈ 1603.90
        double finalVal = indicator.LinesSeries[0].GetValue(0);
        Assert.InRange(finalVal, 1600, 1610);
    }

    [Fact]
    public void PvtIndicator_LargeVolume_HandlesCorrectly()
    {
        var indicator = new PvtIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Test with large volume values
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 1_000_000_000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // PVT += 2_000_000_000 * (108-100)/100 = 160_000_000
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 110, 98, 108, 2_000_000_000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(160_000_000, val, 1);
    }

    [Fact]
    public void PvtIndicator_StartsAtZero()
    {
        var indicator = new PvtIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // First bar - PVT should be 0
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 100000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double firstVal = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(0, firstVal);
    }
}