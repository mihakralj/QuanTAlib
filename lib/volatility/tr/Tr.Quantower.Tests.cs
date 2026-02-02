using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class TrIndicatorTests
{
    [Fact]
    public void TrIndicator_Constructor_SetsDefaults()
    {
        var indicator = new TrIndicator();

        Assert.True(indicator.ShowColdValues);
        Assert.Equal("TR - True Range", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void TrIndicator_ShortName_IsTr()
    {
        var indicator = new TrIndicator();
        Assert.Equal("TR", indicator.ShortName);
    }

    [Fact]
    public void TrIndicator_MinHistoryDepths_EqualsOne()
    {
        var indicator = new TrIndicator();

        Assert.Equal(1, TrIndicator.MinHistoryDepths);
        Assert.Equal(1, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void TrIndicator_Initialize_CreatesInternalTr()
    {
        var indicator = new TrIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void TrIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new TrIndicator();
        indicator.Initialize();

        // Add historical data with varying ranges
        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100 + i;
            double range = 2 + (i % 5);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + range, basePrice - range, basePrice + 1, 1000);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
        Assert.True(val >= 0, "True Range should be non-negative");
    }

    [Fact]
    public void TrIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new TrIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar with gap up
        indicator.HistoricalData.AddBar(now.AddMinutes(20), 130, 135, 125, 133, 1500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void TrIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new TrIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void TrIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new TrIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Tr.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void TrIndicator_FirstBar_UsesHighMinusLow()
    {
        var indicator = new TrIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // First bar: High=110, Low=90, so TR should be 20
        indicator.HistoricalData.AddBar(now, 100, 110, 90, 105, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(20.0, val, 10);
    }

    [Fact]
    public void TrIndicator_GapUp_CapturesGap()
    {
        var indicator = new TrIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // First bar: close at 100
        indicator.HistoricalData.AddBar(now, 98, 102, 98, 100, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Second bar: gap up to 110-115, so TR = max(5, 15, 10) = 15
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 112, 115, 110, 113, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(15.0, val, 10);
    }

    [Fact]
    public void TrIndicator_GapDown_CapturesGap()
    {
        var indicator = new TrIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // First bar: close at 100
        indicator.HistoricalData.AddBar(now, 98, 102, 98, 100, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Second bar: gap down to 85-90, so TR = max(5, 10, 15) = 15
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 88, 90, 85, 87, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(15.0, val, 10);
    }

    [Fact]
    public void TrIndicator_NoGap_EqualsHighMinusLow()
    {
        var indicator = new TrIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // First bar: close at 100
        indicator.HistoricalData.AddBar(now, 98, 102, 98, 100, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Second bar: no gap, H=108, L=92, pC=100, so TR = max(16, 8, 8) = 16
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 99, 108, 92, 105, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(16.0, val, 10);
    }

    [Fact]
    public void TrIndicator_HigherVolatility_ProducesHigherTr()
    {
        var indicator1 = new TrIndicator();
        var indicator2 = new TrIndicator();
        indicator1.Initialize();
        indicator2.Initialize();

        var now = DateTime.UtcNow;

        // Indicator 1: low volatility (narrow range)
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100;
            indicator1.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 1, basePrice - 1, basePrice + 0.5, 1000);
            indicator1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Indicator 2: high volatility (wide range)
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100;
            indicator2.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 10, basePrice - 10, basePrice + 2, 1000);
            indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lowVol = indicator1.LinesSeries[0].GetValue(0);
        double highVol = indicator2.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(lowVol));
        Assert.True(double.IsFinite(highVol));
        Assert.True(highVol > lowVol, "Higher volatility bars should produce higher TR value");
    }

    [Fact]
    public void TrIndicator_FlatBar_ProducesZero()
    {
        var indicator = new TrIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Flat bar: H=L=O=C
        indicator.HistoricalData.AddBar(now, 100, 100, 100, 100, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(0.0, val, 10);
    }

    [Fact]
    public void TrIndicator_FlatBarWithGap_CapturesGap()
    {
        var indicator = new TrIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // First bar: close at 100
        indicator.HistoricalData.AddBar(now, 100, 100, 100, 100, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Second bar: flat but at 105 (gap of 5)
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 105, 105, 105, 105, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(5.0, val, 10);  // Gap = |105-100| = 5
    }

    [Fact]
    public void TrIndicator_IsHotImmediately()
    {
        var indicator = new TrIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // TR has warmup of 1, so should be hot after first bar
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Value should be valid (not cold)
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
        Assert.True(val >= 0);
    }

    [Fact]
    public void TrIndicator_UsesAllOhlcComponents()
    {
        // TR uses H, L, and previous Close - verify it captures gaps properly
        var indicator = new TrIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // First bar: standard range
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double firstTr = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(10.0, firstTr, 10);  // H-L = 105-95 = 10

        // Second bar: big gap up (prevClose=100, current range 150-160)
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 155, 160, 150, 158, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double secondTr = indicator.LinesSeries[0].GetValue(0);
        // TR = max(10, 60, 50) = 60
        Assert.Equal(60.0, secondTr, 10);
    }
}