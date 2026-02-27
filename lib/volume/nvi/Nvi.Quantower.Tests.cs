using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class NviIndicatorTests
{
    [Fact]
    public void NviIndicator_Constructor_SetsDefaults()
    {
        var indicator = new NviIndicator();

        Assert.Equal("NVI - Negative Volume Index", indicator.Name);
        Assert.Equal(100, indicator.StartValue);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(2, indicator.MinHistoryDepths);
    }

    [Fact]
    public void NviIndicator_ShortName_ReflectsStartValue()
    {
        var indicator = new NviIndicator { StartValue = 1000 };
        Assert.Equal("NVI(1000)", indicator.ShortName);
    }

    [Fact]
    public void NviIndicator_MinHistoryDepths_EqualsTwo()
    {
        var indicator = new NviIndicator();

        Assert.Equal(2, indicator.MinHistoryDepths);
        Assert.Equal(2, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void NviIndicator_Initialize_CreatesInternalNvi()
    {
        var indicator = new NviIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void NviIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new NviIndicator();
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            // Volume decreasing pattern to trigger NVI changes
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 100000 - (i * 1000));

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void NviIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new NviIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 100000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar with lower volume to trigger NVI update
        indicator.HistoricalData.AddBar(now.AddMinutes(30), 130, 140, 120, 135, 80000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void NviIndicator_Value_IsPositive()
    {
        var indicator = new NviIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            // Create varying price and volume patterns
            double open = 100 + i;
            double high = open + 10 + (i % 5);
            double low = open - 5;
            double close = (i % 2 == 0) ? high - 1 : low + 1;
            // Alternate volume up/down to trigger NVI updates
            double volume = (i % 2 == 0) ? 100000 + (i * 1000) : 100000 - (i * 1000);

            indicator.HistoricalData.AddBar(now.AddMinutes(i), open, high, low, close, volume);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(val > 0, $"NVI value {val} should be positive");
    }

    [Fact]
    public void NviIndicator_CustomStartValue_AffectsResult()
    {
        var indicator1 = new NviIndicator { StartValue = 100 };
        var indicator2 = new NviIndicator { StartValue = 1000 };

        indicator1.Initialize();
        indicator2.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator1.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 100000 - (i * 2000));
            indicator2.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 100000 - (i * 2000));

            indicator1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val1 = indicator1.LinesSeries[0].GetValue(0);
        double val2 = indicator2.LinesSeries[0].GetValue(0);

        // Ratio should be approximately 10:1
        Assert.Equal(10.0, val2 / val1, 1);
    }

    [Fact]
    public void NviIndicator_VolumeIncrease_NviUnchanged()
    {
        var indicator = new NviIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // First bar
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102, 100000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double firstVal = indicator.LinesSeries[0].GetValue(0);

        // Second bar with higher volume - NVI should not change
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 110, 100, 108, 150000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double secondVal = indicator.LinesSeries[0].GetValue(0);

        Assert.Equal(firstVal, secondVal);
    }

    [Fact]
    public void NviIndicator_VolumeDecrease_NviUpdates()
    {
        var indicator = new NviIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // First bar
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 100000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double firstVal = indicator.LinesSeries[0].GetValue(0);

        // Second bar with lower volume and higher close - NVI should increase
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 110, 98, 108, 80000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double secondVal = indicator.LinesSeries[0].GetValue(0);

        Assert.True(secondVal > firstVal, $"NVI should increase when volume decreases and price rises: {secondVal} vs {firstVal}");
    }
}
