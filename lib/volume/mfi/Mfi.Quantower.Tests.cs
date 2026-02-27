using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class MfiIndicatorTests
{
    [Fact]
    public void MfiIndicator_Constructor_SetsDefaults()
    {
        var indicator = new MfiIndicator();

        Assert.Equal("MFI - Money Flow Index", indicator.Name);
        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(14, indicator.MinHistoryDepths);
    }

    [Fact]
    public void MfiIndicator_ShortName_ReflectsPeriod()
    {
        var indicator = new MfiIndicator { Period = 20 };
        Assert.Equal("MFI(20)", indicator.ShortName);
    }

    [Fact]
    public void MfiIndicator_MinHistoryDepths_EqualsDefault()
    {
        var indicator = new MfiIndicator();

        Assert.Equal(14, indicator.MinHistoryDepths);
        Assert.Equal(14, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void MfiIndicator_Initialize_CreatesInternalMfi()
    {
        var indicator = new MfiIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void MfiIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new MfiIndicator();
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 100000);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void MfiIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new MfiIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 100000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(30), 130, 140, 120, 135, 150000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void MfiIndicator_Value_IsBounded()
    {
        var indicator = new MfiIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            // Create varying price patterns to exercise full MFI range
            double open = 100 + i;
            double high = open + 10 + (i % 5);
            double low = open - 5;
            double close = (i % 2 == 0) ? high - 1 : low + 1; // Alternate high/low closes
            double volume = 100000 + (i * 10000);

            indicator.HistoricalData.AddBar(now.AddMinutes(i), open, high, low, close, volume);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(val >= 0 && val <= 100, $"MFI value {val} should be between 0 and 100");
    }

    [Fact]
    public void MfiIndicator_CustomPeriod_AffectsMinHistoryDepths()
    {
        var indicator = new MfiIndicator { Period = 21 };

        Assert.Equal(21, indicator.MinHistoryDepths);
        Assert.Equal(21, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }
}
