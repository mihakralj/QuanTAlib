using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class CmfIndicatorTests
{
    [Fact]
    public void CmfIndicator_Constructor_SetsDefaults()
    {
        var indicator = new CmfIndicator();

        Assert.Equal("CMF - Chaikin Money Flow", indicator.Name);
        Assert.Equal(20, indicator.Period);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(20, indicator.MinHistoryDepths);
    }

    [Fact]
    public void CmfIndicator_ShortName_ReflectsPeriod()
    {
        var indicator = new CmfIndicator { Period = 14 };
        Assert.Equal("CMF(14)", indicator.ShortName);
    }

    [Fact]
    public void CmfIndicator_MinHistoryDepths_EqualsDefault()
    {
        var indicator = new CmfIndicator();

        Assert.Equal(20, indicator.MinHistoryDepths);
        Assert.Equal(20, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void CmfIndicator_Initialize_CreatesInternalCmf()
    {
        var indicator = new CmfIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void CmfIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new CmfIndicator();
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 1000);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void CmfIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new CmfIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 1000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(30), 130, 140, 120, 135, 1500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void CmfIndicator_Value_IsBounded()
    {
        var indicator = new CmfIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            // Create varying price patterns to exercise full CMF range
            double open = 100 + i;
            double high = open + 10 + (i % 5);
            double low = open - 5;
            double close = (i % 2 == 0) ? high - 1 : low + 1; // Alternate high/low closes
            double volume = 1000 + (i * 100);

            indicator.HistoricalData.AddBar(now.AddMinutes(i), open, high, low, close, volume);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(val >= -1 && val <= 1, $"CMF value {val} should be between -1 and +1");
    }
}
