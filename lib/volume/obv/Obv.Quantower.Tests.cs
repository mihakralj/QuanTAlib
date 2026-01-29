using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class ObvIndicatorTests
{
    [Fact]
    public void ObvIndicator_Constructor_SetsDefaults()
    {
        var indicator = new ObvIndicator();

        Assert.Equal("OBV - On Balance Volume", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(2, indicator.MinHistoryDepths);
    }

    [Fact]
    public void ObvIndicator_ShortName_IsConstant()
    {
        var indicator = new ObvIndicator();
        Assert.Equal("OBV", indicator.ShortName);
    }

    [Fact]
    public void ObvIndicator_MinHistoryDepths_EqualsTwo()
    {
        var indicator = new ObvIndicator();

        Assert.Equal(2, indicator.MinHistoryDepths);
        Assert.Equal(2, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void ObvIndicator_Initialize_CreatesInternalObv()
    {
        var indicator = new ObvIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void ObvIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new ObvIndicator();
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            // Varying close prices to trigger OBV changes
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
    public void ObvIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new ObvIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 105, 100000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar with higher close to increase OBV
        indicator.HistoricalData.AddBar(now.AddMinutes(30), 105, 115, 100, 112, 80000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void ObvIndicator_UpClose_IncreasesObv()
    {
        var indicator = new ObvIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // First bar
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 100000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double firstVal = indicator.LinesSeries[0].GetValue(0);

        // Second bar with higher close - OBV should increase by volume
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 110, 98, 108, 50000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double secondVal = indicator.LinesSeries[0].GetValue(0);

        Assert.True(secondVal > firstVal, $"OBV should increase when close rises: {secondVal} vs {firstVal}");
        Assert.Equal(50000, secondVal - firstVal, 1); // Volume added
    }

    [Fact]
    public void ObvIndicator_DownClose_DecreasesObv()
    {
        var indicator = new ObvIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // First bar
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 100000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double firstVal = indicator.LinesSeries[0].GetValue(0);

        // Second bar with lower close - OBV should decrease by volume
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 102, 90, 92, 50000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double secondVal = indicator.LinesSeries[0].GetValue(0);

        Assert.True(secondVal < firstVal, $"OBV should decrease when close falls: {secondVal} vs {firstVal}");
        Assert.Equal(-50000, secondVal - firstVal, 1); // Volume subtracted
    }

    [Fact]
    public void ObvIndicator_EqualClose_ObvUnchanged()
    {
        var indicator = new ObvIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // First bar
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 100000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double firstVal = indicator.LinesSeries[0].GetValue(0);

        // Second bar with same close - OBV should not change
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 110, 90, 100, 200000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double secondVal = indicator.LinesSeries[0].GetValue(0);

        Assert.Equal(firstVal, secondVal);
    }

    [Fact]
    public void ObvIndicator_Cumulative_CorrectAccumulation()
    {
        var indicator = new ObvIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Bar 1: close=100, volume=10000 -> OBV=0 (first bar)
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 10000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Bar 2: close=110 (up), volume=20000 -> OBV=+20000
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 115, 98, 110, 20000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        // Bar 3: close=105 (down), volume=15000 -> OBV=+20000-15000=5000
        indicator.HistoricalData.AddBar(now.AddMinutes(2), 110, 112, 100, 105, 15000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        // Bar 4: close=108 (up), volume=10000 -> OBV=5000+10000=15000
        indicator.HistoricalData.AddBar(now.AddMinutes(3), 105, 110, 104, 108, 10000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double finalVal = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(15000, finalVal, 1);
    }

    [Fact]
    public void ObvIndicator_LargeVolume_HandlesCorrectly()
    {
        var indicator = new ObvIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Test with large volume values
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 1_000_000_000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 110, 98, 108, 2_000_000_000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(2_000_000_000, val, 1);
    }

    [Fact]
    public void ObvIndicator_StartsAtZero()
    {
        var indicator = new ObvIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // First bar - OBV should be 0
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 100000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double firstVal = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(0, firstVal);
    }
}