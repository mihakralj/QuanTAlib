using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class EriIndicatorTests
{
    [Fact]
    public void EriIndicator_Constructor_SetsDefaults()
    {
        var indicator = new EriIndicator();

        Assert.Equal("ERI - Elder Ray Index", indicator.Name);
        Assert.Equal(13, indicator.Period);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(13, indicator.MinHistoryDepths);
    }

    [Fact]
    public void EriIndicator_ShortName_ReflectsPeriod()
    {
        var indicator = new EriIndicator { Period = 20 };
        Assert.Equal("ERI(20)", indicator.ShortName);
    }

    [Fact]
    public void EriIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new EriIndicator { Period = 26 };

        Assert.Equal(26, indicator.MinHistoryDepths);
        Assert.Equal(26, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void EriIndicator_Initialize_CreatesInternalEri()
    {
        var indicator = new EriIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, two line series should exist (Bull Power + Bear Power)
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void EriIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new EriIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 1000 + (i * 100));
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double bullVal = indicator.LinesSeries[0].GetValue(0);
        double bearVal = indicator.LinesSeries[1].GetValue(0);
        Assert.True(double.IsFinite(bullVal));
        Assert.True(double.IsFinite(bearVal));
    }

    [Fact]
    public void EriIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new EriIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 1000 + (i * 100));
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(30), 130, 140, 120, 135, 1500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void EriIndicator_Value_IsFinite()
    {
        var indicator = new EriIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            double open = 100 + i;
            double high = open + 10 + (i % 5);
            double low = open - 5;
            double close = (i % 2 == 0) ? high - 1 : low + 1;
            double volume = 1000 + (i * 100);

            indicator.HistoricalData.AddBar(now.AddMinutes(i), open, high, low, close, volume);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double bullVal = indicator.LinesSeries[0].GetValue(0);
        double bearVal = indicator.LinesSeries[1].GetValue(0);
        Assert.True(double.IsFinite(bullVal), $"Bull Power value {bullVal} should be finite");
        Assert.True(double.IsFinite(bearVal), $"Bear Power value {bearVal} should be finite");
    }

    [Fact]
    public void EriIndicator_BullPowerPositive_OnHighAboveEma()
    {
        var indicator = new EriIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Feed bars with high consistently above close (and thus above EMA)
        for (int i = 0; i < 20; i++)
        {
            double close = 100 + (i * 2);
            double high = close + 15;  // High well above close
            double low = close - 5;

            indicator.HistoricalData.AddBar(now.AddMinutes(i), close, high, low, close, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double bullVal = indicator.LinesSeries[0].GetValue(0);
        Assert.True(bullVal > 0, $"Bull Power should be positive when High > EMA, got {bullVal}");
    }

    [Fact]
    public void EriIndicator_BearPowerNegative_OnLowBelowEma()
    {
        var indicator = new EriIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Feed bars with low consistently below close (and thus below EMA)
        for (int i = 0; i < 20; i++)
        {
            double close = 100 + (i * 2);
            double high = close + 5;
            double low = close - 15;  // Low well below close

            indicator.HistoricalData.AddBar(now.AddMinutes(i), close, high, low, close, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double bearVal = indicator.LinesSeries[1].GetValue(0);
        Assert.True(bearVal < 0, $"Bear Power should be negative when Low < EMA, got {bearVal}");
    }
}
