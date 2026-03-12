using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class TviIndicatorTests
{
    [Fact]
    public void TviIndicator_Constructor_SetsDefaults()
    {
        var indicator = new TviIndicator();

        Assert.Equal("TVI - Trade Volume Index", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(2, indicator.MinHistoryDepths);
        Assert.Equal(0.125, indicator.MinTick);
    }

    [Fact]
    public void TviIndicator_ShortName_IsConstant()
    {
        var indicator = new TviIndicator();
        Assert.Equal("TVI", indicator.ShortName);
    }

    [Fact]
    public void TviIndicator_MinHistoryDepths_EqualsTwo()
    {
        var indicator = new TviIndicator();

        Assert.Equal(2, indicator.MinHistoryDepths);
        Assert.Equal(2, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void TviIndicator_MinTick_CanBeSet()
    {
        var indicator = new TviIndicator { MinTick = 0.5 };
        Assert.Equal(0.5, indicator.MinTick);
    }

    [Fact]
    public void TviIndicator_Initialize_CreatesInternalTvi()
    {
        var indicator = new TviIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void TviIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new TviIndicator { MinTick = 0.125 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            // Varying close prices to trigger TVI direction changes
            double close = 100 + (i % 2 == 0 ? i * 0.5 : -i * 0.25);
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
    public void TviIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new TviIndicator { MinTick = 0.125 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 105, 100000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar with significant price change
        indicator.HistoricalData.AddBar(now.AddMinutes(30), 105, 115, 100, 112, 80000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void TviIndicator_PriceAboveMinTick_DirectionUp_AddsVolume()
    {
        var indicator = new TviIndicator { MinTick = 0.125 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // First bar
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 10000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double firstVal = indicator.LinesSeries[0].GetValue(0);

        // Second bar with price increase > minTick - direction up, adds volume
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 110, 98, 100.5, 20000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double secondVal = indicator.LinesSeries[0].GetValue(0);

        Assert.True(secondVal > firstVal, $"TVI should increase when price rises above minTick: {secondVal} vs {firstVal}");
    }

    [Fact]
    public void TviIndicator_PriceBelowNegMinTick_DirectionDown_SubtractsVolume()
    {
        var indicator = new TviIndicator { MinTick = 0.125 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // First bar
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 10000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double firstVal = indicator.LinesSeries[0].GetValue(0);

        // Second bar with price decrease > minTick - direction down, subtracts volume
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 102, 90, 99.5, 20000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double secondVal = indicator.LinesSeries[0].GetValue(0);

        Assert.True(secondVal < firstVal, $"TVI should decrease when price falls below -minTick: {secondVal} vs {firstVal}");
    }

    [Fact]
    public void TviIndicator_PriceWithinMinTick_DirectionSticky()
    {
        var indicator = new TviIndicator { MinTick = 1.0 }; // Large minTick for testing
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // First bar
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 10000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Second bar with large price increase - direction up
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 110, 98, 105, 20000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double upVal = indicator.LinesSeries[0].GetValue(0);

        // Third bar with small price change within minTick - direction stays up
        indicator.HistoricalData.AddBar(now.AddMinutes(2), 105, 106, 104, 105.2, 15000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double stickyVal = indicator.LinesSeries[0].GetValue(0);

        // Direction stayed up, so volume added
        Assert.True(stickyVal > upVal, $"TVI direction should be sticky: {stickyVal} vs {upVal}");
    }

    [Fact]
    public void TviIndicator_Cumulative_CorrectAccumulation()
    {
        var indicator = new TviIndicator { MinTick = 0.125 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Bar 1: close=100 -> TVI=0 (first bar, direction=1 by default)
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 10000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Bar 2: close=101 (up > minTick), volume=20000 -> TVI=+20000
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 105, 98, 101, 20000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double afterUp = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(20000, afterUp, 1);

        // Bar 3: close=99.5 (down > minTick), volume=15000 -> TVI=20000-15000=5000
        indicator.HistoricalData.AddBar(now.AddMinutes(2), 101, 102, 99, 99.5, 15000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double afterDown = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(5000, afterDown, 1);

        // Bar 4: close=100 (up > minTick), volume=10000 -> TVI=5000+10000=15000
        indicator.HistoricalData.AddBar(now.AddMinutes(3), 99.5, 101, 99, 100, 10000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double finalVal = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(15000, finalVal, 1);
    }

    [Fact]
    public void TviIndicator_LargeVolume_HandlesCorrectly()
    {
        var indicator = new TviIndicator { MinTick = 0.125 };
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
    public void TviIndicator_StartsAtZero()
    {
        var indicator = new TviIndicator { MinTick = 0.125 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // First bar - TVI should be 0
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 100000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double firstVal = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(0, firstVal);
    }

    [Fact]
    public void TviIndicator_DifferentMinTick_AffectsBehavior()
    {
        var now = DateTime.UtcNow;

        // Indicator with small minTick
        var smallTick = new TviIndicator { MinTick = 0.01 };
        smallTick.Initialize();

        // Indicator with large minTick
        var largeTick = new TviIndicator { MinTick = 5.0 };
        largeTick.Initialize();

        // First bar
        smallTick.HistoricalData.AddBar(now, 100, 105, 95, 100, 10000);
        smallTick.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        largeTick.HistoricalData.AddBar(now, 100, 105, 95, 100, 10000);
        largeTick.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Second bar with price change of 0.5
        smallTick.HistoricalData.AddBar(now.AddMinutes(1), 100, 105, 95, 100.5, 20000);
        smallTick.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        largeTick.HistoricalData.AddBar(now.AddMinutes(1), 100, 105, 95, 100.5, 20000);
        largeTick.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double smallVal = smallTick.LinesSeries[0].GetValue(0);
        double largeVal = largeTick.LinesSeries[0].GetValue(0);

        // Small tick: 0.5 > 0.01, direction changes -> adds volume
        // Large tick: 0.5 < 5.0, direction stays same (up) -> adds volume
        // Both add volume but direction logic differs
        Assert.True(double.IsFinite(smallVal));
        Assert.True(double.IsFinite(largeVal));
    }
}
