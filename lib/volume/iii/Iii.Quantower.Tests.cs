using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class IiiIndicatorTests
{
    [Fact]
    public void IiiIndicator_Constructor_SetsDefaults()
    {
        var indicator = new IiiIndicator();

        Assert.Equal("III - Intraday Intensity Index", indicator.Name);
        Assert.Equal(21, indicator.Period);
        Assert.False(indicator.Cumulative);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(21, indicator.MinHistoryDepths);
    }

    [Fact]
    public void IiiIndicator_ShortName_ReflectsPeriod()
    {
        var indicator = new IiiIndicator { Period = 14 };
        Assert.Equal("III(14)", indicator.ShortName);
    }

    [Fact]
    public void IiiIndicator_ShortName_ShowsCumulativeMode()
    {
        var indicator = new IiiIndicator { Period = 14, Cumulative = true };
        Assert.Equal("III(14,Cum)", indicator.ShortName);
    }

    [Fact]
    public void IiiIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new IiiIndicator { Period = 30 };

        Assert.Equal(30, indicator.MinHistoryDepths);
        Assert.Equal(30, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void IiiIndicator_Initialize_CreatesInternalIii()
    {
        var indicator = new IiiIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void IiiIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new IiiIndicator();
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 1000 + (i * 100));

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void IiiIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new IiiIndicator();
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
    public void IiiIndicator_Value_IsFinite()
    {
        var indicator = new IiiIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            // Create varying price patterns with price ranges
            double open = 100 + i;
            double high = open + 10 + (i % 5);
            double low = open - 5;
            double close = (i % 2 == 0) ? high - 1 : low + 1;
            double volume = 1000 + (i * 100);

            indicator.HistoricalData.AddBar(now.AddMinutes(i), open, high, low, close, volume);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val), $"III value {val} should be finite");
    }

    [Fact]
    public void IiiIndicator_PositiveValue_OnCloseNearHigh()
    {
        var indicator = new IiiIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add bars with close consistently near high (buying pressure)
        for (int i = 0; i < 10; i++)
        {
            double basePrice = 100 + i;
            double low = basePrice - 10;
            double high = basePrice + 10;
            double close = high - 1; // Close near high
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, high, low, close, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(val > 0, $"III should be positive when close is near high, got {val}");
    }

    [Fact]
    public void IiiIndicator_NegativeValue_OnCloseNearLow()
    {
        var indicator = new IiiIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add bars with close consistently near low (selling pressure)
        for (int i = 0; i < 10; i++)
        {
            double basePrice = 100 + i;
            double low = basePrice - 10;
            double high = basePrice + 10;
            double close = low + 1; // Close near low
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, high, low, close, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(val < 0, $"III should be negative when close is near low, got {val}");
    }

    [Fact]
    public void IiiIndicator_CumulativeMode_ProducesDifferentResults()
    {
        var indicator1 = new IiiIndicator { Period = 5, Cumulative = false };
        var indicator2 = new IiiIndicator { Period = 5, Cumulative = true };
        indicator1.Initialize();
        indicator2.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double basePrice = 100 + i;
            double high = basePrice + 5;
            double low = basePrice - 5;
            double close = (i % 2 == 0) ? high - 1 : low + 1;

            indicator1.HistoricalData.AddBar(now.AddMinutes(i), basePrice, high, low, close, 1000);
            indicator2.HistoricalData.AddBar(now.AddMinutes(i), basePrice, high, low, close, 1000);
            indicator1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val1 = indicator1.LinesSeries[0].GetValue(0);
        double val2 = indicator2.LinesSeries[0].GetValue(0);

        // Different modes should produce different results
        Assert.NotEqual(val1, val2);
        Assert.True(double.IsFinite(val1));
        Assert.True(double.IsFinite(val2));
    }
}
