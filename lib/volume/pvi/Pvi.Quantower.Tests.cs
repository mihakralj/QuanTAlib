using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class PviIndicatorTests
{
    [Fact]
    public void PviIndicator_Constructor_SetsDefaults()
    {
        var indicator = new PviIndicator();

        Assert.Equal("PVI - Positive Volume Index", indicator.Name);
        Assert.Equal(100, indicator.StartValue);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(2, indicator.MinHistoryDepths);
    }

    [Fact]
    public void PviIndicator_ShortName_ReflectsStartValue()
    {
        var indicator = new PviIndicator { StartValue = 1000 };
        Assert.Equal("PVI(1000)", indicator.ShortName);
    }

    [Fact]
    public void PviIndicator_MinHistoryDepths_EqualsTwo()
    {
        var indicator = new PviIndicator();

        Assert.Equal(2, indicator.MinHistoryDepths);
        Assert.Equal(2, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void PviIndicator_Initialize_CreatesInternalPvi()
    {
        var indicator = new PviIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void PviIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new PviIndicator();
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            // Volume increasing pattern to trigger PVI changes
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 100000 + (i * 1000));

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void PviIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new PviIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 100000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar with higher volume to trigger PVI update
        indicator.HistoricalData.AddBar(now.AddMinutes(30), 130, 140, 120, 135, 150000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void PviIndicator_Value_IsPositive()
    {
        var indicator = new PviIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            // Create varying price and volume patterns
            double open = 100 + i;
            double high = open + 10 + (i % 5);
            double low = open - 5;
            double close = (i % 2 == 0) ? high - 1 : low + 1;
            // Alternate volume up/down to trigger PVI updates
            double volume = (i % 2 == 0) ? 100000 + (i * 1000) : 100000 - (i * 1000);

            indicator.HistoricalData.AddBar(now.AddMinutes(i), open, high, low, close, volume);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(val > 0, $"PVI value {val} should be positive");
    }

    [Fact]
    public void PviIndicator_CustomStartValue_AffectsResult()
    {
        var indicator1 = new PviIndicator { StartValue = 100 };
        var indicator2 = new PviIndicator { StartValue = 1000 };

        indicator1.Initialize();
        indicator2.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator1.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 100000 + (i * 2000));
            indicator2.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 100000 + (i * 2000));

            indicator1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val1 = indicator1.LinesSeries[0].GetValue(0);
        double val2 = indicator2.LinesSeries[0].GetValue(0);

        // Ratio should be approximately 10:1
        Assert.Equal(10.0, val2 / val1, 1);
    }

    [Fact]
    public void PviIndicator_VolumeDecrease_PviUnchanged()
    {
        var indicator = new PviIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // First bar
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102, 100000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double firstVal = indicator.LinesSeries[0].GetValue(0);

        // Second bar with lower volume - PVI should not change
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 110, 100, 108, 80000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double secondVal = indicator.LinesSeries[0].GetValue(0);

        Assert.Equal(firstVal, secondVal);
    }

    [Fact]
    public void PviIndicator_VolumeIncrease_PviUpdates()
    {
        var indicator = new PviIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // First bar
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 100000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double firstVal = indicator.LinesSeries[0].GetValue(0);

        // Second bar with higher volume and higher close - PVI should increase
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 110, 98, 108, 150000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double secondVal = indicator.LinesSeries[0].GetValue(0);

        Assert.True(secondVal > firstVal, $"PVI should increase when volume increases and price rises: {secondVal} vs {firstVal}");
    }
}