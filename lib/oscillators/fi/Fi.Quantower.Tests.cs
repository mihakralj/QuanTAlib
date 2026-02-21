using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class FiIndicatorTests
{
    [Fact]
    public void FiIndicator_Constructor_SetsDefaults()
    {
        var indicator = new FiIndicator();

        Assert.Equal("FI - Force Index", indicator.Name);
        Assert.Equal(13, indicator.Period);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(13, indicator.MinHistoryDepths);
    }

    [Fact]
    public void FiIndicator_ShortName_ReflectsPeriod()
    {
        var indicator = new FiIndicator { Period = 20 };
        Assert.Equal("FI(20)", indicator.ShortName);
    }

    [Fact]
    public void FiIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new FiIndicator { Period = 26 };

        Assert.Equal(26, indicator.MinHistoryDepths);
        Assert.Equal(26, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void FiIndicator_Initialize_CreatesInternalFi()
    {
        var indicator = new FiIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void FiIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new FiIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 1000 + (i * 100));
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void FiIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new FiIndicator();
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
    public void FiIndicator_Value_IsFinite()
    {
        var indicator = new FiIndicator();
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

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val), $"FI value {val} should be finite");
    }

    [Fact]
    public void FiIndicator_PositiveForce_OnPriceIncrease()
    {
        var indicator = new FiIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // First bar: baseline
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add bars with increasing prices and high volume
        for (int i = 1; i <= 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + (i * 5), 110 + (i * 5), 95 + (i * 5), 105 + (i * 5), 5000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(val > 0, $"FI should be positive on sustained price increase, got {val}");
    }

    [Fact]
    public void FiIndicator_NegativeForce_OnPriceDecrease()
    {
        var indicator = new FiIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // First bar: baseline
        indicator.HistoricalData.AddBar(now, 150, 155, 145, 150, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add bars with decreasing prices and high volume
        for (int i = 1; i <= 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 150 - (i * 5), 155 - (i * 5), 145 - (i * 5), 145 - (i * 5), 5000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(val < 0, $"FI should be negative on sustained price decrease, got {val}");
    }
}
