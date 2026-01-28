using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class AobvIndicatorTests
{
    private const int SlowPeriod = 14;

    [Fact]
    public void AobvIndicator_Constructor_SetsDefaults()
    {
        var indicator = new AobvIndicator();

        Assert.Equal("AOBV - Archer On-Balance Volume", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(SlowPeriod, indicator.MinHistoryDepths);
    }

    [Fact]
    public void AobvIndicator_ShortName_IsFixed()
    {
        var indicator = new AobvIndicator();
        Assert.Equal("AOBV", indicator.ShortName);
    }

    [Fact]
    public void AobvIndicator_MinHistoryDepths_EqualsSlowPeriod()
    {
        var indicator = new AobvIndicator();

        Assert.Equal(SlowPeriod, indicator.MinHistoryDepths);
        Assert.Equal(SlowPeriod, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void AobvIndicator_Initialize_CreatesInternalAobv()
    {
        var indicator = new AobvIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, two line series should exist (Fast and Slow)
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void AobvIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new AobvIndicator();
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

        // Both line series should have values
        double fastVal = indicator.LinesSeries[0].GetValue(0);
        double slowVal = indicator.LinesSeries[1].GetValue(0);
        Assert.True(double.IsFinite(fastVal), "Fast EMA should be finite");
        Assert.True(double.IsFinite(slowVal), "Slow EMA should be finite");
    }

    [Fact]
    public void AobvIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new AobvIndicator();
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
        Assert.Equal(2, indicator.LinesSeries[1].Count);
    }

    [Fact]
    public void AobvIndicator_FastSlowRelationship_InUptrend()
    {
        var indicator = new AobvIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Create consistent uptrend: closes always rising
        for (int i = 0; i < 50; i++)
        {
            double basePrice = 100 + i;
            indicator.HistoricalData.AddBar(
                now.AddMinutes(i),
                basePrice,        // Open
                basePrice + 2,    // High
                basePrice - 1,    // Low
                basePrice + 1,    // Close (rising)
                1000000);         // Volume
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // In sustained uptrend, both EMAs should be rising
        double fastVal = indicator.LinesSeries[0].GetValue(0);
        double slowVal = indicator.LinesSeries[1].GetValue(0);

        // Both should be positive (accumulating volume)
        Assert.True(fastVal > 0, $"Fast EMA should be positive in uptrend: {fastVal}");
        Assert.True(slowVal > 0, $"Slow EMA should be positive in uptrend: {slowVal}");
    }

    [Fact]
    public void AobvIndicator_Values_AreFinite()
    {
        var indicator = new AobvIndicator();
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

        double fastVal = indicator.LinesSeries[0].GetValue(0);
        double slowVal = indicator.LinesSeries[1].GetValue(0);

        Assert.True(double.IsFinite(fastVal), $"Fast EMA value should be finite: {fastVal}");
        Assert.True(double.IsFinite(slowVal), $"Slow EMA value should be finite: {slowVal}");
    }

    [Fact]
    public void AobvIndicator_TwoLineSeries_Exist()
    {
        var indicator = new AobvIndicator();
        indicator.Initialize();

        Assert.Equal(2, indicator.LinesSeries.Count);
        Assert.Equal("Fast", indicator.LinesSeries[0].Name);
        Assert.Equal("Slow", indicator.LinesSeries[1].Name);
    }
}