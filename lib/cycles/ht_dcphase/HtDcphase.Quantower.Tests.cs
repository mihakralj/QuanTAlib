using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Quantower.Tests;

public class HtDcphaseIndicatorTests
{
    [Fact]
    public void HtDcphaseIndicator_Constructor_SetsDefaults()
    {
        var indicator = new HtDcphaseIndicator();

        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("HT_DCPHASE - Hilbert Transform Dominant Cycle Phase", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void HtDcphaseIndicator_MinHistoryDepths_EqualsLookback()
    {
        var indicator = new HtDcphaseIndicator();

        Assert.Equal(63, HtDcphaseIndicator.MinHistoryDepths);
        Assert.Equal(63, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void HtDcphaseIndicator_ShortName_IsFixed()
    {
        var indicator = new HtDcphaseIndicator();
        Assert.Equal("HT_DCPHASE", indicator.ShortName);
    }

    [Fact]
    public void HtDcphaseIndicator_Initialize_CreatesInternalHtDcphase()
    {
        var indicator = new HtDcphaseIndicator();

        indicator.Initialize();

        // 2 line series: DCPhase + Zero
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void HtDcphaseIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new HtDcphaseIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void HtDcphaseIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new HtDcphaseIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void HtDcphaseIndicator_ProcessUpdate_NewTick_NoThrow()
    {
        var indicator = new HtDcphaseIndicator();
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
    public void HtDcphaseIndicator_MultipleUpdates_ProducesSequence()
    {
        var indicator = new HtDcphaseIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 104, 103, 105, 106, 107, 108 };

        foreach (var close in closes)
        {
            indicator.HistoricalData.AddBar(now, close, close + 2, close - 2, close);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        for (int j = 0; j < indicator.LinesSeries[0].Count; j++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(j)));
        }
    }
}
