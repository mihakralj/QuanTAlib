using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Quantower.Tests;

public class HtPhasorIndicatorTests
{
    [Fact]
    public void HtPhasorIndicator_Constructor_SetsDefaults()
    {
        var indicator = new HtPhasorIndicator();

        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("HT_PHASOR - Ehlers Hilbert Transform Phasor Components", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void HtPhasorIndicator_MinHistoryDepths_EqualsLookback()
    {
        var indicator = new HtPhasorIndicator();

        Assert.Equal(32, HtPhasorIndicator.MinHistoryDepths);
        Assert.Equal(32, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void HtPhasorIndicator_ShortName_IsFixed()
    {
        var indicator = new HtPhasorIndicator();
        Assert.Equal("HT_PHASOR", indicator.ShortName);
    }

    [Fact]
    public void HtPhasorIndicator_Initialize_CreatesInternalHtPhasor()
    {
        var indicator = new HtPhasorIndicator();

        indicator.Initialize();

        Assert.Equal(3, indicator.LinesSeries.Count);
    }

    [Fact]
    public void HtPhasorIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new HtPhasorIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void HtPhasorIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new HtPhasorIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void HtPhasorIndicator_ProcessUpdate_NewTick_NoThrow()
    {
        var indicator = new HtPhasorIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        double first = indicator.LinesSeries[0].GetValue(0);

        // simulate same-bar update should not advance or corrupt; value remains finite
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        double second = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(first));
        Assert.True(double.IsFinite(second));
        Assert.Equal(first, second);
    }

    [Fact]
    public void HtPhasorIndicator_MultipleUpdates_ProducesSequence()
    {
        var indicator = new HtPhasorIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 104, 103, 105, 106, 107, 108 };

        foreach (var close in closes)
        {
            indicator.HistoricalData.AddBar(now, close, close + 2, close - 2, close);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        for (int i = 0; i < indicator.LinesSeries.Count; i++)
        {
            for (int j = 0; j < indicator.LinesSeries[i].Count; j++)
            {
                Assert.True(double.IsFinite(indicator.LinesSeries[i].GetValue(j)));
            }
        }
    }
}
