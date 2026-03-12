using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class TtmWaveIndicatorTests
{
    [Fact]
    public void TtmWaveIndicator_Constructor_SetsDefaults()
    {
        var indicator = new TtmWaveIndicator();

        Assert.True(indicator.ShowColdValues);
        Assert.Contains("TTM Wave", indicator.Name, StringComparison.Ordinal);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void TtmWaveIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new TtmWaveIndicator();

        Assert.Equal(0, TtmWaveIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void TtmWaveIndicator_ShortName_IncludesIdentifier()
    {
        var indicator = new TtmWaveIndicator();
        indicator.Initialize();

        Assert.Contains("TTM_Wave", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void TtmWaveIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new TtmWaveIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("TtmWave", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void TtmWaveIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new TtmWaveIndicator();

        indicator.Initialize();

        // 6 wave histograms + 1 zero line = 7 series
        Assert.Equal(7, indicator.LinesSeries.Count);
    }

    [Fact]
    public void TtmWaveIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new TtmWaveIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 800; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i * 0.1, 110 + i * 0.1, 90 + i * 0.1, 105 + i * 0.1);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Wave A1 (index 4 — added 5th in constructor order: C1,C2,B1,B2,A1,A2,Zero)
        double waveA1 = indicator.LinesSeries[4].GetValue(0);

        Assert.True(double.IsFinite(waveA1));
    }

    [Fact]
    public void TtmWaveIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new TtmWaveIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 800; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i * 0.1, 110 + i * 0.1, 90 + i * 0.1, 105 + i * 0.1);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Simulate a new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(800), 180, 190, 170, 185);
        var newArgs = new UpdateArgs(UpdateReason.NewBar);
        indicator.ProcessUpdate(newArgs);

        double waveA1 = indicator.LinesSeries[4].GetValue(0);

        Assert.True(double.IsFinite(waveA1));
    }

    [Fact]
    public void TtmWaveIndicator_ZeroLine_IsSet()
    {
        var indicator = new TtmWaveIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Zero line is the last series (index 6)
        double zero = indicator.LinesSeries[6].GetValue(0);
        Assert.Equal(0.0, zero, 1e-10);
    }

    [Fact]
    public void TtmWaveIndicator_Description_IsSet()
    {
        var indicator = new TtmWaveIndicator();

        Assert.NotNull(indicator.Description);
        Assert.NotEmpty(indicator.Description);
        Assert.Contains("TTM", indicator.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TtmWaveIndicator_AllSeries_ProduceFiniteValues()
    {
        var indicator = new TtmWaveIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 800; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i * 0.1, 110 + i * 0.1, 90 + i * 0.1, 105 + i * 0.1);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // All 7 series should have finite values
        for (int s = 0; s < 7; s++)
        {
            double val = indicator.LinesSeries[s].GetValue(0);
            Assert.True(double.IsFinite(val), $"Series {s} value not finite: {val}");
        }
    }
}
