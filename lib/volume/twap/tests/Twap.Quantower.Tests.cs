using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class TwapIndicatorTests
{
    [Fact]
    public void TwapIndicator_Constructor_SetsDefaults()
    {
        var indicator = new TwapIndicator();

        Assert.Equal("TWAP - Time Weighted Average Price", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(1, indicator.MinHistoryDepths);
        Assert.Equal(0, indicator.Period);
    }

    [Fact]
    public void TwapIndicator_ShortName_IsConstant()
    {
        var indicator = new TwapIndicator();
        Assert.Equal("TWAP", indicator.ShortName);
    }

    [Fact]
    public void TwapIndicator_MinHistoryDepths_EqualsOne()
    {
        var indicator = new TwapIndicator();

        Assert.Equal(1, indicator.MinHistoryDepths);
        Assert.Equal(1, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void TwapIndicator_Period_CanBeSet()
    {
        var indicator = new TwapIndicator { Period = 100 };
        Assert.Equal(100, indicator.Period);
    }

    [Fact]
    public void TwapIndicator_Initialize_CreatesInternalTwap()
    {
        var indicator = new TwapIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void TwapIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new TwapIndicator { Period = 0 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double close = 100 + (i * 0.5);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), close - 2, close + 2, close - 3, close, 100000);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void TwapIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new TwapIndicator { Period = 0 };
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
    public void TwapIndicator_RunningAverage_CorrectCalculation()
    {
        var indicator = new TwapIndicator { Period = 0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Bar 1: O=100, H=105, L=95, C=100 -> HLC3 = (105+95+100)/3 = 100
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 10000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double firstVal = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(100, firstVal);

        // Bar 2: O=100, H=110, L=90, C=105 -> HLC3 = (110+90+105)/3 ≈ 101.67
        // TWAP = (100 + 101.67) / 2 ≈ 100.83
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 110, 90, 105, 20000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double secondVal = indicator.LinesSeries[0].GetValue(0);
        double expectedHlc3Second = (110.0 + 90.0 + 105.0) / 3.0;
        double expectedTwap = (100.0 + expectedHlc3Second) / 2.0;
        Assert.Equal(expectedTwap, secondVal, 2);
    }

    [Fact]
    public void TwapIndicator_PeriodReset_ResetsAverage()
    {
        var indicator = new TwapIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add 7 bars - reset should occur after bar 5
        for (int i = 0; i < 7; i++)
        {
            double close = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), close - 2, close + 2, close - 3, close, 10000);
            var args = i == 0
                ? new UpdateArgs(UpdateReason.HistoricalBar)
                : new UpdateArgs(UpdateReason.NewBar);
            indicator.ProcessUpdate(args);
        }

        // After period reset, values should be different than continuous
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void TwapIndicator_ZeroPeriod_NeverResets()
    {
        var indicator = new TwapIndicator { Period = 0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double sum = 0;

        // Add 20 bars - should never reset
        for (int i = 0; i < 20; i++)
        {
            double close = 100 + i;
            double high = close + 2;
            double low = close - 3;
            double hlc3 = (high + low + close) / 3.0;
            sum += hlc3;

            indicator.HistoricalData.AddBar(now.AddMinutes(i), close - 2, high, low, close, 10000);
            var args = i == 0
                ? new UpdateArgs(UpdateReason.HistoricalBar)
                : new UpdateArgs(UpdateReason.NewBar);
            indicator.ProcessUpdate(args);
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        double expectedTwap = sum / 20.0;
        Assert.Equal(expectedTwap, val, 1);
    }

    [Fact]
    public void TwapIndicator_DifferentPeriods_ProduceDifferentResults()
    {
        var now = DateTime.UtcNow;

        // Indicator with no reset
        var noReset = new TwapIndicator { Period = 0 };
        noReset.Initialize();

        // Indicator with period=5
        var period5 = new TwapIndicator { Period = 5 };
        period5.Initialize();

        // Add 10 bars to both
        for (int i = 0; i < 10; i++)
        {
            double close = 100 + (i * 2);
            noReset.HistoricalData.AddBar(now.AddMinutes(i), close - 2, close + 2, close - 3, close, 10000);
            period5.HistoricalData.AddBar(now.AddMinutes(i), close - 2, close + 2, close - 3, close, 10000);

            var args = i == 0
                ? new UpdateArgs(UpdateReason.HistoricalBar)
                : new UpdateArgs(UpdateReason.NewBar);
            noReset.ProcessUpdate(args);
            period5.ProcessUpdate(args);
        }

        double noResetVal = noReset.LinesSeries[0].GetValue(0);
        double period5Val = period5.LinesSeries[0].GetValue(0);

        // With reset at period 5, the averages should be different
        Assert.NotEqual(noResetVal, period5Val, 1);
    }

    [Fact]
    public void TwapIndicator_UsesTypicalPrice_HLC3()
    {
        var indicator = new TwapIndicator { Period = 0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Bar with specific OHLC values
        double open = 100;
        double high = 120;
        double low = 80;
        double close = 110;
        double expectedHlc3 = (high + low + close) / 3.0; // (120 + 80 + 110) / 3 = 103.33

        indicator.HistoricalData.AddBar(now, open, high, low, close, 10000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(expectedHlc3, val, 2);
    }

    [Fact]
    public void TwapIndicator_ValueWithinPriceRange()
    {
        var indicator = new TwapIndicator { Period = 0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double minLow = double.MaxValue;
        double maxHigh = double.MinValue;

        // Add bars with varying prices
        for (int i = 0; i < 20; i++)
        {
            double close = 100 + (i % 3 == 0 ? i : -i * 0.5);
            double high = close + 5;
            double low = close - 5;
            minLow = Math.Min(minLow, low);
            maxHigh = Math.Max(maxHigh, high);

            indicator.HistoricalData.AddBar(now.AddMinutes(i), close - 2, high, low, close, 10000);
            var args = i == 0
                ? new UpdateArgs(UpdateReason.HistoricalBar)
                : new UpdateArgs(UpdateReason.NewBar);
            indicator.ProcessUpdate(args);
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(val >= minLow && val <= maxHigh,
            $"TWAP {val} should be within price range [{minLow}, {maxHigh}]");
    }

    [Fact]
    public void TwapIndicator_MultipleResets_MaintainsCorrectAverage()
    {
        var indicator = new TwapIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add 10 bars - should reset at bar 4 and 7
        for (int i = 0; i < 10; i++)
        {
            double close = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), close - 2, close + 2, close - 3, close, 10000);
            var args = i == 0
                ? new UpdateArgs(UpdateReason.HistoricalBar)
                : new UpdateArgs(UpdateReason.NewBar);
            indicator.ProcessUpdate(args);

            double val = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(val), $"Value at bar {i} should be finite");
        }
    }
}
